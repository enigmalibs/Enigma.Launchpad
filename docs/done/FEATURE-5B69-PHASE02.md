# FEATURE-5B69-PHASE02 — Core model, persistence & launcher

**Item:** FEATURE-5B69 — Launchpad shortcut manager
**Phase:** PHASE02 — Core model, persistence & launcher
**Branch:** `feature/feature-5b69-phase02-core-model`
**Run:** vibe/2026-10-12-launchpad-shortcut-manager

## Summary

This phase builds the Avalonia-free core the UI phases build on. Nothing is wired into the running app
yet; PHASE03 loads and saves through it.

- **Model** (`Shortcuts/`):
  - `ShortcutKind` is `Application`, `Directory` or `Url`.
  - `Shortcut(Id, Name, Target, Arguments?)` and `ShortcutGroup(Id, Name, Shortcuts, IsCollapsed)`.
  - `LaunchpadData(Version, Applications, Directories, Urls)`, with `Empty`, `GroupsOf(kind)` and
    `With(kind, groups)`.
  - A shortcut's kind is the category it is stored in, never a field of its own.
- **Validation** (`Validation/`):
  - `IShortcutValidator` / `ShortcutValidator` checks names: required, trimmed, at most 100
    characters. Group names must also be unique within the category, ignoring case.
  - It checks targets for their kind:
    - Applications: an absolute path to an existing file. Surrounding quotes from "copy as path" are
      stripped and `..` segments resolved. On macOS a `.app` bundle directory also counts. A folder
      gets "add it under Directories".
    - Directories: an absolute path to an existing folder. A file gets "add it under Applications".
    - URLs: through `UrlNormalizer`.
  - Results come back as a `TargetValidation`: the normalised target, or the error.
- **`UrlNormalizer`** accepts only `http`/`https`:
  - Text without `://` gets `https://`, unless it starts with a scheme of its own (`mailto:`, `C:`).
    A colon followed by a digit counts as a port (`localhost:8080`), not a scheme.
  - The user's spelling is kept. `TryParse` re-checks a stored address before it is opened.
- **Persistence** (`Persistence/`):
  - `IAppPaths` / `AppPaths` puts the data under `ApplicationData/Enigma.Launchpad`.
  - `AtomicFile` writes a temp file beside the target, then moves it over. The move is retried on
    Windows sharing violations.
  - `ILaunchpadStore` / `JsonLaunchpadStore` stores `launchpad.json` as camelCase, indented JSON.
    Comments and trailing commas are accepted on read; required fields and nullability are enforced.
    Load and save are serialised by a `SemaphoreSlim`.
  - A file the store cannot read is moved aside to `launchpad.corrupt-<UTC stamp>[-n].json` and an
    empty document is returned. "Cannot read" covers malformed JSON, a missing or null required field,
    a `null` entry in a list, and a version outside 1…current.
    `LaunchpadLoadResult.UnreadableFileMovedTo` says where the file went; it is never overwritten.
- **Launching** (`Launching/`):
  - `IShortcutLauncher` / `ShortcutLauncher` opens everything through the shell
    (`UseShellExecute`). An application starts in its own folder with its arguments; a directory
    opens in the file manager; a URL is re-validated and passed escaped.
  - `Reveal` (show in folder) uses `explorer /select,<path>` on Windows, `open -R` on macOS, and opens
    the parent directory on Linux. The path is always a separate argument.
  - Results come back as a `LaunchResult` (`Launched`, `TargetMissing`, `Refused`, `Failed`) with a
    message for the user. Start failures (`Win32Exception`, `InvalidOperationException`,
    `PlatformNotSupportedException`) are logged at Warning.
  - `IProcessStarter` / `ProcessStarter` is the seam tests replace.
- **DI:** `AddLaunchpadCore()` registers `TimeProvider.System` (tried, so a test clock wins),
  `IAppPaths` through a factory, the store, the validator, the process starter and the launcher, all as
  singletons.

## Files / modules touched

**Created**

