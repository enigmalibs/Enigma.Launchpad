# FEATURE-0BB1 — Group columns

**Status:** IN PROGRESS
**Type:** FEATURE
**Branch:** `feature/feature-0bb1-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-15-group-columns

## Objective

Today every group of an open workspace takes a full line. Let a group be **one third**, **two thirds** or the **full**
width of the line, so groups that fit together share a line — "columns" — and let the drag and drop place a group
beside another one on the same line.

## Context & constraints

- **The model:** `Core/Shortcuts/ShortcutGroup.cs` — `record ShortcutGroup(Guid Id, string Name, ShortcutKind Kind,
  IReadOnlyList<Shortcut> Shortcuts, bool IsCollapsed = false)`, serialized as is into `launchpad.json` (format
  version 3) by `JsonLaunchpadStore`: camelCase, strict enums (`JsonStringEnumConverter`, camelCase, no integers),
  required constructor parameters enforced — optional ones are not — and unknown members skipped (the default).
- **The export file:** `Core/Exchange/ShortcutExchange.cs` — version 2, its own `ExportGroup(Name, Kind, Shortcuts)`
  record; no identities, no fold state; untrusted input, read leniently entry by entry. `Profile.Import` merges an
  incoming group into a same-named group of the same kind, or adds `new ShortcutGroup(Guid.NewGuid(), name, kind, [])`
  at the end; `Profile.Copy` duplicates with `with`.
- **The page:** `Desktop/Views/Pages/WorkspacePageView.axaml` — `ItemsControl x:Name="GroupList"` over a
  `StackPanel Spacing="12"`, inside a `ScrollViewer` (`HorizontalScrollBarVisibility="Disabled"`), margin
  `32,4,32,32`. Each group item is a `StackPanel`: a 48-pixel dashed bar before (`IsDropBefore`), the
  `Border.groupCard` with its dashed frame (`IsDropInside`), a dashed bar after (`IsDropAfter`).
  - The card header is a grid: the grip, the fold `ToggleButton.groupHeader` (a horizontal `StackPanel` of caret,
    kind glyph, name, count badge), the **+** button and the actions flyout (Rename…, Move up, Move down, Move to
    workspace…, Delete…).
  - Tiles are 248 pixels wide (`Button.tile`) in a `StackPanel.tileSlot` with a `0,0,8,8` margin, in a `WrapPanel`
    6 pixels in from the card's 10-pixel padding and 1-pixel border: a card needs **276 pixels** to show one tile.
- **The drag** (`WorkspacePageView.axaml.cs`, FEATURE-23F9): a dragged group's slot is found in the *natural*
  geometry — the cards as they stand with no placeholder open — with `DropPlacement.Stack` / `SlotFor` over the
  cards' heights, so the gap does not move while the cards make room. Tiles use `DropPlacement.Flow` / `FlowSlot`
  over a wrap layout. The copy under the pointer (`GroupGhost`) is the group's header, at the list's left and width,
  moving up and down only. The ViewModel (`MovesGroup`, `DropGroup`) works on the flat order with
  `DropPlacement.MoveIndex`.
- **Window:** 1180 × 760 by default, 760 wide at least; the navigation rail is to the left of the page.
- **Tests:** `dotnet test --solution Enigma.Launchpad.slnx` — Core unit tests; Desktop unit tests, headless
  (`WindowHarness`, raw drag events through `harness.Window.DragDrop`).

## Design

- **Width, not position.** A group keeps its place in the workspace's one ordered list and gains a width. The page
  lays the groups out in **lines of three columns**, in order: a group that does not fit in what is left of the
  current line starts the next one. Two ⅓ groups and a ⅓, a ⅓ and a ⅔, or three ⅓ share a line; a full-width group
  is always alone on its line. Nothing about the order, the groups' identities or the other workspaces changes.
- **Cards on a shared line** are top-aligned and keep their own heights.
- **Narrow pages** — when a third of the line is narrower than one tile's card (276 pixels) — show every group at
  full width, in order, as today. The width is kept; only the drawing falls back.
- **Default** is full width: every existing group, file and export looks exactly as it does today.

## Shared rules (all phases)

- Build with zero warnings; the whole suite green.
- No new dependency. `launchpad.json` stays format version 3; the export stays version 2.
- Drop rules do not change: groups reorder whatever their kind, a tile goes only to a group of its own kind, foreign
  or unknown payloads are refused, a drop next to the item's own place changes nothing.

## PHASE01 — Group width in model & files

**Status:** DONE
**Branch:** `feature/feature-0bb1-phase01-group-width-model`

### Steps

1. `Core/Shortcuts/GroupWidth.cs` — `public enum GroupWidth { Full, TwoThirds, OneThird }` (`Full` is the zero
   value, so `default` is a valid width), with a `Columns(this GroupWidth)` mapping (3, 2, 1) in the same file or as
   a C# 14 extension block, per the house extension-method convention.
2. `ShortcutGroup` gains `GroupWidth Width = GroupWidth.Full` as its last parameter; its doc says what it is.
3. `launchpad.json`: nothing to change in the store — the field is written as `"width": "full" | "twoThirds" |
   "oneThird"`; a file without it reads as full width; a value the enum does not know makes the file unreadable,
   like an unknown kind (the existing strictness). Version 1/2 groups (`GroupV2`) read as full width.
4. `Profile.Import`: a group added from the source keeps its width; a group merged into an existing one keeps the
   existing group's width. `Profile.Copy` keeps widths (it already copies with `with`; covered by a test).
5. Export: `ExportGroup` gains `string? Width = null`, written only when the width is not full (camelCase name);
   read back ignoring case; a missing, empty or unknown value reads as full width, and costs nothing else.
6. Tests (Core):
   - store: round-trip of each width; a version 3 file without `width` reads as full; an unknown width moves the file
     aside like an unknown kind; version 2 groups read as full;
   - `Profile`: `Import` adds a new group with its width and keeps an existing group's width; `Duplicate` keeps
     widths;
   - exchange: a ⅓ group exports `"width": "oneThird"`; a full group writes no `width`; widths round-trip; a missing
     or unknown width imports as full and skips nothing.

### Acceptance criteria

- A group has a width — full, two thirds or one third — full by default.
- `launchpad.json` keeps each group's width; a file written before this change opens with every group at full width.
- An export carries the widths that are not full, and an import applies them to the groups it adds; an unknown width
  in a file is read as full, and nothing is skipped for it.
- Build 0 warnings; whole suite green.

## PHASE02 — Columns layout & width menu

**Status:** DONE
**Branch:** `feature/feature-0bb1-phase02-columns-layout`

### Steps

1. **The layout arithmetic** — a pure static, `Desktop/Controls/ColumnFlow.cs`:
   - `Layout(width, items, columns, columnSpacing, rowSpacing, minColumnWidth)` — `items` is each item's span (in
     columns) and height, in order; it returns each item's rectangle. The column count falls to 1 (every span
     clamped to it) when a column would be narrower than `minColumnWidth`. A span is clamped to `1..columns`. Items
     fill lines left to right; an item whose span does not fit in what is left of the line starts the next one. A
     column's width is `(width − (columns − 1) × columnSpacing) / columns`; an item spanning `n` columns is
     `n` columns plus `n − 1` spacings wide. A line is as high as its highest item; lines are `rowSpacing` apart.
   - The rectangles keep each item's own height (top-aligned); a second result — or a flag — gives each item's line
     height, for PHASE03's slot finding.
2. **The panel** — `Desktop/Controls/ColumnFlowPanel.cs`, a `Panel` with styled properties `Columns` (3),
   `ColumnSpacing`, `RowSpacing`, `MinColumnWidth`, and an attached `Span` (int, default = all the columns) read
   from each child. `MeasureOverride` measures each child at its span's width (infinite height), then lays them out
   with `ColumnFlow.Layout`; `ArrangeOverride` arranges each child at its rectangle. Infinite available width (never
   the case under the scroll viewer, which disables horizontal scrolling) lays every child out at its desired width
   in one column.
3. **ViewModel** — `GroupViewModel`:
   - `Width` (`GroupWidth`, read from the model, written by `ToModel()`), `Columns` (its span), and `IsFullWidth` /
     `IsTwoThirds` / `IsOneThird` for the menu;
   - `SetWidthCommand` (`RelayCommand<GroupWidth>`) — sets the width and saves (`_page.Persist()`) when it changes;
     a width already set saves nothing.
4. **Markup** (`WorkspacePageView.axaml`):
   - `GroupList`'s items panel becomes `ColumnFlowPanel Columns="3" ColumnSpacing="12" RowSpacing="12"
     MinColumnWidth="276"`; the item containers get `ColumnFlowPanel.Span` bound to `Columns` through the
     `ItemsControl`'s `ItemContainerTheme`;
   - the actions flyout gains a **Width** submenu after Rename…: *Full width*, *Two thirds*, *One third* — radio
     items (`ToggleType="Radio"`) checked by the matching `Is…` property, each running `SetWidthCommand` with its
     width;
   - *Move up* / *Move down* become *Move earlier* / *Move later* with left/right arrows, as on tiles: in columns,
     the next group in order may be beside, not below;
   - the header's name trims with an ellipsis when the card is narrow, the count badge staying right after it (a
     left-aligned grid whose name column is `*`); the same in the dragged header's copy; the empty group's sentence
     wraps.
5. **View code:** `GroupRows` reads the row spacing from the `ColumnFlowPanel` (it read the `StackPanel`'s), so the
   existing vertical slot finding keeps working for full-width groups until PHASE03 replaces it.
6. **README:** one sentence in the workspace paragraph — a group's menu sets its width (full, two thirds or one
   third), and narrower groups share a line.
7. Tests:
   - `ColumnFlow.Layout`: three ⅓ on one line; ⅔ + ⅓; ⅓ + ⅔ + ⅓ wraps the last; full alone; a ⅔ after two ⅓ wraps;
     spacing arithmetic; line heights and top alignment; the one-column fallback under `minColumnWidth`; clamped
     spans; no items;
   - `GroupViewModel`: the width is read and written back; `SetWidthCommand` saves once and only on a change; the
     `Is…` flags follow;
   - headless: three ⅓ groups share a line, side by side, each a third of the list wide; a full group under them; a
     window too narrow for thirds stacks them at full width; the Width menu's radio items show the current width and
     setting one re-lays the page out and is saved; a long group name in a ⅓ card is trimmed and its badge stays
     inside the card.

### Acceptance criteria

- A group's menu sets its width to full, two thirds or one third; the choice shows as checked and is kept.
- Groups are laid out in lines of three columns, in order; groups that fit share a line, top-aligned.
- When the page is too narrow for a tile in a third of a line, every group shows at full width.
- A narrow card's name is trimmed rather than overlapping its buttons.
- Existing group and tile drag and drop keeps working with full-width groups.
- Build 0 warnings; whole suite green.

## PHASE03 — Drag groups into columns

**Status:** TODO
**Branch:** `feature/feature-0bb1-phase03-column-drag`

### Steps

1. **The gap belongs to the panel.** A gap among cards in two dimensions cannot live inside one card's item (it may
   sit on another line than the card it precedes), so:
   - `ColumnFlowPanel` gains attached `GapBefore` / `GapAfter` (int: the gap's span; 0 for none) read from each
     child, and styled `GapHeight` (48), `GapBrush`; it lays the gap out in the flow as an item of its own — so the
     cards after it make room and wrap as they would — and draws it in `Render` with the placeholder look (the
     brush at 0.08 opacity, a 1.5-pixel `4,3` dashed outline, 10-pixel corners); `GapBounds` (`Rect?`) says where
     it is;
   - `GroupViewModel` gains `DropColumns` (the dragged group's span, set with the hint) and `GapBefore` / `GapAfter`
     (`DropColumns` when the hint is before / after, else 0), raised with `DropHint`;
   - the item containers bind `GapBefore` / `GapAfter` like `Span`; the group item's two dashed bars and their
     `Panel.placeholder.group` styles go; the card's dashed frame (`IsDropInside`) stays.
2. **The slot in two dimensions.** `ResolveGroup` builds the natural layout — each card's span and height, laid out
   by `ColumnFlow.Layout` in the panel's width with no gap — gives each rectangle its line's height plus the row
   spacing, and asks `DropPlacement.FlowSlot` for the gap under the pointer: the line under it (the first when above
   them all, the last when below), before the first card whose middle is right of it, else after the line's last
   card. Then, as today, `MovesGroup` → own place shows nothing and is accepted; otherwise the hint (and its
   columns) is set, or the drop is made. `GroupRows` and the vertical-only path go.
3. **The copy follows in two dimensions.** `GroupGhost` takes the dragged card's width (the list's when it has no
   card) and follows the pointer both ways where it was taken hold of, kept inside the list's horizontal extent and
   the layer's height: a full-width group's copy therefore still moves up and down only, at the cards' left.
4. Tests:
   - `ColumnFlowPanel` (headless or measured directly): a gap before / after a child takes its span and the
     `GapHeight`, the children after it reflow — a ⅓ gap fits beside two ⅓ cards, a full gap goes alone on its line
     — and `GapBounds` is where it is drawn; no gap, no bounds;
   - headless drag: a ⅓ group dropped on the right half of a ⅓ group lands after it, on the same line; on the left
     half, before it; over the empty end of a line, after its last card; above the first line, before the first
     card; the dashed gap is a third wide and on that line; the gap stays put once the cards have made room
     (pointer unchanged, second `DragOver`); a full-width group dragged between two ⅓ cards opens a full-width gap on
     its own line and lands between them in order; own place opens nothing and is accepted;
   - the copy of a ⅓ group is a third wide and follows the pointer both ways, inside the list; a full-width group's
     copy keeps today's behaviour (aligned with the cards, up and down only) — the existing test kept;
   - the existing group-drag tests updated to the panel's gap (`GapBounds`, the `GapBefore` / `GapAfter` hints)
     instead of the removed dashed bars, with the same assertions on what lands where.

### Acceptance criteria

- Dragging a group shows a dashed gap as wide as the group where it would land: beside a card when it fits on that
  line, on a line of its own otherwise; the cards after it make room, and the gap does not move while they do.
- Dropping a group beside another one puts it there in the order, so the two share the line when their widths fit.
- The dragged group's copy is as wide as its card and follows the pointer in both directions, inside the list.
- Tile drag and drop, the dashed frame on a group receiving a tile, own place and foreign data are unchanged.
- Build 0 warnings; whole suite green.

## Out of scope

- Resizing a group by dragging its edge; widths other than thirds; more than three columns.
- Stretching the cards of a line to the same height; empty columns kept on purpose (rows as objects).
- Auto-scrolling near the list's edges during a drag (unchanged from FEATURE-23F9).
- Release notes and the version number: written by the next release item.

## Decisions taken autonomously

| Question                         | Chosen                                                                                       | Why                                                                                                              | Alternatives rejected                                                         |
|----------------------------------|----------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------|
| Layout model                     | A width per group (full, ⅔, ⅓) in a flow of 3-column lines, over the existing order          | One ordered list stays the truth; additive, no migration; every drop is still a reorder                          | Rows as objects in the workspace; grid coordinates per group                  |
| Width values                     | `GroupWidth { Full, TwoThirds, OneThird }`, `Full` the zero value                            | What the request names; a zero value that is valid; readable in the JSON                                         | An integer span; a percentage                                                 |
| `launchpad.json`                 | Optional `width`, default full; format version stays 3; strict like `kind`                   | Old files read unchanged; older builds skip the unknown member; one rule for enums in the file                   | Bumping to version 4 with a migration; a lenient converter                    |
| Export / import                  | Carry `width` when not full; an unknown value reads as full                                  | Order travels already, and width is part of how a workspace is arranged; the file is untrusted                   | Leaving layout out of exports, like the fold state                            |
| Setting the width                | A Width submenu of radio items in the group's menu                                           | Discoverable, keyboard-reachable, one command; the menu already holds the group's arrangement                    | A resize handle on the card's edge; a choice in the New group prompt          |
| Heights on a shared line         | Top-aligned, each card its own height                                                        | A folded card stays a header; no empty stretched bodies                                                          | Stretching every card of a line to the tallest                                |
| Narrow pages                     | Every group at full width when a third cannot hold one tile's card (276 px)                  | Tiles have a fixed width; a third narrower than one would clip it                                                | A two-column fallback (thirds do not map onto two columns); clipping          |
| Layout implementation            | A small `ColumnFlowPanel` over a pure `ColumnFlow.Layout`                                    | Spans need the available width, which only a panel knows; the pure layout is what PHASE03's slot finding reuses  | A `WrapPanel` with widths bound through a converter; a `Grid` rebuilt in code |
| Span to the panel                | Attached `ColumnFlowPanel.Span` on the item containers, bound through `ItemContainerTheme`   | The containers are the panel's children; the standard Avalonia way to feed a panel per item                      | The panel reading the ViewModel from `DataContext`                            |
| The group gap in two dimensions  | Laid out and drawn by the panel, as an item of the flow with the dragged group's span        | The gap may sit on another line than the card it precedes, which no element inside that card's item can do      | Keeping the dashed bars in the item; inserting a fake item into `Groups`      |
| Slot finding                     | The natural column layout with `DropPlacement.FlowSlot`                                      | FEATURE-23F9's steady-gap rule, reused as is from the tiles' flow                                                | Hit-testing the laid-out cards (the gap would move them)                      |
| The dragged group's copy         | The card's width, both axes, kept inside the list                                            | Groups now move sideways; a full-width group's copy cannot move sideways, so today's look is kept for it         | Always the list's width, vertical only                                        |
| Group move commands              | Renamed *Move earlier* / *Move later* with left/right arrows                                 | In columns the neighbour in order may be beside; same wording as tiles                                           | Keeping *Move up* / *Move down*                                               |
| Narrow cards' header             | The name trims with an ellipsis, the badge right after it; the empty sentence wraps          | A ⅓ card leaves about a hundred pixels for the name                                                              | Letting it overlap the buttons; moving the badge to the right edge            |
| Phasing                          | Model & files, then layout & menu, then drag                                                 | Each phase is one reviewable, green commit; the layout stands before the drag that depends on it                 | One phase; drag before layout                                                 |
