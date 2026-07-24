namespace SoulCore.Adapters.Ws.Protocol;

/// <summary>
/// SoulCore-side Unreal verb names (logical). Wire format is UE-native via <see cref="UeVerbWireMapper"/>.
/// </summary>
public static class UnrealVerbTypes
{
    public const string SetEmotion = "set_emotion";
    public const string Speak = "speak";
    public const string PlayAnimation = "play_animation";
    public const string Loco = "loco";
    public const string Look = "look";
}
