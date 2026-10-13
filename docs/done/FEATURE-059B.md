# FEATURE-059B — Shortcut descriptions

## Summary

Every shortcut can now carry an optional description. You enter it in the application, folder and link forms,
and the tile shows it under its icon row.

- Core: `Shortcut` gains `Description` (optional, last, so files without it still load and older builds skip it).
  `IShortcutValidator.ValidateDescription` accepts blank text or text of at most
  `ShortcutValidator.MaxDescriptionLength` (200) characters once trimmed.
- Exports carry `description` when it isn't blank. On import it is trimmed, and blank becomes none. Over 200
  characters, the entry is refused and counted, like any field the form would refuse.
- Form: a **Description (optional)** field after the arguments. It wraps, shows its error once edited, has a hint
  ("Shown on the tile, under its name."), and is stored trimmed or as `null`.
- Tile: `ShortcutViewModel.Description` / `HasDescription`.
  - A second grid row spans the tile under the icon, name and subtitle: at most 2 lines, with an ellipsis after
    that.
  - The tooltip adds the full description on its own line.
  - `Button.tile` has `MinHeight` 62 instead of `Height`, stretches vertically, and aligns its content to the top
    with 12 px vertical padding. A tile without a description is exactly as tall and centred as before. Tiles in a
    row stretch to the tallest, so their icon rows stay aligned.

## Files

Modified:
- `src/Enigma.Launchpad.Core/Shortcuts/Shortcut.cs` — `Description`.
- `src/Enigma.Launchpad.Core/Validation/IShortcutValidator.cs`, `ShortcutValidator.cs` — `ValidateDescription`,
  `MaxDescriptionLength`.
- `src/Enigma.Launchpad.Core/Exchange/ShortcutExchange.cs` — write/read descriptions; remarks.
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/ShortcutEditorViewModel.cs` — description field and validation.
- `src/Enigma.Launchpad.Desktop/Views/Dialogs/ShortcutEditorView.axaml` — the field.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ShortcutViewModel.cs` — `Description`, `HasDescription`, tooltip.
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml` — description row in the tile.
- `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml` — tile sizing, `TextBlock.tileDescription`.
- `README.md` — export section (descriptions held, example, refusal rule).
- Tests: `ShortcutValidatorTests`, `JsonLaunchpadStoreTests`, `ShortcutExchangeTests`, `ProfileTests` (Core);
  `ShortcutEditorViewModelTests`, `CategoryPageShortcutsTests`, `CategoryPageViewTests` (Desktop).

## Deviations & follow-ups

- No deviation from the plan.
- Fix budget: 1 of 3 cycles used. The first run had 2 failures:
  - an editor test used a folder as an application's target (a test mistake);
  - tiles in one row did not stretch to the tallest, because Fluent's `Button` centres itself vertically.
    `Button.tile` now sets `VerticalAlignment="Stretch"`.
- Documentation sweep: README "Export files" — descriptions are held, the example shows one, and an over-long
  description is in the refusal list. Nothing else became wrong.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                          | Chosen                                                        | Why                                                                         |
|-----------------------------------|---------------------------------------------------------------|-----------------------------------------------------------------------------|
| Field `MaxLength`                 | 400 in the box, 200 by the validator                          | Same pattern as the name: typing past the limit shows the message           |
| Field shape                       | Single line, wrapping (`AcceptsReturn` off)                   | A tile shows a paragraph, not lines                                         |
| Description colour                | Secondary foreground, 11 px, 8 px under the icon row          | Matches the subtitle; spacing separates the two                             |
| Tooltip                           | Target (+ arguments), then the description on its own line    | The tile may cut the description short                                      |
| Row alignment                     | Tiles stretch to the row's tallest; content aligned to the top | Icon rows stay level across a row                                           |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 574 passed, 0 failed, 0 skipped (559 before; 15 added).
