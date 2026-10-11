#!/bin/bash
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
if command -v codium >/dev/null 2>&1; then
  echo ALREADY_INSTALLED
  codium --version
  exit 0
fi
apt-get update
apt-get install -y wget gpg apt-transport-https ca-certificates
install -d -m 0755 /usr/share/keyrings
wget -qO- https://gitlab.com/paulcarroty/vscodium-deb-rpm-repo/raw/master/pub.gpg \
  | gpg --dearmor -o /usr/share/keyrings/vscodium-archive-keyring.gpg
echo 'deb [ signed-by=/usr/share/keyrings/vscodium-archive-keyring.gpg ] https://download.vscodium.com/debs vscodium main' \
  > /etc/apt/sources.list.d/vscodium.list
apt-get update
apt-get install -y codium
echo INSTALL_OK
command -v codium
codium --version
