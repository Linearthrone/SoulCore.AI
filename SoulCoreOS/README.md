# SoulCoreOS

House Victoria **soul-machine OS**: a custom [bootc](https://bootc.dev/) image
derived from **[Bazzite nvidia-open](https://bazzite.gg/)**.

| | |
| --- | --- |
| Base | `ghcr.io/ublue-os/bazzite-nvidia-open:stable` |
| Target GPU | NVIDIA RTX 5070 Ti (Blackwell) |
| Sister app | SoulCore.AI (`SoulCore.Host`) → body `ws://house-victoria:8888` |
| Explicitly excluded | Unreal Engine · proprietary `bazzite-nvidia` · Cursor/Stability Matrix bake |

Architecture: [`docs/architecture/SOULCOREOS.md`](docs/architecture/SOULCOREOS.md).

---

## Why `bazzite-nvidia-open` (not proprietary)

Blackwell (RTX 50-series, including **5070 Ti**) **requires** NVIDIA's **open**
GPU kernel modules. The legacy closed modules used by `bazzite-nvidia` will not
drive this GPU (`NVRM: ... requires use of the NVIDIA open kernel modules`).

Bazzite's own FAQ:

- `-nvidia-open` → Turing and newer (GTX 16 + all RTX)
- `-nvidia` → Pascal / Maxwell / Volta legacy only

SoulCoreOS therefore **must** `FROM ghcr.io/ublue-os/bazzite-nvidia-open`. Do not
rebase this machine onto `bazzite-nvidia`.

---

## What the image actually installs

Reviewed against Universal Blue **image-template** + Bazzite packaging (2026-07):

| Component | Mechanism | Notes |
| --- | --- | --- |
| Open NVIDIA kmods + userspace | **Base image** | Already in `bazzite-nvidia-open` — we do not reinstall |
| Steam / gaming stack | **Base image** | Bazzite desktop |
| Distrobox + Podman | Base / `dnf5 install distrobox` | Idempotent ensure |
| Tailscale | Base or official Fedora repo + `systemctl enable tailscaled` | Soft-install if missing |
| .NET 8 ASP.NET Core runtime | `dnf5 install aspnetcore-runtime-8.0` (Microsoft feed fallback) | Runs Host |
| Ollama | Official GitHub `ollama-linux-*.tgz` → `/usr` + `ollama.service` | Pinned `OLLAMA_VERSION` |
| Hermes | `hermes.service` + `/usr/libexec/soulcoreos/hermes-bootstrap` | First start creates venv under `/var/lib/hermes` |
| Opera | Optional `opera-stable` RPM | Soft-fail → `opera=skipped` in manifest |
| SoulCore.Host | `soulcore-host.service` only | Publish DLL to `/opt/soulcore/host` after boot |
| p4d | `p4d.service` **disabled** | Phase-later stub |
| Cursor / Stability Matrix | **Not baked** | AppImage / Flatpak home layer |
| Unreal Engine | **Not installed** | Body on Shadow PC `house-victoria` |

Build script: [`image/build_files/build.sh`](image/build_files/build.sh).
Manifest written into the image at `/usr/share/soulcoreos/image-manifest.txt`.

---

## Build (local)

Requires Podman or Docker with enough disk (~40+ GiB free recommended; base is large).

```bash
cd SoulCoreOS/image

# Optional: pin Ollama
export OLLAMA_VERSION=v0.9.6

podman build \
  --build-arg BASE_IMAGE=ghcr.io/ublue-os/bazzite-nvidia-open:stable \
  --build-arg OLLAMA_VERSION="${OLLAMA_VERSION}" \
  -t ghcr.io/<org>/soulcoreos:latest \
  -f Containerfile \
  .
```

`bootc container lint` runs as the final Containerfile step; a failed lint fails
the build.

### GitHub Actions (optional)

A starter workflow lives at [`.github/workflows/build-image.yml`](.github/workflows/build-image.yml).
Enable packages write permission + `GITHUB_TOKEN` (or a PAT) for GHCR push. For
Universal Blue–style cosign signing, copy keys from
[ublue-os/image-template](https://github.com/ublue-os/image-template) and set
`SIGNING_SECRET`.

---

## Push to GHCR

```bash
echo "$GHCR_TOKEN" | podman login ghcr.io -u <github-user> --password-stdin

podman push ghcr.io/<org>/soulcoreos:latest
podman tag ghcr.io/<org>/soulcoreos:latest ghcr.io/<org>/soulcoreos:$(date -u +%Y%m%d)
podman push ghcr.io/<org>/soulcoreos:$(date -u +%Y%m%d)
```

Make the package public (or grant the target machine a pull token) under GitHub
→ Packages.

---

## Switch a machine onto SoulCoreOS

Prefer **bootc** when the install is already bootc-managed with no conflicting
local layers:

```bash
sudo bootc switch ghcr.io/<org>/soulcoreos:latest
sudo systemctl reboot
```

On many Bazzite installs, end-user flow still uses **rpm-ostree** (signed or
unsigned registry transport). Examples:

```bash
# Signed (when your image is cosign-signed and trust is configured)
sudo rpm-ostree rebase ostree-image-signed:docker://ghcr.io/<org>/soulcoreos:latest

# Unsigned / first bring-up (lab only)
sudo rpm-ostree rebase ostree-unverified-registry:ghcr.io/<org>/soulcoreos:latest

sudo systemctl reboot
```

Check status:

```bash
sudo bootc status
# or
rpm-ostree status
nvidia-smi
tailscale status
```

### After first boot

```bash
# Deploy Host publish output
sudo mkdir -p /opt/soulcore/host /var/lib/soulcore /etc/soulcore
sudo rsync -a ./publish/ /opt/soulcore/host/
# Secrets (gitignored)
sudo install -m 0600 /dev/null /etc/soulcore/host.env
# edit SOULCORE_* overrides

sudo systemctl enable --now ollama.service tailscaled.service
sudo systemctl enable --now hermes.service   # first start bootstraps venv
sudo systemctl enable --now soulcore-host.service

curl -sS http://127.0.0.1:7700/health
# Body (remote): ensure Tailscale MagicDNS resolves house-victoria
```

Home-layer (do **not** layer into the image for Phase 1):

```bash
# Cursor — download AppImage to ~/Applications
# Stability Matrix — Flatpak or AppImage from upstream
flatpak install --user <stability-matrix-ref>   # when published
```

---

## Rollback

If a new image misbehaves:

1. **GRUB** → pick the previous ostree/bootc deployment at boot, or
2. From a working shell:

```bash
sudo bootc rollback
# or
sudo rpm-ostree rollback
sudo systemctl reboot
```

Bazzite also ships `bazzite-rollback-helper` / `ujust` helpers for rebasing to
older tags within the retention window.

Pin a known-good tag and rebase explicitly:

```bash
sudo bootc switch ghcr.io/<org>/soulcoreos:20260729
sudo systemctl reboot
```

---

## Dual-boot Phase 1 (no wipe mandatory)

Phase 1 goal: **run SoulCoreOS without requiring a full-disk wipe**.

Recommended approaches (pick one; wipe is optional, not acceptance-mandatory):

1. **Free disk / partition** — Install Bazzite/SoulCoreOS ISO onto a secondary
   NVMe or a pre-shrunk partition. Keep Windows (or other OS) boot entry in
   firmware/EFI. Use firmware boot menu to choose OS.
2. **Rebase in place** — If the machine already runs Bazzite KDE nvidia-open,
   `bootc switch` / `rpm-ostree rebase` onto SoulCoreOS preserves `/var` and
   home data. Stay on the **same desktop environment** (KDE ↔ KDE).
3. **VM / spare box first** — Validate GHCR image + GPU passthrough before
   touching the daily driver.

Do **not** treat “delete Windows / wipe disk” as a required step for Phase 1
sign-off. Full-disk single-boot can be a later OPS ticket if the user chooses.

Firmware tips for RTX 5070 Ti: UEFI mode; disable CSM if the board shows early
boot freezes with Blackwell cards (vendor forums). Enroll MOK if Secure Boot is
on and modules need user signing (uBlue images usually ship signed kmods).

---

## Layout

```text
SoulCoreOS/
  AGENTS.md
  Agents/                 # role stubs → monorepo Agents/
  docs/architecture/SOULCOREOS.md
  docs/agents/PRODUCT_ROOT.md
  docs/agents/tasks/TASK-20260729-001-PM01-to-OPS01.md
  docs/agents/reports/TASK-20260729-001-OPS01-to-PM01.md
  image/Containerfile
  image/build_files/build.sh
  image/system_files/     # units + static files copied into the image
  systemd/                # canonical unit sources (copied into system_files)
  README.md
```

---

## Related

- Monorepo SoulCore Host: `../SoulCore/SoulCore.Host`
- Tailscale companion runbook: `../docs/runbooks/tailscale-serve-soulcore.md`
- OPS report: `docs/agents/reports/TASK-20260729-001-OPS01-to-PM01.md`
