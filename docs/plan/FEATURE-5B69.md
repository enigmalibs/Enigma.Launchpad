# FEATURE-5B69 — Launchpad shortcut manager

**Status:** IN PROGRESS
**Type:** FEATURE
**Branch:** `feature/feature-5b69-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-12-launchpad-shortcut-manager

## Objective

Build **Enigma.Launchpad**, a cross-platform (Windows / Linux / macOS) Avalonia 12 desktop app that manages
"shortcuts" to **applications**, **directories** and **URLs**. Each of the three fixed main categories holds
user-created **groups** (sub-categories); each group holds shortcuts. The look is modern (Enigma Fluent theme,
Phosphor icons, cards and tiles), and groups and shortcuts are **reordered by drag-and-drop**. The UI is built
on `Enigma.Avalonia.Desktop` and `Enigma.Icons.Avalonia`.

## Context & constraints

- Greenfield repository: only `README.md` and one commit on `main`, no remote.
- House stack and conventions (skills `dotnet-solution-setup`, `dotnet-solution-config`, `git-repo-hygiene`,
  `avalonia`, `communitytoolkit-mvvm`, `enigma-avalonia-desktop`, `enigma-icons-avalonia`, `xunit-v3`,
  `dotnet-di-design`, `dotnet-async`) are authoritative.
- Reference implementations for house patterns: sibling repos `Enigma.GitClient` (composition root, Enigma
  services, headless test fixture, ContentDialog forms, `SystemInterop`, `AtomicFile`) and
  `Enigma.MarkdownEditor` (Avalonia 12 `DataTransfer` drag-and-drop with tunnelling pointer handlers).
- Package set (identical to `Enigma.GitClient`, all present in the local NuGet cache): Avalonia /
  Avalonia.Desktop / Avalonia.Themes.Fluent / Avalonia.Fonts.Inter / Avalonia.Headless / Avalonia.Skia
  **12.1.1**, AvaloniaUI.DiagnosticsSupport 2.2.3, CommunityToolkit.Mvvm 8.4.2, Enigma.Avalonia.Desktop
  **1.2.0**, Enigma.Icons.Avalonia **1.0.0**, Microsoft.Extensions.Hosting 10.0.12, xunit.v3 4.0.1,
  Microsoft.Testing.Extensions.CodeCoverage 18.12.0.

## Design

### Solution layout

```
Enigma.Launchpad.slnx
src/Enigma.Launchpad.Core/            net10.0 library — model, validation, JSON store, launcher (no Avalonia)
src/Enigma.Launchpad.Desktop/         net10.0 WinExe — Avalonia app, ViewModels, Views, DI composition root
tests/Enigma.Launchpad.Core.UnitTests/
tests/Enigma.Launchpad.Desktop.UnitTests/
docs/  (roadmap, plan, done)
```

Root files: `.gitignore`, `.gitattributes`, `.editorconfig` (full C#), `Directory.Build.props`,
`Directory.Packages.props` (CPM), `global.json` (SDK 10.0.100 latestFeature + MTP runner), `LICENSE.md` (MIT),
`README.md`, `RELEASENOTES.md` (empty placeholder).

### Core model (`Enigma.Launchpad.Core`)

- `ShortcutKind` — `Application`, `Directory`, `Url`.
- `Shortcut` (record) — `Guid Id`, `string Name`, `string Target`, `string? Arguments` (applications only).
- `ShortcutGroup` (record) — `Guid Id`, `string Name`, `bool IsCollapsed`, `IReadOnlyList<Shortcut> Shortcuts`.
- `LaunchpadData` (record) — `int Version` (= 1), `Applications`, `Directories`, `Urls`
  (`IReadOnlyList<ShortcutGroup>` each), plus `GroupsOf(kind)` / `With(kind, groups)` helpers.
- `ShortcutValidator` — name rules (required, trimmed, ≤ 100 chars), group-name uniqueness per category
  (case-insensitive), application path (absolute, existing file), directory path (absolute, existing directory),
  URL via `UrlNormalizer` (trim; `https://` prefixed when no scheme; only `http`/`https` accepted).
