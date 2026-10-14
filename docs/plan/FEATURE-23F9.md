# FEATURE-23F9 — Workspace drag visuals

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-23f9-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-14-workspace-drag-visuals

## Objective

Give the open workspace's drag and drop the look Enigma.GitClient's repository list got in FEATURE-8970-PHASE02:

1. **A dashed slot opens where the item lands.** Instead of the 3-pixel accent bar drawn over the gap, a dashed
   placeholder is laid out in the gap and the cards or tiles after it make room for it. The gap stays put while they
   move.
2. **The dragged item travels with the pointer.** A copy of the dragged tile, or of the dragged group's header,
   follows the pointer with a shadow, while the original stays faded in its place.

## Context & constraints

- **The page:** `Views/Pages/WorkspacePageView.axaml` — a header, then a `Panel` holding a `ScrollViewer` around
  `ItemsControl x:Name="GroupList"` (`StackPanel Spacing="12"`, `Margin="32,4,32,32"`) and the empty state.
  - Each group item is a `Panel`: the `Border.groupCard` (header grid with the `Border.grip`, the fold toggle, add
    and menu buttons; body with the empty text or `ItemsControl.tiles` over a `WrapPanel`) and two
    `Border.dropBar.horizontal` overlays (`IsDropBefore` / `IsDropAfter`, negative margins into the 12-pixel gap).
  - Each tile item is a `Panel.tileSlot` (`Margin 0,0,8,8`): the `Button.tile` (`Width 248`, `MinHeight 62`,
    `CornerRadius 8`) and two `Border.dropBar.vertical` overlays.
  - `Border.groupCard.dropInside` frames a group receiving a tile at its end in the accent colour;
    `Border.groupCard.dragging` / `Button.tile.dragging` fade the original (0.45). All in `Themes/Styles.axaml`.
- **The gesture** (`WorkspacePageView.axaml.cs`): tunnelling press/move/release with a 4-pixel threshold starts a
  **platform drag session** (`DragDrop.DoDragDropAsync`) carrying a `DragPayload` (kind + id) in `GroupFormat` /
  `ShortcutFormat`. `OnDragOver` / `OnDrop` call `Resolve`, which finds the `groupCard` and `tile` under
  `e.Source` and splits the target in halves (`DropPlacement.Vertical` / `Horizontal`); `OnDragLeave` clears every
  hint. The `finally` after the session calls `ClearDragState`.
- **Avalonia 12.1.1 drag routing** (decompiled `DragDropDevice`): the drag target is the **deepest** element under
  the pointer (`AllowDrop` is inherited). Whenever that element changes, a `DragLeave` on the old one and a
  `DragEnter` on the new one are raised synchronously, in the same input pass, and both bubble to the page. The page
  handles no `DragEnter` today.
