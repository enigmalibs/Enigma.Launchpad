# FEATURE-0BB1-PHASE03 — Drag groups into columns

**Item:** FEATURE-0BB1 — Group columns
**Phase:** PHASE03 — Drag groups into columns
**Branch:** `feature/feature-0bb1-phase03-column-drag`
**Run:** vibe/2026-10-15-group-columns

## Summary

Dragging a group now works in two dimensions: a group can be dropped beside another one on the same line.

- **The gap is as wide as the dragged group.** A dashed gap of the group's columns, 48 pixels high, opens where the
  group would land:
  - beside a card when it fits on that line;
  - on a line of its own when it does not — a full-width group dropped between two ⅓ cards, for example.
  - The cards after it make room and wrap as they would.
- **The gap belongs to the panel.** `ColumnFlowPanel` gains:
  - the attached `GapBefore` / `GapAfter` (the gap's span; 0 for none), bound per item container like `Span`;
  - `GapHeight` and `GapBrush`;
  - `GapBounds`, which says where the gap is.
  - The gap is laid out in the flow as an item of its own. It is drawn by a private visual of the panel (`GapMark`)
    in the placeholders' look: the accent at 0.08, a 1.5-pixel `4,3` dashed outline, 10-pixel corners.
  - `GroupViewModel` gains `DropColumns`, the dragged group's span, set with the hint. `GapBefore` / `GapAfter` follow
    the hint.
  - The group item's two dashed bars and their `Panel.placeholder.group` styles are gone. The dashed frame over a
    card receiving a tile stays.
- **The slot in two dimensions:** `DropPlacement.ColumnSlot`, over the cards' cells in the natural layout
  (`ColumnFlowPanel.Place`: no gap open; each cell is its line's height plus the row spacing).
  - It picks the line under the pointer: the first line when the pointer is above them all, the last when below.
  - On a shared line: before the first card whose middle is right of the pointer, else after the line's last card.
    That includes the empty end of a line.
  - A card alone on its line and as wide as the line has no side, so its upper half is *before* and its lower half
    *after*, as in the single column of before.
  - `FlowSlot` and `ColumnSlot` now share the line finding (`LineAt`) and the slot across a line (`SlotAcross`).
  - Because the slot is found in the natural layout, cards moving to make room never move the gap.
  - Own place, foreign data and the tile rules are unchanged.
- **The dragged group's copy:**
  - it is as wide as the group's card (the list's width if there is no card);
  - it follows the pointer both ways, where it was taken hold of, inside the cards' horizontal extent and the layer's
    height;
  - a full-width group's copy therefore still moves up and down only, at the cards' left.
- **Dead code removed:** `GroupRows`, and `DropPlacement.Stack` / `SlotFor` with their tests. Nothing uses them any
  more, as with `Vertical` / `Horizontal` in FEATURE-23F9.

## Files touched

- Created:
  - `tests/Enigma.Launchpad.Desktop.UnitTests/Controls/ColumnFlowPanelTests.cs`
- Modified:
  - `src/Enigma.Launchpad.Desktop/Controls/ColumnFlowPanel.cs` — the gap: the attached properties, `GapHeight`,
    `GapBrush`, `GapBounds`, `GapMark`.
  - `src/Enigma.Launchpad.Desktop/DragAndDrop/DropPlacement.cs` — `ColumnSlot`, `LineAt`, `SlotAcross`; `Stack` and
    `SlotFor` removed; `Flow`'s remarks carry the steady-gap reason.
  - `src/Enigma.Launchpad.Desktop/ViewModels/Pages/GroupViewModel.cs` — `DropColumns`, `GapBefore`, `GapAfter`.
  - `src/Enigma.Launchpad.Desktop/Views/Pages/WorkspacePageView.axaml`:
    - the panel's gap settings;
    - the containers' gap bindings;
    - the group item without its dashed bars.
  - `src/Enigma.Launchpad.Desktop/Views/Pages/WorkspacePageView.axaml.cs` — `ResolveGroup` on `ColumnSlot`;
    `GroupCells`; the group copy's width and both-way movement.
  - `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml` — the group placeholder styles go; `Rectangle.dropFrame` keeps
    its corners; the drag-and-drop comment says where the group gap is drawn.
  - Tests:
    - `tests/Enigma.Launchpad.Desktop.UnitTests/DragAndDrop/DropPlacementTests.cs`
    - `tests/Enigma.Launchpad.Desktop.UnitTests/WorkspacePageViewDragDropTests.cs`

## Decisions taken at build time

| Question                                        | Chosen                                                                                       | Why                                                                                                                                         | Alternatives rejected                                                         |
|-------------------------------------------------|----------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------|
| How the panel draws the gap                     | A private visual of the panel (`GapMark`), kept last among its visual children               | `Panel.Render` is sealed in Avalonia 12; `Panel` syncs its visual children with its children by index (decompiled), so one extra visual kept last is safe | An overlay canvas in the view positioned from `GapBounds`; a fake item in `Groups` |
| The slot rule for a card taking the whole line  | Its upper half is before it, its lower half after it (`ColumnSlot`)                          | `FlowSlot` as is would split a full-width card into left and right halves: "top half → above" would break                                   | Reusing `FlowSlot` unchanged                                                  |
| A full-line card                                | Alone on its line and at least the line's width less half a pixel                            | Covers full-width groups and the one-column fallback alike, from the cells alone                                                            | Passing each card's span                                                      |
| The gap's brush                                 | `GapBrush` bound to `EnigmaAccentBrush`; a brush change re-arranges                          | Follows a theme switch like the styled placeholders                                                                                         | A fixed colour                                                                |
| `Stack` / `SlotFor`                             | Removed, with their tests                                                                    | Nothing uses them any more (FEATURE-23F9 precedent)                                                                                         | Keeping dead public helpers                                                   |
| Testing a slot above every card                 | Through `view.Resolve`, the way the own-place tests already do                               | A raw drag event above the cards lands on the page header, which is not hit-testable, so it never reaches the page                          | Raw drag events at that point                                                 |

## Deviations & follow-ups

- **`FlowSlot` was not reused as is**, as the plan said it would be. `ColumnSlot` reuses its line finding and its
  slot across a line, and adds the vertical halves for a card that takes the whole line. See *Decisions*.
- **The gap is drawn by a private visual**, not by the panel's `Render`, which is sealed. See *Decisions*.
- **One existing test was renamed and asserts the new gap:** `AGroupDragged_OpensADashedGapThatMakesRoom_…` (was
  `…DashedBar…`).
  - It now asserts `panel.GapBounds` and `GapBefore` instead of the removed in-item bar.
  - What lands where is unchanged.
- **Fix budget: 1 cycle.** Both failures were in the new tests:
  - `Card(view, group)` also matched the drag copy's card once the copy showed. The fix measures the card before the
    drag.
  - A raw drag event above the cards fell on the non-hit-testable page header. That case now goes through
    `view.Resolve`.
- **Known gap, unchanged from FEATURE-23F9:** no auto-scroll near the list's edges during a drag.
- **Follow-up (pre-existing, cosmetic):** the group item template's children in `WorkspacePageView.axaml` sit at the
  same indentation as their root `<Panel>`, as they did inside the old `<StackPanel>`. Left unchanged to keep the diff
  small.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx`: **965 total, 964 passed, 0 failed, 1 skipped** (the Unix-only
  resolver test on this Windows host). The PHASE02 baseline was 951: 23 added, and 9 removed with `Stack` / `SlotFor`.
- Tests added:
  - `ColumnFlowPanelTests` (headless):
    - children at their spans, with no gap;
    - a gap before a child takes its columns and the children make room;
    - a gap that does not fit starts the next line;
    - closing the gap puts the children back;
    - `Place` ignores the open gap;
    - the one-column fallback for the gap too.
  - `DropPlacementTests`: `ColumnSlot` at 9 positions (sides on a shared line, the line's end, above and below,
    halves of a full-line card, a ⅓ card alone); one column; no cells refused.
  - `WorkspacePageViewDragDropTests` (headless):
    - a ⅓ group over another's right half opens a ⅓ gap beside it on that line; the gap stays put; the drop lands on
      that line;
    - the left half, a line's empty end, and above every card each land in the gap there;
    - a full-width group between two ⅓ cards opens a full-width gap on its own line and lands between them;
    - a ⅓ group's copy is its card's width, follows both ways, and stops at the cards' right edge.
