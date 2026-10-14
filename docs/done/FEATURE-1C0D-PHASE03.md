# FEATURE-1C0D-PHASE03 — Open workspace, mixed groups

## Summary

A workspace now opens in place of the list and shows its groups of every kind side by side. The Applications,
Directories and URLs pages are gone: the rail holds **Workspaces** only, with Profiles and Settings at its foot. The
temporary bridge over "the first workspace" is removed from Core and the session.

- **Open workspace (`WorkspacePageViewModel`)** — grown from `CategoryPageViewModel` (renamed by `git mv`; the three
  subclasses are deleted):
  - shows one workspace's title, description and glyph, and its groups;
  - persists through `ILaunchpadSession.UpdateGroups(workspaceId, groups)`;
  - **New group** takes a kind (`AsyncRelayCommand<ShortcutKind>`); its prompt reads "New applications group" /
    "New folders group" / "New links group";
  - a group name is checked only against groups of the same kind, for both new and rename;
  - **Move to workspace…**: a choice of the other workspaces, then `ILaunchpadSession.MoveGroup`. A target with a
    group of the same name and kind is refused with a warning naming both. With no other workspace, the user is told
    to create one;
  - **Back** (button, Alt+Left) asks the Workspaces page to show the list again;
  - **drag and drop:** groups reorder whatever their kind; a tile goes only to a group of its own kind, judged from
    the tile itself rather than from what the drag payload claims.
- **Groups and tiles:**
  - `GroupViewModel` has `Kind`, `KindGlyph`, `KindTitle` (tooltip), `EmptyText` ("No folders in this group
    yet…") and `MoveToWorkspaceCommand`;
  - `ShortcutViewModel.Kind` is its group's, and every kind-dependent flag (glyph, subtitle, run with arguments,
    terminal, copy, reveal, icon lookup) follows it;
  - `GroupKindOption` names the kinds ("Applications", "an applications group").
- **Drill-in:**
  - `WorkspacesPageViewModel` holds `OpenWorkspace` / `IsWorkspaceOpen`. `Open(card)` builds the page; `Close()`
    goes back and re-reads the cards.
  - On `WorkspacesChanged` and `ActiveProfileChanged` it re-reads the cards and has the open workspace re-read
    itself, or closes it when the workspace is gone — after a profile switch or a deletion.
  - The open page never subscribes to the session, so it leaves nothing attached when it goes.
  - Cards are now `Button.workspaceCard`s (click or Enter opens) with **Open** first in their menu.
  - The page view swaps the list and a `ContentControl` templated to `WorkspacePageView`.
- **Session:**
  - `GroupsOf` / `Update` removed.
  - `UpdateGroups` saves without announcing (the open page is the source).
  - `MoveGroup` is one change: refuses an unknown group or workspace, the same workspace, or a name and kind clash,
    and raises `WorkspacesChanged`.
- **Core:** `Profile.GroupsOf` / `Profile.With(kind, groups)` and the `LaunchpadData` twins are removed.
- **Dialog:** `ILaunchpadDialogs.ChooseWorkspaceAsync` with `WorkspaceChoiceViewModel` / `WorkspaceChoiceView`
  (nothing chosen at first; confirm follows the choice).
- **Wording:**
  - the Profiles page reads "Each profile keeps its own workspaces.";
  - comments that pointed at the category pages now point at workspaces;
  - README: the workspaces paragraph, a new paragraph on an open workspace (kinds, New group, Move to workspace…,
    back), and the profiles paragraph.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/WorkspaceChoiceViewModel.cs`
- `src/Enigma.Launchpad.Desktop/Views/Dialogs/WorkspaceChoiceView.axaml` (+ `.cs`)
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/WorkspaceFixtures.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/WorkspacePageKindsTests.cs`

Renamed:
- `ViewModels/Pages/CategoryPageViewModel.cs` → `WorkspacePageViewModel.cs`
- `Views/Pages/CategoryPageView.axaml(.cs)` → `WorkspacePageView.axaml(.cs)`
- tests `CategoryPage{Groups,Shortcuts,DragDrop,ViewDragDrop,View}Tests` → `WorkspacePage…Tests`

Deleted:
- `ViewModels/Pages/{Applications,Directories,Urls}PageViewModel.cs`

Modified:
- Core: `Shortcuts/{Profile,LaunchpadData}.cs` (bridge removed).
- Desktop:
  - `Services/{LaunchpadSession,LaunchpadDialogs}.cs`
  - `ViewModels/Pages/{GroupViewModel,ShortcutViewModel,WorkspacesPageViewModel,WorkspaceItemViewModel,ProfilesPageViewModel}.cs`
  - `ViewModels/MainWindowViewModel.cs`, `DependencyInjection/ServiceCollectionExtensions.cs`
  - `DragAndDrop/DragPayload.cs` (docs)
  - `Views/Pages/{WorkspacesPageView,ProfilesPageView,SettingsPageView}.axaml`, `Views/MainWindow.axaml.cs`
  - `Themes/Styles.axaml`
