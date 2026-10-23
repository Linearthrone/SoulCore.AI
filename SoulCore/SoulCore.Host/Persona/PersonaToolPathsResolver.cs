using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Persona;

namespace SoulCore.Host.Persona;

/// <summary>
/// Active-persona tool path resolver (PROP-15.5). Reads pack stubs first, then Host Tools.
/// </summary>
public sealed class PersonaToolPathsResolver : IPersonaToolPathsResolver
{
    private readonly IPersonaSession _session;
    private readonly IOptions<ToolsOptions> _tools;
    private readonly IOptions<PersonaOptions> _persona;

    public PersonaToolPathsResolver(
        IPersonaSession session,
        IOptions<ToolsOptions> tools,
        IOptions<PersonaOptions> persona)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _persona = persona ?? throw new ArgumentNullException(nameof(persona));
    }

    public string ResolveDesktopTargetWindowTitle()
    {
        var pack = _session.GetActive();
        return ResolveDesktopTargetWindowTitle(pack);
    }

    /// <summary>
    /// PROP-16.6: resolve from the pack the chat turn already pinned.
    /// Does not call <see cref="IPersonaSession.GetActive"/>.
    /// </summary>
    public string ResolveDesktopTargetWindowTitle(PersonaPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var host = _tools.Value?.DesktopTargetWindowTitle;
        return PersonaToolPaths.ResolveDesktopTargetWindowTitle(pack.VmWindowTitle, host);
    }

    public string ResolvePlaywrightUserDataDir()
    {
        var pack = _session.GetActive();
        var host = _tools.Value?.PlaywrightUserDataDir;
        var root = (_persona.Value ?? new PersonaOptions()).ResolveRootDirectory();
        return PersonaToolPaths.ResolvePlaywrightUserDataDir(
            pack.PlaywrightProfileDir,
            host,
            root,
            pack.PersonaId);
    }
}
