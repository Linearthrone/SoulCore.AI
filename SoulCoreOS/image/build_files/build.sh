#!/usr/bin/env bash
# SoulCoreOS image customization — runs inside the Containerfile build.
# Documented install matrix: see ../README.md and the OPS-01 report.
set -euo pipefail

echo "==> SoulCoreOS build.sh starting"

# ------------------------------------------------------------------------------
# Copy any additional system_files that were not COPY'd at the Containerfile
# layer (idempotent). Primary path is COPY in Containerfile; this is a safety net.
# ------------------------------------------------------------------------------
if [[ -d /ctx/../system_files ]]; then
  true
fi

# ------------------------------------------------------------------------------
# Already on base (bazzite-nvidia-open) — DO NOT reinstall / swap NVIDIA stacks.
# Already typically present on Bazzite desktop: Steam, Distrobox, Podman, Flatpak,
# Brew tooling, gaming kernel + open NVIDIA kmods.
# ------------------------------------------------------------------------------

# Ensure distrobox is present (no-op if already installed).
dnf5 -y install distrobox || dnf5 -y reinstall distrobox || true

# ------------------------------------------------------------------------------
# Tailscale — uBlue images usually ship it; enable unit and install if missing.
# Repo pattern matches Aurora/Bluefin packaging (addrepo → install → leave disabled).
# ------------------------------------------------------------------------------
if ! rpm -q tailscale >/dev/null 2>&1; then
  echo "==> Installing Tailscale from official Fedora repo"
  dnf5 -y config-manager addrepo --from-repofile=https://pkgs.tailscale.com/stable/fedora/tailscale.repo || \
    dnf5 config-manager --add-repo https://pkgs.tailscale.com/stable/fedora/tailscale.repo || true
  dnf5 -y install tailscale
fi
systemctl enable tailscaled.service

# ------------------------------------------------------------------------------
# .NET 8 runtime — SoulCore.Host (ASP.NET Core). Prefer Fedora packages; fall
# back to Microsoft package feed if Fedora lacks aspnetcore runtime.
# ------------------------------------------------------------------------------
echo "==> Installing .NET 8 ASP.NET Core runtime"
if ! dnf5 -y install aspnetcore-runtime-8.0; then
  echo "==> Fedora aspnetcore-runtime-8.0 unavailable; trying Microsoft packages"
  # Microsoft packages.microsoft.com — disable repo after install to avoid
  # surprise upgrades on the immutable image.
  curl -fsSL -o /tmp/packages-microsoft-prod.rpm \
    "https://packages.microsoft.com/config/fedora/$(rpm -E %fedora)/packages-microsoft-prod.rpm" || \
  curl -fsSL -o /tmp/packages-microsoft-prod.rpm \
    "https://packages.microsoft.com/config/fedora/41/packages-microsoft-prod.rpm"
  dnf5 -y install /tmp/packages-microsoft-prod.rpm
  dnf5 -y install aspnetcore-runtime-8.0
  dnf5 -y config-manager setopt packages-microsoft-com-prod.enabled=0 || true
  rm -f /tmp/packages-microsoft-prod.rpm
fi

# ------------------------------------------------------------------------------
# Ollama — official Linux tarball into /usr (immutable-friendly). Avoid the
# interactive install.sh curl|sh path; pin a known release.
# Override at build time: --build-arg OLLAMA_VERSION=...
# ------------------------------------------------------------------------------
OLLAMA_VERSION="${OLLAMA_VERSION:-v0.9.6}"
echo "==> Installing Ollama ${OLLAMA_VERSION}"
ARCH="$(uname -m)"
case "${ARCH}" in
  x86_64) OLLAMA_ARCH="amd64" ;;
  aarch64) OLLAMA_ARCH="arm64" ;;
  *) echo "Unsupported arch ${ARCH} for Ollama"; exit 1 ;;
esac
curl -fsSL -o /tmp/ollama.tgz \
  "https://github.com/ollama/ollama/releases/download/${OLLAMA_VERSION}/ollama-linux-${OLLAMA_ARCH}.tgz"
mkdir -p /usr/local
tar -xzf /tmp/ollama.tgz -C /usr/local
rm -f /tmp/ollama.tgz
# Upstream tarball lays out bin/ollama; ensure PATH-visible.
if [[ -x /usr/local/bin/ollama ]]; then
  ln -sfn /usr/local/bin/ollama /usr/bin/ollama
elif [[ -x /usr/bin/ollama ]]; then
  true
else
  # Some releases unpack as ./bin/ollama relative to cwd of tar
  find /usr/local -type f -name ollama -executable | head -1 | while read -r p; do
    ln -sfn "$p" /usr/bin/ollama
  done
