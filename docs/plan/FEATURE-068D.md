# FEATURE-068D — Settings page & theme

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-068d-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-12-settings-and-profiles

## Objective

Give Enigma.Launchpad a **Settings** page and the preferences behind it: the **theme** (System, Light, Dark —
applied before the window shows and switched live), **minimize after opening a shortcut**, where the user's
**data** lives (with a way to open that folder), and an **About** line with the version. Preferences persist in
their own `settings.json`.

## Context & constraints

- The app currently starts in `RequestedThemeVariant="Dark"` with no runtime switch and no settings file
  (FEATURE-5B69 deliberately left a settings page out of scope).
- `App.axaml` documents that Enigma.Avalonia.Desktop defines **Dark and Light** theme dictionaries only, so the
  `Default` variant is avoided: "System" must be resolved explicitly to Light or Dark.
- House reference: `Enigma.GitClient` — `ISettingsService` in Core (`Current`, `Changed`, `LoadAsync`,
  `Update(Func<AppSettings, AppSettings>)`, `FlushAsync`), `ThemePreference` enum, settings loaded before the
  window is built, `SettingsCard` page with a centred `MaxWidth` column, enum descriptions via converters.
- Profiles (FEATURE-4D2E) are planned after this item and will add a "Profiles" footer item **before**
  "Settings"; nothing here depends on them.
- No new package: everything needed is in the current set.

## Design

### Core (`Enigma.Launchpad.Core.Settings`)

- `ThemePreference` — `System`, `Light`, `Dark`.
- `AppSettings` (record) — `int Version` (= 1), `ThemePreference Theme` (default `System`),
  `bool MinimizeAfterLaunch` (default `false`); `Defaults`; `Normalised()` (an undefined enum value → default).
- `ISettingsService` / `SettingsService` — `AppSettings Current`, `event EventHandler<SettingsChangedEventArgs>
  Changed` (raised on the thread that changed them), `Task LoadAsync()`, `AppSettings Update(Func<AppSettings,
  AppSettings>)` (no-op when nothing changed), `Task FlushAsync()`. JSON (camelCase, indented, enums as strings)
  in `settings.json` beside `launchpad.json`, written atomically through `AtomicFile`, saves serialised and always
  writing the latest settings. An unreadable file is moved aside to `settings.corrupt-<UTC stamp>.json`, defaults
  are used, and a warning is logged. A failed write is logged (warning) — the preference stays live this session.
- Registered in `AddLaunchpadCore()`.

### Desktop

- `App`: after the host starts and **before the window is built**, load the settings (blocking — the service is
  `ConfigureAwait(false)` throughout) and apply the theme; re-apply on `Changed`; flush on exit.
