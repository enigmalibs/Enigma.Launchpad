# FEATURE-2DA2-PHASE02 — Run with arguments

## Summary

Application tiles can now run their program with other arguments, once, without changing the shortcut.

- **Run with arguments…** — a `PlayCircle` button in the tile's action strip, and a context-menu item after
  **Open**. Applications only (`ShortcutViewModel.CanRunWithArguments`, `RunWithArgumentsCommand`).
- It opens a dialog titled "Run <name>" (`ArgumentsDialogViewModel` / `ArgumentsDialogView`):
  - shows the program's path (trimmed from the left, full path in its tooltip);
  - has an **Arguments** field pre-filled with the shortcut's default arguments and selected, ready to type over;
  - has a hint saying where the field started from and that changes apply to this run only.
- **Run** starts the program through `IShortcutActions.Open` with `shortcut with { Arguments = typed }`. Empty means
  no arguments; Cancel runs nothing. Nothing is saved, and the minimize-after-launch setting applies as for a click.
- A click on the tile is unchanged: it runs with the default arguments.
- Form: the field is now **Default arguments (optional)**, with a hint explaining both uses.
- `ILaunchpadDialogs.PromptArgumentsAsync(shortcut)` returns the trimmed arguments, or `null` when cancelled.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/ArgumentsDialogViewModel.cs`
- `src/Enigma.Launchpad.Desktop/Views/Dialogs/ArgumentsDialogView.axaml` (+ `.axaml.cs`)
- `tests/Enigma.Launchpad.Desktop.UnitTests/RunWithArgumentsTests.cs` — dialog defaults, hints, trimming; runs with
  typed arguments and the shortcut unchanged and unsaved; empty runs without; cancel runs nothing; a click still uses
  the defaults; applications only.

Modified:
- `src/Enigma.Launchpad.Desktop/Services/LaunchpadDialogs.cs` — `PromptArgumentsAsync`.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/CategoryPageViewModel.cs` — `RunWithArgumentsAsync`.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ShortcutViewModel.cs` — command and flag.
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml` — tile button and menu item.
- `src/Enigma.Launchpad.Desktop/Views/Dialogs/ShortcutEditorView.axaml` — "Default arguments" label and hint.
- `tests/Enigma.Launchpad.Desktop.UnitTests/TileActionsViewTests.cs` — headless: the application tile's button
  opens the dialog pre-filled, and Run runs with what was typed.
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/ScriptedDialogs.cs` — `ArgumentAnswers`,
  `ArgumentPrompts`.

## Deviations & follow-ups

- `PromptArgumentsAsync` takes the shortcut, not a name and default arguments, so the dialog can also show the
  program's path.
- Fix budget: none used. The first build and test run were green.
- Documentation sweep: the README's tile sentence gets all the new tile actions in PHASE03. Nothing else became
  wrong.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                          | Chosen                                                     | Why                                                                |
|-----------------------------------|------------------------------------------------------------|--------------------------------------------------------------------|
| Tile button icon                  | `PlayCircle`                                               | "Run", set apart from the tile's own click                         |
| Field start state                 | Defaults pre-filled and selected                           | Type to replace, or press End to add to them                       |
| Validation                        | None: any text, empty allowed                              | Arguments are free-form, as in the shortcut form                   |
| Path display                      | Trimmed from the left, full path in a tooltip              | The program's name, at the end, stays visible                      |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 695 passed, 0 failed, 0 skipped (684 before; 11 added).
