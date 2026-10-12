# FEATURE-4D2E — Profiles

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-4d2e-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-12-settings-and-profiles

## Objective

Introduce **profiles**: each profile has its own applications, directories and URLs (its own groups and
shortcuts). A **combobox** in a top bar selects the active profile anywhere in the app, and a **Profiles** page
creates, duplicates, renames, reorders, deletes and activates profiles. The active profile is remembered across
restarts; existing data becomes the first profile.

## Context & constraints

- `launchpad.json` is format **v1**: `{ version, applications, directories, urls }`. The store refuses any
  other version by moving the file aside — a v2 file is therefore safe from older builds (moved aside, not
  overwritten).
- `ILaunchpadSession` loads once, hands each category its groups (`GroupsOf` / `Update`), and coalesces saves;
  `CategoryPageViewModel`s are singletons that load their groups once in `OnAppearingAsync`.
- `IShortcutValidator.ValidateGroupName` is the model for name rules (required, trimmed, ≤ 100, unique ignoring
  case).
- FEATURE-068D (settings) is built first in this run; its "Settings" footer item stays last.

## Design

### Document format v2

```json
{
  "version": 2,
  "activeProfileId": "…",
  "profiles": [
    { "id": "…", "name": "Default", "applications": [], "directories": [], "urls": [] }
  ]
}
```

- `Profile` (record) — `Guid Id`, `string Name`, `Applications`, `Directories`, `Urls`; `GroupsOf(kind)` /
  `With(kind, groups)`; `Create(name)`; `Duplicate(name)` (fresh ids for the profile, its groups and shortcuts).
- `LaunchpadData` (record) — `int Version` (= 2), `Guid ActiveProfileId`, `IReadOnlyList<Profile> Profiles`;
  `ActiveProfile`; `GroupsOf(kind)` / `With(kind, groups)` act on the **active** profile (existing callers
  unchanged); `AddProfile`, `RenameProfile`, `RemoveProfile` (refuses the last; removing the active one activates
  its neighbour), `MoveProfile`, `WithActiveProfile`; `Empty` = one empty "Default" profile.
- Store: reads v1 (migrated into one "Default" profile holding the old groups) and v2; anything else is moved
  aside as today. Normalised on load: no profile → one "Default"; unknown active id → the first profile.
- `IShortcutValidator.ValidateProfileName(name, otherProfileNames)`.

### Session & switching

- `ILaunchpadSession` gains `Profiles`, `ActiveProfile`, `SwitchProfile`, `AddProfile`, `DuplicateProfile`,
  `RenameProfile`, `DeleteProfile`, `MoveProfile`, and two events raised on the caller's thread after the change:
  `ProfilesChanged` (list, names, order or active changed) and `ActiveProfileChanged`. Every change is saved like
  the category updates.
- The launchpad is loaded **before the window is built** (as the settings are), so the switcher and pages start
  on the remembered profile.
- `CategoryPageViewModel` rebuilds its groups from the session on `ActiveProfileChanged` (when loaded).
- `ProfileSwitcherViewModel` — `Profiles` (id + name options), two-way `SelectedProfile` that switches; refreshes
  on `ProfilesChanged` without re-entering the switch.
- `MainWindow` — a slim top bar over the page area, right-aligned: user icon, the profile `ComboBox`, and a
  "Manage profiles" ghost button that opens the Profiles page.

### Profiles page

- Footer navigation item "Profiles" (Phosphor `UsersThree`), placed before "Settings".
- Header in the category-page style with a "New profile" button; one card per profile: name, an "Active" badge,
  a summary ("3 groups · 12 shortcuts"), a "Use" button (not on the active one), and a menu: Rename…,
  Duplicate…, Move up, Move down, Delete… (disabled when it is the only profile).
- New / Rename / Duplicate prompt through the existing name dialog with profile-name validation; Delete asks the
  existing red confirmation, naming what is lost.

## Phases

### PHASE01 — Profile document model

**Status:** TODO

Steps:
1. `Profile` record; `LaunchpadData` v2 with the profile operations; `Empty` as one "Default" profile.
2. `JsonLaunchpadStore`: version probe, v1 → v2 migration, v2 parse, well-formedness (no null profile / group /
   shortcut), normalisation (no profile, unknown active id).
3. `IShortcutValidator.ValidateProfileName`.
4. Tests: profile operations (add, duplicate with fresh ids, rename, remove incl. last-refused and active
   fallback, move, switch, `GroupsOf` / `With` on the active profile); store round-trip v2; v1 file migrated;
   v2 JSON shape; unreadable cases (incl. v3, v0, null profile); normalisation; profile-name validation theory.
   Existing tests adapted to v2 where they assert the format.

