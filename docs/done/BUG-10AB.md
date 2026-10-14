# BUG-10AB — Tests that fail on Linux

**Item:** BUG-10AB — Tests that fail on Linux
**Branch:** `bugfix/bug-10ab-linux-tests`
**Run:** bugfix/2026-10-14-release-1-1-0

## Summary

On Linux the suite is green again: 912 tests, 905 passed, 0 failed, 7 skipped (the Windows-only ones). Six tests
assumed Windows and now hold on every platform. Only test files changed. No test was skipped or deleted, and each one
still checks what its name says.

- **`PathCommandResolverTests`**
  - `OnWindows_ANameWithOneOfTheExtensions_IsTriedAsItIs` looks up `"  code.cmd  "`, not `"  CODE.CMD  "`.
    - The name still ends in an extension that `PATHEXT` spells in upper case. So the test still shows the resolver
      matches the extension whatever its case, trims the name, and tries it without adding extensions.
    - It no longer asks a case-sensitive file system to find `CODE.CMD` for `code.cmd`.
  - `Elsewhere_TheNameIsTriedAsItIs` gives both of its files the owner's execute bit outside Windows, through a new
    `AllowExecution` helper. Outside Windows only a file that may be executed is a program. With the bit set,
    `code` is unfound because its name is not completed, which is what the test is about.
- **`ShortcutExchangeTests.TryParse_LeavesOutTheEntriesTheFormsWouldRefuse_AndCountsThem`**: the "Relative" entry's
  target is `Path.Combine("bin", "tool.exe")`.
  - On Linux, `bin\tool.exe` is a command name, since a backslash is a file-name character there.
  - An application's command is imported unresolved, so the entry was kept and one fewer was counted.
- **`ShortcutIconsTests`** (`AProgramsIcon_IsFoundFromItsPath_AsAnAutomaticIcon`, `ARefresh_AsksTheSourcesAgain`):
  the program is `Path.Combine(Path.GetTempPath(), "Enigma.Launchpad.Tools", "code.exe")`.
  - On Linux, `C:\Tools\code.exe` is not fully qualified.
  - `ShortcutIcons` took it for a command, the fake resolver knew no such command, and no icon was asked for.
- **`WorkspacePageViewTests.AnApplicationTile_TradesItsGlyphForItsProgramsIcon_OnceItArrives`**: the two tiles'
  targets are built the same way, for the same reason. The wait for the icon no longer times out.

## Files / modules touched

**Modified**

- `tests/Enigma.Launchpad.Core.UnitTests/Launching/PathCommandResolverTests.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Exchange/ShortcutExchangeTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/ShortcutIconsTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/WorkspacePageViewTests.cs`
- `docs/roadmap.md`, `docs/plan/BUG-10AB.md` — statuses

## Deviations & follow-ups

- No deviation from the plan.
- **Follow-up:** run the suite on Windows before the release is announced. The changed tests build their paths from
  `Path.GetTempPath()`, which is fully qualified on Windows too, and `AllowExecution` does nothing there. They were
  run on Linux only.
- The other `C:\…` literals in the test projects were left as they are. They pass on Linux, because those paths are
  only displayed or never resolved.
- Fix budget: none used. The first build and test run after the change were green.
- Documentation sweep: nothing to update. No user-facing document describes the tests.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` (Linux) — total 912, 905 passed, 0 failed, 7 skipped.
- Before the change, at `develop` (2429042): total 912, 899 passed, 6 failed (the six above), 7 skipped.

## Decisions taken at build time

| Question                                         | Chosen                                                        | Why                                                                                         |
|--------------------------------------------------|---------------------------------------------------------------|---------------------------------------------------------------------------------------------|
| How to make the two Unix files programs          | A private `AllowExecution` helper, guarded by `OperatingSystem.IsWindows()` | The guard keeps the analyzer's platform check (CA1416) quiet and makes the call a no-op on Windows |
| Where the icon tests' program lives              | `Path.GetTempPath()/Enigma.Launchpad.Tools/code.exe`          | The same folder `ProfileExportImportTests.CodePath` uses; the file never needs to exist     |
| Shared helper for the Desktop paths              | None: a field in one class, two locals in the other           | Two call sites; a shared helper would be a new piece of test infrastructure for this alone  |
