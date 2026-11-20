# PROP-22.3 — SEC-01 → PM-01: secrets, mailbox confirm, loopback bind

**Status:** Pass (code + build + unit tests). Built by the same FED/SEC pass as 22.4–22.9 so named controls stay consistent.

## What changed

| Requirement | Result |
|---|---|
| Saved API tokens are never shown | Settings → *Keys* rows (Companion, Ollama, Soniox, Synthetic) show only **Saved** / **Not set**, a masked (`PasswordChar`) replace box and Save/Clear. After Save the box is emptied; the stored value is never put back on screen. Companion row no longer prints `tokenLen` (`RefreshCompanionTokenStatus` → `Not set` / `Saved` / `Saved (.env)`); notices that used to embed `DescribePresence()` now say only "saved"/"cleared". |
| Companion token | Unchanged store (`CompanionTokenStore`). **Bug fixed on the way:** `Save` marks the file Hidden, so the *second* save threw `UnauthorizedAccessException` on Windows (this is also why two `CompanionTokenStoreTests` failed on a machine with an existing token). `Save` now resets attributes to Normal before writing. |
| Ollama / Soniox / Synthetic write path | `Services/SecretOverrideStore.cs` — `%LOCALAPPDATA%\HouseVictoria\secret-overrides.json` (hidden attr cleared before write) **plus** the Presence process env (`ApplyToProcessEnv` at startup, `overwrite:true` on Save). Env names: `SOULCORE_OLLAMA_API_KEY`, `SONIOX_API_KEY` (+ alias `SOULCORE_Voice__ApiKey`), `SOULCORE_SYNTHETIC_API_KEY` (+ alias `SYNTHETIC_API_KEY`). A Host that Presence starts inherits process env. |
| **Caveat (documented, UI says so)** | `SoulCore/.env` is loaded by the Host and **wins** for non-empty keys; a Settings value only takes effect for keys that are blank in `.env`. A Host that is already running must be restarted (Settings → Restart) to see a new key. *Clear* removes only Presence's copy. |
| Mailbox form | `EmailSetupWindow.EmailAccountSave_Click` now opens `Controls/ConfirmDialogWindow` naming the exact account (`Add/Update mailbox "<id>" for <address>?`) before `UpsertAsync`. Cancel writes nothing (status: "Cancelled — nothing was saved"). Reached from Settings → Advanced → *Mailbox form…* |
| Host bind | Settings → Advanced shows **Bind 127.0.0.1** read-only (`ConnectionDefaults.DefaultHost`, never editable). The only editable network value is the *client* port: `AdvancedPortSave_Click` validates 1024–65535, shows a **WARNING** confirm ("only changes where this app connects… does not move the Host"), stores `LocalUiSettings.ClientPort` and calls `ConnectionDefaults.UseClientPort`. `HOUSE_SOULCORE_PORT` in the environment still wins and the UI says so instead of pretending. |
| Access tool gates | Kept (Settings → *Access tools* expander, same handlers); hidden `EmailGate*` shims preserved for code-behind. |

## Evidence

```
> dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --no-incremental
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```
> dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj
Failed!  - Failed: 1, Passed: 242, Skipped: 0, Total: 243
  Failed: CompanionTokenEnvTests.TryLoadFromEnvFile_UsesConfiguredRepoRootOutsideBaseDirectory
          (environmental: this PC has a real saved companion token in %LOCALAPPDATA%\HouseVictoria,
           which outranks the temp .env the test writes — failed before PROP-22 too, untouched)
```

New unit tests: `SecretOverrideStoreTests` (Saved/Not set never echoes the value; second save over a hidden file; clear; unknown key rejected; alias env), `OperatorSurfaceClientsTests` (no secrets in bodies).

## Not verified / needs a human

- Visual check of the Keys rows and the mailbox confirm dialog on Windows (no automated UI driver here).
- End-to-end "key reaches Host" needs a Host restart; not done (live Host on :7710 left running).
