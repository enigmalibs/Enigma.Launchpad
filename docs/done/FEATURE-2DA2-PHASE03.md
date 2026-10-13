# FEATURE-2DA2-PHASE03 — Open in terminal

## Summary

Folder tiles can now open a terminal in their folder.

- Core: `IShortcutLauncher.OpenInTerminal(shortcut)`.
  - A missing folder gives `TargetMissing`, and nothing is started.
  - Otherwise the platform's terminals are tried in order. One that isn't installed (`Win32Exception` with native
    code 2 — `ERROR_FILE_NOT_FOUND` / `ENOENT`) gives way to the next, logged at Debug. Any other failure is the
    answer (`Failed`, with the reason, logged at Warning). None installed gives "No terminal was found on this
    computer."
  - `BuildTerminalStartInfos(platform, folder)`:
    - Windows: `wt.exe -d .`, then `cmd.exe`, both with the folder as working directory and no shell execute. The
      path never reaches Windows Terminal's command line, where a `;` would start a new tab.
    - macOS: `open -a Terminal <folder>`, with the folder as an argument of its own.
    - Linux: `x-terminal-emulator`, `gnome-terminal`, `konsole`, `xfce4-terminal`, `xterm`, each with the folder as
      working directory.
- Desktop: `IShortcutActions.OpenInTerminal(shortcut)` reports a failure and minimizes after success when the
  setting asks, like a click (shared `StepAsideAfter`).
  - `ShortcutViewModel.CanOpenInTerminal` / `OpenInTerminalCommand` cover folders only.
  - It appears as an **Open in terminal** button (`TerminalWindow`) in the folder tile's action strip, before Copy
    path, and as a context-menu item.
- README: the tile paragraph describes the tile buttons of all three kinds.

## Files

Modified:
- `src/Enigma.Launchpad.Core/Launching/ShortcutLauncher.cs` — `OpenInTerminal`, `BuildTerminalStartInfos`, the
  fallback chain.
- `src/Enigma.Launchpad.Desktop/Services/ShortcutActions.cs` — `OpenInTerminal`, `StepAsideAfter`.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/CategoryPageViewModel.cs` — `OpenInTerminal`.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ShortcutViewModel.cs` — command and flag.
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml` — tile button and menu item.
- `README.md` — tile paragraph.
- Tests:
  - `ShortcutLauncherTests` — start infos on Windows, macOS and Linux; the preferred terminal starts; fallback to
    the next; none installed; another failure stops the chain; a missing folder starts nothing.
  - `Infrastructure/RecordingProcessStarter.cs` — `Missing`, `Attempted`.
  - `ShortcutActionsTests` — success minimizes when asked; failure reports and never minimizes.
  - `TileActionsTests` — folders only; the command opens the folder's terminal.
  - `TileActionsViewTests` — headless: a folder tile offers Open in terminal and Copy path. The Copy path click test
    now picks its button by name.
  - `Infrastructure/RecordingShortcutActions.cs` — `OpenInTerminal`.

## Deviations & follow-ups

- No deviation from the plan.
- Not checked for real: no terminal was launched. Every candidate goes through `IProcessStarter`, as the existing
  launch paths do, and the start information is asserted per platform.
- Follow-up (plan: out of scope): a setting naming a preferred terminal, for Linux desktops whose terminal isn't
  among the five, or for PowerShell on Windows.
- Fix budget: none used. The first build and test run were green.
- Documentation sweep: README tile paragraph, as above.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                              | Chosen                                                       | Why                                                                          |
|---------------------------------------|--------------------------------------------------------------|------------------------------------------------------------------------------|
| How the folder reaches the terminal   | Working directory (`wt -d .`); an argument of its own on macOS | No terminal or shell ever parses the path                                  |
| What makes the next terminal be tried | Only "program not found" (native error 2)                    | Any other failure is real and is reported, not hidden by a fallback          |
| Windows fallback                      | `cmd.exe`                                                    | Present on every Windows; opens a console in its working directory           |
| Minimize after opening                | As a click: when the setting says so                         | A terminal is opened outside the launchpad                                   |
| Button order on folder tiles          | Open in terminal, then Copy path                             | The action first, the utility second                                         |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 710 passed, 0 failed, 0 skipped (695 before; 15 added).
