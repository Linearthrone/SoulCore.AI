using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Persona;

namespace SoulCore.Host.Persona;

/// <summary>
/// File-backed PersonaPack store under LocalAppData/SoulCore/personas/{id}/pack.json.
/// Seeds Blank + starters on first use; Victoria is optional and never the boot default.
/// </summary>
public sealed class PersonaPackStore : IPersonaPackStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly PersonaOptions _options;
    private readonly ILogger<PersonaPackStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PersonaPackStore(IOptions<PersonaOptions> options, ILogger<PersonaPackStore> logger)
    {
        _options = options?.Value ?? new PersonaOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        RootDirectory = _options.ResolveRootDirectory();
    }

    /// <summary>Test helper: construct against an explicit root.</summary>
    public PersonaPackStore(string rootDirectory, ILogger<PersonaPackStore>? logger = null)
    {
        _options = new PersonaOptions { RootDirectory = rootDirectory };
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PersonaPackStore>.Instance;
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(RootDirectory);
            await SeedOrMergeStarterAsync(PersonaPack.CreateBlank(), cancellationToken).ConfigureAwait(false);
            await SeedOrMergeStarterAsync(PersonaPack.CreateMentorStarter(), cancellationToken).ConfigureAwait(false);
            await SeedOrMergeStarterAsync(PersonaPack.CreateAnalystStarter(), cancellationToken).ConfigureAwait(false);
            await SeedOrMergeStarterAsync(PersonaPack.CreateVictoriaStarter(), cancellationToken).ConfigureAwait(false);

            var activePath = ActiveMarkerPath();
            if (!File.Exists(activePath))
            {
                var defaultId = string.IsNullOrWhiteSpace(_options.DefaultActivePersonaId)
                    ? PersonaPack.BlankPersonaId
                    : _options.DefaultActivePersonaId.Trim();
                // Never force Victoria as boot default even if misconfigured.
                if (string.Equals(defaultId, PersonaPack.VictoriaPersonaId, StringComparison.OrdinalIgnoreCase))
                    defaultId = PersonaPack.BlankPersonaId;
                await WriteActiveIdUnlockedAsync(defaultId, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Persona store seeded under {Root}; active={ActiveId}",
                    RootDirectory,
                    defaultId);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<PersonaPack>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = new List<PersonaPack>();
            foreach (var dir in Directory.EnumerateDirectories(RootDirectory))
            {
                var packPath = Path.Combine(dir, "pack.json");
                if (!File.Exists(packPath))
                    continue;
                var pack = await ReadPackFileAsync(packPath, cancellationToken).ConfigureAwait(false);
                if (pack is not null)
                    list.Add(pack);
            }

            list.Sort((a, b) => string.Compare(a.PersonaId, b.PersonaId, StringComparison.OrdinalIgnoreCase));
            return list;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonaPack?> GetAsync(string personaId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadPackUnlockedAsync(PersonaMemoryPaths.NormalizePersonaId(personaId), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonaPack> UpsertAsync(PersonaPack pack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(pack.PersonaId);

        var description = (pack.PhysicalDescription ?? string.Empty).Trim();
        if (description.Length > PersonaPack.PhysicalDescriptionMaxLength)
        {
            throw new ArgumentException(
                $"physicalDescription must be at most {PersonaPack.PhysicalDescriptionMaxLength} characters.");
        }

        pack.PersonaId = PersonaMemoryPaths.NormalizePersonaId(pack.PersonaId);
        if (string.IsNullOrWhiteSpace(pack.DisplayName))
            pack.DisplayName = pack.PersonaId;
        if (string.IsNullOrWhiteSpace(pack.ContactId))
            pack.ContactId = pack.PersonaId;
        if (string.IsNullOrWhiteSpace(pack.HumanAddress))
            pack.HumanAddress = "Friend";
        pack.Traits ??= new PersonaTraitScales();
        pack.ToolPolicy ??= new PersonaToolPolicy();
        pack.PhysicalDescription = description;
        pack.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dir = PackDirectory(pack.PersonaId);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "pack.json");
            var json = JsonSerializer.Serialize(pack, JsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Persona pack upserted: {PersonaId}", pack.PersonaId);
            return pack.Clone();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(string personaId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        var id = PersonaMemoryPaths.NormalizePersonaId(personaId);
        if (string.Equals(id, PersonaPack.BlankPersonaId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot delete the Blank persona pack.");

        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var activeId = await ReadActiveIdUnlockedAsync(cancellationToken).ConfigureAwait(false);
            if (string.Equals(activeId, id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cannot delete the active persona; activate another pack first.");

            var dir = PackDirectory(id);
            if (!Directory.Exists(dir))
                return false;

            Directory.Delete(dir, recursive: true);
            _logger.LogInformation("Persona pack deleted: {PersonaId}", id);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetActivePersonaIdAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadActiveIdUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetActivePersonaIdAsync(string personaId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        var id = PersonaMemoryPaths.NormalizePersonaId(personaId);
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var pack = await ReadPackUnlockedAsync(id, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Unknown personaId '{id}'.");
            _ = pack;
            await WriteActiveIdUnlockedAsync(id, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Active persona set: {PersonaId}", id);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// PROP-15.8: write starter when missing; when present, fill blank tool-path
    /// fields from the built-in starter so stale on-disk seeds pick up
    /// <c>VmWindowTitle</c> / <c>PlaywrightProfileDir</c> without clobbering
    /// operator-set values.
    /// </summary>
    private async Task SeedOrMergeStarterAsync(PersonaPack starter, CancellationToken cancellationToken)
    {
        var dir = PackDirectory(starter.PersonaId);
        var path = Path.Combine(dir, "pack.json");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(starter, JsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            return;
        }

        var existing = await ReadPackFileAsync(path, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return;

        if (!TryMergeMissingToolPaths(existing, starter))
            return;

        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var merged = JsonSerializer.Serialize(existing, JsonOptions);
        await File.WriteAllTextAsync(path, merged, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Persona starter tool-path fields refreshed: {PersonaId} vmWindowTitle={VmTitle}",
            existing.PersonaId,
            existing.VmWindowTitle ?? "");
    }

    /// <summary>
    /// Copies non-blank starter tool-path fields into <paramref name="existing"/>
    /// only where the on-disk pack left them blank. Returns true when mutated.
    /// </summary>
    public static bool TryMergeMissingToolPaths(PersonaPack existing, PersonaPack starter)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(starter);

        var changed = false;

        if (string.IsNullOrWhiteSpace(existing.VmWindowTitle)
            && !string.IsNullOrWhiteSpace(starter.VmWindowTitle))
        {
            existing.VmWindowTitle = starter.VmWindowTitle.Trim();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(existing.PlaywrightProfileDir)
            && !string.IsNullOrWhiteSpace(starter.PlaywrightProfileDir))
        {
            existing.PlaywrightProfileDir = starter.PlaywrightProfileDir.Trim();
            changed = true;
        }

        return changed;
    }

    private async Task<PersonaPack?> ReadPackUnlockedAsync(string personaId, CancellationToken cancellationToken)
    {
        var path = Path.Combine(PackDirectory(personaId), "pack.json");
        if (!File.Exists(path))
            return null;
        return await ReadPackFileAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PersonaPack?> ReadPackFileAsync(string path, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var pack = JsonSerializer.Deserialize<PersonaPack>(json, JsonOptions);
        if (pack is null || string.IsNullOrWhiteSpace(pack.PersonaId))
            return null;
        pack.Traits ??= new PersonaTraitScales();
        pack.ToolPolicy ??= new PersonaToolPolicy();
        pack.PhysicalDescription ??= string.Empty;
        return pack;
    }

    private async Task<string> ReadActiveIdUnlockedAsync(CancellationToken cancellationToken)
    {
        var path = ActiveMarkerPath();
        if (!File.Exists(path))
            return PersonaPack.BlankPersonaId;
        var id = (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
        return string.IsNullOrWhiteSpace(id) ? PersonaPack.BlankPersonaId : PersonaMemoryPaths.NormalizePersonaId(id);
    }

    private Task WriteActiveIdUnlockedAsync(string personaId, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(ActiveMarkerPath(), personaId.Trim() + Environment.NewLine, cancellationToken);

    private string PackDirectory(string personaId) => Path.Combine(RootDirectory, personaId);

    private string ActiveMarkerPath() => Path.Combine(RootDirectory, "active.txt");
}