- `src/Enigma.Launchpad.Core/Shortcuts/ShortcutKind.cs`, `Shortcut.cs`, `ShortcutGroup.cs`, `LaunchpadData.cs`
- `src/Enigma.Launchpad.Core/Validation/IShortcutValidator.cs`, `ShortcutValidator.cs`, `TargetValidation.cs`,
  `UrlNormalizer.cs`
- `src/Enigma.Launchpad.Core/Persistence/AppPaths.cs`, `AtomicFile.cs`, `ILaunchpadStore.cs`,
  `JsonLaunchpadStore.cs`
- `src/Enigma.Launchpad.Core/Launching/LaunchResult.cs`, `ProcessStarter.cs`, `ShortcutLauncher.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Infrastructure/TempDirectory.cs`, `FixedTimeProvider.cs`,
  `RecordingProcessStarter.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Validation/UrlNormalizerTests.cs`, `ShortcutValidatorTests.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Persistence/JsonLaunchpadStoreTests.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Launching/ShortcutLauncherTests.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Shortcuts/LaunchpadDataTests.cs`
- `docs/done/FEATURE-5B69-PHASE02.md`

**Modified**

- `src/Enigma.Launchpad.Core/DependencyInjection/ServiceCollectionExtensions.cs`: the registrations.
- `tests/Enigma.Launchpad.Core.UnitTests/DependencyInjection/ServiceCollectionExtensionsTests.cs`:
  every service resolves, and a pre-registered clock is kept.
- `docs/roadmap.md`, `docs/plan/FEATURE-5B69.md`

## Decisions taken at build time

| Question             | Chosen                                                                                           | Why                                                                                                               | Alternatives rejected                                             |
|----------------------|--------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------|
| Validator shape      | Injectable `IShortcutValidator` singleton                                                        | It touches the file system, so per house DI rules it is a seam, not a static                                      | Static helper class                                               |
| Error representation | User-worded `string?` messages / `TargetValidation`                                              | The UI shows them as-is next to the field                                                                         | Error codes mapped in the UI                                      |
| macOS `.app` bundles | Accepted as applications when on macOS                                                           | They are directories, so a plain "file exists" check would refuse every Mac app                                   | Refusing them; accepting any directory                            |
| Path normalisation   | Trim, strip surrounding quotes, `Path.GetFullPath`                                               | Pasting from Explorer's "Copy as path" just works; stored paths are canonical                                     | Environment-variable / `~` expansion (out of scope)               |
| URL spelling         | Keep the user's text (`https://` prefixed when missing); escape only when launching              | No surprising trailing slash or re-casing in the tile                                                             | Storing `Uri.AbsoluteUri`                                         |
| Unreadable document  | Moved aside, never overwritten, for a newer format version too                                   | A newer build's file must survive being opened by an older one                                                    | Loading what is understood and dropping the rest on the next save |
| JSON strictness      | `RespectNullableAnnotations` + `RespectRequiredConstructorParameters`, plus a null-element check | A damaged file is caught on load, not as a null deep in the UI                                                    | Lenient read with defaults                                        |
| Hand-edited files    | Comments and trailing commas accepted                                                            | Cheap; the file is meant to be human-readable                                                                     | Strict JSON only                                                  |
| Launch API           | Synchronous `LaunchResult Launch(...)`                                                           | `Process.Start` is synchronous; an async wrapper would only pretend                                               | `Task<LaunchResult>`                                              |
| Save ordering        | The store only serialises writes; the session (PHASE03) will always write the latest snapshot    | `SemaphoreSlim` does not guarantee FIFO, so ordering belongs to the one place that knows which snapshot is newest | FIFO queue inside the store                                       |

## Deviations & follow-ups

- The plan's file name for a moved-aside document is kept (`launchpad.corrupt-<yyyyMMddHHmmss>.json`), and
  a `-n` suffix is added when that name is already taken.
- No other deviations.

## Build / test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental`: **0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.Launchpad.slnx`: **104 passed, 0 failed, 0 skipped** (Core 92, Desktop 12).
- One fix cycle: an ambiguous `TheoryData<Exception>` constructor call in a test (xunit.v3 4.x overloads),
  rewritten as a collection initializer.
