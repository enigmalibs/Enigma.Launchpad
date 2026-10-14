# FEATURE-1C0D — Workspaces

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-1c0d-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-14-workspaces-documents-commands

## Objective

Replace the three fixed category pages (Applications, Directories, URLs) with **workspaces**:

- Each profile holds its own list of workspaces. A workspace has a **title**, a **description** and an **icon**, and
  holds **groups**.
- Every group is of **one kind** — applications, folders or links (documents come with FEATURE-1FA9) — so a group
  never mixes kinds, but a workspace mixes groups of every kind.
- The main page lists the workspaces of the profile in use, with a search box, and lets the user add, edit,
  duplicate, reorder, delete and **open** them. An open workspace shows its groups and tiles as the category pages
  did.

## Context & constraints

- Core model today: `LaunchpadData` (profiles, active profile) → `Profile` (`Applications`, `Directories`, `Urls`:
  lists of `ShortcutGroup`) → `ShortcutGroup` (name, shortcuts, folded) → `Shortcut`. The kind is the category a
  shortcut is stored in.
- `launchpad.json` is format version 2. Version 1 (categories at the root) is still read. A file this build cannot
  read is moved aside as `launchpad.corrupt-<stamp>.json`.
- The export file (`enigma-launchpad-shortcuts`, version 1) has `applications` / `directories` / `urls`.
  `Profile.Import` merges groups by name within a category and skips duplicate shortcuts.
- Desktop:
  - `CategoryPageViewModel`, with three subclasses, is a page per category, reading and writing its category through
    `ILaunchpadSession.GroupsOf` / `Update`.
  - `GroupViewModel` and `ShortcutViewModel` forward to the page.
  - `CategoryPageView` holds the drag and drop.
  - The export/import selection dialog is a category → group → shortcut tree.
- Views are transient and ViewModels singletons; pages are resolved by `ContainerPageFactory`.
- `INavigationService` drops a navigation requested while one is running, and re-selecting the selected rail item
  does nothing.

## Design

### Model (Core)

- `ShortcutGroup(Guid Id, string Name, ShortcutKind Kind, IReadOnlyList<Shortcut> Shortcuts, bool IsCollapsed = false)`
  — the kind moves onto the group.
- `Workspace(Guid Id, string Title, IReadOnlyList<ShortcutGroup> Groups, string? Description = null, string? Icon = null)`.
  - `Icon` is the name of a glyph, resolved by the Desktop; `null` or unknown → the default glyph.
  - Constants: `DefaultTitle = "Shortcuts"`, the title of the workspace older files and version 1 exports are read
    into.
- `Profile(Guid Id, string Name, IReadOnlyList<Workspace> Workspaces)`, with:
  - workspace operations: find, add, replace, remove, can-move/move;
  - `Duplicate` — new identities everywhere;
  - `Import` — see below;
  - `CountWorkspaces`, `CountGroups`, `CountShortcuts`.
- `LaunchpadData` keeps the profile operations, plus workspace operations on the profile in use.
- **Import merge:**
  - A source workspace whose title matches one of the profile's (ignoring case) merges its groups into it.
  - Otherwise it is added at the end, with its description and icon.
  - Groups merge by name **and kind**, ignoring case.
  - Shortcuts identical to one already in the group are skipped, as today.
  - Everything added gets new identities.

### Storage — format version 3

- Profiles → workspaces → groups (with `kind`, written as a camelCase string: `"application"`, `"directory"`,
  `"url"`) → shortcuts.
- **Migration:**
  - Version 2: each profile with at least one group becomes a profile with one workspace titled **"Shortcuts"**
    (glyph `SquaresFour`, description "Your shortcuts from before workspaces."), holding its application groups,
    then its directory groups, then its URL groups, each typed by its old category.
  - A profile with no group gets no workspace.
  - Version 1 goes through the same path.
- **Backup:** when a file of an older version is read, it is copied once to `launchpad.v<N>.backup.json` beside it
  (never overwritten), before anything is saved in version 3. A downgrade can then go back to it. An older build
  moves a version 3 file aside rather than overwriting it.
- Well-formedness check extended to workspaces.

### Export format — version 2

```json
{ "format": "enigma-launchpad-shortcuts", "version": 2, "profile": "Work",
  "workspaces": [ { "title": "Dev", "description": "…", "icon": "Code",
    "groups": [ { "name": "Tools", "kind": "application", "shortcuts": [ … ] } ] } ] }
```

- Version 2 is written; versions 1 and 2 are read. A version 1 file is read into one workspace titled "Shortcuts".
- Skipped and counted:
  - a workspace whose title is not valid, together with everything in it;
  - a group whose kind is unknown or whose name is not valid, together with its shortcuts;
  - an invalid shortcut, as today.
- An icon name that is not a plain identifier (letters and digits, ≤ 64) is dropped.

### Validation