- `ILaunchpadStore` / `JsonLaunchpadStore` — `LoadAsync` / `SaveAsync`, System.Text.Json (camelCase, indented,
  enums as strings), file `launchpad.json` under `IAppPaths` (`ApplicationData/Enigma.Launchpad`), atomic write
  (temp file + move, retried on Windows sharing violations), saves serialised by a `SemaphoreSlim`. A file that
  fails to parse is moved aside to `launchpad.corrupt-<yyyyMMddHHmmss>.json` and an empty document is returned
  with a `RecoveredFrom` path so the UI can warn.
- `IShortcutLauncher` / `ShortcutLauncher` — builds a `ProcessStartInfo` per kind (`UseShellExecute = true`;
  application: working directory = the executable's folder, `Arguments` passed through; directory: opened with
  the platform file manager; URL: re-validated http/https) and starts it through an injectable process-start
  delegate. Returns a `LaunchResult` (`Launched`, `TargetMissing`, `Refused`, `Failed` + message).
- `IPathRevealer` (in the launcher) — "show in folder" (`explorer /select,` · `open -R` · parent dir on Linux).

### Desktop (`Enigma.Launchpad.Desktop`)

- `App` = composition root: `Host.CreateApplicationBuilder` (content root = `AppContext.BaseDirectory`),
  `AddEnigmaDesktopServices()` (the six library services), `AddLaunchpadCore()`, `AddLaunchpadApp()`;
  host **started, not run**; hosts registered before the window shows; `RequestedThemeVariant="Dark"`.
- `MainWindow` — `NavigationView` rail (Applications · Directories · URLs), page `ContentControl`, then the three
  host controls (`ContentDialog`, `Overlay`, `InfoBar`) as the last children of the root `Panel`.
- `ILaunchpadSession` (singleton) — loads the document once, hands each category its groups, and saves the
  whole document when a category reports a change (failures → InfoBar error + log).
- `CategoryPageViewModel` (abstract) + `ApplicationsPageViewModel`, `DirectoriesPageViewModel`,
  `UrlsPageViewModel` — the live state for one category: `ObservableCollection<GroupViewModel>`, each holding
  `ObservableCollection<ShortcutViewModel>`. One shared `CategoryPageView`.
- Views transient, ViewModels singleton (house split); `ContainerPageFactory` resolves pages from DI.
- Dialogs: group name dialog, shortcut editor dialog (per-kind fields, Browse… via `IFileDialogService` /
  `IFolderDialogService`, inline validation, primary button enabled only when valid), destructive confirmation.
