# FEATURE-23F9-PHASE02 — Dragged item follows the pointer

**Item:** FEATURE-23F9 — Workspace drag visuals
**Phase:** PHASE02 — Dragged item follows the pointer
**Branch:** `feature/feature-23f9-phase02-drag-ghost`
**Run:** vibe/2026-10-14-workspace-drag-visuals

## Summary

While something is dragged over an open workspace, a copy of it now follows the pointer. The original stays faded
in its place, as before.

- **A tile** is carried whole.
  - The copy is the tile itself, drawn from the same template as the list.
  - It follows the pointer in both directions, because tiles move across lines and across groups.
  - It is held where it was grabbed, and kept inside the layer it is drawn on (`DropPlacement.GhostOffset` on each
    axis).
- **A group** is carried as its header: grip, kind glyph, name, count.
  - It follows the pointer up and down only, at the cards' left and as wide as the cards.
  - A whole card can be hundreds of pixels tall and would cover the gaps it is being dropped between.
- **The look:** opacity 0.92 with a shadow (`0 6 18 0 #50000000`), as in GitClient.
  - The copy is never faded, although it shows the same item the `.dragging` style fades in the list.
  - It shows none of the tile's placeholders, and none of the room its slot keeps around it.
- **Where it is drawn:** a `DragLayer` canvas over the groups. It cannot be hit, so the drag events still reach the
  page underneath, and it is clipped to the groups' area.
- **Driven by the drag's payload, not the gesture.**
  - Every `DragEnter` / `DragOver` shows the copy of the item the payload names, if that item is on the page.
  - A drag the page started also records where the press took hold of the item (`Lift`). A drag with no record,
    such as the headless tests' raw drag events, holds the copy by its middle.
  - Foreign or unknown payloads show no copy.
- **Ending the drag:**
  - `DragLeave` hides the copy but keeps its content: the leave/enter pair between two elements must not rebuild
    the tile.
  - A drop or the end of the drag session hides the copy, empties it, and forgets the `Lift`.
- **One tile definition.** The tile's `DataTemplate` moved to `UserControl.Resources` (`ShortcutTile`). Every group's
  tile list and the copy use it.

## Files touched

- Modified:
  - `src/Enigma.Launchpad.Desktop/Views/Pages/WorkspacePageView.axaml`:
    - added the `ShortcutTile` resource (the moved tile template, its slot's children now indented under it);
    - the tile list uses it;
    - added the `DragLayer` canvas with `TileGhost` and `GroupGhost`.
  - `src/Enigma.Launchpad.Desktop/Views/Pages/WorkspacePageView.axaml.cs`:
    - added `Lift`, `GrabIn`, `CarryTheCopy`, `ShowTheCopy`, `GrabFor`, `HideTheCopy`, `PutTheCopyDown`;
    - the drag handlers and the end of the session call them.
  - `src/Enigma.Launchpad.Desktop/DragAndDrop/DropPlacement.cs`: added `GhostOffset`.
  - `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml`: added the `ContentControl.ghost` styles (opacity, shadows,
    never faded, no slot margin).
  - Tests:
    - `tests/Enigma.Launchpad.Desktop.UnitTests/DragAndDrop/DropPlacementTests.cs`
    - `tests/Enigma.Launchpad.Desktop.UnitTests/WorkspacePageViewDragDropTests.cs`

## Decisions taken at build time

| Question                                   | Chosen                                                                      | Why                                                                                                           | Alternatives rejected                                          |
|--------------------------------------------|-----------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------|
| What decides which copy shows              | The payload, looked up on the page                                          | The same events as the slot; testable with raw drag events; a platform drag session cannot run headless       | State set only when the page starts a drag                     |
| `DragLeave`                                | Hides the copy, keeps its content                                           | A leave/enter pair is raised whenever the element under the pointer changes; emptying would rebuild the tile  | Emptying it on every leave                                      |
| The copy's size                            | Measured when shown                                                         | It is placed by its size before any layout pass has run on it                                                 | Waiting a layout pass (a frame in the wrong place)              |
| The shadow on a tile's copy                | On the button template's `ContentPresenter`                                 | The tile's slot has margins; a shadow on a wrapper would fall short of the tile                               | A shadowed wrapper border                                       |
| A tile's copy and its slot                 | The slot's margin is dropped inside the copy                                | The copy is exactly the tile, so it is held and clamped by the tile's own size                                | Clamping by the slot's size (8 pixels off at the edges)         |
| Where the press took hold of a group       | Its offset in the card, clamped into the header copy                        | Groups are held by their grip, which is in the header                                                         | Holding the header by its middle                                |
| The moved tile template's indentation      | Its slot's children indented under the slot                                 | The block moved anyway; the old flat indentation hid the slot's structure                                     | Moving it unchanged                                            |

## Deviations & follow-ups

- No deviation from the plan.
- **Not covered by an automated test:**
  - What: the copy being held exactly where a real press took hold of the item (`Lift`/`GrabIn`).
  - Why: it is recorded just before `DragDrop.DoDragDropAsync`, and a platform drag session cannot run headless.
  - Covered instead: the middle-of-the-copy fallback and the clamping (`GhostOffset`), which share the same code
    path.
  - Worth one look in the running app on Windows.
- **Follow-ups** (out of scope in the plan):
  - auto-scrolling near the list's edges while dragging;
  - GitClient's pointer-driven gesture (BUG-11A4), if the refusal cursor shows on XWayland here too.
- No README change: nothing it says became wrong.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx`: **912 total, 911 passed, 0 failed, 1 skipped** (the Unix-only
  resolver test on this Windows host).
- Fix budget: no cycle used. The suite also passed after the template move alone, before any new test.
- Tests added:
  - `DropPlacementTests.GhostOffset` theory: held at the grab, clamped at both ends, taller than the layer.
  - `WorkspacePageViewDragDropTests` (headless):
    - a dragged tile's copy:
      - shows the dragged item, held by its middle and as wide as a tile;
      - follows the pointer in both directions;
      - is not faded, while the original is (0.45);
      - shows no placeholder;
    - the copy stays inside the layer at both corners;
    - a dragged group's copy:
      - is its header (its name, no tiles) at the cards' left and width, shorter than a card;
      - moves vertically only;
    - `DragLeave` hides the copy and a new `DragOver` brings it back;
    - a drop hides and empties it;
    - foreign and unknown payloads show no copy.
