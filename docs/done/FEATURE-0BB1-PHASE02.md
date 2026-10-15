# FEATURE-0BB1-PHASE02 — Columns layout & width menu

**Item:** FEATURE-0BB1 — Group columns
**Phase:** PHASE02 — Columns layout & width menu
**Branch:** `feature/feature-0bb1-phase02-columns-layout`
**Run:** vibe/2026-10-15-group-columns

## Summary

An open workspace now lays its groups out in lines of three columns, and a group's menu sets its width.

- **The layout:**
  - Groups are placed in order. A group that does not fit in what is left of the current line starts the next one:
    three ⅓ groups, or a ⅔ and a ⅓, share a line, and a full-width group is alone on its line.
  - Cards on a shared line sit at its top, each at its own height. The line is as high as its highest card, and lines
    are 12 pixels apart, as the cards were before.
  - When a third of the line is narrower than one tile's card (276 pixels: a 248-pixel tile, its 6-pixel inset, the
    10-pixel padding and the border), every group takes the whole line, in order. The widths are kept, only the
    drawing falls back.
- **`ColumnFlow`** — the pure arithmetic, `Desktop/Controls/ColumnFlow.cs`:
  - `ColumnsFor` gives the column count: the line's columns, or 1 when a column is too narrow;
  - `SpanWidth` gives an item's width;
  - `Layout` places the items. Each placement has its `Bounds` (the item's own height) and its `Cell` (the same columns
    over the whole line's height, for PHASE03's slot finding);
  - `Height` gives the layout's total height.
- **`ColumnFlowPanel`**, `Desktop/Controls/ColumnFlowPanel.cs`:
  - its properties are `Columns`, `ColumnSpacing`, `RowSpacing`, `MinColumnWidth` and the attached `Span`; a child
    with no `Span` takes the whole line;
  - it measures each child at its span's width and arranges it at its placement;
  - `ColumnsAt(width)` and `Place(width)` expose the layout for the drag code.
  - It is `GroupList`'s items panel. Each item container's `Span` is bound to `GroupViewModel.Columns` through the
    `ItemsControl`'s `ItemContainerTheme`.
- **The Width menu.** The group menu has a **Width** submenu after *Rename…* with three radio items: *Full width*,
  *Two thirds*, *One third*.
  - The current width is checked: each item's `IsChecked` is bound one-way to `IsFullWidth`, `IsTwoThirds` or
    `IsOneThird`.
  - Each item runs `SetWidthCommand` with its width. `WorkspacePageViewModel.SetGroupWidth` applies and saves a change,
    and ignores the current width or an undefined value.
- **Group moves:** *Move up* / *Move down* are now *Move earlier* / *Move later*, with left and right arrows, as on
  tiles. The next group in order may now be beside this one, not below it.
- **Narrow cards:**
  - the header is a left-aligned grid whose name column is `*`, so a long name is trimmed with an ellipsis and the
    count stays right after it. The dragged header's copy uses the same grid;
  - the empty group's sentence wraps.
- **README:** the workspace paragraph describes the Width menu and the narrow-window fallback.

## Files touched

- Created:
  - `src/Enigma.Launchpad.Desktop/Controls/ColumnFlow.cs`
  - `src/Enigma.Launchpad.Desktop/Controls/ColumnFlowPanel.cs`
  - `tests/Enigma.Launchpad.Desktop.UnitTests/Controls/ColumnFlowTests.cs`
  - `tests/Enigma.Launchpad.Desktop.UnitTests/WorkspacePageViewColumnsTests.cs`