- `README.md`
- Tests:
  - harnesses: `Infrastructure/{Pages,WindowHarness,ScriptedDialogs}.cs`. `PageHarness` opens the first workspace;
    `LoadedPageAsync` opens it through the Workspaces page.
  - `LaunchpadSessionTests`: `UpdateGroups` and `MoveGroup`.
  - `ProfileSwitchingTests`: through the Workspaces page.
  - `MainWindowTests`, `CompositionRootTests`, `TileActionsViewTests`, `ProfileSwitcherViewTests`,
    `WorkspacesPageViewModelTests` (open, back, deleted, moved out, retitled).
  - `WorkspacesPageViewTests`: drill-in, empty workspace, card buttons, Open in the menu.
  - The ported page tests.
  - Core `ProfileTests` / `LaunchpadDataTests`: bridge tests removed with the bridge.

## Deviations & follow-ups

- **Test fixtures keep the bridge's shorthand.** `WorkspaceFixtures` gives `GroupsOf(kind)` / `With(kind, groups)`
  / `Update(kind, groups)` on `Profile`, `LaunchpadData` and `ILaunchpadSession`, in the test project only, so the
  hundreds of fixture lines building "a workspace with these application groups" stay short. The application itself
  always names its workspace.
- **Kind menu as explicit items.** The New group menu and the empty workspace's buttons list the three kinds in XAML,
  rather than through a templated `MenuFlyout`, which is fragile to bind. FEATURE-1FA9 adds the fourth kind there.
- **Drop rules follow the tile, not the payload.** A drag payload's kind no longer decides anything. The source
  tile's own kind is compared with the target group's, so a forged payload cannot carry a tile into a group of
  another kind. Two drag tests were rewritten to say so.
- **Tests replaced, not weakened:**
  - `OnAppearing_LoadsTheGroupsOfItsOwnCategory_Once` and `APageWithoutGroups_IsEmpty_OnlyOnceLoaded` tested page
    loading and the single category, neither of which exists any more. They became tests of the open workspace's
    mixed groups and of an empty workspace.
  - The "foreign data" view test's second payload became an unknown shortcut, since a payload's claimed kind is no
    longer a reason to refuse.
- **Fix budget: 2 cycles, plus one void run.**
  1. Twelve tests still written against the old pages: they relied on category pages following profile switches,
     drops gated by payload kind, `Border` cards and a menu without Open.
  2. Two mistakes in new tests: `Single()` where the applications case also holds the starting group, and `Click`
     raised on buttons, which does not run their commands.
  - One run in between re-ran unchanged code, because the edit had failed to apply; it is not counted as a cycle.
- Follow-ups (plan: out of scope): moving a single shortcut to another workspace; a search inside an open workspace;
  remembering the open workspace across restarts.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx`: succeeded, 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx`: 860 total, 859 passed, 0 failed, 1 skipped (the Unix-only
  resolver test, on this Windows host).
- New tests:
  - kinds: new group per kind (prompt, glyph, empty text, saved kind), name checks per kind for new and rename,
    tiles by kind, add by kind;
  - move to workspace (moved, clash, cancelled, no other workspace);
  - the choice dialog;
  - session `UpdateGroups` / `MoveGroup`;
  - open, back, deleted elsewhere, moved out, retitled;
  - headless drill-in and empty workspace.

## Decisions taken at build time

| Question                                  | Chosen                                                            | Why                                                                          |
|-------------------------------------------|-------------------------------------------------------------------|------------------------------------------------------------------------------|
| Who keeps the open workspace current      | The Workspaces page, telling it to re-read                        | One subscriber, no handlers left on the session by closed workspaces         |
| Moving a group                            | One session change (`MoveGroup`), checked first by the page       | A two-step remove-then-add could lose the group if the second step failed    |
| Saving the open workspace                 | `UpdateGroups` announces nothing                                  | Re-reading on its own edits would rebuild every tile and lose drag state     |
| A drop between kinds                      | Judged on the tile's real kind                                    | The payload is data from a drag, not a fact                                  |
| Back                                      | A button at the top left, plus Alt+Left                           | The rail cannot re-select itself; the usual back shortcut works too          |
| Cards                                     | Buttons that open, with the menu button inside                    | Same pattern as tiles and their action buttons                               |
| An open workspace across rail navigation  | Stays open (the page ViewModel is a singleton)                    | Leaving to Settings and coming back finds the user where they were           |
