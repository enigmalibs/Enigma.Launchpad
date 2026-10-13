# FEATURE-4D2E-PHASE03 — Profiles management page

## Summary

A **Profiles** item now sits at the foot of the rail, above Settings, and a "Manage profiles" button beside the
profile list opens the same page. The page, in the category pages' header style with a **New profile** button,
shows one card per profile: name, an **Active** badge and accent frame on the profile in use, a summary
("2 groups · 5 shortcuts", or "No groups yet"), a **Use** button on the others, and a menu with Rename…,
Duplicate…, Move up, Move down and Delete….

- New / Rename / Duplicate prompt through the existing name dialog with profile-name validation (required,
  ≤ 100 characters, unique ignoring case). Duplicate proposes a free name — "Work (copy)", "Work (copy 2)", … —
  and inserts the copy right after the original.
- Delete asks the red confirmation, naming what is lost and, for the profile in use, that another one takes over;
  it is disabled (and refused) for the only profile.
- The cards are rebuilt from the session whenever the profiles change — here or from the profile list — and each
  time the page appears, so the counts follow edits made on the category pages.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ProfilesPageViewModel.cs`
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ProfileItemViewModel.cs`
- `src/Enigma.Launchpad.Desktop/Views/Pages/ProfilesPageView.axaml` / `.axaml.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/ProfilesPageViewModelTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/ProfilesPageViewTests.cs`

Modified:
- `src/Enigma.Launchpad.Desktop/ViewModels/MainWindowViewModel.cs` — "Profiles" footer item before "Settings";
  `ManageProfilesCommand`.
- `src/Enigma.Launchpad.Desktop/Views/MainWindow.axaml` — "Manage profiles" button beside the list.
- `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml` — `Border.profileCard` (`.active`), `Border.activeBadge`.
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` — page and ViewModel.
- `tests/Enigma.Launchpad.Desktop.UnitTests/SettingsPageViewTests.cs` — Settings is now the last of two footer
  items; `CompositionRootTests.cs` — the new ViewModel.
- `README.md` — documentation sweep (see below).

## Deviations & follow-ups

- No functional deviation. The profile list is full-width like the category pages (not capped like Settings): the
  cards carry actions at their trailing edge, as group cards do.
- Fix budget: one cycle — the first build flagged two `xUnit2017` analyzer findings in the new view test
  (`Assert.True(collection.Contains(…))`), rewritten as `Assert.Contains` / `Assert.DoesNotContain`.
- Layout checked on headless screenshots in both variants (throwaway, not committed).
- Documentation sweep: `README.md` — a paragraph on profiles (switching, the Profiles page) and, under
  "Your data", that `launchpad.json` holds every profile and that an older file becomes a *Default* profile.
- Follow-up suggestion (out of scope): moving or copying a single group to another profile.
- Line endings: new files are LF, per `.gitattributes`.

## Decisions taken at build time

| Question                          | Chosen                                                          | Why                                                                     |
|-----------------------------------|-----------------------------------------------------------------|-------------------------------------------------------------------------|
| Card state                        | Immutable snapshots rebuilt on every profile change and appear  | No stale counts or flags; the session stays the only source of truth    |
| Badge wording                     | "Active", accent pill + accent card frame                        | Matches the plan; visible at a glance in both variants                  |
| Copy name proposal                | "X (copy)", then "X (copy N)", skipping taken names              | The dialog opens on an accepted name                                    |
| Delete message                    | Counts of what is lost, plus a note when the profile is in use  | The user knows exactly what goes and what happens next                  |
| Manage button                     | Ghost icon button (UsersThree) beside the list                  | Matches the rail's Profiles icon; light in the bar                      |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 359 passed, 0 failed (339 before; +20: page commands,
  validation, copy names, delete rules, card refresh, headless footer / manage button / cards). The Desktop
  suite was re-run twice more: 204 / 204 each time.
