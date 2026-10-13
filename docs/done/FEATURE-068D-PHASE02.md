# FEATURE-068D-PHASE02 — Settings page

## Summary

A **Settings** item now sits at the foot of the navigation rail. Its page, in the category pages' header style,
shows four SettingsCards in a left-aligned column capped at 900 px:

- **Theme** — "Follow the system", "Light", "Dark"; a choice re-themes the app at once and is saved.
- **Minimize after opening a shortcut** — when on, `ShortcutActions.Open` minimizes the main window after a
  successful launch (never after a failure).
- **Data folder** — the folder holding `launchpad.json` and `settings.json`, with an "Open folder" button
  (`IShortcutActions.OpenFolder`, which never minimizes and reports a failure like the other actions).
- **Enigma Launchpad** — "Version x.y.z" (informational version without the `+sha` build metadata), MIT license.

The page reads the preferences from `ISettingsService` and writes every change straight back; it listens to
`Changed`, so a change made elsewhere shows on the page too.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/Services/MainWindowService.cs` (`IMainWindowService` / `MainWindowService`)
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/SettingsPageViewModel.cs` (with `ThemeOption`)
- `src/Enigma.Launchpad.Desktop/Views/Pages/SettingsPageView.axaml` / `.axaml.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FakeSettingsService.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/RecordingMainWindow.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/SettingsPageViewModelTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/SettingsPageViewTests.cs`

Modified:
- `src/Enigma.Launchpad.Desktop/Services/ShortcutActions.cs` — minimize after a successful open when enabled;
  `OpenFolder`.
- `src/Enigma.Launchpad.Desktop/ViewModels/MainWindowViewModel.cs` — "Settings" footer item; the item helper
  takes the page type.
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` — page, ViewModel, window
  service.
- `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml` — `TextBlock.sectionHeader`.
- `tests/Enigma.Launchpad.Desktop.UnitTests/ShortcutActionsTests.cs`,
  `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/RecordingShortcutActions.cs`,
  `tests/Enigma.Launchpad.Desktop.UnitTests/CompositionRootTests.cs`.
- `README.md` — documentation sweep (see below).

## Deviations & follow-ups

- No functional deviation. The theme list uses `ThemeOption` records (value + label) instead of a value converter
  over the enum: no converter class, and the label is testable on the ViewModel.
- `ShortcutActions` now takes five dependencies — at the house's over-injection threshold. Acceptable for one
  cohesive "what happens around opening a shortcut" service; split the clipboard copy out if it grows further.
- Layout verified on headless screenshots in both variants (throwaway capture, not committed).
- Documentation sweep: `README.md` — the usage paragraph now mentions the Settings page; "Your data" mentions
  `settings.json` and its moved-aside name.
- Line endings: all new files are LF, as `.gitattributes` requires — nothing to recommend.

## Decisions taken at build time

| Question                          | Chosen                                                          | Why                                                                   |
|-----------------------------------|-----------------------------------------------------------------|-----------------------------------------------------------------------|
| Theme list items                  | `ThemeOption(Value, Label)` records                             | Readable labels without a converter; testable                         |
| Where minimize happens            | `ShortcutActions.Open`, after a successful launch only          | The one place every launch goes through; failures keep the window up  |
| Opening the data folder           | `IShortcutActions.OpenFolder` through the launcher, no minimize | Same checks and messages as a directory shortcut                      |
| Window access                     | `IMainWindowService` over the desktop lifetime's main window    | Testable seam, like `IClipboardService`                               |
| Version text                      | Informational version, `+metadata` dropped, "unknown" if absent | The SDK appends the commit hash; users need the version only          |
| Column layout                     | Left-aligned, star column capped at 900 px                      | Lines up with the header; width does not jump with the path length    |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 271 passed, 0 failed (248 before; +23: settings page
  ViewModel, headless page tests, minimize / open-folder actions, registrations).
