# BUG-10AB — Tests that fail on Linux

**Status:** TODO
**Type:** BUG
**Branch:** `bugfix/bug-10ab-linux-tests`
**Run:** bugfix/2026-10-14-release-1-1-0

## Objective

Make the whole suite green on Linux. At `develop` (2429042), `dotnet test --solution Enigma.Launchpad.slnx` on Linux
runs 912 tests: 6 fail, 899 pass, 7 are skipped. All six are written for Windows. The application itself does the
right thing on Linux: no product code changes.

## Context & constraints

The six failures, and why each happens:

| Test                                                                                     | Why it fails on Linux                                                                                                                                                                                                        |
|------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `PathCommandResolverTests.OnWindows_ANameWithOneOfTheExtensions_IsTriedAsItIs`           | It looks up `CODE.CMD` for a file named `code.cmd`. Linux file names are case-sensitive.                                                                                                                                      |
| `PathCommandResolverTests.Elsewhere_TheNameIsTriedAsItIs`                                | Outside Windows, only a file with an execute bit is a program. The test's files are created without one, so `firefox` is not found.                                                                                          |
| `ShortcutExchangeTests.TryParse_LeavesOutTheEntriesTheFormsWouldRefuse_AndCountsThem`    | Its "relative path" is `bin\tool.exe`. On Linux a backslash is a file-name character, so that text is a command name: it is imported, and one fewer entry is counted (15, not 16).                                            |
| `ShortcutIconsTests.AProgramsIcon_IsFoundFromItsPath_AsAnAutomaticIcon`                  | `C:\Tools\code.exe` is not a fully qualified path on Linux. `ShortcutIcons` takes it for a command, asks the command resolver, and finds nothing.                                                                              |
| `ShortcutIconsTests.ARefresh_AsksTheSourcesAgain`                                        | The same `C:\Tools\code.exe` program.                                                                                                                                                                                         |
| `WorkspacePageViewTests.AnApplicationTile_TradesItsGlyphForItsProgramsIcon_OnceItArrives` | The same: the tiles' `C:\Tools\…` targets are taken for commands, so their program icons are never asked for, and the wait times out.                                                                                         |

The house precedent for a path that is absolute on every platform is
`ProfileExportImportTests.CodePath = Path.Combine(Path.GetTempPath(), "Enigma.Launchpad.Tools", "Code.exe")`; for a
Windows-only fact, `Assert.SkipUnless(OperatingSystem.IsWindows(), …)`.

## Steps

1. `PathCommandResolverTests`:
   - `OnWindows_ANameWithOneOfTheExtensions_IsTriedAsItIs`: look up `"  code.cmd  "`. The name still ends in an
     extension `PATHEXT` spells in upper case (`.CMD`), so the test still shows that such a name is tried as typed,
     trimmed, without the extensions added. That Windows file names ignore case is the file system's behaviour, not
     the resolver's, and is dropped from this test.
   - `Elsewhere_TheNameIsTriedAsItIs`: create both files as programs, with the user's execute bit outside Windows.
     Then `code` is not found because its name is not completed, and not because `code.exe` cannot be run.
2. `ShortcutExchangeTests.TryParse_LeavesOutTheEntriesTheFormsWouldRefuse_AndCountsThem`: the relative path is
   `Path.Combine("bin", "tool.exe")`, serialized like the absolute ones.
3. `ShortcutIconsTests`: the program is `Path.Combine(Path.GetTempPath(), "Enigma.Launchpad.Tools", "code.exe")`.
4. `WorkspacePageViewTests.AnApplicationTile_TradesItsGlyphForItsProgramsIcon_OnceItArrives`: the two tiles' targets
   are built the same way.

## Acceptance criteria

- `dotnet build Enigma.Launchpad.slnx` is clean, with zero warnings.
- `dotnet test --solution Enigma.Launchpad.slnx` on Linux: 912 tests, 0 failed, the same 7 skipped (the
  Windows-only ones).
- Only test files change. No test is skipped or deleted to get there, and each changed test still checks what its
  name says.

## Out of scope

- Any change to `src/`.
- Changing the other tests' `C:\…` literals, which pass on Linux because their paths are only displayed or never
  resolved.
- Running the suite on Windows from this machine.

## Decisions taken autonomously

| Question                                         | Chosen                                                                                    | Why                                                                                                                       | Alternatives rejected                                                           |
|--------------------------------------------------|-------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------|
| Product bug or test bug?                         | Test bug in all six                                                                       | On Linux, `C:\Tools\code.exe` and `bin\tool.exe` really are command names, and a program needs its execute bit              | Changing `PathCommandResolver.IsCommandName` to refuse backslashes on Linux    |
| A separate item, or folded into the release      | Its own `BUG` item, built before the release                                              | The release must be built on a green suite, and its commit should hold only the release                                   | Fixing the tests inside the release commit                                      |
| Make the tests portable, or skip them on Linux   | Portable                                                                                  | What they check — command lookup, import refusals, program icons — is the same on every platform; a skip would hide it    | `Assert.SkipUnless(OperatingSystem.IsWindows(), …)` on each                     |
| An absolute path on every platform               | `Path.Combine(Path.GetTempPath(), "Enigma.Launchpad.Tools", …)`                           | The precedent in `ProfileExportImportTests`; the paths never need to exist                                                | A new shared test helper                                                        |
| Only the failing tests, or every `C:\…` literal  | Only the failing tests                                                                    | The others pass, and a sweep through them would be churn outside this bug                                                  | Replacing every Windows literal in the test projects                            |
