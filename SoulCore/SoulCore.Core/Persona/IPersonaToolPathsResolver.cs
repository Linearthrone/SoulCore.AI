namespace SoulCore.Core.Persona;

/// <summary>
/// PROP-15.5: resolve desktop VM title + Playwright profile for the <b>active</b> PersonaPack.
/// Tool loop / desktop_* / Playwright bridges consume this instead of Victoria hardcodes.
/// </summary>
public interface IPersonaToolPathsResolver
{
    /// <summary>
    /// Active pack <see cref="PersonaPack.VmWindowTitle"/>, else Host
    /// <c>Tools:DesktopTargetWindowTitle</c>, else empty (unrestricted).
    /// </summary>
    string ResolveDesktopTargetWindowTitle();

    /// <summary>
    /// Desktop title for a pack already pinned on this turn.
    /// Does not read the live session. Host fallback is applied by the Host resolver;
    /// the default returns the pack title only.
    /// </summary>
    string ResolveDesktopTargetWindowTitle(PersonaPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return (pack.VmWindowTitle ?? string.Empty).Trim();
    }

    /// <summary>
    /// Active pack <see cref="PersonaPack.PlaywrightProfileDir"/>, else Host
    /// <c>Tools:PlaywrightUserDataDir</c>, else persona-scoped
    /// <c>{personasRoot}/{personaId}/browser</c>.
    /// </summary>
    string ResolvePlaywrightUserDataDir();
}