Acceptance criteria:
- A v1 file loads as one "Default" profile with all its groups and is written back as v2; the app behaves exactly
  as before on that profile.
- 0 warnings; suite green.

### PHASE02 — Session profiles & switcher

**Status:** TODO

Steps:
1. Session profile API and events; saves through the existing chain.
2. Load the launchpad before the window is built (`App` + `WindowHarness`).
3. `CategoryPageViewModel` reloads on `ActiveProfileChanged`.
4. `ProfileSwitcherViewModel` + top bar with the combobox in `MainWindow`; the "Manage profiles" button is
   added in PHASE03 together with the page it opens.
5. Tests: session operations persisted and events raised; switching reloads a loaded page and leaves the other
   profile's data intact; switcher lists profiles, switches on selection, follows renames without switching;
   headless — the combobox shows the active profile and selecting another changes the page's groups.

Acceptance criteria:
- Selecting a profile in the combobox shows that profile's groups on every category page; edits land in the active
  profile only; the choice survives a restart.
- 0 warnings; suite green.

### PHASE03 — Profiles management page

**Status:** TODO

Steps:
1. `ProfilesPageViewModel` / `ProfileItemViewModel` / `ProfilesPageView`; footer item "Profiles" before
   "Settings"; "Manage profiles" button in the top bar navigates there.
2. Commands: new, use, rename, duplicate, move up / down, delete (confirmation; last profile protected).
3. Tests: each command persisted through the session with validation (duplicate / empty names refused); delete
   of the active profile activates its neighbour; delete disabled for the last; headless — the page lists the
   profiles with the active badge, and the manage button opens it.
4. README: describe profiles and the new file format.

Acceptance criteria:
- Profiles can be created, duplicated, renamed, reordered, activated and deleted (after confirmation, never the
  last one) from the Profiles page; the combobox follows every change.
- 0 warnings; suite green.

## Out of scope

- Per-profile settings (theme etc. stay global), profile icons / colours, keyboard shortcut to switch.
- Moving or copying single groups / shortcuts between profiles.
- Import / export of profiles; a v1 backup file at migration time.

## Decisions taken autonomously

| Question                           | Chosen                                                                | Why                                                                          | Alternatives rejected                                   |
|------------------------------------|-----------------------------------------------------------------------|------------------------------------------------------------------------------|---------------------------------------------------------|
| Storage of profiles                | `launchpad.json` v2 with a `profiles` array                           | One atomic file; profile operations never span files                         | One file per profile                                    |
| Remembering the active profile     | `activeProfileId` in `launchpad.json`                                 | Keeps the reference in the same file as its target                           | In `settings.json` (GitClient-style)                     |
| Existing v1 data                   | Migrated on load into a "Default" profile, written as v2 on next save | Nothing for the user to do; nothing lost                                     | Start empty; separate import                            |
| Backup at migration                | None                                                                  | Pre-release (0.1.0); older builds move a v2 file aside rather than overwrite | Copy `launchpad.v1.json` before the first v2 write      |
| Hand-edited oddities               | No profile → "Default"; unknown active id → first profile             | Tolerant where nothing is lost                                               | Treat as unreadable and move aside                      |
| Combobox placement                 | Slim top bar over the page area, right-aligned                        | Global and always visible; the 90 px rail cannot hold a combobox             | Rail logo slot; each category page header               |
| Management page placement          | Footer item "Profiles" before "Settings"                              | Library convention for secondary pages                                       | A section of the Settings page                          |
| Profile operations                 | New, duplicate, rename, move up / down, delete, use                   | Covers the life cycle; reordering sets the combobox order                    | Drag-and-drop reordering; no duplicate                  |
| Duplicate identities               | Fresh ids for the profile, groups and shortcuts                       | Ids stay unique across the document                                          | Shared ids                                              |
| New profile activation             | Not automatic                                                         | Switching stays explicit and visible                                         | Switch to the new profile at once                       |
| Last profile                       | Cannot be deleted (command disabled)                                  | The app always needs an active profile                                       | Delete and recreate an empty "Default"                  |
| Deleting the active profile        | Activates the next profile (else the previous)                        | Predictable, no extra question                                               | Ask which one; always the first                         |
| Profile name rules                 | Required, trimmed, ≤ 100, unique ignoring case                        | Same rules as group names                                                    | Free text                                               |
| When the launchpad is loaded       | Before the window is built                                            | Switcher and pages start on the right profile; no cross-thread event         | Lazily on first page                                    |
| Page refresh on switch             | Pages rebuild their groups from the session on `ActiveProfileChanged` | Singleton pages keep working; no re-navigation                               | Re-create the page ViewModels                           |