- Drag-and-drop: Avalonia 12 `DataTransfer` with application formats carrying the dragged group / shortcut id;
  tunnelling pointer handlers with a 4 px threshold; a pure `DropPlacement` calculator (before/after by pointer
  position vs. the target's midpoint) unit-tested; an accent insertion indicator while dragging.

### Data file example

```json
{
  "version": 1,
  "applications": [
    { "id": "…", "name": "Dev tools", "isCollapsed": false,
      "shortcuts": [ { "id": "…", "name": "VS Code", "target": "C:\\…\\Code.exe", "arguments": null } ] }
  ],
  "directories": [],
  "urls": []
}
```

## Phases

### PHASE01 — Solution scaffold & app shell

**Status:** DONE

Steps:
1. Root files from the house templates: `.gitignore`, `.gitattributes`, `.editorconfig` (full C#),
   `Directory.Build.props` (authors "Josué Clément", 2026, `Version` 0.1.0), `Directory.Packages.props` (the
   package set above, grouped Core / Desktop / Tests), `global.json`, `LICENSE.md` (MIT), empty `RELEASENOTES.md`.
2. `Enigma.Launchpad.slnx` with `/src/` and `/tests/` folders.
3. `Enigma.Launchpad.Core` (net10.0 library, `GenerateDocumentationFile`), with
   `DependencyInjection/ServiceCollectionExtensions.AddLaunchpadCore()` (empty for now, filled in PHASE02).
4. `Enigma.Launchpad.Desktop` (WinExe, compiled bindings, `app.manifest`, `Assets/app.ico`): `Program`, `App`
   (composition root), `MainWindow` with the three host controls and the navigation rail with the three category
   items (Phosphor `AppWindow`, `Folder`, `Globe`), each opening a placeholder page; `ContainerPageFactory`.
5. Generate the placeholder `app.ico` (16/32/48/256 px) from a Phosphor glyph.
6. Test projects (xunit.v3 4.0.1, MTP): Core smoke test; Desktop `CompositionRootTests` (container validates on
   build, every Enigma service resolves) and a `HeadlessAvaloniaFixture` + a smoke test that the main window
   builds and shows three navigation items.
7. Fill `README.md` (what it is, requirements, build / run / test commands).

Acceptance criteria:
- `dotnet build Enigma.Launchpad.slnx` succeeds with 0 warnings (including `AVLN*`), `dotnet test --solution
  Enigma.Launchpad.slnx` passes.
- The app starts (dark theme) with a three-item rail; selecting an item shows its page.

### PHASE02 — Core model, persistence & launcher

**Status:** DONE

Steps:
1. Model records (`ShortcutKind`, `Shortcut`, `ShortcutGroup`, `LaunchpadData`).
2. `ShortcutValidator` + `UrlNormalizer`.
3. `IAppPaths` / `AppPaths`, `AtomicFile`, `ILaunchpadStore` / `JsonLaunchpadStore` with corrupt-file recovery.
4. `IShortcutLauncher` / `ShortcutLauncher` (+ reveal in folder) with an injectable process starter.
5. Register everything in `AddLaunchpadCore()`.
6. Unit tests: validation and URL normalisation theories; store round-trip, missing file → empty document,
   corrupt file → moved aside + empty document + `RecoveredFrom`, atomic replace; launcher start-info per kind,
   missing target, refused URL scheme, start failure → `Failed`.

Acceptance criteria:
- All behaviour above covered by tests; Core has no Avalonia reference; 0 warnings; suite green.

### PHASE03 — Category pages & groups UI

**Status:** DONE

Steps:
1. `ILaunchpadSession` (load at startup, per-category snapshot save, InfoBar on load recovery / save failure).
2. `CategoryPageViewModel` + three concrete page ViewModels; `GroupViewModel`; `CategoryPageView` replacing the
   placeholders: page header (category title + "New group" button), group cards (grip, chevron collapse, name,
   count, rename, delete), empty states (no groups; empty group).
3. Commands: new group, rename group, delete group (confirmation), toggle collapse, move group up / down — each
   persisted through the session.
4. Group name dialog with inline validation (required, ≤ 100, unique per category).
5. Tests: page ViewModel commands against a fake store and fake dialogs (create / rename / delete / move /
   collapse persisted; duplicate name refused); headless render of a page with groups.

Acceptance criteria:
- Groups can be created, renamed, collapsed, reordered via menu, deleted (after confirmation), and survive a restart.
- 0 warnings; suite green.

### PHASE04 — Shortcut tiles, editing & launching

**Status:** TODO

Steps:
1. `ShortcutViewModel`; tiles in each group card (wrap layout; Phosphor icon per kind; name; target subtitle;
   tooltip with the full target); empty-group hint.
2. Add / edit shortcut dialog: name, target (+ Browse… file picker for applications, folder picker for
   directories), arguments (applications only), inline validation, URL normalised on save.
3. Click / Enter launches through `IShortcutLauncher`; failures → InfoBar error.
4. Tile context menu: Open, Edit…, Copy target, Show in folder (applications / directories), Move up, Move down,
   Delete… (confirmation).
5. Tests: add / edit / delete / move persisted; validation per kind; launch success and each failure path reported.

Acceptance criteria:
- Shortcuts of all three kinds can be added, edited, launched, reordered via menu and deleted; changes persist.
- 0 warnings; suite green.

### PHASE05 — Drag-and-drop reordering

**Status:** TODO

Steps:
1. `DropPlacement` pure calculator (before / after from pointer position and target bounds, horizontal for tiles,
   vertical for groups) + ViewModel move operations (`MoveGroup(id, targetId, placement)`,
   `MoveShortcut(id, targetGroupId, targetShortcutId?, placement)`), cross-group within the same category only.
2. View wiring: drag sources (group grip / header, tiles) with a 4 px threshold via tunnelling handlers;
   `DragOver` effects and the accent insertion indicator; drop onto an empty group or a group's empty area
   appends; drops from another category or foreign data are refused.
3. Every completed drop persists through the session.
4. Tests: `DropPlacement` theories; ViewModel move operations (same group, cross group, to empty group, no-op
   drop on itself, foreign category refused) persisted.

Acceptance criteria:
- Groups and shortcuts can be reordered by drag-and-drop, shortcuts can be moved between groups of the same
  category, an insertion indicator shows where the drop lands, and the order survives a restart.
- 0 warnings; suite green.

## Out of scope

- Custom per-shortcut icons, extracted executable icons, URL favicons.
- Dragging files / folders / links in from the OS to create shortcuts; import / export.
- Search / filter, global hotkey, tray icon, single-instance enforcement, launch at login.
- Runtime theme switching and a settings page.
- Installers / MSI / packaging and release documentation (dotnet-release).
- Custom URL schemes (`mailto:`, `ssh:`, app protocols) — only `http` / `https`.

## Decisions taken autonomously

| Question              | Chosen                                                                                                        | Why                                                          | Alternatives rejected                                             |
|-----------------------|---------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------|-------------------------------------------------------------------|
| Solution layout       | Core + Desktop projects, `.UnitTests` per project                                                             | House pattern of the sibling apps; logic testable without UI | Single Desktop project (logic only testable through the UI layer) |
| Package versions      | GitClient's set (Avalonia 12.1.1, Enigma.Avalonia.Desktop 1.2.0, Enigma.Icons.Avalonia 1.0.0, xunit.v3 4.0.1) | Validated together in a sibling, all cached locally          | Avalonia 12.1.2/12.1.3 (not the set Enigma.Avalonia.Desktop pins) |
| Shell navigation      | `NavigationView` rail with the three categories                                                               | The library's native shell; categories are fixed             | Tabs; a single page with three sections                           |
| Group presentation    | Collapsible cards stacked vertically, collapse state persisted                                                | Modern, scales with many groups                              | Flat list with headers; a group tree                              |
| Shortcut presentation | Button tiles in a wrap layout, click launches, context menu for the rest                                      | Launcher semantics; keyboard-accessible                      | List rows; double-click to launch                                 |
| Application fields    | Name, path, optional arguments; working dir = exe folder                                                      | Minimal useful set                                           | Working-directory field, env vars, run-as-admin                   |
| URL schemes           | http / https only, `https://` auto-prefix                                                                     | Keeps URLs from acting as arbitrary scheme launchers         | Any absolute URI                                                  |
| Path validation       | Absolute + must exist at save time                                                                            | Catches typos; launch re-checks                              | Accept any string; warn only                                      |
| Drag-and-drop scope   | Groups within a category; shortcuts within and across groups of one category                                  | "Regroup things" needs cross-group moves; kinds never mix    | Within-group only; cross-category moves                           |
| Non-mouse reordering  | Move up / Move down in context menus                                                                          | Accessibility; reuses move logic                             | Keyboard shortcuts only; none                                     |
| Persistence           | Versioned JSON in per-user ApplicationData, atomic write, saved on every change                               | Durable, readable, migratable                                | SQLite; save on exit only                                         |
| Corrupt data file     | Move aside + start empty + InfoBar warning                                                                    | Never silently lose or overwrite user data                   | Refuse to start; overwrite                                        |
| Deletion              | Confirmation dialog for groups and shortcuts                                                                  | Destructive with no undo                                     | Undo stack; no confirmation                                       |
| Theme                 | Dark start variant, no runtime switch                                                                         | House default; Enigma ships Dark/Light only                  | Follow OS; toggle button + persisted setting                      |
| App icon              | Generated placeholder rocket `.ico`                                                                           | House rule requires one; art stays user-owned                | No icon; borrowing a sibling's icon                               |
| Multi-instance        | Not prevented; last write wins (documented)                                                                   | Not asked; atomic writes prevent corruption                  | Single-instance mutex / IPC                                       |
| Logging               | Host default `ILogger` providers, Warning on failures                                                         | Enough for a desktop utility                                 | File sink (NLog)                                                  |
| Tests                 | xUnit v3 MTP; Core + Desktop suites; headless fixture for UI smoke tests                                      | House testing conventions                                    | UI automation                                                     |
