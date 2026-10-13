# FEATURE-4111 — Expand all selection tree items

## Summary

The export and import dialogs now open with their whole checkbox tree expanded: every category, every group and
every shortcut is in view without unfolding anything. Every line of the tree starts with `IsExpanded = true`
(previously only the categories did, and groups started folded). The fold arrows still work while a dialog is open;
the next dialog starts fully open again, since each one builds a fresh tree.

## Files

Modified:
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/SelectionNodeViewModel.cs` — `IsExpanded` starts `true`;
  `ForCategory` no longer special-cases the categories; docs.
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/ShortcutSelectionViewModel.cs` — remarks.
- `src/Enigma.Launchpad.Desktop/Services/LaunchpadDialogs.cs` — `SelectShortcutsAsync` doc ("checked and open").
- `tests/Enigma.Launchpad.Desktop.UnitTests/ShortcutSelectionViewModelTests.cs` — every line starts open.
- `tests/Enigma.Launchpad.Desktop.UnitTests/ShortcutSelectionViewTests.cs` — the shortcuts' checkboxes are visible
  on opening; a group still folds.

## Deviations & follow-ups

- No deviation from the plan.
- Fix budget: none used — the first build and test run were green.
- Documentation sweep: nothing to change — the README does not describe the dialogs' folding.
- Line endings: touched files are LF, per `.gitattributes`.

## Decisions taken at build time

| Question                | Chosen                                                      | Why                                                         |
|-------------------------|-------------------------------------------------------------|-------------------------------------------------------------|
| Where the default lives | The property initialiser of `IsExpanded`                    | One rule for every level, including any line added later    |
| Folding still possible? | Asserted in the headless test (a group folds, its lines go) | Shows "always open" is where each dialog starts, not a lock |
| Shortcut lines          | Also `true`                                                 | Harmless — a line with nothing under it has nothing to show |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors (no `AVLN` XAML warnings).
- `dotnet test --solution Enigma.Launchpad.slnx` — 434 passed, 0 failed (434 before; two existing tests rewritten
  for the new behaviour: the ViewModel's initial state asserts every line open, and the headless dialog test sees
  all seven checkboxes on opening, then folds a group and sees its two shortcuts go).
