using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SoulCore.Adapters.Ws;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Body;

namespace SoulCore.Protocol.Tests;

public class BodyToolsTests
{
    // ─────────────────────────────────────────────────────────────────────
    // speak
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Speak_CallsSpeakAsync_WithText()
    {
        var unreal = new FakeUnrealVerbClient(connected: true);
        var tool = new SpeakTool(unreal);
        var args = JsonDocument.Parse("""{"text":"one moment"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal(BodyToolBridge.OkContent, result.Content);
        Assert.Single(unreal.SpeakCalls);
        Assert.Equal("one moment", unreal.SpeakCalls[0]);
    }

    [Fact]
    public async Task Speak_BridgeDown_ReturnsUnavailable()
    {
        var unreal = new FakeUnrealVerbClient(connected: false);
        var tool = new SpeakTool(unreal);
        var args = JsonDocument.Parse("""{"text":"hello"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Equal(BodyToolBridge.UnavailableContent, result.Content);
        Assert.Single(unreal.SpeakCalls); // attempted; client returned false
    }

    [Fact]
    public async Task Speak_MissingText_ReturnsError()
    {
        var tool = new SpeakTool(new FakeUnrealVerbClient(connected: true));
        var args = JsonDocument.Parse("""{}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("text", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(BodyToolBridge.UnavailableContent, result.Content);
    }

    [Fact]
    public void Speak_Definition_MatchesSchema()
    {
        var tool = new SpeakTool(new FakeUnrealVerbClient(connected: true));
        Assert.Equal("speak", tool.Definition.Name);
        Assert.Equal(JsonValueKind.Object, tool.Definition.Parameters.ValueKind);
        Assert.True(tool.Definition.Parameters.TryGetProperty("properties", out var props));
        Assert.True(props.TryGetProperty("text", out _));
    }

    // ─────────────────────────────────────────────────────────────────────
    // play_animation
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PlayAnimation_CallsPlayAnimationAsync_WithCanonicalName()
    {
        var unreal = new FakeUnrealVerbClient(connected: true);
        var tool = new PlayAnimationTool(unreal);
        var args = JsonDocument.Parse("""{"name":"wave"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal("ok", result.Content);
        Assert.Equal(new[] { "wave" }, unreal.PlayAnimationCalls);
    }

    [Theory]
    [InlineData("wave_goodbye", "wave")]
    [InlineData("wave hello", "wave")]
    [InlineData("thumbs-up", "thumbs_up")]
    [InlineData("sit_down", "sit")]
    [InlineData("yes", "nod")]
    [InlineData("no", "shake_head")]
    [InlineData("applaud", "clap")]
    [InlineData("giggle", "laugh")]
    [InlineData("point_at", "point")]
    [InlineData("stand up", "stand")]
    public async Task PlayAnimation_MapsAliases_ToDetectAnimationIntentNames(string raw, string expected)
    {
        var unreal = new FakeUnrealVerbClient(connected: true);
        var tool = new PlayAnimationTool(unreal);
        var args = JsonDocument.Parse($"{{\"name\":\"{raw}\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal(expected, unreal.PlayAnimationCalls.Single());
    }

    [Fact]
    public void AnimationNameMap_CoversTwelveCanonicalNames()
    {
        Assert.Equal(12, AnimationNameMap.CanonicalNames.Count);
        foreach (var name in AnimationNameMap.CanonicalNames)
            Assert.Equal(name, AnimationNameMap.Resolve(name));
    }

    [Fact]
    public async Task PlayAnimation_BridgeDown_ReturnsUnavailable()
    {
        var unreal = new FakeUnrealVerbClient(connected: false);
        var tool = new PlayAnimationTool(unreal);
        var args = JsonDocument.Parse("""{"name":"wave"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Equal(BodyToolBridge.UnavailableContent, result.Content);
    }

    // ─────────────────────────────────────────────────────────────────────
    // move_to
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveTo_CallsMoveToAsync_WithAbsoluteCoords()
    {
        var unreal = new FakeUnrealVerbClient(connected: true);
        var tool = new MoveToTool(unreal);
        var args = JsonDocument.Parse("""{"x":100,"y":-50,"z":10}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Single(unreal.MoveToCalls);
        Assert.Empty(unreal.LocoCalls);
        var json = JsonSerializer.Serialize(unreal.MoveToCalls[0]);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(100, root.GetProperty("x").GetDouble());
        Assert.Equal(-50, root.GetProperty("y").GetDouble());
        Assert.Equal(10, root.GetProperty("z").GetDouble());
        Assert.Equal("absolute_path_follow", root.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task MoveTo_ZOptional_DefaultsToZero()
    {
        var unreal = new FakeUnrealVerbClient(connected: true);
        var tool = new MoveToTool(unreal);
        var args = JsonDocument.Parse("""{"x":30,"y":0}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        var json = JsonSerializer.Serialize(unreal.MoveToCalls[0]);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("z").GetDouble());
    }

    [Fact]
    public async Task MoveTo_BridgeDown_ReturnsUnavailable()
    {
        var unreal = new FakeUnrealVerbClient(connected: false);
        var tool = new MoveToTool(unreal);
        var args = JsonDocument.Parse("""{"x":1,"y":2}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Equal(BodyToolBridge.UnavailableContent, result.Content);
    }

    [Fact]
    public async Task MoveTo_MissingY_ReturnsError()
    {
        var tool = new MoveToTool(new FakeUnrealVerbClient(connected: true));
        var args = JsonDocument.Parse("""{"x":1}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("'y'", result.Content);
    }

    // ─────────────────────────────────────────────────────────────────────
    // look_at
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LookAt_Player_CallsLookAsync()
    {
        var unreal = new FakeUnrealVerbClient(connected: true);
        var tool = new LookAtTool(unreal);
        var args = JsonDocument.Parse("""{"target":"player"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Single(unreal.LookCalls);
        var json = JsonSerializer.Serialize(unreal.LookCalls[0]);
        Assert.Contains("player", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookAt_Point_CallsLookAsync_WithCoords()
    {
        var unreal = new FakeUnrealVerbClient(connected: true);
        var tool = new LookAtTool(unreal);
        var args = JsonDocument.Parse("""{"target":"10,20,30"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        var json = JsonSerializer.Serialize(unreal.LookCalls[0]);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("point", doc.RootElement.GetProperty("target").GetString());
        Assert.Equal(10, doc.RootElement.GetProperty("x").GetDouble());
        Assert.Equal(20, doc.RootElement.GetProperty("y").GetDouble());
        Assert.Equal(30, doc.RootElement.GetProperty("z").GetDouble());
    }

    [Fact]
    public async Task LookAt_BridgeDown_ReturnsUnavailable()
    {
        var unreal = new FakeUnrealVerbClient(connected: false);
        var tool = new LookAtTool(unreal);
        var args = JsonDocument.Parse("""{"target":"player"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Equal(BodyToolBridge.UnavailableContent, result.Content);
    }

    [Fact]
    public async Task LookAt_BadTarget_ReturnsError()
    {
        var tool = new LookAtTool(new FakeUnrealVerbClient(connected: true));
        var args = JsonDocument.Parse("""{"target":"somewhere"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("player", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────
    // set_emotion
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("happy")]
    [InlineData("sad")]
    [InlineData("angry")]
    [InlineData("curious")]
    [InlineData("neutral")]
    public async Task SetEmotion_CallsSetEmotionAsync_WithPreset(string emotion)
    {
        var unreal = new FakeUnrealVerbClient(connected: true);
        var tool = new SetEmotionTool(unreal);
        var args = JsonDocument.Parse($"{{\"emotion\":\"{emotion}\"}}").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Single(unreal.SetEmotionCalls);
        var json = JsonSerializer.Serialize(unreal.SetEmotionCalls[0]);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(emotion, doc.RootElement.GetProperty("label").GetString());
        Assert.True(doc.RootElement.TryGetProperty("valence", out _));
        Assert.True(doc.RootElement.TryGetProperty("arousal", out _));
        Assert.True(doc.RootElement.TryGetProperty("dominance", out _));
    }

    [Fact]
    public async Task SetEmotion_UnknownEmotion_ReturnsError()
    {
        var unreal = new FakeUnrealVerbClient(connected: true);
        var tool = new SetEmotionTool(unreal);
        var args = JsonDocument.Parse("""{"emotion":"ecstatic"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Empty(unreal.SetEmotionCalls);
        Assert.Contains("neutral", result.Content);
    }

    [Fact]
    public async Task SetEmotion_BridgeDown_ReturnsUnavailable()
    {
        var unreal = new FakeUnrealVerbClient(connected: false);
        var tool = new SetEmotionTool(unreal);
        var args = JsonDocument.Parse("""{"emotion":"happy"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Equal(BodyToolBridge.UnavailableContent, result.Content);
    }

    // ─────────────────────────────────────────────────────────────────────
    // NullUnrealVerbClient (Enabled=false path)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AllTools_NullUnrealVerbClient_ReturnUnavailable()
    {
        var unreal = new NullUnrealVerbClient();
        Assert.False(unreal.IsConnected);

        var speak = await new SpeakTool(unreal).ExecuteAsync(
            JsonDocument.Parse("""{"text":"hi"}""").RootElement.Clone());
        var anim = await new PlayAnimationTool(unreal).ExecuteAsync(
            JsonDocument.Parse("""{"name":"wave"}""").RootElement.Clone());
        var move = await new MoveToTool(unreal).ExecuteAsync(
            JsonDocument.Parse("""{"x":1,"y":2}""").RootElement.Clone());
        var look = await new LookAtTool(unreal).ExecuteAsync(
            JsonDocument.Parse("""{"target":"player"}""").RootElement.Clone());
        var emotion = await new SetEmotionTool(unreal).ExecuteAsync(
            JsonDocument.Parse("""{"emotion":"happy"}""").RootElement.Clone());

        Assert.Equal(BodyToolBridge.UnavailableContent, speak.Content);
        Assert.Equal(BodyToolBridge.UnavailableContent, anim.Content);
        Assert.Equal(BodyToolBridge.UnavailableContent, move.Content);
        Assert.Equal(BodyToolBridge.UnavailableContent, look.Content);
        Assert.Equal(BodyToolBridge.UnavailableContent, emotion.Content);
        Assert.False(speak.Success);
        Assert.False(anim.Success);
        Assert.False(move.Success);
        Assert.False(look.Success);
        Assert.False(emotion.Success);
    }

    // ─────────────────────────────────────────────────────────────────────
    // DI / registry
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void BodyTools_AppearInToolRegistry_GetDefinitions()
    {
        var unreal = new FakeUnrealVerbClient(connected: true);
        var services = new ServiceCollection();
        services.AddSingleton<IUnrealVerbClient>(unreal);
        services.AddSingleton<ITool, SpeakTool>();
        services.AddSingleton<ITool, PlayAnimationTool>();
        services.AddSingleton<ITool, MoveToTool>();
        services.AddSingleton<ITool, LookAtTool>();
        services.AddSingleton<ITool, SetEmotionTool>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        var names = registry.GetDefinitions().Select(d => d.Name).OrderBy(n => n).ToArray();

        Assert.Equal(
            new[] { "look_at", "move_to", "play_animation", "set_emotion", "speak" },
            names);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Fake
    // ─────────────────────────────────────────────────────────────────────

    private sealed class FakeUnrealVerbClient : IUnrealVerbClient
    {
        private readonly bool _connected;

        public FakeUnrealVerbClient(bool connected) => _connected = connected;

        public bool IsConnected => _connected;
        public string TargetUrl => _connected ? "ws://test" : "disabled";

        public List<string> SpeakCalls { get; } = new();
        public List<string> PlayAnimationCalls { get; } = new();
        public List<object> LocoCalls { get; } = new();
        public List<object> MoveToCalls { get; } = new();
        public int StopCallCount { get; private set; }
        public List<object> LookCalls { get; } = new();
        public List<object> SetEmotionCalls { get; } = new();

        public Task EnsureConnectedAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> SetEmotionAsync(object emotionPayload, CancellationToken cancellationToken = default)
        {
            SetEmotionCalls.Add(emotionPayload);
            return Task.FromResult(_connected);
        }

        public Task<bool> SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            SpeakCalls.Add(text);
            return Task.FromResult(_connected);
        }

        public Task<bool> SpeakAsync(object speakPayload, CancellationToken cancellationToken = default)
        {
            var text = speakPayload?.ToString() ?? "";
            if (speakPayload is not null)
            {
                var prop = speakPayload.GetType().GetProperty("text")
                    ?? speakPayload.GetType().GetProperty("Text");
                if (prop?.GetValue(speakPayload) is string s)
                    text = s;
            }
            SpeakCalls.Add(text);
            return Task.FromResult(_connected);
        }

        public Task<bool> PlayAnimationAsync(string animationName, CancellationToken cancellationToken = default)
        {
            PlayAnimationCalls.Add(animationName);
            return Task.FromResult(_connected);
        }

        public Task<bool> LocoAsync(object locoPayload, CancellationToken cancellationToken = default)
        {
            LocoCalls.Add(locoPayload);
            return Task.FromResult(_connected);
        }

        public Task<bool> MoveToAsync(object moveToPayload, CancellationToken cancellationToken = default)
        {
            MoveToCalls.Add(moveToPayload);
            return Task.FromResult(_connected);
        }

        public Task<bool> StopAsync(CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            return Task.FromResult(_connected);
        }

        public Task<bool> LookAsync(object lookPayload, CancellationToken cancellationToken = default)
        {
            LookCalls.Add(lookPayload);
            return Task.FromResult(_connected);
        }
    }
}
