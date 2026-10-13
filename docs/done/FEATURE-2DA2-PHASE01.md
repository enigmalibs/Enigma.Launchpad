# FEATURE-2DA2-PHASE01 — Action strip & copy buttons

## Summary

Tiles now have their own buttons. In this phase: **Copy path** on folder tiles and **Copy URL** on link tiles.

- Inside each tile, a `StackPanel.tileActions` sits in the icon row's text cell, right-aligned:
  - hidden by default; shown while the tile is hovered (`Button.tile:pointerover`) or holds the keyboard focus
    (`Button.tile:focus-within`);
  - it overlays the end of the name and subtitle with the hover colour, so nothing reflows when it appears.
- Buttons are `Button.tileAction`: 5 px padding, 16 px icon, transparent until hovered or pressed. Each has a
  tooltip and an automation name.
- The copy button runs the existing `CopyTargetCommand`.
  - `ShortcutViewModel.CanCopyFromTile` is true for folders and links.
  - `CopyTargetLabel` is "Copy path" or "Copy URL".
- The action buttons are nested inside the tile button. A press on one is handled by that button, so the tile
  doesn't open. `CategoryPageView` also no longer treats a press on a `tileAction` as the start of a tile drag.

## Files

Created:
- `tests/Enigma.Launchpad.Desktop.UnitTests/TileActionsTests.cs` — which kinds copy from the tile; labels.
- `tests/Enigma.Launchpad.Desktop.UnitTests/TileActionsViewTests.cs` (headless):
  - the strip is hidden until hover;
  - it is shown on keyboard focus;
  - a real pointer click on Copy path copies and does not open;
  - link tiles offer Copy URL, application tiles no copy button.

Modified:
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml` — the strip and the copy button.
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml.cs` — no drag from an action button; remarks.
- `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml` — `StackPanel.tileActions`, `Button.tileAction`.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ShortcutViewModel.cs` — `CanCopyFromTile`, `CopyTargetLabel`.

## Deviations & follow-ups

- No deviation from the plan.
- Fix budget: 1 of 3 cycles used. The first build missed a `using Avalonia.Automation;` in the new headless test.
- Not checked on a real desktop session: the app wasn't launched. Hover, focus and a real pointer click were
  exercised through the headless platform.
- Documentation sweep: nothing became wrong. The README describes clicking and right-clicking tiles, and that
  still holds. The tile buttons are added to its description in PHASE03, once all of them exist.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                          | Chosen                                                        | Why                                                                         |
|-----------------------------------|---------------------------------------------------------------|-----------------------------------------------------------------------------|
| Where the strip lives             | Inside the tile button, in the text cell, right-aligned       | Hover stays on the tile; nested buttons handle their own press             |
| Reveal                            | `:pointerover` and `:focus-within` on the tile                | Mouse and keyboard users both reach the buttons                            |
| Strip background                  | The hover brush                                               | Covers the trimmed text cleanly; matches the hovered tile                  |
| Copy button for applications      | None                                                          | Not asked; "Copy target" stays in the context menu                         |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 684 passed, 0 failed, 0 skipped (675 before; 9 added).