- Theme application (`ThemeApplier`, Desktop service): `ThemePreference` → `ThemeVariant` via a pure
  `Resolve(ThemePreference, PlatformThemeVariant?)` (System → the platform's variant, Dark when unknown); while
  the preference is System, follows `PlatformSettings.ColorValuesChanged`.
- `SettingsPageViewModel` + `SettingsPageView`, reached from a **footer** item "Settings" (Phosphor `Gear`):
  - *Appearance* — Theme `ComboBox` (System → "Follow the system", Light, Dark).
  - *Launching* — "Minimize after opening a shortcut" `ToggleSwitch`.
  - *Data* — the data folder path (selectable text) + "Open folder" button.
  - *About* — product name, version (informational version without the `+sha` suffix), MIT license.
- `IShortcutActions.Open` minimizes the main window after a successful launch when the preference is on (through
  a small `IMainWindow` seam); `IShortcutActions.OpenFolder(path)` opens a folder (no minimize) and reports a
  failure like the other actions.

## Phases

### PHASE01 — Settings service & live theme

**Status:** DONE

Steps:
1. Core: `ThemePreference`, `AppSettings`, `SettingsChangedEventArgs`, `ISettingsService` / `SettingsService`;
   register in `AddLaunchpadCore()`.
2. Desktop: `ThemeApplier` (resolve + apply + follow the platform while System); `App` loads the settings before
   building the window, applies the theme, re-applies on `Changed`, flushes on exit. `WindowHarness` mirrors
   the startup step.
3. Tests — Core: defaults when no file; round-trip; `Update` raises `Changed` once and writes; unchanged update
   neither writes nor raises; unreadable file moved aside + defaults; undefined enum normalised; concurrent
   updates leave the latest settings on disk. Desktop: `Resolve` theory; composition root resolves the new
   services; headless: changing the theme preference changes `Application.RequestedThemeVariant`.

Acceptance criteria:
- The app starts in the persisted theme (System by default, following the OS); changing the preference in code
  re-themes the running app; `settings.json` is written atomically and survives restarts.
- 0 warnings; suite green.

### PHASE02 — Settings page

**Status:** DONE

Steps:
1. `IMainWindow` seam (minimize the main window); `IShortcutActions.Open` minimizes after a successful launch
   when `MinimizeAfterLaunch` is on; `IShortcutActions.OpenFolder(path)`.
2. `SettingsPageViewModel` (theme options + selected theme, minimize toggle, data folder, open-folder command,
   version) and `SettingsPageView` (SettingsCards in a centred column, page header in the category-page style);
   footer navigation item "Settings".
3. Tests: VM — theme / toggle changes go through `ISettingsService.Update`; open folder; version string;
   `ShortcutActions` minimize on success only and only when enabled; headless — the footer item opens the page
   and the cards render.
4. README: mention the Settings page and `settings.json`.

Acceptance criteria:
- A "Settings" footer item opens a page where the theme can be switched live, minimize-after-opening toggled,
  the data folder opened, and the version read; every change persists.
- 0 warnings; suite green.

## Out of scope

- Start with the OS, tray icon, global hotkey, single-instance (still out, as in FEATURE-5B69).
- Accent colour, font size, tile density, localisation.
- Import / export / reset-to-defaults of settings.

## Decisions taken autonomously

| Question                         | Chosen                                                                 | Why                                                                         | Alternatives rejected                                         |
|----------------------------------|------------------------------------------------------------------------|-----------------------------------------------------------------------------|---------------------------------------------------------------|
| Theme choices                    | System, Light, Dark                                                    | The standard trio; System costs a few lines                                 | Light / Dark only                                             |
| Default theme                    | System                                                                 | Matches the house sibling (GitClient) and OS expectations                   | Dark (the old fixed start variant)                            |
| How System is applied            | Resolved explicitly to Light / Dark from the platform, followed live   | `App.axaml` documents that the `Default` variant can leave brushes unresolved | `ThemeVariant.Default`                                        |
| Where settings live              | Own `settings.json` beside `launchpad.json`                            | Preferences and user content fail and migrate independently                 | Inside `launchpad.json`                                       |
| Settings service location        | Core (`ISettingsService`), no UI dependency                            | Same split as GitClient; testable without Avalonia                          | Desktop-only service                                          |
| Unreadable settings file         | Moved aside, defaults used, warning logged                             | Never silently overwrite a user file; settings are cheap to redo            | Refuse to start; overwrite silently                           |
| Failed settings write            | Logged as a warning, preference stays live                             | Not worth an error bar for a preference                                     | InfoBar error                                                 |
| When settings are read           | Blocking, before the window is built                                   | No dark-then-light flash; tiny file; `ConfigureAwait(false)` chain          | Async after the window shows                                  |
| Extra settings ("etc.")          | Minimize after opening, data folder, about                             | Useful for a launcher, small, no new infrastructure                         | Tray / hotkey / autostart (new infrastructure); density, accent |
| Minimize-after-opening default   | Off                                                                    | Keeps today's behaviour unless chosen                                       | On                                                            |
| Page placement                   | Rail footer item "Settings" (Gear)                                     | Library convention for settings                                             | Toolbar button; dialog                                        |
| Open data folder                 | `IShortcutActions.OpenFolder` (no minimize)                            | Reuses the launcher and its failure reporting                               | Direct `Process.Start` in the ViewModel                       |
