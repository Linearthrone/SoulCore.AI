# Presence + Host Update and Stack Restart Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Chrome Update covers Presence + Host (git pull → Host rebuild → Velopack); Restart stack detaches ALLSTOP→ALLSTART.

**Architecture:** Extend `LocalStackControl` for git/Host/restart; new `House/scripts/restart-stack.ps1`; wire `MainWindow.Updates.cs` + axaml. Order Host work before Presence apply-restart.

**Tech Stack:** Avalonia / .NET 8, PowerShell 5.1, Velopack, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-05-presence-host-update-and-stack-restart-design.md`

---

## File map

| File | Responsibility |
|------|----------------|
| `House/House.ChatDesktop/Services/LocalStackControl.cs` | Git status/pull, UpdateHost, RestartStack |
| `House/scripts/restart-stack.ps1` | Detached ALLSTOP → ALLSTART + log |
| `House/House.ChatDesktop/MainWindow.Updates.cs` | Pipeline + busy + confirms |
| `House/House.ChatDesktop/MainWindow.axaml` | Chrome Restart stack; Settings status fields |
| `House/House.ChatDesktop.Tests/LocalStackControlTests.cs` | Parse helpers + missing script |
| `House/House.ChatDesktop/Services/PresenceUpdateService.cs` | Copy: Host now part of Update path |

---

### Task 1: restart-stack.ps1

**Files:** Create `House/scripts/restart-stack.ps1`

Steps: write script → manual dry-run of path resolution only in CI/linux (syntax check) → commit.

### Task 2: LocalStackControl APIs + tests

**Files:** `LocalStackControl.cs`, `LocalStackControlTests.cs`

Steps: add parse helpers + methods → failing tests for parse/missing script → implement → pass → commit.

### Task 3: UI pipeline

**Files:** `MainWindow.axaml`, `MainWindow.Updates.cs`, messages in `PresenceUpdateService.cs`

Steps: chrome + Settings controls → Update now pipeline → Restart stack confirm → bump Presence version → build/test → commit.
