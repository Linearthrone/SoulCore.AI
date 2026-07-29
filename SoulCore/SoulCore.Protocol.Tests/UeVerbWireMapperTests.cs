using System.Text.Json;
using SoulCore.Adapters.Ws.Protocol;

namespace SoulCore.Protocol.Tests;

public class UeVerbWireMapperTests
{
    [Fact]
    public void Speak_maps_to_plain_args_frame()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.Speak, new { text = "hello victoria" });

        Assert.Equal(UeVerbWireMapper.UeWireMapKind.Send, result.Kind);
        Assert.Equal("speak", result.UeCommandName);
        Assert.Equal("speak hello victoria", result.WireJson);
    }

    [Fact]
    public void Speak_collapses_whitespace_for_plain_frame()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.Speak, new { text = "line one\nline  two\t" });

        Assert.Equal("speak line one line two", result.WireJson);
    }

    [Fact]
    public void Speak_empty_text_is_bare_verb()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.Speak, new { text = "   " });

        Assert.Equal("speak", result.WireJson);
    }

    [Fact]
    public void PlayAnimation_maps_to_ue_command_envelope()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.PlayAnimation, new { name = "wave" });

        Assert.Equal(UeVerbWireMapper.UeWireMapKind.Send, result.Kind);
        Assert.Equal("play_animation", result.UeCommandName);
        Assert.NotNull(result.WireJson);

        using var doc = JsonDocument.Parse(result.WireJson!);
        Assert.Equal("command", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("play_animation", doc.RootElement.GetProperty("payload").GetProperty("name").GetString());
        Assert.Equal("wave", doc.RootElement.GetProperty("payload").GetProperty("args").GetProperty("name").GetString());
    }

    [Fact]
    public void Look_maps_to_autonomy_look_at_player()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.Look, new { target = "player" });

        Assert.Equal(UeVerbWireMapper.UeWireMapKind.Send, result.Kind);
        Assert.Equal("autonomy", result.UeCommandName);
        Assert.NotNull(result.WireJson);

        using var doc = JsonDocument.Parse(result.WireJson!);
        var args = doc.RootElement.GetProperty("payload").GetProperty("args");
        Assert.Equal("look_at_player", args.GetProperty("command").GetString());
    }

    [Fact]
    public void SetEmotion_maps_to_ue_command_envelope()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.SetEmotion, new
        {
            valence = 0.0,
            arousal = 0.0,
            dominance = 0.0,
            label = "calm"
        });

        Assert.Equal(UeVerbWireMapper.UeWireMapKind.Send, result.Kind);
        Assert.Equal("set_emotion", result.UeCommandName);
        Assert.NotNull(result.WireJson);

        using var doc = JsonDocument.Parse(result.WireJson!);
        Assert.Equal("command", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("set_emotion", doc.RootElement.GetProperty("payload").GetProperty("name").GetString());
        var args = doc.RootElement.GetProperty("payload").GetProperty("args");
        Assert.Equal(0.0, args.GetProperty("valence").GetDouble());
        Assert.Equal(0.0, args.GetProperty("arousal").GetDouble());
        Assert.Equal(0.0, args.GetProperty("dominance").GetDouble());
        Assert.Equal("calm", args.GetProperty("label").GetString());
    }

    [Fact]
    public void SetEmotion_derives_label_when_missing()
    {
        // Chat path sends V/A/D without label; mapper fills DescribeLabel(valence, arousal).
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.SetEmotion, new
        {
            valence = 0.0,
            arousal = 0.0,
            dominance = 0.5
        });

        Assert.Equal(UeVerbWireMapper.UeWireMapKind.Send, result.Kind);
        using var doc = JsonDocument.Parse(result.WireJson!);
        var args = doc.RootElement.GetProperty("payload").GetProperty("args");
        Assert.Equal("calm", args.GetProperty("label").GetString());
        Assert.Equal(0.5, args.GetProperty("dominance").GetDouble());
    }

    [Fact]
    public void Loco_maps_to_plain_move_avatar_relative()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.Loco, new
        {
            forward = 50.0,
            right = 10.0,
            up = -5.0
        });

        Assert.Equal(UeVerbWireMapper.UeWireMapKind.Send, result.Kind);
        Assert.Equal("move_avatar_relative", result.UeCommandName);
        Assert.Equal("move_avatar_relative 50 10 -5", result.WireJson);
    }

    [Fact]
    public void Loco_empty_payload_defaults_forward_50()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.Loco, null);

        Assert.Equal(UeVerbWireMapper.UeWireMapKind.Send, result.Kind);
        Assert.Equal("move_avatar_relative 50 0 0", result.WireJson);
    }

    [Fact]
    public void Loco_explicit_zeros_are_preserved()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.Loco, new
        {
            forward = 0.0,
            right = 0.0,
            up = 0.0
        });

        Assert.Equal("move_avatar_relative 0 0 0", result.WireJson);
    }

    [Fact]
    public void MoveTo_maps_to_plain_move_to()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.MoveTo, new { x = -100.5, y = 420.0, z = 10.0 });

        Assert.Equal(UeVerbWireMapper.UeWireMapKind.Send, result.Kind);
        Assert.Equal("move_to", result.UeCommandName);
        Assert.Equal("move_to -100.5 420 10", result.WireJson);
    }

    [Fact]
    public void Stop_maps_to_plain_stop()
    {
        var result = UeVerbWireMapper.Map(UnrealVerbTypes.Stop, null);

        Assert.Equal(UeVerbWireMapper.UeWireMapKind.Send, result.Kind);
        Assert.Equal("stop", result.UeCommandName);
        Assert.Equal("stop", result.WireJson);
    }

    [Fact]
    public void Sample_wire_speak_for_report_evidence()
    {
        var wire = UeVerbWireMapper.Map(UnrealVerbTypes.Speak, new { text = "sample" }).WireJson;
        Assert.Equal("speak sample", wire);
    }
}
