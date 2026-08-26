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
| `home-pc` | Windows home (`kaia-reimagined`) | `C:\Users\kurtw\Soul_Core` | Host, ChatDesktop, `.env`, ALLSTART, Ollama, Tailscale serve, **primary My Machines worker** |
| `kayleigh-tab` | Samsung Tab — **not native Termux** (see below) | n/a for now | SMS gateway stays Termux **scripts**; Cursor worker deferred |

Same Cursor account. Workers must start inside a **git checkout** of
`github.com/Linearthrone/SoulCore.AI` (remote URL must match the agent’s repo).

### Tablet / Termux: CLI will not run (known)

Official `agent` ships a Linux **glibc** Node binary. Termux is Android **bionic** and
rejects it with:

```text
error: ".../cursor-agent/versions/.../node" has unexpected e_type: 2
```

That is **not** a PATH bug — PATH can be correct and `agent` still fails.
Do **not** chase unofficial Termux patches for the companion token box.

**Practical split:**

1. Run My Machines only on **`home-pc`** (Windows install + worker).
2. Keep tablet SMS as Termux scripts (`sms-to-victoria.sh` / Tasker) — Kurt or
   home-pc agent writes the script; tablet only executes it.
3. Optional later: `proot-distro` Ubuntu on the tab, then install `agent` **inside**
   that distro (real Linux userland). Only if we need a true `kayleigh-tab` worker.

## One-time: home PC

### Native Windows worker — currently broken (Cursor bug)

`agent login` works, but `agent worker start` on **native Windows** dies with:

```text
better_sqlite3.node was compiled against ... NODE_MODULE_VERSION 127
This version of Node.js requires NODE_MODULE_VERSION 137
```

Same on build `2026.08.11-e8db854`. Reinstall does **not** fix it — bad Windows
package. Cursor is tracking it; use **WSL** until fixed.

### Workaround: worker inside WSL Ubuntu

In **elevated** PowerShell once:

```powershell
wsl --install -d Ubuntu
# reboot if prompted, finish Ubuntu user setup
```

Then in **Ubuntu (WSL)**:

```bash
curl https://cursor.com/install -fsS | bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
source ~/.bashrc

# Repo must live in the WSL filesystem — NOT /mnt/c/...
mkdir -p ~/repos && cd ~/repos
git clone https://github.com/Linearthrone/SoulCore.AI.git
cd SoulCore.AI

agent login
agent worker start --name home-pc
```

Leave that WSL window open. Machine shows as **`home-pc`** in
[cursor.com/agents](https://cursor.com/agents).

**Notes for SoulCore:**

- WSL can usually hit Host at `http://127.0.0.1:7700` (health/WS probes).
- `ALLSTART.ps1` is Windows — from WSL call:
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\kurtw\Soul_Core\ALLSTART.ps1' -RestartHost`
- Live `.env` stays on the Windows tree; don’t commit secrets from the WSL clone.

## Tablet (Termux) — no My Machines worker (for now)

Skip `agent worker start` on native Termux (see e_type error above).

Tablet stays a **dumb gateway**:

```bash
# after sms-to-victoria.sh is in place
~/bin/sms-to-victoria.sh '+1XXXXXXXXXX' 'script smoke'
```

Wire Tasker → that script when ready. Home-pc agent can draft/update the script
in git; Kurt copies or `scp`s it to the tab.

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