- Modified:
  - `src/Enigma.Launchpad.Desktop/ViewModels/Pages/GroupViewModel.cs` — the settable `Width`, `Columns`, the `Is…`
    flags, `SetWidthCommand`.
  - `src/Enigma.Launchpad.Desktop/ViewModels/Pages/WorkspacePageViewModel.cs` — `SetGroupWidth`; the move docs say
    *earlier*.
  - `src/Enigma.Launchpad.Desktop/Views/Pages/WorkspacePageView.axaml` — the panel, the container theme, the Width
    submenu, the renamed moves, the header grids.
  - `src/Enigma.Launchpad.Desktop/Views/Pages/WorkspacePageView.axaml.cs` — `GroupRows` reads the panel's row
    spacing.
  - `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml` — `groupName` trims, `groupEmpty` wraps.
  - `README.md` — the Width sentence.
  - `tests/Enigma.Launchpad.Desktop.UnitTests/WorkspacePageGroupsTests.cs`

## Decisions taken at build time

| Question                                 | Chosen                                                                                    | Why                                                                                                                       | Alternatives rejected                                  |
|------------------------------------------|-------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------|
| Where the panel lives                    | A new `Desktop/Controls/` folder, namespace `Enigma.Launchpad.Desktop.Controls`           | A reusable layout control; neither a view, a ViewModel nor drag-and-drop                                                  | `Views/Pages/`; `DragAndDrop/`                         |
| The line-height result for PHASE03       | `ColumnFlowPlacement(Bounds, Cell)`                                                       | One layout call gives both what the panel arranges and what the slot finding needs                                        | A second method; a flag                                |
| A child without a span                   | `Span` defaults to `int.MaxValue`, clamped to the whole line                              | A panel with nothing bound behaves like the stack it replaces                                                             | Default 1 (a third)                                    |
| Infinite width                           | Lines as wide as the widest child                                                         | Never happens under a vertical-only scroll viewer; keeps the measure finite                                               | Throwing; one column at desired widths                 |
| Radio items' checked state               | `IsChecked` bound one-way to the ViewModel; `Command` + `CommandParameter`                | The click's own toggle and the ViewModel agree; a test clicks through all three                                           | A two-way bound `IsChecked` with setters on three flags |
| `GroupName` on the radio items           | None                                                                                      | Items of one submenu are already one group; each group card has its own flyout                                            | A shared group name across every card                  |
| Menu icon                                | Phosphor `Columns`                                                                        | Says columns                                                                                                              | `SquareSplitHorizontal`                                 |

## Deviations & follow-ups

- **The `Width` property moved from read-only (PHASE01) to settable**, with an `internal` setter used by the page,
  as the existing commands are.
- **Between this phase and PHASE03,** group drag and drop still finds its slot vertically (`GroupRows` / `SlotFor`)
  and still opens the dashed bars inside the group's item. With full-width groups this is unchanged and the existing
  tests pass. With columns, a group is still reordered correctly, but the bar opens under or over the card instead of
  beside it. PHASE03 replaces this.
- **Follow-up (outside this item):** the Profiles page's card menu still says *Move up* / *Move down*, which is right
  for that vertical list.
- **Fix budget:** 0 cycles. The first build and test run was green.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx`: **951 total, 950 passed, 0 failed, 1 skipped** (the Unix-only
  resolver test on this Windows host). The baseline was 925.
- Tests added:
  - `ColumnFlowTests` (pure):
    - three ⅓ on a line; ⅔ + ⅓; a group that does not fit wraps;
    - a full-width group is alone on its line;
    - each item keeps its own height, cells take the line's height, and the total height;
    - spans are clamped to the line;
    - the one-column fallback at the 276-pixel limit, with every item at full width;
    - `SpanWidth` for spans 1–3 and beyond, and never negative;
    - no items; a line with zero columns is refused.
  - `WorkspacePageGroupsTests`:
    - `SetWidth` changes the width, `Columns` and the flags, and saves;
    - the current width or an undefined value raises nothing and saves nothing.
  - `WorkspacePageViewColumnsTests` (headless):
    - three ⅓ cards side by side, each a third wide, with a full-width card under them;
    - ⅔ + ⅓ top-aligned at their own heights;
    - a 760-pixel window stacks everything at full width;
    - the menu's entries;
    - the radio items check the current width; clicking re-lays the page out and is saved;
    - a long name in a ⅓ card is trimmed, with its badge before the **+** button, and a short name keeps its badge
      10 pixels after it.
