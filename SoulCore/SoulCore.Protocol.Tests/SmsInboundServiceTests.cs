using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Host.Companion;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

public class SmsE164Tests
{
    [Theory]
    [InlineData("+15551234567", "+15551234567")]
    [InlineData("5551234567", "+15551234567")]
    [InlineData("1-555-123-4567", "+15551234567")]
    [InlineData("+44 7700 900123", "+447700900123")]
    public void Normalize_CommonForms(string raw, string expected) =>
        Assert.Equal(expected, SmsE164.Normalize(raw));

    [Fact]
    public void IsAllowlisted_EmptyAllowlist_Denies()
    {
        Assert.False(SmsE164.IsAllowlisted("+15551234567", ""));
        Assert.False(SmsE164.IsAllowlisted("+15551234567", null));
    }

    [Fact]
    public void IsAllowlisted_Match_AfterNormalize()
    {
        Assert.True(SmsE164.IsAllowlisted("5551234567", "+15551234567, +19998887777"));
        Assert.False(SmsE164.IsAllowlisted("+15550001111", "+15551234567"));
    }

    [Fact]
    public void Redact_DoesNotEchoFullNumber()
    {
        var r = SmsE164.Redact("+15551234567");
        Assert.DoesNotContain("1234567", r);
        Assert.Contains('*', r);
    }
}

public class SmsInboundServiceTests
{
    private const string AllowlistedPhone = "+15551234567";

    [Fact]
    public async Task UnknownSender_SilentDrop_NoInference()
    {
        var inference = new CountingInference();
        var sut = CreateSut(inference, allow: AllowlistedPhone, stub: true);
        var result = await sut.HandleAsync(new SmsInboundRequest("+19998887777", "hi", null, null));
        Assert.True(result.Ok);
        Assert.True(result.Dropped);
        Assert.Null(result.ReplyText);
        Assert.Equal(0, inference.CompleteCalls);
    }

    [Fact]
    public async Task Allowlisted_WithoutRegistry_UsesCompleteAsync_AppendsPresenceHistory()
    {
        // No IToolRegistry → CompleteAsync fallback (tests / degraded host).
        var inference = new CountingInference { Reply = "hey AllowlistedPhone" };
        var history = new ChatSessionHistoryStore(32);
        var sut = CreateSut(inference, allow: AllowlistedPhone, stub: false, history: history);
        var result = await sut.HandleAsync(new SmsInboundRequest("5551234567", "hello from phone", null, null));
        Assert.True(result.Ok);
        Assert.False(result.Dropped);
        Assert.Equal("hey AllowlistedPhone", result.ReplyText);
        Assert.Equal(1, inference.CompleteCalls);
        Assert.Equal(0, inference.ToolLoopCalls);
        var msgs = history.GetMessages("presence-local");
        Assert.Equal(2, msgs.Count);
        Assert.Equal("user", msgs[0].Role);
        Assert.Equal("assistant", msgs[1].Role);
    }

    [Fact]
    public async Task InboundImage_StoredAsMedia_NotToolInput()
    {
        var inference = new CountingInference { Reply = "nice pic" };
        var media = new FakeMedia();
        var sut = CreateSut(inference, allow: AllowlistedPhone, stub: true, media: media);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var result = await sut.HandleAsync(
            new SmsInboundRequest(AllowlistedPhone, "look", png, "image/png"));
        Assert.True(result.Ok);
        Assert.False(string.IsNullOrWhiteSpace(result.MediaId));
        Assert.Equal(1, media.StoreCalls);
        Assert.Equal(0, inference.ToolLoopCalls);
    }

