namespace SoulCore.Config;

/// <summary>
/// Victoria Link / companion phone surface (WS push + HTTP media).
/// Single-contact stub for now; <see cref="DefaultContactId"/> reserved for a future
/// external persona service.
/// </summary>
public sealed class CompanionOptions
{
    public const string SectionName = "Companion";

    /// <summary>Built-in Victoria contact id (framework stub for multi-persona later).</summary>
    public string DefaultContactId { get; set; } = "victoria";

    /// <summary>Display name for the default contact.</summary>
    public string DefaultContactName { get; set; } = "Victoria";

    /// <summary>ComfyUI HTTP base (loopback by default).</summary>
    public string ComfyUiBaseUrl { get; set; } = "http://127.0.0.1:8188";

    /// <summary>Optional path to a ComfyUI workflow JSON ({{positive}} / {{negative}} / {{seed}} placeholders).</summary>
    public string? ComfyUiWorkflowPath { get; set; }

    /// <summary>Preferred checkpoint filename when listing / auto-picking models. Empty = first available.</summary>
    public string? ComfyUiPreferredCheckpoint { get; set; }

    /// <summary>Default generation width.</summary>
    public int DefaultWidth { get; set; } = 512;

    /// <summary>Default generation height.</summary>
    public int DefaultHeight { get; set; } = 512;

    /// <summary>
    /// Directory for generated media files. Empty →
    /// <c>%LocalAppData%/SoulCore/companion-media</c>.
    /// </summary>
    public string MediaStorePath { get; set; } = "";

    /// <summary>Poll timeout for ComfyUI /history.</summary>
    public int ComfyUiTimeoutSeconds { get; set; } = 180;

    public string ResolveMediaStorePath()
    {
        if (!string.IsNullOrWhiteSpace(MediaStorePath))
            return Path.GetFullPath(MediaStorePath.Trim());

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "SoulCore", "companion-media");
    }
}
