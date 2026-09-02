using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Host.Companion;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

public class SmsOutboundServiceTests
{
    private const string Kurt = "+15551234567";

    [Fact]
    public async Task EnqueueSms_Allowlisted_PendingThenAck()
    {
        var sut = CreateSut();
        var r = await sut.EnqueueSmsAsync(Kurt, "hello kurt", "test");
        Assert.True(r.Ok);
        Assert.False(string.IsNullOrWhiteSpace(r.JobId));

        var pending = sut.ListPending();
        Assert.Single(pending);
        Assert.Equal(SmsOutboundKind.Sms, pending[0].Kind);
        Assert.Equal(Kurt, pending[0].ToE164);
        Assert.Equal("hello kurt", pending[0].Text);

        Assert.True(sut.TryAck(r.JobId!, true));
        Assert.Empty(sut.ListPending());
        Assert.Equal(SmsOutboundStatus.Sent, sut.ListRecent(1)[0].Status);
    }

    [Fact]
    public async Task EnqueueSms_NotAllowlisted_Rejected()
    {
        var sut = CreateSut();
        var r = await sut.EnqueueSmsAsync("+19998887777", "nope");
        Assert.False(r.Ok);
        Assert.Equal("not_allowlisted", r.Error);
        Assert.Empty(sut.ListPending());
    }

    [Fact]
    public async Task EnqueueSms_RateMinGap_DropsSecond()
    {
        var sut = CreateSut(minSms: 60);
        Assert.True((await sut.EnqueueSmsAsync(Kurt, "one")).Ok);
        var second = await sut.EnqueueSmsAsync(Kurt, "two");
        Assert.False(second.Ok);
        Assert.True(second.RateLimited);
        Assert.Equal("rate_min_gap_sms", second.Error);
        Assert.Single(sut.ListPending());
    }

    [Fact]
    public async Task EnqueueMms_WithImage_PendingIncludesBytes()
    {
        var sut = CreateSut(minMms: 0);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var r = await sut.EnqueueMmsAsync(Kurt, png, "image/png", "cap", "test");
        Assert.True(r.Ok);
        var job = Assert.Single(sut.ListPending());
        Assert.Equal(SmsOutboundKind.Mms, job.Kind);
        Assert.NotNull(job.ImageBytes);
        Assert.Equal(png.Length, job.ImageBytes!.Length);
    }

    [Fact]
    public async Task EnqueueScreenshotMms_PrefersBrowserHub()
    {
        var browser = new VictoriaBrowserViewHub();
        browser.Publish(new byte[] { 0xFF, 0xD8, 0xFF, 0x00 }, "https://x", "t", "nav");
        var desk = new DesktopViewHub();
        desk.RecordScreenshot(new byte[] { 1, 2, 3, 4 }, "png", 1, 1, null, DesktopViewHub.SourceDesktop);

        var sut = CreateSut(browser: browser, desktop: desk, minMms: 0);
        var r = await sut.EnqueueScreenshotMmsToKurtAsync("still");
        Assert.True(r.Ok);
        var job = Assert.Single(sut.ListPending());
        Assert.Equal("image/jpeg", job.ContentType);
        Assert.Equal(4, job.ImageBytes!.Length);
        Assert.Contains("browser", job.Source ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnqueueScreenshotMms_NoFrame_Fails()
    {
        var sut = CreateSut(minMms: 0);
        var r = await sut.EnqueueScreenshotMmsToKurtAsync();
        Assert.False(r.Ok);
        Assert.Equal("no_frame", r.Error);
    }

    [Fact]
    public void ScreenshotAsk_DetectsPhrases()
    {
        Assert.True(SmsScreenshotAsk.LooksLikeScreenshotAsk("send me a screenshot"));
        Assert.True(SmsScreenshotAsk.LooksLikeScreenshotAsk("What do you see?"));
        Assert.False(SmsScreenshotAsk.LooksLikeScreenshotAsk("how are you"));
    }

    [Fact]
    public async Task Inbound_AutoEnqueuesOutboundSms()
    {
        var outbound = CreateSut(minSms: 0);
        var inference = new InboundCountingInference { Reply = "hey back" };
        var inbound = new SmsInboundService(
            Options.Create(new SmsOptions
            {
                KurtAllowlistE164 = Kurt,
                StubWhenModelDown = false,
                OutboundEnabled = true,
                AutoReplySmsEnabled = true,
                MinSecondsBetweenSms = 0
            }),
            Options.Create(new InferenceOptions { Enabled = true }),
            Options.Create(new ChatWsOptions()),
            Options.Create(new CompanionOptions()),
            inference,
            new InboundFakeMemory(),
            new ChatSessionHistoryStore(32),
            new InboundFakeMedia(),
            new PresenceWsHub(NullLogger<PresenceWsHub>.Instance),
            NullLogger<SmsInboundService>.Instance,
            outbound);

        var result = await inbound.HandleAsync(new SmsInboundRequest(Kurt, "hi", null, null));
        Assert.True(result.Ok);
        Assert.Equal("hey back", result.ReplyText);
        var pending = outbound.ListPending();
        Assert.Single(pending);
        Assert.Equal("hey back", pending[0].Text);
        Assert.Equal(SmsOutboundKind.Sms, pending[0].Kind);
    }

    private static SmsOutboundService CreateSut(
        int minSms = 0,
        int minMms = 0,
        IVictoriaBrowserViewHub? browser = null,
        IDesktopViewHub? desktop = null) =>
        new(
            Options.Create(new SmsOptions
            {
                KurtAllowlistE164 = Kurt,
                OutboundEnabled = true,
                MinSecondsBetweenSms = minSms,
                MinSecondsBetweenMms = minMms,
                MaxSmsPerHour = 30,
                MaxMmsPerHour = 6
            }),
            NullLogger<SmsOutboundService>.Instance,
            browser,
            desktop);

    private sealed class InboundCountingInference : IInferenceClient
    {
        public string Reply { get; set; } = "ok";

        public Task<string> CompleteAsync(
            string prompt,
            string? systemPreamble = null,
            CancellationToken cancellationToken = default,
            int? maxTokens = null) =>
            Task.FromResult(Reply);

        public Task<string> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            IToolRegistry registry,
            CancellationToken cancellationToken = default,
            ToolLoopOptions? loopOptions = null) =>
            Task.FromResult("SHOULD_NOT_RUN");
    }

    private sealed class InboundFakeMemory : IMemoryStore
    {
        public bool IsDatabaseOpen => true;
        public string DatabasePath => ":memory:";

        public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);

        public Task StoreEmbeddingAsync(long episodicId, float[] vector, string model, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(
            int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(long, string)>>(Array.Empty<(long, string)>());

        public Task<IReadOnlyList<string>> RecallSimilarAsync(
            float[] queryVector, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class InboundFakeMedia : ICompanionMediaService
    {
        public Task<IReadOnlyList<object>> ListModelsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());

        public Task<CompanionMediaAsset> GenerateAsync(
            string positivePrompt, string? negativePrompt, string? model, string? contactId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<CompanionMediaAsset> StoreInboundAsync(
            byte[] bytes, string contentType, string? contactId, CancellationToken ct = default) =>
            Task.FromResult(new CompanionMediaAsset("x", "victoria", "x.png", contentType, bytes.LongLength, DateTimeOffset.UtcNow, null));

        public bool TryGetFile(string mediaId, out string fullPath, out CompanionMediaAsset? meta)
        {
            fullPath = "";
            meta = null;
            return false;
        }

        public Task PushGeneratedToChatAsync(string mediaId, string? caption, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
