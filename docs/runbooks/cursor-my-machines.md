# Cursor My Machines — home PC + tablet workers

Cloud agents (including this one) plan in Cursor’s cloud. **My Machines** workers
run the actual Shell/Read/Write/browser tools on hardware you control — no inbound
ports, outbound HTTPS only.

Use this when Kurt should stop hand-running PowerShell / Termux steps that a
local agent can do: Host restarts, ChatDesktop WS checks, SMS→POST scripts,
Tailscale probes, Tasker wiring on the tab.

Official docs: [My Machines](https://cursor.com/docs/cloud-agent/self-hosted-guides/my-machines.md)

## Machines (names are locked)

| Name | Where | Worker dir | Jobs |
|------|--------|------------|------|
| `home-pc` | Windows home (`kaia-reimagined`) | `C:\Users\kurtw\Soul_Core` | Host, ChatDesktop, `.env`, ALLSTART, Ollama, Tailscale serve |
| `kayleigh-tab` | Samsung Tab SM-X218U Termux | `~/repos/SoulCore.AI` | Termux curl, `sms-to-victoria.sh`, Tailscale client, SMS bridge |

Same Cursor account on both. Workers must start inside a **git checkout** of
`github.com/Linearthrone/SoulCore.AI` (remote URL must match the agent’s repo).

## One-time: home PC

PowerShell:

```powershell
cd C:\Users\kurtw\Soul_Core
agent --version
agent login
agent worker start --name home-pc --worker-dir C:\Users\kurtw\Soul_Core
```

Leave that window open (or run under Task Scheduler / NSSM later). Debug:

```powershell
agent worker debug
```

## One-time: tablet (Termux)

```bash
agent --version
agent login

mkdir -p ~/repos
cd ~/repos
# if missing:
git clone https://github.com/Linearthrone/SoulCore.AI.git
cd SoulCore.AI
git pull

# keep Termux awake (acquire wake lock in notification if available)
agent worker start --name kayleigh-tab
```

Debug:

```bash
agent worker debug
```

If `agent` is not on PATH after install, reopen Termux or `source ~/.bashrc`.

## How to send work to a machine

### From cursor.com/agents

1. Open [cursor.com/agents](https://cursor.com/agents)
2. New agent → pick environment / machine **`home-pc`** or **`kayleigh-tab`**
3. Paste the task (examples below)

### From Slack / GitHub (optional)

Include the machine name:

- `@Cursor worker=home-pc restart Host with ALLSTART -RestartHost and confirm /health`
- `@Cursor worker=kayleigh-tab run sms-to-victoria.sh smoke against Tailscale Host`

### From this cloud session

This managed cloud VM **cannot** execute on your LAN. After workers are online,
Kurt (or a follow-up) starts a **new** agent targeting `home-pc` / `kayleigh-tab`.
Paste that agent’s URL back here if you want the cloud agent to read results via
dashboard / PR.

## Example prompts

**home-pc — WS / Host**

```text
On home-pc: confirm SoulCore Host /health and ChatDesktop WS auth.
Run: .\SoulCore\scripts\ws-companion-auth-probe.ps1
If WS auth fails, clear User-level SOULCORE_COMPANION_API_TOKEN, ALLSTART -RestartHost,
and report Conn status + tokenLen only (never the secret).
```

**kayleigh-tab — SMS bridge**

```text
On kayleigh-tab: ensure ~/bin/sms-to-victoria.sh exists with HOST/TOKEN matching
the working Termux curl. Smoke: ~/bin/sms-to-victoria.sh '+17066274581' 'script smoke'
Then document Tasker Received Text → script wiring. Do not paste the raw token into chat.
```

## Networking

Workers need outbound HTTPS to:

- `api2.cursor.sh` / `api2direct.cursor.sh`
- `cloud-agent-artifacts.s3.us-east-1.amazonaws.com` (artifacts)

Tablet + home already use Tailscale for Host reachability; workers do **not** need
extra inbound ports.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Machine missing in dropdown | Worker not running / wrong Cursor login / wrong `--name` |
| “registered for a different repository” | `cd` into SoulCore.AI checkout before `agent worker start` |
| Tablet CLI not found | Re-install: `curl https://cursor.com/install -fsS \| bash` then new Termux session |
| Worker dies when Termux sleeps | Enable wake lock; consider `tmux` + `agent worker start` inside |
| Home worker dies when you close PS | Keep window open or install as a background service later |

## Security

- Prefer `agent login` (browser) over pasting API keys into tablet chat logs
- Never commit `SoulCore/.env` or put companion tokens in agent prompts
- Tablet worker can read SMS/scripts on-device — treat it as a privileged gateway box
