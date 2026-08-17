---
name: virtualbox-ubuntu-admin
description: Administer Oracle VirtualBox on Windows and Ubuntu guests with VBoxManage. Use when the user mentions VirtualBox, VBoxManage, victoria-sandbox, Ubuntu VM, Guest Additions, snapshots, NAT port-forward, shared folders, or guest SSH.
---

# VirtualBox + Ubuntu admin

Host binary: `C:\Program Files\Oracle\VirtualBox\VBoxManage.exe` (`$VBox` below).

Canonical guest: **`victoria-sandbox`**. Not the Tailscale PC `house-victoria`.

## Inventory (always first)

```powershell
$VBox = "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"
& $VBox list vms
& $VBox list runningvms
& $VBox showvminfo "victoria-sandbox" --machinereadable
& $VBox guestproperty enumerate "victoria-sandbox"
```

Guest IPv4 is under `/VirtualBox/GuestInfo/Net/0/V4/IP`. NAT guests are typically `10.0.2.15` and **not** pingable from Windows until you add a port-forward.

## Power / snapshots

```powershell
& $VBox snapshot "victoria-sandbox" list
& $VBox snapshot "victoria-sandbox" take "pre-reason-YYYYMMDD" --description "why"
& $VBox controlvm "victoria-sandbox" acpipowerbutton   # prefer ACPI
& $VBox startvm "victoria-sandbox" --type gui
```

Take a snapshot before kernel, Guest Additions, or NIC changes. Do not `poweroff` a dirty guest unless asked.

## NAT port-forward (SSH example)

Guest listens on 22; host uses 2222:

```powershell
# VM must be off for nicconf, or use controlvm while running:
& $VBox controlvm "victoria-sandbox" natpf1 "ssh,tcp,127.0.0.1,2222,,22"
ssh -p 2222 victoria@127.0.0.1
```

List rules from `showvminfo` (`Forwarding(...)`). Delete: `natpf1 delete ssh`.

## Guest Control (no SSH)

Needs Guest Additions + credentials Kurt supplies in-session (never commit):

```powershell
& $VBox guestcontrol "victoria-sandbox" run --exe /usr/bin/lsb_release --username victoria --password $env:VBOX_GUEST_PASS --wait-stdout --wait-stderr -- -a
```

If password is missing, ask. Do not invent one.

## Guest Additions

ISO is often attached at `IDE-0-0` = `VBoxGuestAdditions.iso`. Inside Ubuntu:

```bash
sudo apt-get update
sudo apt-get install -y build-essential dkms linux-headers-$(uname -r)
sudo mkdir -p /mnt/cdrom && sudo mount /dev/sr0 /mnt/cdrom
sudo /mnt/cdrom/VBoxLinuxAdditions.run
```

Host and guest Additions versions should match (`guestproperty` `GuestAdd/Version` vs `VBoxManage --version`).

## Shared folders

```powershell
& $VBox sharedfolder add "victoria-sandbox" --name hv --hostpath "C:\Users\kurtw\Soul_Core" --automount
```

Guest (Additions): `/media/sf_hv` ; user must be in `vboxsf`. None configured on last probe.

## Ubuntu inside the guest

Prefer evidence from the guest, not VBox `ostype`:

```bash
lsb_release -a
uname -a
ip -4 addr; ss -lntu
systemctl --failed
sudo apt-get update && sudo apt-get -s upgrade
```

Network: netplan under `/etc/netplan/`. Do not switch the VM from NAT to bridged unless Kurt wants LAN exposure.

## House Victoria coupling

- CUA / desktop tools scope to window title **`victoria-sandbox`**. Keep that VM name unless BED retickets.
- Do not move SoulCore.Host into the guest.
- Unreal body is on **shadow** `house-victoria:8888` — out of scope.

## Evidence

Paste command output. For “VM is up”: `list runningvms` + guest IP property + (if claimed) SSH or Guest Control `uname -a`.
