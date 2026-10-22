---
type: report
prop_id: PROP-15.22
prop_root: PROP-15-persona-creation-platform
from: FED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
ticket: docs/agents/tasks/PROP-15.22-PM01-to-FED01.md
depends_on: none
siblings: PROP-15.21 (OPS Host recycle)
---

# PROP-15.22 — Persona switcher honesty + Host/Presence version currency — Pass

## Verdict

**Pass (client).** Presence switcher shows pack **DisplayName** (+ muted id), surfaces an actionable warning when Host lacks `/api/personas`, and title-bar / Settings → Updates answer Presence version, Host version, Presence update currency, and Host personas OK vs outdated. Default Velopack feed is **House-VictoriAI/SoulCore.AI_CreatorEdition**.

## Done

1. **PersonaSwitcher ItemTemplate** — `DisplayName` bold + `PersonaId` muted; title-bar ↻ reload; chrome status line under switcher (synced with Settings Identity status).
2. **404 / unreachable honesty** — `GET /api/personas` 404 → clear packs, red/orange status: *Host has no persona API — restart CreatorEdition Host*. Frame **Refresh** also reloads personas after Host recover.
3. **Version currency chrome** — `BuildVersionsText` = `Presence X · {Up to date|Update available|Unpackaged} · Host Y · {Personas OK|Host outdated|Host behind}`. Settings → Updates adds Presence currency + Host personas fields. `/health` parses `persona` object for Host stale signal; optional Host-behind-Presence informational flag.
4. **CreatorEdition feed default** — `PresenceUpdateService.DefaultGithubRepoUrl` → `https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition` (env/settings override unchanged).

## Key files

| File | Role |
| --- | --- |
| `House/House.ChatDesktop/MainWindow.axaml` | Switcher template, chrome status, Updates currency fields |
| `House/House.ChatDesktop/MainWindow.Persona.cs` | 404 clear + status; title-bar refresh |
| `House/House.ChatDesktop/MainWindow.Updates.cs` | Currency chrome wiring |
| `House/House.ChatDesktop/MainWindow.axaml.cs` | Frame Refresh → personas reload |
| `House/House.ChatDesktop/MainWindow.Presence.cs` | Health `persona` → Host currency |
| `House/House.ChatDesktop/Services/BuildCurrencyStatus.cs` | Format / stale helpers |
| `House/House.ChatDesktop/Services/PresenceUpdateService.cs` | CreatorEdition feed default |
| `House/House.ChatDesktop/Services/SoulCorePersonaClient.cs` | `PersonaApiMissing` on 404 |
| `House/House.ChatDesktop/Services/SoulCoreHealthClient.cs` | Parse `/health` `persona` |
| `House/House.ChatDesktop.Tests/BuildCurrencyStatusTests.cs` | Currency unit tests |
| `House/scripts/pack-presence.ps1` | Feed comment → CreatorEdition |

## Evidence

### Build (ChatDesktop)

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Release --no-incremental

Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:07.39
```

### Tests (currency + update + persona)

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj -c Release --filter "FullyQualifiedName~BuildCurrencyStatus|FullyQualifiedName~PresenceUpdateService|FullyQualifiedName~Persona"

Passed!  - Failed:     0, Passed:    51, Skipped:     0, Total:    51, Duration: 29 ms
```

### Acceptance checklist

- [x] Switcher shows pack display names; 404 Host shows actionable status
- [x] Chrome/Settings clearly answer: Presence version, Host version, Presence latest?, Host OK for personas?
- [x] CreatorEdition GitHub feed default
- [x] Report `docs/agents/reports/PROP-15.22-FED01-to-PM01.md`

## Notes for PM

- Live Host recycle still OPS (`PROP-15.21`); this client work makes a stale Host **visible** without guessing.
- Unpackaged desktop builds always show Presence currency **Unpackaged** (Velopack cannot install); installed builds run the background check against the CreatorEdition releases feed.
- No Host versioning scheme change (BED owns csproj bumps if OPS still sees 0.1.5 after recycle).
