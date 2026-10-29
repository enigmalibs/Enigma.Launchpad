# FEATURE-2961 — Uniform About dialog

## Summary

Enigma Launchpad now has the About dialog every Enigma app shows, ported from Enigma.GitClient. Its layout,
sizes and brushes are identical:

- the 96 px icon;
- "Enigma Launchpad";
- "Version X.Y.Z";
- a selectable "Build <sha>" line, hidden without a revision;
- the copyright, hidden when empty;
- in the main window's content dialog, with no title, no icon and one Close button.

It opens from a new "About Enigma Launchpad" button on the Settings page's About card. The version and the
revision are read by the new Core `Diagnostics/ProductInformation` and `ProductVersion`, which also give the
Settings page its version. `SettingsPageViewModel.FormatVersion` is gone.

## Files/modules touched

- Created: `src/Enigma.Launchpad.Core/Diagnostics/ProductVersion.cs`, `ProductInformation.cs`
- Created: `src/Enigma.Launchpad.Desktop/Services/AboutDialogService.cs`,
  `ViewModels/Dialogs/AboutViewModel.cs`, `Views/Dialogs/AboutView.axaml(.cs)`
- Modified: `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs`,
  `ViewModels/Pages/SettingsPageViewModel.cs`, `Views/Pages/SettingsPageView.axaml`
- Created: `tests/Enigma.Launchpad.Core.UnitTests/Diagnostics/ProductVersionTests.cs`, `ProductInformationTests.cs`
- Created: `tests/Enigma.Launchpad.Desktop.UnitTests/AboutDialogTests.cs`, `Infrastructure/RecordingAboutDialog.cs`
- Modified: `tests/Enigma.Launchpad.Desktop.UnitTests/SettingsPageViewModelTests.cs`, `CompositionRootTests.cs`
- Modified: `docs/RELEASE.md`, `docs/roadmap.md`, `docs/plan/FEATURE-2961.md`; created `docs/done/FEATURE-2961.md`

## Deviations & follow-ups

- `ProductInformation` has no `Name` or `ScopeStatement` constant. GitClient uses `Name` for its HTTP user
  agent and `ScopeStatement` for its own scope; neither has a use here.
- With no informational version, the Settings page shows `0.0.0` (`ProductVersion.Unknown`) instead of
  `unknown`, the uniform fallback. A real build always stamps a version.
- Not carried over from GitClient: the rendered-snapshot test (`TheView_IsDrawn`, which needs GitClient's
  `SnapshotColours` helper) and the StartWindow cases (Launchpad has one window).
- Follow-up, out of scope: `WebsiteIconFetcher.ProductVersion` (the User-Agent) still reads the assembly
  version itself. It could use `ProductInformation.Version`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx -c Release`: 0 warnings, 0 errors (no AVLN warning either).
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 997 total, 996 passed, 0 failed, 1 skipped
  (Unix-only).
- Tests added:
  - Core: `ProductVersionTests` (12 cases, including the two `FormatVersion` cases that moved here),
    `ProductInformationTests` (4);
  - Desktop, `AboutDialogTests` (11, headless, through the real main window and `ContentDialogService`):
    - the ViewModel and its line flags;
    - the service's untitled dialog with one Close button;
    - a real click on the Settings button opening it;
    - the view's content, and its hidden lines;
    - in the main window, no scrolling and Close under the view on the right;
  - `SettingsPageViewModelTests.OpenAbout_ShowsTheAboutDialog`;
  - `CompositionRootTests` resolves `IAboutDialogService`.
- Removed: the 4 `FormatVersion_DropsTheBuildMetadata` cases (the method is gone).
- Fix cycle 1 of 3: raising `Button.ClickEvent` does not run a bound command (it skips `OnClick`), so the
  Settings test now clicks with the headless pointer, as `TileActionsViewTests` does.

## Documentation sweep

- `docs/RELEASE.md` §1: "The application does not show it" was now wrong. The About dialog shows the version
  and the short revision, and the recipes read the full `X.Y.Z+<sha>`.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `ConfigureAwait(true)` in the service | Dropped | Launchpad's dialog code awaits plainly on the UI thread |
| The button on the card | Beside the version text, in a horizontal `StackPanel` | The card takes one content; the version stays where it was |
| The Settings test's click | A headless pointer click | It proves the binding, not just the command |
