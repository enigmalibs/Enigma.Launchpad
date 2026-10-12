# FEATURE-5B69-PHASE01 — Solution scaffold & app shell

**Item:** FEATURE-5B69 — Launchpad shortcut manager
**Phase:** PHASE01 — Solution scaffold & app shell
**Branch:** `feature/feature-5b69-phase01-solution-scaffold`
**Run:** vibe/2026-10-12-launchpad-shortcut-manager

## Summary

This phase sets up the solution and an app shell that runs. There are no shortcuts yet.

- **Root configuration**, from the house templates: `.gitignore`, `.gitattributes` (LF), the full C#
  `.editorconfig`, `Directory.Build.props` (LangVersion 14, nullable, no implicit usings, warnings as
  errors, code style enforced, `Version` 0.1.0), `Directory.Packages.props` (CPM, GitClient's validated
  Avalonia 12.1.1 / Enigma.Avalonia.Desktop 1.2.0 / Enigma.Icons.Avalonia 1.0.0 set), `global.json`
  (SDK 10.0.100 latestFeature + MTP runner), MIT `LICENSE.md`, and an empty `RELEASENOTES.md`.
- **`Enigma.Launchpad.slnx`** with `src/` and `tests/` folders.
- **`Enigma.Launchpad.Core`** (net10.0 library, XML docs on, DI and logging abstractions only). Its
  `AddLaunchpadCore()` registration extension is empty until PHASE02.
- **`Enigma.Launchpad.Desktop`** (WinExe):
  - `App` is the composition root. It builds an `IHost` (content root beside the executable), starts
    it, never runs it, and stops it when the app exits.
  - `ConfigureServices` registers Core, the six Enigma.Avalonia.Desktop services
    (`AddEnigmaDesktopServices`) and the app (`AddLaunchpadApp`).
  - `RegisterHosts` hands over the dialog, overlay and info-bar hosts and the storage provider before
    the window shows.
  - The theme starts Dark, with the Enigma Fluent dictionary merged as a `ResourceInclude`.
  - `MainWindow` has a `NavigationView` rail with Applications, Directories and URLs (Phosphor
    `AppWindow`, `Folder`, `Globe`), a page area, and the three host controls as the last children of
    the root `Panel`.
  - One `CategoryPageView` serves three singleton page ViewModels (`ApplicationsPageViewModel`,
    `DirectoriesPageViewModel`, `UrlsPageViewModel`, all over `CategoryPageViewModel`). Each page
    shows a header (icon tile, title, description) and a placeholder empty state.
  - `ContainerPageFactory` resolves pages and their ViewModels from DI.
  - `Themes/Styles.axaml` sets the icon scale and the page-header text styles on `Enigma*` brushes.
- **App icon:** `Assets/app.ico` (16/24/32/48/64/128/256 px) and `Assets/app-256.png`, rendered from
  Phosphor `RocketLaunch` (Fill) on a blue rounded square. It is wired as both `<ApplicationIcon>` and
  `Window.Icon`.
- **README** now covers what the app is, its requirements, the build/run/test commands, and the
  solution layout.

## Files / modules touched

**Created**

- `.editorconfig`, `.gitattributes`, `.gitignore`, `Directory.Build.props`, `Directory.Packages.props`,
  `global.json`, `LICENSE.md`, `RELEASENOTES.md`, `Enigma.Launchpad.slnx`
- `src/Enigma.Launchpad.Core/Enigma.Launchpad.Core.csproj`
- `src/Enigma.Launchpad.Core/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/Enigma.Launchpad.Desktop/Enigma.Launchpad.Desktop.csproj`, `app.manifest`, `Program.cs`,
  `App.axaml`, `App.axaml.cs`
- `src/Enigma.Launchpad.Desktop/Assets/app.ico`, `Assets/app-256.png`
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/Enigma.Launchpad.Desktop/Navigation/ContainerPageFactory.cs`
- `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml`
- `src/Enigma.Launchpad.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/CategoryPageViewModel.cs`,
  `ApplicationsPageViewModel.cs`, `DirectoriesPageViewModel.cs`, `UrlsPageViewModel.cs`
- `src/Enigma.Launchpad.Desktop/Views/MainWindow.axaml(.cs)`,
  `Views/Pages/CategoryPageView.axaml(.cs)`
- `tests/Enigma.Launchpad.Core.UnitTests/Enigma.Launchpad.Core.UnitTests.csproj`,
  `DependencyInjection/ServiceCollectionExtensionsTests.cs` (2 tests)
- `tests/Enigma.Launchpad.Desktop.UnitTests/Enigma.Launchpad.Desktop.UnitTests.csproj`,
  `Infrastructure/HeadlessAvaloniaFixture.cs`, `CompositionRootTests.cs` (10 tests),
  `MainWindowTests.cs` (2 headless tests)
- `docs/done/FEATURE-5B69-PHASE01.md`

**Modified**

- `README.md`, `docs/roadmap.md`, `docs/plan/FEATURE-5B69.md`

## Decisions taken at build time

| Question                                 | Chosen                                                                | Why                                                                              | Alternatives rejected                                                                              |
|------------------------------------------|-----------------------------------------------------------------------|----------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------|
| Core's Microsoft.Extensions dependencies | `DependencyInjection.Abstractions` + `Logging.Abstractions` (10.0.12) | Core takes seams, never a container or a sink (Enigma.Backup's pattern)          | `Microsoft.Extensions.Hosting` in Core (GitClient's pattern; heavier than needed)                  |
| Main window navigation test placement    | Headless collection, not the plain composition-root tests             | Selecting a rail item builds a `UserControl`, which needs the Avalonia UI thread | Asserting the ViewModel off the UI thread (navigation would fail silently into `NavigationFailed`) |
| Icon artwork                             | Rendered with a throwaway SkiaSharp tool, not committed               | The app needs a real `.ico`; the generator is not part of the product            | Committing the generator; borrowing a sibling app's icon                                           |
| Window size                              | 1180 × 760, minimum 760 × 520, centred                                | Room for wrap-laid tiles beside the rail                                         | Maximised on start (GitClient's choice, which suits a git history, not a launcher)                 |

## Deviations & follow-ups

- None from the plan.
- Line endings: `core.autocrlf` is on in this environment, so git warns about LF→CRLF in the working
  copy. `.gitattributes` (`* text=auto eol=lf`) now governs what is committed. Recommendation only: run
  `git add --renormalize .` if mixed endings ever show up in diffs.

## Build / test evidence

- `dotnet build Enigma.Launchpad.slnx`: **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.Launchpad.slnx`: **14 passed, 0 failed, 0 skipped** (Core 2, Desktop
  12).
- Manual smoke run: `Enigma.Launchpad.Desktop.exe` started and showed a window titled "Enigma Launchpad",
  still running after 5 s, then closed.
