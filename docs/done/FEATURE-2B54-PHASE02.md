# FEATURE-2B54-PHASE02 — Shortcut selection dialog

## Summary

The dialog that lets the user choose, with checkboxes, which part of a profile to export or import — not yet
reachable from the UI (the Profiles page wires it in PHASE03).

- `ShortcutSelectionViewModel` builds a tree over a profile: one line per category that has groups (Applications,
  Directories, URLs, with the rail's icons), then its groups (with "N shortcuts" / "empty"), then their shortcuts
  (with their target). Everything starts checked; categories open, groups folded.
- `SelectionNodeViewModel` carries the three-state logic: checking or unchecking a line does the same to everything
  under it; a line with lines under it shows the state they share, or indeterminate; only shortcuts and empty groups
  keep a state of their own. Indeterminate is shown, never chosen — clicking it unchecks, as Avalonia's two-state
  checkbox does.
- The dialog shows a summary ("2 groups · 5 shortcuts selected" / "Nothing selected") and `HasSelection`, which
  drives the confirm button. `ToProfile()` returns the content reduced to the selection, in order, keeping the
  content's identity and name: a group with shortcuts when any of them is checked, an empty group when it is checked.
- `ILaunchpadDialogs.SelectShortcutsAsync(title, message, confirmText, content)` shows it on the dialog host and
  returns the chosen part, or `null` on cancel. `ShortcutSelectionView` is a `TreeView` of checkboxes, framed and
  capped at 340 px so the dialog stays inside the 640 px host and the tree scrolls.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/SelectionNodeViewModel.cs`
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/ShortcutSelectionViewModel.cs`
- `src/Enigma.Launchpad.Desktop/Views/Dialogs/ShortcutSelectionView.axaml` / `.axaml.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/ShortcutSelectionViewModelTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/ShortcutSelectionViewTests.cs`

Modified:
- `src/Enigma.Launchpad.Desktop/Services/LaunchpadDialogs.cs` — `SelectShortcutsAsync`.
- `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml` — `Border.selectionTree`, `TextBlock.selectionDetail`.
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/ScriptedDialogs.cs` — answers act on the real dialog
  model (`SelectionAnswers`, `SelectEverything`), and record what was asked (`Selections`).

## Deviations & follow-ups

- No functional deviation from the plan.
- Layout checked on headless screenshots (throwaway test, not committed): the first one showed a long target
  wrapping onto two lines — the CheckBox template wraps its content — so the line's texts are now `NoWrap` and
  trimmed with an ellipsis.
- Fix budget: none used — the first build and test run were green.
- Documentation sweep: nothing user-facing is reachable yet; README is updated with the page in PHASE03.
- Line endings: new files are LF, per `.gitattributes`.

## Decisions taken at build time

| Question                      | Chosen                                                         | Why                                                          |
|-------------------------------|----------------------------------------------------------------|--------------------------------------------------------------|
| Tree control                  | `TreeView` + `TreeDataTemplate` over one node ViewModel type   | Folding for free; one compiled `x:DataType` for every level  |
| Clicking an indeterminate box | Unchecks everything under it (Avalonia's two-state toggle)     | The toolkit's own behaviour; no custom checkbox              |
| Setting `IsChecked` to `null` | Ignored                                                        | Indeterminate is a consequence, never a choice               |
| Backing state                 | A private `State` property raising `IsChecked`                 | Cascades set it without re-entering the user's setter        |
| Group icon / category icons   | `SquaresFour` (the empty state's) / the rail's icons           | Matches what the user already sees for groups and categories |
| Summary wording               | `ProfileItemViewModel.Summarise` + " selected"                 | Same counts wording as the profile cards                     |
| Test double                   | Answers are functions of the real `ShortcutSelectionViewModel` | Page tests choose as a user would, through the real cascade  |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors (no `AVLN` XAML warnings).
- `dotnet test --solution Enigma.Launchpad.slnx` — 413 passed, 0 failed (399 before; +14: initial state, line
  details, partial and full cascades both ways, indeterminate never chosen, change notifications, `HasSelection`
  and summary, `ToProfile` filtering and order, empty groups, empty content; headless — the dialog shows the open
  categories and folded groups as checked boxes, a box drives its line, the confirm button follows the selection, and
  confirming returns the checked part).
