# FEATURE-1F8C — Commands as application targets

## Summary

An application shortcut's target can now be a program's full path, as before, or a **command** found on the
`PATH` — `notepad`, `code`, `git`. The command is stored as typed and looked up each time it is checked, opened,
shown in its folder, or has its icon found.

- **Core:** `ICommandResolver` / `PathCommandResolver` (`Launching/CommandResolver.cs`).
  - Searches the `PATH` in order, never the current directory. Empty and relative entries are skipped, quoted
    entries are read, and Windows entries have their variables expanded.
  - On Windows: the process `PATH`, then the machine's and the user's, read fresh on every call. A name without one
    of the `PATHEXT` extensions has each of them added, in lower case.
  - Elsewhere: only a file with an execute bit counts.
  - `IsCommandName` says what can be a command: not a path, nothing a file name cannot hold.
  - Registered as a singleton in `AddLaunchpadCore`.
- **Validation:** an application target is either a fully qualified path (unchanged rules) or a command name that
  resolves, stored as typed.
  - "Enter a path or a command." for empty.
  - "No program called “x” was found on the PATH." — plus "Arguments go in Default arguments." when the text has a
    space.
  - "Enter a full path, or a command found on the PATH." for a relative path.
  - New public constructor `ShortcutValidator(ICommandResolver)` (DI picks it); the parameterless one uses the
    `PATH`.
- **Launcher:** takes `ICommandResolver`.
  - A command's program is started through the shell, in the user's home folder, with the default arguments
    (`BuildCommandStartInfo`).
  - An unresolved command → `TargetMissing`, nothing started.
  - `Reveal` shows the resolved program. A full-path target never looks at the `PATH`.
- **Import:** an application's command is accepted unresolved, as paths need not exist.
- **Desktop:**
  - `ShortcutIcons` resolves a command off the UI thread and finds its program's icon.
  - The shortcut form reads "Program or command", with a placeholder and a hint naming the `PATH`.
- **README:** a paragraph on commands; the import paragraph says commands need not be on the importer's `PATH`.

## Files

Created:
- `src/Enigma.Launchpad.Core/Launching/CommandResolver.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Launching/PathCommandResolverTests.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Infrastructure/FakeCommandResolver.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FakeCommandResolver.cs`

Modified:
- `src/Enigma.Launchpad.Core/DependencyInjection/ServiceCollectionExtensions.cs` — registration.
- `src/Enigma.Launchpad.Core/Validation/ShortcutValidator.cs` — commands, constructors, `Unquote`.
- `src/Enigma.Launchpad.Core/Launching/ShortcutLauncher.cs` — command launch and reveal.
- `src/Enigma.Launchpad.Core/Exchange/ShortcutExchange.cs` — `ReadTarget`.
- `src/Enigma.Launchpad.Desktop/Services/ShortcutIcons.cs` — command icons.
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/ShortcutEditorViewModel.cs` — label, placeholder, `TargetHint`.
- `src/Enigma.Launchpad.Desktop/Views/Dialogs/ShortcutEditorView.axaml` — hint line.
- `README.md`
- Tests:
  - `ShortcutLauncherTests` — command launch in home with arguments; unresolved; full path never looked up;
    reveal; unresolved reveal.
  - `ShortcutValidatorTests` — command kept as typed; not found; space hint; relative path; empty messages per
    kind; directories never looked up.
  - `ShortcutExchangeTests` — commands imported, a relative directory still refused.
  - `ServiceCollectionExtensionsTests` — `ICommandResolver`.
  - `ShortcutIconsTests` — a command's icon is its program's; an unresolved command asks no program.
  - `ShortcutEditorViewModelTests` — label and hint.

## Deviations & follow-ups

- No functional deviation from the plan.
- `PathCommandResolver` adds `PATHEXT` extensions in lower case, so a found path reads `code.exe` rather than
  `code.EXE`. Windows treats both the same; this only affects how the path looks.
- Follow-up: the Windows *App Paths* registry (names the Run dialog knows but the `PATH` does not) is out of scope.
- Fix budget: 3 cycles used.
  1. CA1416 on the Unix-only test: now marked `[UnsupportedOSPlatform("windows")]`.
  2. Resolved paths spelled `.EXE`: extensions now added in lower case.
  3. A test expected the on-disk spelling for a name typed with its extension, which is kept as typed: the
     comparison now ignores case, as Windows does. The assertion still checks that `.cmd` wins over `.exe`.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx`: succeeded, 0 warnings.
- `dotnet test --solution Enigma.Launchpad.slnx`: 748 total, 747 passed, 0 failed, **1 skipped** —
  `PathCommandResolverTests.Elsewhere_AFileThatMayNotBeExecuted_IsNoProgram`, which needs Unix file permissions and
  is skipped on this Windows host.
- 38 tests added.

## Decisions taken at build time

| Question                                  | Chosen                                                     | Why                                                                            |
|-------------------------------------------|------------------------------------------------------------|--------------------------------------------------------------------------------|
| Spelling of an added extension            | Lower case                                                 | `PATHEXT` is upper case by convention; files are not                           |
| Where "is it a command" is decided        | `PathCommandResolver.IsCommandName`, shared                | Validator, launcher, exchange and icons agree on one rule                       |
| Unix execute-bit test on a Windows host   | Kept, skipped with a reason                                | The suite runs on Linux too; the rule deserves its test there                   |
| Validator constructor for DI              | A new public `(ICommandResolver)` constructor              | The container picks it; existing `new ShortcutValidator()` calls keep working  |
