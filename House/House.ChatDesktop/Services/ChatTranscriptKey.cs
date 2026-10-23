namespace House.ChatDesktop.Services;

/// <summary>
/// PROP-16.1: local presence-chat.db rows are keyed by persona.
/// The legacy shared id stays readable so old rows are not copied into a persona.
/// </summary>
public static class ChatTranscriptKey
{
    public const string UnassignedId = "persona:none";

    public static string ForPersona(string? personaId)
    {
        var id = (personaId ?? string.Empty).Trim();
        if (id.Length == 0)
            return UnassignedId;
        return "persona:" + id;
    }
}
