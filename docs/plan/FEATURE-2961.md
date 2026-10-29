# FEATURE-2961 — Uniform About dialog

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-2961-about-dialog`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

An About dialog identical in layout, style and colours to Enigma.GitClient's, the reference for every Enigma
app. It shows the application's icon, "Enigma Launchpad", the version, the build revision and the copyright,
in the window's content dialog, with one Close button. It is opened from the Settings page's About card.

## Context & constraints

- **The reference** is GitClient's (`develop` @ 2c781d3):
  - `Services/AboutDialogService.cs` shows `AboutView` in `IContentDialogService`: no title, no icon,
    `CloseButtonText = "Close"`, `DefaultButton.Close`;
  - `Views/Dialogs/AboutView.axaml` is 380 wide:
    - the 96 px `app-256.png`;
    - the name at 18 SemiBold `EnigmaForegroundBrush`;
    - "Version x" in `EnigmaForegroundSecondaryBrush`;
    - a selectable "Build sha" line at 11 `EnigmaForegroundTertiaryBrush`, hidden without a revision;
    - the copyright at 11 `EnigmaForegroundSecondaryBrush`, hidden when empty;
  - `ViewModels/Dialogs/AboutViewModel.cs` takes version, build sha and copyright (a test seam);
  - `Core/Diagnostics/ProductInformation.cs` and `ProductVersion.cs` read and parse the informational
    version (`X.Y.Z+sha` → version and the 7-character sha) and the copyright.
- **Launchpad today:**
  - the Settings page's About card shows "Enigma Launchpad", the MIT line and `Version {0}`;
  - `SettingsPageViewModel.FormatVersion` parses the informational version, and its tests cover it;
  - `MainWindow.HostDialog` is 380–640 wide and at most 640 high, the same as GitClient's main-window
    bounds, so the view fits;
  - `Directory.Build.props` stamps `Copyright © 2026 Josué Clément`;
  - `Assets/app-256.png` is the icon's 256 frame (FEATURE-7298).
- Launchpad ViewModels derive from `ObservableObject` (no `ViewModelBase`).

## Steps

1. Core, `Diagnostics/`:
   - `ProductVersion` (pure parser) and `ProductInformation`, ported from GitClient;
   - `Name = "Enigma.Launchpad"`, `DisplayName = "Enigma Launchpad"`;
   - no `ScopeStatement`;
   - plus `ProductVersionTests` and `ProductInformationTests` in the Core tests.
2. Desktop:
   - `ViewModels/Dialogs/AboutViewModel` (`ObservableObject`);
   - `Views/Dialogs/AboutView.axaml(.cs)`, the reference's markup with only the namespaces changed;
   - `Services/AboutDialogService` (`IAboutDialogService`), registered as a singleton in `AddLaunchpadApp`.
3. Settings page:
   - `SettingsPageViewModel` takes `IAboutDialogService` and exposes `OpenAboutCommand`;
   - its `Version` comes from `ProductInformation.Version`, and `FormatVersion` is removed with its tests,
     which move to `ProductVersionTests`;
   - the About card gets an "About Enigma Launchpad" button (`x:Name="OpenAboutButton"`, automation name)
     next to the version text.
4. Tests (`AboutDialogTests`, headless), in GitClient's shape:
   - the ViewModel describes the running build;
   - the build and copyright lines appear only when there is a value;
   - the service shows the untitled view with one Close button;
   - the Settings page opens it;
   - in `MainWindow` with the real `ContentDialogService`, the dialog shows the view itself, without
     scrolling, with Close under it on the right.
   - `CompositionRootTests` resolves `IAboutDialogService`.

## Acceptance criteria

- Settings → About → "About Enigma Launchpad" opens the dialog. It shows the icon, "Enigma Launchpad",
  "Version 1.2.0" (the version of the build), "Build <sha>" when the build recorded one, and the copyright.
- The view's markup equals GitClient's apart from namespaces (layout, sizes, brushes).
- The whole suite is green; the Release build has zero warnings (XAML warnings included).

## Out of scope

- An About entry in the navigation rail or the app bar.
- A "built with" / credits list (GitClient removed its own).
- Localisation (the application is English-only).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Where it opens from | A button on the Settings page's About card | GitClient's main window opens it from its Settings About card; Launchpad has no toolbar on the start page | A rail footer item (rail items navigate, not open dialogs); the app bar |
| Where the version parsing lives | Core `Diagnostics/ProductInformation` + `ProductVersion`, as GitClient | Uniform across apps; the pure parser is testable in Core | Keeping `SettingsPageViewModel.FormatVersion` |
| `FormatVersion` | Replaced by `ProductInformation.Version`; tests moved | One source for the version shown in two places | Two parsers side by side |
| Missing version | `0.0.0`, GitClient's `ProductVersion.Unknown` | Uniform; it never happens in a real build | Launchpad's "unknown" |
| Markup | GitClient's view verbatim, namespaces changed | The spec: same layout, style and colours | A restyle |
| Base class | `ObservableObject` | Launchpad's ViewModels use it; there is no `ViewModelBase` here | Adding a `ViewModelBase` |
