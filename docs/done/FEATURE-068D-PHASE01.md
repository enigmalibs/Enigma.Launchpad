# FEATURE-068D-PHASE01 — Settings service & live theme

## Summary

The application now has persisted preferences and applies the theme the user chose. `ISettingsService` (Core)
reads and writes `settings.json` beside `launchpad.json` — atomically, saves serialised and always writing the
latest settings, an unreadable file moved aside to `settings.corrupt-<stamp>.json` with the defaults used.
`ThemeApplier` (Desktop) resolves the preference (System · Light · Dark) to a concrete Light or Dark variant,
puts it on the application **before the main window is built**, re-applies it whenever the preference changes,
and — while the preference is System — follows the operating system's colour changes. The settings are flushed at
exit with the launchpad.

## Files

Created:
- `src/Enigma.Launchpad.Core/Settings/ThemePreference.cs`
- `src/Enigma.Launchpad.Core/Settings/AppSettings.cs`
- `src/Enigma.Launchpad.Core/Settings/ISettingsService.cs` (with `SettingsChangedEventArgs`)
- `src/Enigma.Launchpad.Core/Settings/SettingsService.cs`
- `src/Enigma.Launchpad.Desktop/Services/ThemeApplier.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Settings/SettingsServiceTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/ThemeApplierTests.cs`

Modified:
- `src/Enigma.Launchpad.Core/DependencyInjection/ServiceCollectionExtensions.cs` — registers `ISettingsService`.
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` — registers `ThemeApplier`.
- `src/Enigma.Launchpad.Desktop/App.axaml.cs` — `LoadSettings` before the window; settings flushed on exit.
- `src/Enigma.Launchpad.Desktop/App.axaml` — comment: the start variant is replaced by the preference.
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/WindowHarness.cs` — mirrors the startup step, flushes
  the settings on dispose.
- `tests/Enigma.Launchpad.Desktop.UnitTests/CompositionRootTests.cs`,
  `tests/Enigma.Launchpad.Core.UnitTests/DependencyInjection/ServiceCollectionExtensionsTests.cs` — new services.

## Deviations & follow-ups

- No deviation from the plan.
- `LoadAsync` does not raise `Changed` (documented on the interface): whoever loads applies what was read, which
  keeps the startup path explicit.
- A settings file whose read fails with an I/O error (rather than bad content) is left in place and the defaults
  are used; the next change overwrites it. Acceptable for preferences.
- Line endings: the repository is LF-normalised (`.gitattributes`); every new file is LF — nothing to recommend.

## Decisions taken at build time

| Question                                | Chosen                                                              | Why                                                                      |
|-----------------------------------------|---------------------------------------------------------------------|--------------------------------------------------------------------------|
| `AppSettings` shape                     | Record with `init` properties and defaults (not positional)         | A file missing a field still reads, with that field's default            |
| Enum format in `settings.json`          | camelCase names (`"theme": "dark"`)                                 | Readable; numeric values are still accepted and normalised               |
| Unknown enum value                      | Normalised to the default on read and on update                     | The theme code never sees an undefined value                             |
| `ThemeApplier` interface                | Concrete class, `IDisposable`, no interface                         | Composition-root helper nothing substitutes; disposal unhooks its events |
| Startup hook                            | `App.LoadSettings(services, application)`, reused by `WindowHarness` | One startup path for the app and the headless tests                      |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 248 passed, 0 failed (221 before; +27: settings service,
  theme resolution and headless theme application, DI registrations).
