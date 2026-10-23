using Microsoft.Extensions.Logging.Abstractions;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Persona;
using SoulCore.Host.Persona;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// PROP-15.2: dual-persona switch must prove zero cross-read of episodic / charter / journal.
/// </summary>
public class PersonaStoreHubQuarantineTests
{
    [Fact]
    public void ResolveMemoryDbPath_KeysByPersonaId_UnderPersonasRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "soulcore-paths-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pathA = PersonaMemoryPaths.ResolveMemoryDbPath(root, "alpha");
            var pathB = PersonaMemoryPaths.ResolveMemoryDbPath(root, "beta");
            Assert.NotEqual(pathA, pathB);
            Assert.Contains($"{Path.DirectorySeparatorChar}alpha{Path.DirectorySeparatorChar}memory{Path.DirectorySeparatorChar}", pathA);
            Assert.Contains($"{Path.DirectorySeparatorChar}beta{Path.DirectorySeparatorChar}memory{Path.DirectorySeparatorChar}", pathB);
            Assert.EndsWith("soulcore_memory.db", pathA);
            Assert.EndsWith(
                Path.Combine("beta", "vector"),
                PersonaMemoryPaths.ResolveVectorDirectory(root, "beta"));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task DualPersonaSwitch_ZeroCrossRead_EpisodicCharterJournal()
    {
        var root = CreateTempRoot();
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        try
        {
            const string personaA = "persona-a";
            const string personaB = "persona-b";
            const string secretA = "QUARANTINE-SECRET-A-episodic";
            const string secretB = "QUARANTINE-SECRET-B-episodic";
            const string charterA = "I am Persona A and only A.";
            const string charterB = "I am Persona B and only B.";
            const string journalA = "journal-feeling-only-A";
            const string journalB = "journal-feeling-only-B";

            // --- Persona A writes ---
            await hub.SwitchActiveAsync(personaA);
            var pathA = hub.ResolveMemoryDbPath(personaA);
            Assert.Equal(pathA, hub.GetMemoryStore(personaA).DatabasePath);

            await hub.GetMemoryStore(personaA).WriteEpisodicAsync(secretA, "chat");
            await hub.GetCharter(personaA).SeedAsync(new[]
            {
                new CharterAnchorSeed("identity", "Name A", charterA, 10, true)
            });
            await hub.GetJournalStore(personaA).WriteEntryAsync("feeling", journalA, source: "test");

            // --- Switch to B; write distinct rows ---
            await hub.SwitchActiveAsync(personaB);
            var pathB = hub.ResolveMemoryDbPath(personaB);
            Assert.NotEqual(pathA, pathB);
            Assert.True(File.Exists(pathA));
            Assert.True(File.Exists(pathB));

            await hub.GetMemoryStore(personaB).WriteEpisodicAsync(secretB, "chat");
            await hub.GetCharter(personaB).SeedAsync(new[]
            {
                new CharterAnchorSeed("identity", "Name B", charterB, 10, true)
            });
            await hub.GetJournalStore(personaB).WriteEntryAsync("feeling", journalB, source: "test");

            // Active B must not see A's content
            var bEpisodic = await hub.GetMemoryStore(personaB).RecallRecentAsync(20);
            Assert.Contains(bEpisodic, t => t.Contains(secretB));
            Assert.DoesNotContain(bEpisodic, t => t.Contains(secretA));

            var bCharter = await hub.GetCharter(personaB).GetAnchorsByKindAsync("identity");
            Assert.Contains(bCharter, t => t.Contains(charterB));
            Assert.DoesNotContain(bCharter, t => t.Contains(charterA));

            var bJournal = await hub.GetJournalStore(personaB).ListEntriesAsync("feeling", limit: 20);
            Assert.Contains(bJournal, e => e.Body.Contains(journalB));
            Assert.DoesNotContain(bJournal, e => e.Body.Contains(journalA));

            // PROP-16.3: episodic lookup by id stays open while another persona is active.
            // Charter / journal accessors still refuse a non-active id.
            var aWhileB = await hub.GetMemoryStore(personaA).RecallRecentAsync(20);
            Assert.Contains(aWhileB, t => t.Contains(secretA));
            Assert.DoesNotContain(aWhileB, t => t.Contains(secretB));
            Assert.Throws<InvalidOperationException>(() => hub.GetCharter(personaA));

            // --- Switch back to A ---
            await hub.SwitchActiveAsync(personaA);

            var aEpisodic = await hub.GetMemoryStore(personaA).RecallRecentAsync(20);
            Assert.Contains(aEpisodic, t => t.Contains(secretA));
            Assert.DoesNotContain(aEpisodic, t => t.Contains(secretB));

            var aCharter = await hub.GetCharter(personaA).GetAnchorsByKindAsync("identity");
            Assert.Contains(aCharter, t => t.Contains(charterA));
            Assert.DoesNotContain(aCharter, t => t.Contains(charterB));

            var aJournal = await hub.GetJournalStore(personaA).ListEntriesAsync("feeling", limit: 20);
            Assert.Contains(aJournal, e => e.Body.Contains(journalA));
            Assert.DoesNotContain(aJournal, e => e.Body.Contains(journalB));

            // PROP-16.3: B's episodic file is still readable by id while A is active.
            var bWhileA = await hub.GetMemoryStore(personaB).RecallRecentAsync(20);
            Assert.Contains(bWhileA, t => t.Contains(secretB));
            Assert.DoesNotContain(bWhileA, t => t.Contains(secretA));
            Assert.Throws<InvalidOperationException>(() => hub.GetCharter(personaB));
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ActivePersonaSession_SetActive_ReopensStoresForNewPersona()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        try
        {
            var session = new ActivePersonaSession(packs, hub, NullLogger<ActivePersonaSession>.Instance);
            await session.InitializeAsync();

            Assert.Equal(PersonaPack.BlankPersonaId, hub.ActivePersonaId);
            var blankPath = hub.ResolveMemoryDbPath(PersonaPack.BlankPersonaId);
            Assert.True(File.Exists(blankPath));

            await hub.GetMemoryStore(PersonaPack.BlankPersonaId)
                .WriteEpisodicAsync("blank-only-memory", "chat");

            await session.SetActiveAsync(PersonaPack.MentorPersonaId);
            Assert.Equal(PersonaPack.MentorPersonaId, hub.ActivePersonaId);

            var mentorRecent = await hub.GetMemoryStore(PersonaPack.MentorPersonaId).RecallRecentAsync(10);
            Assert.DoesNotContain(mentorRecent, t => t.Contains("blank-only-memory"));

            var mentorPath = hub.ResolveMemoryDbPath(PersonaPack.MentorPersonaId);
            Assert.NotEqual(blankPath, mentorPath);
            Assert.True(File.Exists(mentorPath));
            Assert.True(Directory.Exists(hub.ResolveVectorDirectory(PersonaPack.MentorPersonaId)));
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ScopedFacades_ForwardOnlyToActivePersona()
    {
        var root = CreateTempRoot();
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        try
        {
            IMemoryStore memory = new PersonaScopedMemoryStore(hub);
            ICharter charter = new PersonaScopedCharter(hub);
            IVictoriaJournalStore journals = new PersonaScopedJournalStore(hub);

            await hub.SwitchActiveAsync("facade-a");
            await memory.WriteEpisodicAsync("facade-A-row", "chat");
            await charter.SeedAsync(new[]
            {
                new CharterAnchorSeed("identity", "A", "facade-charter-A", 1, true)
            });
            await journals.WriteEntryAsync("feeling", "facade-journal-A");

            await hub.SwitchActiveAsync("facade-b");
            var recent = await memory.RecallRecentAsync(10);
            Assert.DoesNotContain(recent, t => t.Contains("facade-A-row"));
            Assert.Empty(await charter.GetAnchorsAsync());
            Assert.Empty(await journals.ListEntriesAsync("feeling", 10));

            // SoulLoop / chat DI path: DatabasePath follows active persona
            Assert.Equal(hub.ResolveMemoryDbPath("facade-b"), memory.DatabasePath);
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), "soulcore-quarantine-" + Guid.NewGuid().ToString("N"));

    private static void TryDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // best-effort — SQLite may briefly hold handles on Windows
        }
    }
}