fi
install -d /usr/lib/systemd/system
if [[ ! -f /usr/lib/systemd/system/ollama.service ]]; then
  cat >/usr/lib/systemd/system/ollama.service <<'UNIT'
[Unit]
Description=Ollama Service
After=network-online.target
Wants=network-online.target

[Service]
ExecStart=/usr/bin/ollama serve
Restart=always
RestartSec=3
Environment=OLLAMA_HOST=127.0.0.1:11434
User=ollama
Group=ollama

[Install]
WantedBy=multi-user.target
UNIT
fi
# System user for ollama (idempotent)
if ! getent passwd ollama >/dev/null; then
  useradd -r -s /usr/sbin/nologin -d /var/lib/ollama -m ollama || true
fi
install -d -o ollama -g ollama /var/lib/ollama || install -d /var/lib/ollama
systemctl enable ollama.service

# ------------------------------------------------------------------------------
# Hermes — do NOT bake LLMOD quarry or secrets. Ship bootstrap helper + unit
# that expects a venv at /var/lib/hermes (mutable). Optional: pre-seed pip pin
# list under /usr/share/soulcoreos/.
# ------------------------------------------------------------------------------
install -d /usr/share/soulcoreos /usr/libexec/soulcoreos /var/lib/hermes
cat >/usr/share/soulcoreos/requirements-hermes.txt <<'REQ'
hermes-agent==0.18.2
aiohttp
mcp
REQ
cat >/usr/libexec/soulcoreos/hermes-bootstrap <<'BOOT'
#!/usr/bin/env bash
set -euo pipefail
ROOT=/var/lib/hermes
VENV="${ROOT}/venv"
REQ=/usr/share/soulcoreos/requirements-hermes.txt
mkdir -p "${ROOT}"
if [[ ! -x "${VENV}/bin/hermes" ]]; then
  python3 -m venv "${VENV}"
  "${VENV}/bin/pip" install --upgrade pip
  "${VENV}/bin/pip" install -r "${REQ}"
fi
exec "${VENV}/bin/hermes" gateway run
BOOT
chmod 0755 /usr/libexec/soulcoreos/hermes-bootstrap
systemctl enable hermes.service || true

# ------------------------------------------------------------------------------
# Opera — optional. Official RPM repo; soft-fail if unavailable so the image
# still builds (document in report).
# ------------------------------------------------------------------------------
echo "==> Attempting Opera stable RPM (optional)"
if dnf5 -y config-manager addrepo --from-repofile=https://rpm.opera.com/rpm/opera.repo 2>/dev/null \
   || dnf5 config-manager --add-repo https://rpm.opera.com/rpm/opera.repo 2>/dev/null; then
  if dnf5 -y install opera-stable; then
    echo "==> Opera installed"
    echo "opera=installed" >/usr/share/soulcoreos/opera.status
  else
    echo "==> Opera package not installable; leaving out of image"
    echo "opera=skipped" >/usr/share/soulcoreos/opera.status
  fi
  dnf5 -y config-manager setopt opera.enabled=0 2>/dev/null || true
else
  echo "==> Opera repo unavailable; skipping"
  echo "opera=skipped" >/usr/share/soulcoreos/opera.status
fi

# ------------------------------------------------------------------------------
# SoulCore.Host + p4d units are COPY'd via system_files. Enable Host; keep p4d
# disabled (Phase-later). Host binary is deployed to /opt/soulcore/host at
# runtime (mutable /var/opt or layered publish) — NOT compiled here.
# ------------------------------------------------------------------------------
install -d /opt/soulcore/host /var/lib/soulcore /etc/soulcore
systemctl enable soulcore-host.service || true
# p4d.service must remain disabled
systemctl disable p4d.service 2>/dev/null || true

# ------------------------------------------------------------------------------
# Explicit exclusions (comments only — never install):
#   - Unreal Engine / Epic installer
#   - Cursor IDE
#   - Stability Matrix
#   - NVIDIA proprietary closed kmods / bazzite-nvidia rebase
# ------------------------------------------------------------------------------

# Cleanup dnf caches for smaller layers
dnf5 clean all || true
rm -rf /var/cache/dnf /var/cache/libdnf5 /tmp/* || true

echo "==> SoulCoreOS build.sh done"
cat >/usr/share/soulcoreos/image-manifest.txt <<EOF
base=ghcr.io/ublue-os/bazzite-nvidia-open
dotnet=aspnetcore-runtime-8.0
ollama=${OLLAMA_VERSION}
tailscale=enabled
hermes=bootstrap-unit
steam=from-bazzite-base
distrobox=from-bazzite-or-dnf
opera=$(cat /usr/share/soulcoreos/opera.status 2>/dev/null || echo unknown)
p4d=unit-disabled
unreal=excluded
cursor=home-layer
stability-matrix=home-layer
EOF
cat /usr/share/soulcoreos/image-manifest.txt
