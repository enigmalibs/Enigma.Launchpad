# FEATURE-5B69-PHASE03 — Category pages & groups UI

**Item:** FEATURE-5B69 — Launchpad shortcut manager
**Phase:** PHASE03 — Category pages & groups UI
**Branch:** `feature/feature-5b69-phase03-groups-ui`
**Run:** vibe/2026-10-12-launchpad-shortcut-manager

## Summary

The three category pages now show and manage their groups, and every change is saved.

- **`ILaunchpadSession` / `LaunchpadSession`** (singleton) is the document the running app works on.
  - `LoadAsync` reads the store once (cached task).
  - `GroupsOf(kind)` returns a category's groups.
  - `Update(kind, groups)` swaps in a category's snapshot and saves in the background.
  - `FlushAsync` waits for pending saves.
  - **Ordered, coalesced saves:** saves are chained on the thread pool, and each writes the *latest*
    document. Changes made while a save runs fold into one more save, so the file never goes back in
    time.
  - **Read-only after a failed load:** if the file exists but cannot be read (I/O, access), nothing is
    saved for the rest of the run, so a transient error never replaces the user's shortcuts with an
    empty file.
  - A moved-aside unreadable file is reported as a warning.
  - Save failures are logged and reported; the next save still runs.
- **`IUserNotifications` / `InfoBarNotifications`**: warnings and errors go to the window's InfoBar,
  posted to the UI thread so the thread-pool save chain can report safely.
- **`ILaunchpadDialogs` / `LaunchpadDialogs`**:
  - `PromptNameAsync` shows a name form (`NameDialogView` + `NameDialogViewModel`). The field is
    focused with its text selected. It validates as you type; the error appears only after the first
    edit, and the confirm button is enabled only for an accepted name.
  - `ConfirmDeleteAsync` asks with the confirm button in red (the `danger` class, added and removed
    around the question) and Cancel as the default. Only the Primary result counts as yes.
- **`CategoryPageViewModel`** (now `INavigationViewModel`) holds the category's live state:
  - Groups are loaded on first appearance; `IsLoaded` and `IsEmpty` drive the view.
  - Commands: `NewGroupCommand` (added at the end, expanded), and per group rename, delete (the
    confirmation names the number of shortcuts lost), move up/down (disabled at the edges) and
    fold/unfold.
  - Each change calls `Persist()`, which hands the session a fresh snapshot of the whole category.
  - The three concrete page ViewModels now pass their `ShortcutKind` and the services.
- **`GroupViewModel`** carries the name, `IsExpanded` (saved on change, but not while it is being
  built), `Shortcuts` (an `ObservableCollection<ShortcutViewModel>`), `Count` / `HasShortcuts`, and
  commands that forward to the page. **`ShortcutViewModel`** is a thin wrapper that keeps a group's
  shortcuts intact through saves; PHASE04 builds the tiles on it.
