# FEATURE-1C0D-PHASE01 — Workspace model & storage

## Summary

The data model moves from "profile → three categories → groups" to "profile → workspaces → groups of one kind". The
file format, the export format and the export/import selection dialog follow. The three category pages keep working
unchanged, through a temporary bridge over each profile's first workspace that PHASE03 removes.

- **Model (Core):**
  - `Workspace(Id, Title, Groups, Description?, Icon?)` — `Icon` is a glyph name, resolved by the Desktop. Constants
    `DefaultTitle` ("Shortcuts"), `MigratedDescription`, `MigratedIcon`; `Workspace.Create`.
  - `ShortcutGroup` gains `Kind` (third positional parameter).
  - `Profile(Id, Name, Workspaces)`:
    - workspace operations: find, add (with index), replace, remove, can-move/move;
    - `Duplicate` and the static `Copy` (new identities throughout);
    - `CountWorkspaces` / `CountGroups` / `CountShortcuts`;
    - `Import`: a workspace merges by title, ignoring case, keeping the target's description and icon; groups
      merge by name **and kind**; duplicate shortcuts are skipped.
  - `LaunchpadData`: `CurrentVersion = 3`, and `WithProfile(profile)` (public, replacing the private `Replace`).
  - **Temporary bridge** — `Profile.GroupsOf(kind)` / `Profile.With(kind, groups)`, and their `LaunchpadData`
    twins, act on the profile's first workspace. `With` creates a "Shortcuts" workspace when there is none, keeps
    the other kinds' groups in place, and leaves an already-typed group untouched. Documented as removed by PHASE03.
- **Storage (`JsonLaunchpadStore`), version 3:**
  - Kinds are written as camelCase strings, and integers are refused.
  - Version 2 is read through `LaunchpadDataV2` / `ProfileV2` / `GroupV2`, and version 1 through the same path into
    a *Default* profile. Each profile with groups gets one "Shortcuts" workspace (glyph `SquaresFour`, "Your
    shortcuts from before workspaces."), with apps, then directories, then URLs, all identities kept. A profile
    without groups gets none.
  - An older file is copied once to `launchpad.v<N>.backup.json` (never replaced, failures only logged).
  - The well-formedness check covers workspaces.
  - New internal `TryParse(json, out data, out version, out reason)` overload.
- **Exchange, version 2:**
  - Writes workspaces (title, description, icon, groups with `kind`).
  - Reads versions 1 and 2; version 1 goes into one "Shortcuts" workspace.
  - Skipped and counted with everything in them: a workspace with an invalid title or description, and a group of
    unknown or missing kind (version 2).
  - An icon name that is not a plain identifier is dropped.
- **Validation:**
  - `ValidateWorkspaceTitle` (required, ≤ 100, unique ignoring case) and `ValidateWorkspaceDescription`.
  - Cross-kind target messages now point at "a folders group" / "an applications group".
- **Desktop:**
  - `Glyphs` (`OfKind`, `OfWorkspace`, `DefaultWorkspace`).
  - The selection tree is workspace → group → shortcut (`SelectionNodeViewModel.ForWorkspace`,
    `ShortcutSelectionViewModel.Workspaces`).
  - Profile summary "N workspaces · M shortcuts" / "No workspaces yet"; export offered for a profile with a
    workspace.
  - Import message and `Added(workspaces, groups, shortcuts)`.
  - The category page types its new groups; session docs describe the bridge.
- **README:** export/import paragraph, data paragraph (migration and backup), export-file section (version 2
  example, version 1 still read).

## Files

Created:
- `src/Enigma.Launchpad.Core/Shortcuts/Workspace.cs`
- `src/Enigma.Launchpad.Desktop/ViewModels/Glyphs.cs`

Modified:
- Core:
  - `Shortcuts/{Profile,LaunchpadData,ShortcutGroup,Shortcut,ShortcutKind}.cs`
  - `Persistence/JsonLaunchpadStore.cs`
  - `Exchange/{ShortcutExchange,IShortcutExchange}.cs`
  - `Validation/{ShortcutValidator,IShortcutValidator}.cs`
- Desktop:
  - `ViewModels/Dialogs/{SelectionNodeViewModel,ShortcutSelectionViewModel}.cs`
  - `Views/Dialogs/ShortcutSelectionView.axaml`
  - `ViewModels/Pages/{ProfileItemViewModel,ProfilesPageViewModel,CategoryPageViewModel,GroupViewModel}.cs`
  - `Services/LaunchpadSession.cs` (docs)
- `README.md`
- Tests:
  - Rewritten: `ProfileTests`, `JsonLaunchpadStoreTests`, `ShortcutExchangeTests`, `ShortcutSelectionViewModelTests`.
  - Updated:
    - `LaunchpadDataTests`, `ShortcutValidatorTests`, `ShortcutSelectionViewTests`, `ProfileExportImportTests`,
      `ProfilesPageViewModelTests`, `LaunchpadSessionProfileTests`, `ProfileSwitchingTests`;
    - every test constructing a `ShortcutGroup` (kind added).

## Deviations & follow-ups

- **Bridge location.** The plan placed the temporary bridge in the session only. It lives in `Profile` /
  `LaunchpadData` (with the session delegating, as before), so the existing tests and the category pages compile
  unchanged. PHASE03 still removes all of it.
- **Removed test:** `LaunchpadDataTests.GroupsOf_RefusesAnUnknownKind`. The bridge filters groups by kind and no
  longer has categories to refuse; the bridge goes in PHASE03.
- **Strengthened, not weakened:** `ProfilesPageViewModelTests.OnAppearing_AgainLater_PicksUpEditsMadeOnTheCategoryPages`
  now adds a shortcut rather than an empty group, because the summary counts workspaces and shortcuts, not
  groups.
- **Import counts:** the success message counts new workspaces as well (`Added(workspaces, groups, shortcuts)`).
- Fix budget: 1 cycle. Eight tests still expected the "groups" summary wording, and one expected group identity
  through the bridge, which now keeps already-typed groups as they are.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx`: succeeded, 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx`: 780 total, 779 passed, 0 failed, 1 skipped (the Unix-only
  resolver test, on this Windows host).
- New coverage:
  - workspace operations;
  - bridge placement;
  - import merge by title and by name + kind;
  - v1/v2 migration, with identities kept and an empty profile left without a workspace;
  - backup written once, never replaced, and none for a v3 file;
  - unknown, missing and integer kinds refused;
  - v3 round trip;
  - export v2 shape;
  - v1 read into a workspace;
  - skipped workspaces and groups counted;
  - workspace title and description rules;
  - selection tree by workspace;
  - three-part import wording.

## Decisions taken at build time

| Question                                   | Chosen                                                         | Why                                                                          |
|--------------------------------------------|----------------------------------------------------------------|------------------------------------------------------------------------------|
| Kind in `launchpad.json`                   | camelCase string, integers refused                             | Readable by hand; a number would silently mean another kind if the enum grew |
| Workspace record shape                     | `(Id, Title, Groups, Description = null, Icon = null)`         | Optional fields stay optional in a hand-edited file                           |
| Workspace with an invalid description      | Skipped and counted on import                                  | Same rule as a shortcut whose description the form would refuse              |
| Backup failure                             | Logged, loading goes on                                        | The file itself is never touched by a load                                   |
| Glyph mapping                              | `Glyphs` in the Desktop; Core stores a name                    | Core stays free of UI packages                                               |
| Export/import counts                       | Workspaces counted too                                         | An import can now add whole workspaces                                       |
