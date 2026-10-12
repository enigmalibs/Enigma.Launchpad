# FEATURE-5B69-PHASE05 — Drag-and-drop reordering

**Item:** FEATURE-5B69 — Launchpad shortcut manager
**Phase:** PHASE05 — Drag-and-drop reordering
**Branch:** `feature/feature-5b69-phase05-drag-drop`
**Run:** vibe/2026-10-12-launchpad-shortcut-manager

## Summary

Groups and shortcuts are now reordered by drag and drop. A shortcut can move between groups of its own
category, an accent indicator shows where a drop will land, and every drop is saved.

- **What can be dragged:**
  - A group by its grip: a `DotsSixVertical` handle at the start of the header, with a move cursor.
    Clicking the header still only folds the group.
  - A shortcut by its tile.
  - A drag starts after the pointer has travelled 4 px with the button down, so clicks are unaffected.
    The pointer handlers tunnel and mark nothing handled.
- **Where it can land:**
  - A group lands above or below another, depending on which half of the card the pointer is over.
  - A tile lands to the left or right of another, depending on which half of the tile.
  - A tile dropped anywhere else on a card (header, hint, empty space) goes to the end of that group.
    A folded target group unfolds.
  - Refused: an item on itself, an item from another category, a tile next to a tile of a group other
    than the card under the pointer, and anything that is not a launchpad payload (files, text). The
    cursor shows "no drop" for all of these.
- **Feedback:**
  - The dragged card or tile fades to 45 %.
  - A 3 px accent bar sits in the gap before or after the target card or tile.
  - A group receiving a tile at its end is framed in the accent colour.
  - All hints clear on leave, on drop and when the drag ends.
- **No accidental launch:** a tile dropped where it started does not open. The click that may follow
  the drag is marked handled before the button runs its command.
- **Pure logic:**
  - `DropPlacement`: before/after from pointer and size, vertical or horizontal; `MoveIndex` for
    same-list moves, accounting for the item leaving its place; `InsertIndex` for cross-list moves.
  - `DragPayload`: the category and the identity as `kind:id`, carried in two application formats,
    `enigma-launchpad.group` and `enigma-launchpad.shortcut`. Anything else fails to parse.
- **ViewModels:**
  - `CategoryPageViewModel` gains `CanDropGroup` / `DropGroup`, `CanDropShortcut` / `DropShortcut`
    (same group: `Move`; across groups: remove, re-parent `ShortcutViewModel.Group`, insert),
    `ClearDropHints`, `ClearDragState`, `FindGroup` and `FindShortcut`.
  - A drop that changes nothing returns `false` and saves nothing.
  - `GroupViewModel` and `ShortcutViewModel` gain `DropHint` (`None` / `Before` / `After` / `Inside`),
    with `IsDropBefore` / `IsDropAfter` / `IsDropInside`, and `IsDragging`.
- **README:** says how to drag groups and tiles.

## Files / modules touched

**Created**

- `src/Enigma.Launchpad.Desktop/DragAndDrop/DropPlacement.cs` (`DropPosition`, `DropHint`, `DropPlacement`),
  `DragAndDrop/DragPayload.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/DragAndDrop/DropPlacementTests.cs` (placement 21 + payload 10),
  `CategoryPageDragDropTests.cs` (13), `CategoryPageViewDragDropTests.cs` (5 headless, simulated drops)
- `docs/done/FEATURE-5B69-PHASE05.md`

**Modified**

- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml`: `DragDrop.AllowDrop`, the grip, card and
  tile drop bars, `dragging` and `dropInside` classes.
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml.cs`: drag source, drop target, click
  suppression.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/CategoryPageViewModel.cs`, `GroupViewModel.cs`, `ShortcutViewModel.cs`
- `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml`: grip, drop bars, drop-inside frame, dragging fade. The
  tile's outer margin moved to its new `tileSlot` wrapper, so the bars sit in the gap.
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/WindowHarness.cs`: shared `LoadedPageAsync`.
- `README.md`, `docs/roadmap.md`, `docs/plan/FEATURE-5B69.md` (the item and every phase now `DONE`)

## Decisions taken at build time

| Question                            | Chosen                                                             | Why                                                                                                         | Alternatives rejected                                                             |
|-------------------------------------|--------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------|
| Group drag handle                   | A dedicated grip                                                   | A header click must always fold; a grip is an explicit, discoverable affordance                             | Dragging by the whole header (clicks and drags would compete)                     |
| Where the drag state lives          | `DropHint` / `IsDragging` on the item ViewModels, bound to classes | The view stays declarative; the indicator is testable without pixels                                        | Adorners managed in code-behind                                                   |
| Payload                             | Category and identity as text in application formats               | In-process only, minimal, and lets a drop refuse another category's item                                    | Passing ViewModel references; a platform text format (other apps would accept it) |
| Drop on a card outside any tile     | Append to that group                                               | Covers empty and folded groups and the header naturally                                                     | Requiring a drop exactly on a tile                                                |
| Drop in the 12 px gap between cards | Ignored (no target)                                                | The bars are drawn there, but the cards themselves are the targets, and half of each card is "before/after" | Treating the gap as a target of its own                                           |
| Tile released where it started      | The trailing click is suppressed                                   | Dragging a tile must never launch it                                                                        | Relying on the platform drag loop to swallow the release                          |
| Testing the drop side               | Headless `Window.DragDrop` simulations of enter, over and drop     | Exercises the real view, hit-testing and ViewModel end to end                                               | ViewModel-only tests                                                              |

## Deviations & follow-ups

- **Not covered by automated tests:** the drag *source* gesture — press, 4 px move, then
  `DoDragDropAsync`. The headless platform only simulates drops arriving from outside, so this path
  (and the click suppression after a drag) needs one manual check on each target OS. The drop side,
  placement maths and ViewModel moves are covered.
- Real-app smoke run: the app started and showed "Enigma Launchpad" for 6 s, then was closed. The empty
  `%APPDATA%\Enigma.Launchpad` folder the run created was removed afterwards; it had not existed before.
- Visual check: drag feedback (faded tile, bar between tiles, framed receiving group, bar between cards,
  grips) was rendered headlessly to PNG with a throwaway test, not committed. It looks as intended.
- Follow-ups outside this item's scope: auto-scroll while dragging near the edge of a long page; dragging
  files, folders or links in from the OS to create shortcuts (listed as out of scope in the plan).

## Build / test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental`: **0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.Launchpad.slnx`: **221 passed, 0 failed, 0 skipped** (Core 92, Desktop 129).
- No fix cycles: the suite was green on its first run.
