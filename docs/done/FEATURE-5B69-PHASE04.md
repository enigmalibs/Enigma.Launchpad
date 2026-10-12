# FEATURE-5B69-PHASE04 — Shortcut tiles, editing & launching

**Item:** FEATURE-5B69 — Launchpad shortcut manager
**Phase:** PHASE04 — Shortcut tiles, editing & launching
**Branch:** `feature/feature-5b69-phase04-shortcuts-ui`
**Run:** vibe/2026-10-12-launchpad-shortcut-manager

## Summary

Groups now hold real shortcuts: tiles that open on click, a form to add and edit them, and a context menu
for everything else. Every change is saved.

- **Tiles** (`Button.tile`, 248 × 62, wrapping) show:
  - a kind icon on a tinted plate (`AppWindow`, `Folder`, `Globe`);
  - the name;
  - a subtitle: the path for applications and folders, the host for links;
  - a tooltip with the full target and any arguments.
  - Hovering frames the tile in the accent colour.
- Click or Enter opens the tile. Its context menu (right-click, or Shift+F10) offers Open, Edit…, Copy
  target, Show in folder (hidden for links), Move earlier / Move later (disabled at the ends), and
  Delete… (confirmed in red).
- **Group header** gains a ghost **+** that adds a shortcut. A new shortcut goes at the end, and a
  folded group unfolds to show it. The empty-group hint now says how to add one.
- **Shortcut form** (`ShortcutEditorViewModel` + `ShortcutEditorView`, through
  `ILaunchpadDialogs.EditShortcutAsync`):
  - Fields: Name; a target labelled Program / Folder / Address with a per-platform placeholder;
    Browse… (applications and folders only, starting from the current target); Arguments
    (applications only, with a hint).
  - Validation runs as you type through `IShortcutValidator`. Each field shows its error only once it
    has been edited, and Add / Save stays disabled until the form is valid.
  - A picked program or folder fills an empty Name (file name without extension, or folder name).
  - The result keeps the edited shortcut's identity, a normalised target (quotes stripped, `https://`
    added), and trimmed arguments, or none when blank.
- **`IShortcutActions` / `ShortcutActions`**: Open and Show in folder call `IShortcutLauncher` and turn
  any failure into an InfoBar error titled by outcome ("Shortcut target not found", "This shortcut cannot
  be opened", "The shortcut could not be opened") with the launcher's message. Copy target goes through
  `IClipboardService`.
- **Seams:** `IPathPicker` / `PathPicker` (over the Enigma file and folder dialog services, starting from
  the target's folder) and `IClipboardService` / `ClipboardService` (the main window's clipboard).
- **ViewModels:**
  - `ShortcutViewModel` now knows its page and its group (settable for PHASE05's cross-group moves) and
    exposes `Icon`, `Subtitle`, `ToolTip`, `CanReveal` and seven commands. An edit replaces its `Model`
    in place.
  - `GroupViewModel` gains `AddShortcutCommand` and refreshes its tiles' move commands when its list
    changes.
  - `CategoryPageViewModel` gains add, edit, delete, move, open, reveal and copy for shortcuts, and
    takes `IShortcutActions`.
- **README:** the intro now says how to add, open and manage shortcuts.

## Files / modules touched

**Created**

- `src/Enigma.Launchpad.Desktop/Services/ShortcutActions.cs`, `PathPicker.cs`, `ClipboardService.cs`
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/ShortcutEditorViewModel.cs`
- `src/Enigma.Launchpad.Desktop/Views/Dialogs/ShortcutEditorView.axaml(.cs)`
- `tests/Enigma.Launchpad.Desktop.UnitTests/CategoryPageShortcutsTests.cs` (12), `ShortcutEditorViewModelTests.cs` (13),
  `ShortcutActionsTests.cs` (6)
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FakePathPicker.cs`, `RecordingShortcutActions.cs`
- `docs/done/FEATURE-5B69-PHASE04.md`

**Modified**

- `src/Enigma.Launchpad.Desktop/Services/LaunchpadDialogs.cs`: `EditShortcutAsync`, and a shared
  `ShowFormAsync` for forms whose confirm button follows their validity.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ShortcutViewModel.cs`, `GroupViewModel.cs`,
  `CategoryPageViewModel.cs`, and the three concrete page ViewModels.
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml`: tiles, context menu, the group's **+**.
- `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml`: tile, tile icon, tile text and field hint styles.
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs`: three new services.
- `tests/…/Infrastructure/Pages.cs` (`PageHarness` builds any category's page), `ScriptedDialogs.cs`,
  `CategoryPageGroupsTests.cs`, `CategoryPageViewTests.cs` (+2 headless: tiles, link form),
  `CompositionRootTests.cs` (+3 services)
- `README.md`, `docs/roadmap.md`, `docs/plan/FEATURE-5B69.md`

## Decisions taken at build time

| Question                    | Chosen                                                                                                          | Why                                                                                          | Alternatives rejected                                                                     |
|-----------------------------|-----------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------|
| Page ViewModel dependencies | Launch, reveal and copy moved behind one `IShortcutActions`; shortcut validation and pickers live in the dialog | Keeps the page at four dependencies (session, validator, dialogs, actions) rather than seven | Injecting launcher, notifications, clipboard and pickers into every page                  |
| Picker seam                 | Own `IPathPicker` (paths in, path out)                                                                          | Tests need no fake `IStorageFile`; the Enigma string overloads already do the projection     | Faking `IFileDialogService` and its storage items                                         |
| Tile subtitle               | Host for links, full path otherwise (trimmed with an ellipsis); full target in the tooltip                      | A host reads at a glance; a path's start says where the program lives                        | File name only; the full URL                                                              |
| Move labels on tiles        | "Move earlier" / "Move later" with left/right arrows                                                            | Tiles wrap horizontally, so up/down would be misleading                                      | Reusing the groups' "Move up" / "Move down"                                               |
| Name from Browse            | Filled only when the name is still empty                                                                        | Saves typing without overwriting a name the user chose                                       | Always overwriting; never filling                                                         |
| Validation timing           | On every keystroke, as GitClient's forms do                                                                     | Immediate feedback; local paths are cheap to check                                           | Validating only on confirm (the dialog closes on confirm, so it could not show the error) |
| No-op edit                  | Saving with nothing changed writes nothing                                                                      | Avoids pointless disk writes                                                                 | Always saving after the form                                                              |

## Deviations & follow-ups

- Follow-up: target validation checks the file system on each keystroke. A UNC path to an unreachable
  server can block the UI while Windows times out. Debouncing or moving the check off the UI thread would
  remove this; it was left out as an edge case.
- Follow-up: an application shortcut's tile always shows the generic `AppWindow` icon. Extracting real
  program icons is platform-specific and out of scope, per the plan.
- Visual check: the tiles, the "Add application" form and the URLs page (host subtitles) were rendered
  headlessly to PNG with a throwaway test, not committed, and look as intended.

## Build / test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental`: **0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.Launchpad.slnx`: **176 passed, 0 failed, 0 skipped** (Core 92, Desktop 84).
- No fix cycles: the suite was green on its first run.
