# FEATURE-23F9-PHASE01 — Dashed slot where it lands

**Item:** FEATURE-23F9 — Workspace drag visuals
**Phase:** PHASE01 — Dashed slot where it lands
**Branch:** `feature/feature-23f9-phase01-dashed-slot`
**Run:** vibe/2026-10-14-workspace-drag-visuals

## Summary

In an open workspace, the spot where a dragged group or tile will land now shows as a dashed placeholder. The
cards or tiles after it make room for it, and it does not jump while they move.

- **Groups:** a 48-pixel dashed bar opens between two cards. The cards below it move down.
  - The bar is part of the group's own row: before its card, or after the last card.
  - It works wherever the pointer is on the page, gaps between cards included.
- **Tiles:** a dashed tile, as wide as a tile and as high as its row, opens among the tiles. The tiles after it
  reflow.
  - Inside a line, the gap is drawn in front of the next tile.
  - At the end of a line, it is drawn after that line's last tile.
- **End of a group** (pointer over the header, the padding, or past the last tile):
  - a group showing tiles gets the dashed tile after its last one;
  - an empty or folded group gets a dashed frame over the card, replacing the solid accent frame.
- **The gap holds still.** It is computed as if no placeholder were open:
  - groups: `DropPlacement.Stack` builds the rows from the first row's top, each card's height and the spacing,
    then `DropPlacement.SlotFor` picks the gap;
  - tiles: `DropPlacement.Flow` recomputes how the wrap panel lays out each tile's desired size plus its slot
    margins, then `DropPlacement.FlowSlot` picks the gap on the line under the pointer.
  - So the items making room never change which gap the pointer is over. That includes a tile that a dashed tile
    pushes onto the next line.
- **The dragged item's own place** (a drop that would move nothing):
  - nothing opens there;
  - the drop is accepted rather than refused, so a drag no longer starts on a refusal cursor;
  - `WorkspacePageViewModel.MovesGroup` / `MovesShortcut` decide this, and `DropGroup` / `DropShortcut` now use
    them too.
- **`DragEnter` is answered like `DragOver`.** Avalonia raises a leave on the old element and an enter on the new
  one whenever the element under the pointer changes, in the same input pass. Answering the enter reopens the gap
  the leave closed before anything is drawn. Otherwise a placeholder that takes room would flicker.
- Drop rules are unchanged: groups reorder whatever their kind, a tile goes only to a group of its own kind, and
  foreign or unknown payloads are refused.

## Files touched

- Modified:
  - `src/Enigma.Launchpad.Desktop/DragAndDrop/DropPlacement.cs`:
    - added `Stack`, `SlotFor`, `Flow`, `FlowSlot`;
    - removed `Vertical` / `Horizontal`, which nothing uses any more.
  - `src/Enigma.Launchpad.Desktop/ViewModels/Pages/WorkspacePageViewModel.cs`:
    - added `MovesGroup`, `MovesShortcut` and `IndexWithinItsGroup`;
    - the drops are built on them.
  - `src/Enigma.Launchpad.Desktop/Views/Pages/WorkspacePageView.axaml`:
    - the group row is a `StackPanel` (dashed bar, card with its dashed frame, dashed bar);
    - the tile slot is a horizontal `StackPanel.tileSlot` (dashed tile, tile, dashed tile);
    - the accent bars are gone.
  - `src/Enigma.Launchpad.Desktop/Views/Pages/WorkspacePageView.axaml.cs`:
    - `Resolve` dispatches to `ResolveGroup` / `ResolveShortcut`;
    - added `GroupRows`, `TileSlot`, `TileSizes`, `FindDescendant`;
    - a `DragEnter` handler.
  - `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml`:
    - added the placeholder and dashed-frame styles;
    - removed the `dropBar` and `groupCard.dropInside` styles;
    - the `tileSlot` selector is now `StackPanel.tileSlot`.
  - Tests:
    - `tests/Enigma.Launchpad.Desktop.UnitTests/DragAndDrop/DropPlacementTests.cs`
    - `tests/Enigma.Launchpad.Desktop.UnitTests/WorkspacePageDragDropTests.cs`
    - `tests/Enigma.Launchpad.Desktop.UnitTests/WorkspacePageViewDragDropTests.cs`

## Decisions taken at build time

| Question                                   | Chosen                                                                 | Why                                                                                                   | Alternatives rejected                                  |
|--------------------------------------------|------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------|--------------------------------------------------------|
| The gap between two tiles of one line      | Drawn in front of the next tile; after the last one only at a line end | One spelling per gap; the end of a line stays at the end of that line, where the pointer is           | Always "after the left tile"                           |
| A group drag over the gaps between cards   | Lands in the nearest gap, wherever the pointer is on the page          | The old card-only hit test refused the 12-pixel gaps, which would close the bar and make it flicker   | Refusing outside the cards                             |
| The wrap panel's line break                | Reproduced from Avalonia's `WrapPanel` test, with a local epsilon      | Avalonia's `MathUtilities` is internal in 12.1.1; the natural flow must break where the panel does    | A plain `>` comparison                                 |
| A tile's natural size                      | The tile's desired size plus its slot's margins                        | Independent of any open placeholder and of when layout last ran                                       | The slot's bounds (they include an open placeholder)   |
| `DropPlacement.Vertical` / `Horizontal`    | Removed, with their tests                                              | Nothing uses them any more; the halves are now inside `SlotFor` / `FlowSlot`                          | Keeping dead public helpers                            |

## Deviations & follow-ups

- **One existing assertion now checks the new look.**
  - The test: `ATileDraggedOverAGroupsHeader_LandsAtTheGroupsEnd` asserted `tools.IsDropInside`.
  - Why it changed: the end of a group that shows tiles is now the dashed tile after its last tile, so the test
    asserts that tile's `IsDropAfter`, its visible placeholder, and no group hint.
  - Its drop assertion is unchanged.
- **Fix budget: 1 cycle.**
  - The cause: a new test expected the gap between A and B to show "after A". By design it shows "before B".
  - The fix: the test now asserts B's placeholder. The code did not change.
- No README change: it says only that groups and tiles are dragged to reorder them, which stays true.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx`: **904 total, 903 passed, 0 failed, 1 skipped** (the Unix-only
  resolver test on this Windows host). The baseline was 877.
- Tests added:
  - `DropPlacementTests`:
    - `Stack` and `SlotFor` (gaps, no rows);
    - `Flow` (wrapping, an exact fit, line heights, an item wider than the width, empty);
    - `FlowSlot` (10 positions, including above the first and below the last line; no items refused).
  - `WorkspacePageDragDropTests`: `MovesGroup` and `MovesShortcut` (own place, neighbours, elsewhere, another group,
    unknown).
  - `WorkspacePageViewDragDropTests` (headless):
    - a group's dashed bar opens and takes room, and the gap stays once the card has moved out from under the
      pointer;
    - a tile's dashed tile opens in front of the next tile and pushes it on;
    - a dashed tile at the end of a line stays when its tile wraps to the next line;
    - an empty group gets the dashed frame;
    - a group's and a tile's own place opens nothing, is accepted, and a drop there moves nothing.