- **`CategoryPageView`**:
  - The header has the category icon tile, title and description, plus an accent "New group" button.
  - Each group is a card. Its header is a flat fold toggle (caret, name, count badge) with a ghost "⋯"
    button opening a menu: Rename…, Move up, Move down, Delete…, each with a Phosphor icon.
  - A group with no shortcuts shows a hint.
  - An empty category shows a centred invitation (icon, "No groups yet", a line of help, "Create a
    group") in place of the header button.
- **Styles:** card, count badge, flat group-header toggle, ghost button, empty states, form label and
  error, wrapping dialog text, and the red confirm button.
- **App:** the dialog, overlay and InfoBar hosts are registered *before* the main ViewModel is built,
  because building it navigates to the first page, which loads data and may already need the InfoBar.
  At exit the app waits for the session's last save before stopping the host.
- **README:** the intro now mentions groups, and a "Your data" section gives the file's location on
  each platform and the moved-aside behaviour.

## Files / modules touched

**Created**

- `src/Enigma.Launchpad.Desktop/Services/LaunchpadSession.cs`, `LaunchpadDialogs.cs`, `UserNotifications.cs`
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/NameDialogViewModel.cs`
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/GroupViewModel.cs`, `ShortcutViewModel.cs`
- `src/Enigma.Launchpad.Desktop/Views/Dialogs/NameDialogView.axaml(.cs)`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FakeLaunchpadStore.cs`, `RecordingNotifications.cs`,
  `ScriptedDialogs.cs`, `TempDirectory.cs`, `Pages.cs` (`PageHarness`), `WindowHarness.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/LaunchpadSessionTests.cs` (7), `CategoryPageGroupsTests.cs` (17),
  `NameDialogViewModelTests.cs` (3), `CategoryPageViewTests.cs` (5 headless)
- `docs/done/FEATURE-5B69-PHASE03.md`

**Modified**

- `src/Enigma.Launchpad.Desktop/App.axaml.cs`: host registration order and the flush at exit.
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs`: the three new services.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/CategoryPageViewModel.cs` and the three concrete page ViewModels.
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml`, `Themes/Styles.axaml`
- `tests/Enigma.Launchpad.Desktop.UnitTests/CompositionRootTests.cs`: the user's data is redirected to a temporary
  directory in every test container, and there are new service checks.
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/HeadlessAvaloniaFixture.cs`: installs an
  `AvaloniaSynchronizationContext` on the headless UI thread.
- `tests/Enigma.Launchpad.Desktop.UnitTests/MainWindowTests.cs`: now on `WindowHarness`.
- `README.md`, `docs/roadmap.md`, `docs/plan/FEATURE-5B69.md`

## Decisions taken at build time

| Question                               | Chosen                                                              | Why                                                                                   | Alternatives rejected                                                                |
|----------------------------------------|---------------------------------------------------------------------|---------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------|
| When the document is loaded            | On the first page's `OnAppearingAsync`, awaited, cached             | Fully asynchronous, with no blocking of the UI thread at startup                      | Blocking `LoadAsync().GetResult()` in `App` before the window (GitClient's approach) |
| Save scheduling                        | Thread-pool chain writing the latest snapshot                       | Ordered, coalesced, and flushable at exit without deadlocking the UI thread           | Awaiting every save in each command; a FIFO queue of snapshots                       |
| `ConfigureAwait(false)` in the session | Used, as an explicit, documented exception                          | The session is thread-agnostic (state under a lock, notifications marshal themselves) | Resuming on the UI thread, which would make the exit flush deadlock                  |
| Error reporting seam                   | `IUserNotifications` over the InfoBar, posted to the dispatcher     | Callable from any thread; replaceable in tests                                        | Injecting `IInfoBarService` into the session and ViewModels directly                 |
| Group commands                         | On `GroupViewModel`, forwarding to the page                         | Simple `{Binding RenameCommand}` in the card template; the page keeps the logic       | Page-level commands bound through `$parent[...]` casts                               |
| Group actions UI                       | One "⋯" menu per card                                               | Uncluttered cards; room for PHASE04's add-shortcut button                             | A row of icon buttons per card                                                       |
| Fold control                           | The whole header is the toggle                                      | Bigger hit target; the expected behaviour of a card header                            | A small chevron button only                                                          |
| Drag grip                              | Not shown yet                                                       | A grip that does not drag would mislead; PHASE05 adds it with the drag                | Showing it now                                                                       |
| Test data isolation                    | Every test container redirects `IAppPaths` to a temporary directory | No test can ever touch the developer's own `launchpad.json`                           | Relying on tests not to load                                                         |

## Deviations & follow-ups

- Shortcut tiles are a plain name list for now; PHASE04 replaces them, as the plan sequences it.
- Visual check: the groups page and the empty state were rendered headlessly to PNG with a throwaway test
  (not committed). They match the intended design: cards, count badge, ghost menu button, centred invitation.
- No real-app smoke run this phase, so as not to create a data folder in the developer's profile. The real
  container and window are covered by the headless tests over a temporary directory.

## Build / test evidence

- `dotnet build Enigma.Launchpad.slnx`: **0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.Launchpad.slnx`: **140 passed, 0 failed, 0 skipped** (Core 92, Desktop 48).
- One fix cycle: the headless dialog test looked for the primary button before the dialog's template had been
  laid out. It now forces a layout pass (`Window.UpdateLayout()`) first. The `PART_PrimaryButton` name was
  confirmed by dumping the template.