- `ValidateWorkspaceTitle(title, otherTitles)`: required, ≤ 100, unique in the profile ignoring case.
- `ValidateWorkspaceDescription`: optional, ≤ 200.
- Group names stay unique **within a workspace among groups of the same kind** (callers pass the right sibling list).
- Target messages pointing at the old pages ("Add it under Directories instead") now read "Add it to a folders group
  instead" / "…an applications group…".

### Session (Desktop)

- `ILaunchpadSession` exposes the workspaces of the profile in use:
  - `Workspaces`, `FindWorkspace(id)`;
  - `AddWorkspace(title, description, icon)`, `UpdateWorkspace(id, title, description, icon)`,
    `DuplicateWorkspace(id, title)`, `DeleteWorkspace(id)`, `CanMoveWorkspace` / `MoveWorkspace`;
  - `UpdateGroups(workspaceId, groups)` — the open workspace saving itself;
  - `AddGroup(workspaceId, group)` — receiving a moved group.
- `WorkspacesChanged` is raised after any workspace change made through the list operations.
- `ActiveProfileChanged` keeps its meaning: another profile is in use, or content was imported into the one in use.
- PHASE01 keeps `GroupsOf(kind)` / `Update(kind, groups)` as a **temporary bridge** over the profile's first
  workspace, so the category pages keep working until PHASE03 removes both.

### Workspaces page (PHASE02)

- Rail: **Workspaces** (`SquaresFour`) first.
  - PHASE02 keeps the category pages below it; PHASE03 removes them.
  - Footer unchanged: Profiles, Settings.
- Header: icon, title "Workspaces", "The workspaces of this profile, each with its own groups.", **New workspace**.
- A **search box** filters the cards live on title and description (trimmed, ignoring case). With no match: "No
  workspace matches “x”."
- Empty profile: an invitation to create one, as the empty category page did.
- Cards, in the profile's order:
  - glyph, title, description (when there is one), and a summary — "3 groups · 12 shortcuts" / "Empty".
  - Clicking the card or pressing Enter **opens** it.
  - Menu: Open, Edit…, Duplicate…, Move up, Move down, Delete… (confirmed, saying what is lost).
- **Workspace form** (`WorkspaceEditorViewModel` / `WorkspaceEditorView` through `ILaunchpadDialogs.EditWorkspaceAsync`):
  - title (required, checked as typed);
  - description (optional);
  - **icon**, picked from a grid of about 30 Phosphor glyphs (a keyboard-navigable `ListBox`), defaulting to
    `Briefcase` for a new workspace.
- The page follows `WorkspacesChanged` and `ActiveProfileChanged`.

### Open workspace (PHASE03)

- `WorkspacePageViewModel` (built from `CategoryPageViewModel`, which is removed with its three subclasses) shows one
  workspace's groups. `WorkspacesPageViewModel.OpenWorkspace` holds it, and the page's view shows either the list or
  the open workspace — a drill-in within the page, so the rail stays on Workspaces.
- Header: a **back** button (to the list), the workspace's glyph, title and description, **New group**.
  - **New group** is a menu of kinds — *Applications*, *Folders*, *Links* — each asking for the name.
- Each group card shows its kind's glyph before its name. Its menu gains **Move to workspace…**:
  - a dialog listing the other workspaces;
  - the group is appended to the chosen one;
  - a group of the same name and kind already there → refused, with a warning saying so.
- Tiles: as today, their kind taken from their group.
- Drag and drop:
  - groups reorder freely;
  - a tile moves within its group or to another group **of the same kind**.
- Profile switch, or the open workspace deleted or imported into: the page re-reads it, or goes back to the list
  when it no longer exists.
- Remove `ApplicationsPageViewModel`, `DirectoriesPageViewModel`, `UrlsPageViewModel`, the category rail items and
  the session bridge.

### Profiles page and selection dialog

- PHASE01: the selection tree becomes workspace → group → shortcut; the profile summary counts workspaces and
  shortcuts ("2 workspaces · 14 shortcuts" / "No workspaces yet"). Export is enabled when a profile has a workspace.
- PHASE03: the page description reads "Each profile keeps its own workspaces."

## Phases

### PHASE01 — Workspace model & storage

**Status:** TODO

Steps:
1. Core model: `Workspace`, typed `ShortcutGroup`, `Profile` and `LaunchpadData` workspace operations, import merge,
   duplicate, counts.
2. Storage version 3: read 1/2/3, migration, one-time backup, well-formedness.
3. Export version 2, reading versions 1 and 2.
4. Validator: workspace title and description; target messages.
5. Desktop:
   - session bridge (`GroupsOf` / `Update` over the first workspace, creating "Shortcuts" when there is none);
   - selection tree by workspace;
   - profile summary and export enablement;
   - test infrastructure.
6. Tests:
   - model operations;
   - migration from v1 and v2, backup written once and never overwritten, v3 round trip, unknown kind refused;
   - export v2 round trip, v1 file read, skipped entries;
   - import merge by title and by name + kind;
   - session bridge;
   - selection tree;
   - existing tests ported.
