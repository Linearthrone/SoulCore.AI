---
type: issue
id: "002"
severity: P0
status: Closed
found_by: QA-01
found_in: TASK-20260722-043
created: 2026-07-22 22:19
fixed_by: FED-01
fixed_in: TASK-20260722-044
fixed_report: docs/agents/reports/TASK-20260722-044-FED01-to-PM01.md
verified_by: QA-01
verified_in: TASK-20260722-045
verified_report: docs/agents/reports/TASK-20260722-045-QA01-to-PM01.md
component: House.ChatDesktop
---

[å·²ä¿®å¤ 2026-07-22] FED-01 null-guarded all Correct* sliders/labels in CorrectSlider_ValueChanged; launch verified (see TASK-044 report).

[QAéªŒè¯å…³é—­ 2026-07-22] QA-045 full E2E Pass â€” launch stable, Correctâ€¦ Save â†’ strip/snapshot/revision++, invalid reject + chat tone OK (see TASK-045 report).

# ISSUE-002 Â· House.ChatDesktop crashes on launch (CorrectSlider NRE)

## Summary

`House.ChatDesktop` (Release and Debug) exits immediately on startup with unhandled `NullReferenceException` in `CorrectSlider_ValueChanged` during `MainWindow` XAML load. Presence Correctâ€¦ UI cannot be exercised.

## Severity

**P0** â€” desktop shell will not start after FED-041 correction panel landed.

## Repro

1. Host optional (crash is before/during window init).
2. Run:

```powershell
.\House\House.ChatDesktop\bin\Release\net8.0-windows\House.ChatDesktop.exe
```

1. Process exits within ~1s; no main window.

## Actual

```text
Unhandled exception. System.Reflection.TargetInvocationException
 ---> System.NullReferenceException
   at House.ChatDesktop.MainWindow.CorrectSlider_ValueChanged(...)
     in MainWindow.xaml.cs:line 152
   ...
   at House.ChatDesktop.MainWindow.InitializeComponent()
   at House.ChatDesktop.MainWindow..ctor()
```

Exit code observed: `-532462766` (0xE0434352 CLR unhandled).

## Likely cause

`CorrectSlider_ValueChanged` runs while XAML is still constructing later sliders/labels. Guard only checks `CorrectValenceValue is null`, then unconditionally writes `CorrectArousalValue` / `CorrectDominanceValue` / `CorrectFocusValue` (and uses slider `.Value`), so a mid-init `ValueChanged` NREs.

## Expected

App launches; Correctâ€¦ panel available on Presence.

## Evidence (QA-043)

- Build: `dotnet build House\House.ChatDesktop -c Release` â€” 0 Warning(s) 0 Error(s)
- Launch crash stderr captured 2026-07-22 ~22:18 local
- WS Host path for `emotion.correct` still **Pass** (see `TASK-20260722-043-QA01-to-PM01.md`) â€” regression is UI-only init

## Suggested fix (for FED/DEV)

Null-guard all four value TextBlocks and sliders (or suppress ValueChanged until `Loaded`), e.g. return unless every `Correct*Value` / `Correct*Slider` is non-null.
