# FEATURE-4D2E-PHASE02 — Session profiles & switcher

## Summary

Profiles can now be switched from anywhere in the app. A slim bar above every page holds a **Profile** combobox
listing the profiles in order with the one in use selected; choosing another puts it in use, every loaded category
page reads its groups again, edits land in that profile only, and the choice is saved (`activeProfileId`), so the
next start opens on it.

- `ILaunchpadSession` gains `Profiles`, `ActiveProfile`, `SwitchProfile`, `AddProfile`, `DuplicateProfile` (copy
  inserted right after its source), `RenameProfile`, `DeleteProfile`, `CanMoveProfile` / `MoveProfile`, and two
  events raised on the caller's thread once the change is in place: `ProfilesChanged`, then
  `ActiveProfileChanged` when another profile is in use. Every change goes through the existing coalesced save
  chain (`QueueSave`); a change that throws leaves the document untouched; switching to the profile in use does
  nothing.
- The launchpad is read **before the main ViewModel is built** (`App.LoadLaunchpad`, mirrored by
  `WindowHarness`), so the switcher and the first page start on the remembered profile.
- `CategoryPageViewModel` re-reads its groups on `ActiveProfileChanged` when loaded; a page not shown yet reads
  the profile in use when it first appears.
- `ProfileSwitcherViewModel` (`Profiles` of `ProfileOption(Id, Name)`, two-way `SelectedProfile`) follows the
  session; it rebuilds the list only when a name or the order changed, and ignores selections it makes itself or
  a `null` from a list being rebuilt.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/ViewModels/ProfileSwitcherViewModel.cs` (with `ProfileOption`)
- `tests/Enigma.Launchpad.Desktop.UnitTests/LaunchpadSessionProfileTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/ProfileSwitchingTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/ProfileSwitcherViewTests.cs`

Modified:
- `src/Enigma.Launchpad.Desktop/Services/LaunchpadSession.cs` — profile API, events, `EnsureLoaded` /
  `QueueSave` helpers.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/CategoryPageViewModel.cs` — reload on profile switch.
- `src/Enigma.Launchpad.Desktop/ViewModels/MainWindowViewModel.cs` — `ProfileSwitcher`.
- `src/Enigma.Launchpad.Desktop/Views/MainWindow.axaml` — the profile bar.
- `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml` — `Border.appBar`, `TextBlock.appBarLabel`.
- `src/Enigma.Launchpad.Desktop/App.axaml.cs` — `LoadLaunchpad` before the main ViewModel.
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` — the switcher.
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/WindowHarness.cs`, `Infrastructure/Pages.cs`
  (`WorkAndHome()` document), `CompositionRootTests.cs`.

## Deviations & follow-ups

- As planned, the "Manage profiles" button is left for PHASE03, with the page it opens.
- Layout checked on a headless screenshot (throwaway, not committed): the bar sits top-right above the page header.
- Documentation sweep: nothing factually wrong yet — the README describes profiles in PHASE03 with the page that
  creates them.
- Line endings: new files are LF, per `.gitattributes`.

## Decisions taken at build time

| Question                                  | Chosen                                                            | Why                                                                     |
|-------------------------------------------|-------------------------------------------------------------------|-------------------------------------------------------------------------|
| Event order                               | `ProfilesChanged`, then `ActiveProfileChanged`                    | The list is current before pages re-read                                |
| Events vs. group edits                    | Group edits (`Update`) raise no profile event                     | Only the Profiles page shows counts, and it refreshes when it appears   |
| Where the copy of a profile goes          | Right after its source                                            | Keeps related profiles together                                         |
| Rebuilding the combobox list              | Only when names or order changed                                  | A plain switch never resets the list the user is clicking in            |
| Bar content                               | Icon, "Profile" label, combobox (200–320 px)                      | Self-explanatory on first sight; long names trimmed                     |
| Startup read of the launchpad             | Blocking, after the hosts are registered                          | First frame is right; load failures are posted to the info bar          |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 339 passed, 0 failed (317 before; +22: session profile
  operations and events, page reload, switcher behaviour, headless combobox). The Desktop suite was re-run twice
  more: 184 / 184 each time.