7. README: data and export sections.

Acceptance criteria:
- An existing `launchpad.json` (v1 or v2) loads with every shortcut in a "Shortcuts" workspace per profile, is backed
  up once, and is saved as v3; the category pages show exactly what they showed before.
- Export writes v2; a v1 or v2 file imports.
- 0 warnings; suite green.

### PHASE02 — Workspaces page

**Status:** TODO

Steps:
1. Session workspace operations and `WorkspacesChanged`.
2. `WorkspacesPageViewModel`, `WorkspaceItemViewModel`, `WorkspacesPageView`, rail item first, DI.
3. Workspace form with glyph picker; `ILaunchpadDialogs.EditWorkspaceAsync`; glyph catalogue and name mapping.
4. Search, empty and no-match states.
5. Tests:
   - session operations;
   - page: add, edit, duplicate, delete confirmed or cancelled, move, search;
   - follows profile switches;
   - form validation and glyph choice;
   - headless: cards, search and empty states.
6. README: workspaces paragraph.

Acceptance criteria:
- The Workspaces page lists the profile's workspaces with their glyph, title and description; they can be searched,
  created, edited, duplicated, reordered and deleted, and every change is saved.
- 0 warnings; suite green.

### PHASE03 — Open workspace, mixed groups

**Status:** TODO

Steps:
1. `WorkspacePageViewModel` / view from the category page, with typed groups, the kind menu of New group, kind
   glyphs, back button.
2. Drill-in from the Workspaces page; following profile switches, deletes and imports.
3. Move to workspace… (dialog, refusal on a clash).
4. Drag and drop by kind.
5. Remove the category pages, their rail items and the session bridge; Profiles page wording.
6. Tests:
   - category page tests ported to the workspace page;
   - mixed kinds in one workspace;
   - new group per kind;
   - move to workspace, including a clash;
   - cross-kind drop refused;
   - open, back, and following profile switches;
   - headless view and drag tests ported;
   - composition root and rail.
7. README: page description, tiles, profiles.

Acceptance criteria:
- The rail shows Workspaces, Profiles and Settings only.
- Opening a workspace shows its groups of mixed kinds; groups of each kind can be created, edited, reordered and
  moved to another workspace; tiles behave as before.
- 0 warnings; suite green.

## Out of scope

- Documents (FEATURE-1FA9).
- Searching shortcuts across workspaces; a search inside an open workspace.
- Dragging workspace cards; moving a single shortcut to another workspace.
- Remembering the open workspace across restarts.
- Custom image icons for workspaces.

## Decisions taken autonomously

| Question                         | Chosen                                                                              | Why                                                                                   | Alternatives rejected                                  |
|----------------------------------|-------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|--------------------------------------------------------|
| Migration of existing shortcuts  | One "Shortcuts" workspace per non-empty profile, groups typed by category            | Nothing lost, already mixed; the user then splits it with Move to workspace…           | Three workspaces per profile; starting empty           |
| Backup on migration              | Copy the old file once to `launchpad.v<N>.backup.json`                              | The pages disappear with this format; a downgrade has a file to go back to            | No backup (older builds only move v3 aside)            |
| Where an open workspace shows    | Drill-in inside the Workspaces page, with a back button                             | Rail re-selection is a no-op and navigation drops overlapping requests                | `NavigateToAsync` detail page; workspaces in the rail  |
| Workspace icon                   | A glyph from a curated Phosphor set, stored by name                                 | Matches the app's page/glyph language, themes with it; Core stays UI-free             | Custom image; both                                     |
| Description                      | Optional (≤ 200)                                                                     | A required description is friction for a short-lived workspace                        | Required                                               |
| Title uniqueness                 | Unique in the profile, ignoring case                                                | Import merges by title; two equal titles are indistinguishable on the list            | Free titles                                            |
| Group-name uniqueness            | Within a workspace, per kind                                                        | Migration never has to rename; the kind glyph tells same-named groups apart           | Unique across kinds                                    |
| Choosing a group's kind          | New group opens a kinds menu, then the name prompt                                  | Reuses the name dialog; the kind is fixed for the group's life                        | A combined name + kind dialog                          |
| Search scope                     | Workspace title and description, live                                               | "Search" sits among workspace management in the request                               | Also shortcut names (a global search)                  |
| Workspace actions                | Open, edit, duplicate, move up/down, delete                                         | "add/edit/remove/search/etc."; mirrors the Profiles page                              | Drag-reordering cards                                  |
| Moving groups between workspaces | Move to workspace… on a group; same name + kind there → refused                     | Needed to reorganise migrated content; refusal is explicit and loses nothing          | Merging silently; no move at all                       |
| Export format                    | Version 2 with workspaces; version 1 still read                                     | The structure is what is worth moving between machines                                | Keeping v1 by flattening                               |
| Phasing                          | Model & storage (with a temporary session bridge), list page, open workspace        | Each phase is one reviewable, green commit; the bridge keeps the app working between   | One large phase                                        |