- **Rules (ViewModel, unchanged):** `CanDropGroup` (any other group of the page), `CanDropShortcut` (a group of the
  tile's own kind, never its own tile), `DropGroup`, `DropShortcut` (no-op when the index does not change),
  `ClearDropHints`, `ClearDragState`. `DropPlacement.MoveIndex` / `InsertIndex` hold the index arithmetic.
- **Why the gap must be computed in the natural geometry:** a placeholder that takes room moves the items after it.
  Reading their new places would move the slot, which would move the placeholder, and so on (GitClient's
  `RepositoryReorderGesture.Stack` solves it for one vertical list). In a wrap layout this is worse: a placeholder
  after the last tile of a line pushes that tile to the next line, away from the pointer.
- **Tests:** `WorkspacePageViewDragDropTests` raise raw drag events on the headless window
  (`harness.Window.DragDrop(point, RawDragEventType.…, data, …)`); a platform drag session itself cannot run headless.
  `WorkspacePageDragDropTests` cover the ViewModel; `DropPlacementTests` the arithmetic. The whole suite runs on this
  machine (`dotnet test --solution Enigma.Launchpad.slnx`).

## Shared rules (all phases)

- Build with zero warnings; the whole suite green.
- Drop rules do not change: groups reorder whatever their kind, a tile goes only to a group of its own kind, foreign
  or unknown payloads are refused.
- No new dependency, no data or settings change. Visual metrics follow GitClient's: 1.5-pixel stroke, `4,3` dashes,
  the accent brush (`EnigmaAccentBrush`), a faint accent fill (opacity 0.08), the corners of what the slot stands for.

## PHASE01 — Dashed slot where it lands

**Status:** DONE
**Branch:** `feature/feature-23f9-phase01-dashed-slot`

### Steps

1. `DropPlacement` (pure):
   - `Stack(top, heights)` — rows laid one after the other from a top: `(Top, Height)` each;
   - `SlotFor(y, rows)` — the gap before the first row whose middle is below `y`, or the count;
   - `Flow(width, sizes)` — where a horizontal `WrapPanel` puts items of these sizes in this width: an item that does
     not fit after another starts a new line; each rect takes its line's height;
   - `FlowSlot(point, rects)` — the line under the point (clamped to the first and last), then before the first item
     of that line whose middle is right of the point, else after the line's last item: `(Index, Position)`.
2. `WorkspacePageViewModel`: `MovesGroup(payload, target, position)` and
   `MovesShortcut(payload, group, target, position)` — whether that drop would change anything; `DropGroup` /
   `DropShortcut` share their index computation.
3. Markup:
   - group item: a `StackPanel` — dashed bar before, the card with a dashed frame overlay (`IsDropInside`), dashed
     bar after; the bars bound to `IsDropBefore` / `IsDropAfter`;
   - tile item: a horizontal `StackPanel.tileSlot` — dashed tile before, the tile, dashed tile after;
   - styles: placeholder (`Panel.placeholder`, `.group` 48 pixels high, `.tile` a tile wide), its dashed rectangle
     and faint fill, the dashed frame; the `dropBar` styles and `groupCard.dropInside` go.
4. View (`Resolve`):
   - `DragEnter` is answered like `DragOver`, so the leave/enter pair raised between two elements closes and reopens
     the same gap in one input pass;
   - **group payload:** the slot is found over the whole list in the natural stack (first container's top, then each
     card's height and the 12-pixel spacing), wherever the pointer is on the page;
   - **shortcut payload:** the group card under the pointer as today; inside its tile area, the slot in the natural
     flow (each tile's desired size plus its slot margins, in the wrap panel's width); elsewhere on the card, the
     group's end. The end of a group showing tiles is drawn as a dashed tile after its last tile; a group showing none
     (empty or folded) gets the dashed frame;
   - **own place** (a drop that would move nothing): accepted, nothing shown.
5. Tests:
   - `DropPlacement`: `Stack`, `SlotFor`, `Flow` (one line, wrapping, a wider item, an item wider than the width,
     empty), `FlowSlot` (line choice, before/after, above the first and below the last line);
   - ViewModel: `MovesGroup` / `MovesShortcut` (own place, neighbours, elsewhere, another group);
   - headless: the dashed bar opens and takes room; the gap stays put once the cards have moved; a tile opens a
     dashed tile in the flow, steady at the end of a line; a group's header → dashed tile after its last tile; an
     empty group → dashed frame; own place → nothing shown, accepted; existing tests kept (the header case asserts the
     new end-of-group look).

### Acceptance criteria

- Where a dragged group would land, a small dashed bar opens between the cards and the cards below make room.
- Where a dragged tile would land, a dashed tile opens among the tiles and the tiles after it flow on.
- A group receiving a tile at its end shows a dashed tile after its last tile, or a dashed frame when it shows none.
- The gap does not move while the cards or tiles make room for it, including at the end of a line of tiles.
- Nothing opens next to the dragged item's own place, and dropping there changes nothing.
- Drop rules unchanged; build 0 warnings; whole suite green.

## PHASE02 — Dragged item follows the pointer

**Status:** DONE
**Branch:** `feature/feature-23f9-phase02-drag-ghost`

### Steps

1. Move the tile's `DataTemplate` to `UserControl.Resources` (`x:Key="ShortcutTile"`), used by every group's tile list
   and by the copy.
2. A `Canvas x:Name="DragLayer"` over the scroll viewer (`IsHitTestVisible="False"`, `ClipToBounds="True"`) holding
   two copies, hidden between drags:
   - `TileGhost` — a `ContentControl` drawing the shortcut with `ShortcutTile`;
   - `GroupGhost` — a `ContentControl` drawing a compact copy of the group card's header (grip, kind glyph, name,
     count).
   - Both `Classes="ghost"`: opacity 0.92 and a shadow; never faded by `.dragging`; their placeholders never show.
3. `DropPlacement.GhostOffset(pointer, grab, size, layer)` — `pointer − grab`, kept inside `[0, layer − size]`.
4. View:
   - when a drag starts, remember what is lifted and where in it the pointer pressed;
   - every `DragEnter` / `DragOver` over the page shows the copy of the item the payload names (unknown or foreign
     payloads show none): a tile follows the pointer in both directions, held where it was grabbed; a group follows
     it up and down only, at the cards' left and width;
   - a drag the page did not start (no remembered press) holds the copy by its middle;
   - `DragLeave`, `Drop` and the end of the session hide and empty the copy and forget the lift.
5. Tests: `GhostOffset` theory; headless — a dragged tile's copy appears at the pointer in both directions, follows
   it, stays inside the layer; a dragged group's copy shows its header aligned with the cards and moves vertically
   only; `DragLeave` and `Drop` leave no copy; foreign data shows none; the copy is neither faded nor showing a
   placeholder.

### Acceptance criteria

- While a tile is dragged over the page, a copy of the tile follows the pointer in both directions, with a shadow;
  the original stays faded in place.
- While a group is dragged, a copy of its header follows the pointer up and down, aligned with the cards; the original
  card stays faded.
- Leaving the page, dropping or cancelling leaves no copy behind.
- Drop rules and PHASE01's slots unchanged; build 0 warnings; whole suite green.

## Out of scope

- Replacing the platform drag session with a pointer-driven gesture (GitClient BUG-11A4's refusal cursor on
  XWayland); auto-scrolling near the list's edges; animated movement.
- Dragging workspace cards on the Workspaces page.
- Keyboard reordering beyond the existing menu commands.

## Decisions taken autonomously

| Question                         | Chosen                                                                         | Why                                                                                                     | Alternatives rejected                                                    |
|----------------------------------|--------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------|
| Which drags                      | Groups and tiles of an open workspace                                          | The only drag and drop in the workspaces; the list's cards have none (FEATURE-1C0D out of scope)        | Adding a drag to the workspace cards                                     |
| The gesture                      | Keep the platform drag session; drive the visuals from `DragEnter`/`DragOver`  | The request is visual; the session already delivers every position the visuals need, on Windows and in-process on X11 | A pointer-driven gesture as in GitClient (a rewrite, not asked)          |
| Tile placeholder                 | A dashed tile a tile wide, laid out in the gap                                 | In a grid the gap shows the tile's footprint; tiles are all one width, so a group's height does not change as the gap moves | A narrow dashed bar; an overlay that takes no room                        |
| Group placeholder                | A 48-pixel dashed bar                                                          | A group card can be hundreds of pixels high; GitClient's "small dashed tile" for a list                  | A full-height placeholder                                                |
| A steady gap                     | Slots computed in the natural geometry: a stack of cards, a wrap flow of tiles | The items moving to make room never change the slot (GitClient's `Stack`, extended to the wrap panel)   | Reading the laid-out items (flickers at line ends); caching at drag start |
| End of a group                   | Dashed tile after the last tile; dashed frame when no tile shows               | Shows exactly where the tile goes; a frame where there is no flow to open                               | Keeping the solid accent frame                                           |
| Own place                        | Accepted, nothing shown, the drop does nothing                                 | GitClient's rule; the drag no longer starts on a refusal cursor                                          | Refused, as over its own tile today                                      |
| The tile's copy                  | The tile's own template, following both axes                                   | One tile definition; tiles move across lines and groups                                                 | A hand-made copy; vertical only                                          |
| The group's copy                 | A compact copy of its header, following vertically, aligned with the cards     | A whole card can cover the list; groups only move up and down                                            | The whole card; both axes                                                |
| Copy look                        | 0.92 opacity, shadow, never faded                                              | GitClient's look                                                                                         | A bitmap snapshot (unreliable headless)                                  |
| Phasing                          | Slot first, copy second                                                        | Each phase is one reviewable, green commit; the slot arithmetic stands alone                            | One phase; one phase per drag kind                                       |