    [Fact]
    public void BuildSmsPreamble_SmsAllowListOnly_NoDeniedToolAgency()
    {
        var p = SmsInboundService.BuildSmsPreamble(new[] { "prior note" }, useToolLoop: true);
        Assert.Contains("browser_navigate", p, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("memory_recall", p, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never call desktop_open_app", p, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("guest_shell", p, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ComputerUse", p, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Memory]", p);
        Assert.Contains("Kayleigh", p, StringComparison.Ordinal);
        Assert.Contains("same person as on Presence", p, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>PROP-18.1: SMS [Memory] shows occurred_at and today-first order.</summary>
    [Fact]
    public void BuildSmsPreamble_DatedLines_TodayFirstWithOccurredAt()
    {
        var now = new DateTimeOffset(2026, 11, 19, 18, 0, 0, TimeSpan.Zero);
        var p = SmsInboundService.BuildSmsPreamble(
            new[]
            {
                new MemoryRecallLine("older episode tea", "2026-11-10T09:00:00.000Z"),
                new MemoryRecallLine("today episode coffee", "2026-11-19T08:00:00.000Z"),
            },
            useToolLoop: false,
            mindBlock: null,
            utcNow: now);

        Assert.Contains("[Memory]", p, StringComparison.Ordinal);
        Assert.Contains("- 2026-11-19T08:00:00.000Z | today episode coffee", p, StringComparison.Ordinal);
        Assert.Contains("- 2026-11-10T09:00:00.000Z | older episode tea", p, StringComparison.Ordinal);
        Assert.True(
            p.IndexOf("today episode coffee", StringComparison.Ordinal)
            < p.IndexOf("older episode tea", StringComparison.Ordinal));
        // PROP-18.12 F1: same chronology steering as desk after [Memory].
        Assert.Contains(MemorySteeringGuidance.Marker, p, StringComparison.Ordinal);
        Assert.Contains("do not invent a clock", p, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSmsPreamble_WithoutToolLoop_ForbidsTools()
    {
        var p = SmsInboundService.BuildSmsPreamble(Array.Empty<string>(), useToolLoop: false);
        Assert.Contains("Do not call tools", p, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSmsPreamble_LocksKayleighAddressEvenIfMemorySilent()
    {
        var p = SmsInboundService.BuildSmsPreamble(Array.Empty<string>());
        Assert.Contains("Address her only as Kayleigh", p, StringComparison.Ordinal);
        Assert.Contains("do not become a generic assistant", p, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModelDown_StubDisabled_ReturnsChatModelDown()
    {
        var inference = new ThrowingInference();
        var sut = CreateSut(inference, allow: AllowlistedPhone, stub: false);
        var result = await sut.HandleAsync(new SmsInboundRequest(AllowlistedPhone, "hey", null, null));
        Assert.False(result.Ok);
        Assert.Equal("chat.model_down", result.Error);
        Assert.Null(result.ReplyText);
        Assert.Equal(1, inference.CompleteCalls);
    }

    [Fact]
    public async Task ModelDown_StubEnabled_ReturnsStubOk()
    {
        var inference = new ThrowingInference();
        var sut = CreateSut(inference, allow: AllowlistedPhone, stub: true);
        var result = await sut.HandleAsync(new SmsInboundRequest(AllowlistedPhone, "hey", null, null));
        Assert.True(result.Ok);
        Assert.True(result.UsedStub);
        Assert.Equal("stub", result.Provider);
        Assert.False(string.IsNullOrWhiteSpace(result.ReplyText));
    }

    private static SmsInboundService CreateSut(
        IInferenceClient inference,
        string allow,
        bool stub,
        IChatSessionHistoryStore? history = null,
        ICompanionMediaService? media = null)
    {
        return new SmsInboundService(
            Options.Create(new SmsOptions
            {
                AllowlistE164 = allow,
                StubWhenModelDown = stub,
                ConversationSessionId = "presence-local",
                OutboundEnabled = true,
                AutoReplySmsEnabled = true,
                MinSecondsBetweenSms = 0
            }),
            Options.Create(new InferenceOptions { Enabled = true }),
            Options.Create(new ChatWsOptions { StubWhenModelDown = stub }),
            Options.Create(new CompanionOptions()),
            inference,
            new FakeMemory(),
            history ?? new ChatSessionHistoryStore(32),
            media ?? new FakeMedia(),
            new PresenceWsHub(NullLogger<PresenceWsHub>.Instance),
            new SmsOutboundService(
                Options.Create(new SmsOptions
                {
                    AllowlistE164 = allow,
                    OutboundEnabled = true,
                    MinSecondsBetweenSms = 0,
                    MinSecondsBetweenMms = 0
                }),
                NullLogger<SmsOutboundService>.Instance),
            NullLogger<SmsInboundService>.Instance);
    }

    private sealed class CountingInference : IInferenceClient
    {
        public string Reply { get; set; } = "ok";
        public int CompleteCalls { get; private set; }
        public int ToolLoopCalls { get; private set; }

        public Task<string> CompleteAsync(
            string prompt,
            string? systemPreamble = null,
            CancellationToken cancellationToken = default,
            int? maxTokens = null)
        {
            CompleteCalls++;
            return Task.FromResult(Reply);
        }

        public Task<string> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            IToolRegistry registry,
            CancellationToken cancellationToken = default,
            ToolLoopOptions? loopOptions = null)
        {
            ToolLoopCalls++;
            return Task.FromResult("SHOULD_NOT_RUN");
        }
    }

    private sealed class ThrowingInference : IInferenceClient
    {
        public int CompleteCalls { get; private set; }

        public Task<string> CompleteAsync(
            string prompt,
            string? systemPreamble = null,
            CancellationToken cancellationToken = default,
            int? maxTokens = null)
        {
            CompleteCalls++;
            throw new HttpRequestException("Connection refused (127.0.0.1:11434)");
        }

        public Task<string> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            IToolRegistry registry,
            CancellationToken cancellationToken = default,
            ToolLoopOptions? loopOptions = null) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMemory : IMemoryStore
    {
        public bool IsDatabaseOpen => true;
        public string DatabasePath => ":memory:";

        public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);

        public Task StoreEmbeddingAsync(
            long episodicId,
            float[] vector,
            string model,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(long, string)>>(Array.Empty<(long, string)>());

        public Task<IReadOnlyList<string>> RecallSimilarAsync(
            float[] queryVector,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<MemoryRecallLine>> RecallSimilarDatedAsync(
            float[] queryVector, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemoryRecallLine>>(Array.Empty<MemoryRecallLine>());

        public Task<IReadOnlyList<MemoryRecallLine>> RecallRecentDatedAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemoryRecallLine>>(Array.Empty<MemoryRecallLine>());
    }

    private sealed class FakeMedia : ICompanionMediaService
    {
        public int StoreCalls { get; private set; }

        public Task<IReadOnlyList<object>> ListModelsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());

        public Task<CompanionMediaAsset> GenerateAsync(
            string positivePrompt,
            string? negativePrompt,
            string? model,
            string? contactId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<CompanionMediaAsset> StoreInboundAsync(
            byte[] bytes,
            string contentType,
            string? contactId,
            CancellationToken ct = default)
        {
            StoreCalls++;
            return Task.FromResult(new CompanionMediaAsset(
                Guid.NewGuid().ToString("N"),
                contactId ?? "victoria",
                "x.png",
                contentType,
                bytes.LongLength,
                DateTimeOffset.UtcNow,
                null));
        }

        public bool TryGetFile(string mediaId, out string fullPath, out CompanionMediaAsset? meta)
        {
            fullPath = "";
            meta = null;
            return false;
        }

        public Task PushGeneratedToChatAsync(
            string mediaId,
            string? caption,
            CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
