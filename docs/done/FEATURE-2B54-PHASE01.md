# FEATURE-2B54-PHASE01 — Export format, merge & import

## Summary

The core of profile export / import, without any UI yet.

- **Exchange format** (`Enigma.Launchpad.Core.Exchange`): `IShortcutExchange` / `ShortcutExchange` writes a profile's
  groups and shortcuts as an indented JSON document — `format: "enigma-launchpad-shortcuts"`, `version: 1`,
  `profile`, `applications` / `directories` / `urls`, each group a `name` and its `shortcuts` (`name`, `target`,
  `arguments` only when set) — atomically, through `AtomicFile`. No identities and no fold state are written.
- **Reading** returns a `ShortcutImport`: the content as a profile (named after the file's `profile`, or
  "Imported") under new identities, and how many entries were left out. A file that is not JSON, not marked as an
  export, of another version, or damaged (a `null` entry, a missing required field) is refused with an
  `InvalidDataException` whose message is worded for the user. Inside a valid file, a group or shortcut the
  shortcut form would refuse — blank or over-long name, relative path, non-http(s) URL — is left out and counted
  (a group with each of its shortcuts). Names are trimmed, URLs normalised, arguments kept for applications only.
  Paths need not exist. Comments, trailing commas and missing category lists are tolerated.
- **Merge**: `Profile.Import(source)` adds a same-named group's shortcuts (ignoring case) to the existing group's
  end, appends other groups unfolded, skips shortcuts already in the receiving group (same name and target ignoring
  case, same arguments), and gives everything added a new identity. `LaunchpadData.ImportInto(id, source)` applies
  it to one profile.
- **Session**: `ILaunchpadSession.ImportIntoProfile(id, content)` saves like every change and raises
  `ProfilesChanged`; when the target is the profile in use it also raises `ActiveProfileChanged`, so loaded category
  pages re-read their groups instead of writing a stale copy back on their next edit.

## Files

Created:
- `src/Enigma.Launchpad.Core/Exchange/IShortcutExchange.cs` — interface and `ShortcutImport`.
- `src/Enigma.Launchpad.Core/Exchange/ShortcutExchange.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Exchange/ShortcutExchangeTests.cs`

Modified:
- `src/Enigma.Launchpad.Core/Shortcuts/Profile.cs` — `Import`.
- `src/Enigma.Launchpad.Core/Shortcuts/LaunchpadData.cs` — `ImportInto`.
- `src/Enigma.Launchpad.Core/DependencyInjection/ServiceCollectionExtensions.cs` — `IShortcutExchange` singleton.
- `src/Enigma.Launchpad.Desktop/Services/LaunchpadSession.cs` — `ImportIntoProfile`; `ChangeProfiles` takes the
  profile whose groups changed; event docs widened.
- `tests/Enigma.Launchpad.Core.UnitTests/Shortcuts/ProfileTests.cs`, `Shortcuts/LaunchpadDataTests.cs`,
  `DependencyInjection/ServiceCollectionExtensionsTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/LaunchpadSessionProfileTests.cs`, `ProfileSwitchingTests.cs`

## Deviations & follow-ups

- No functional deviation from the plan.
- The marker and version are read through a `JsonDocument` before the typed parse, so a JSON file of another kind
  (an array, `settings.json`, `launchpad.json`) is reported as "not a launchpad shortcuts export" rather than as
  invalid JSON or a missing field; a syntax error still says "not valid JSON" with its line.
- Fix budget: none used — the first build and test run were green.
- Documentation sweep: nothing user-facing changed yet; README is updated with the page in PHASE03.
- Line endings: new files are LF, per `.gitattributes`.

## Decisions taken at build time

| Question                                    | Chosen                                                                       | Why                                                                        |
|---------------------------------------------|------------------------------------------------------------------------------|----------------------------------------------------------------------------|
| Error type for a refused file               | `InvalidDataException` with a user-worded message                            | Distinct from I/O errors, which propagate as they are                      |
| Header check                                | `JsonDocument` probe of `format` / `version` before the typed parse          | Accurate messages for foreign JSON; the typed parse keeps its line numbers |
| Content name when the file has none         | "Imported" (`ShortcutExchange.DefaultContentName`)                           | Something to show in the import dialog                                     |
| Profile name validity in the file           | Kept when it passes the profile-name rules, else "Imported"                  | Informational only; never trusted as is                                    |
| Path normalisation on import                | `Path.GetFullPath` on fully-qualified paths; `\0` refused                    | Same form as the shortcut form stores; `GetFullPath` throws on `\0`        |
| Merging into an existing group              | Keeps its identity, name and fold state                                      | The user's group is the one that stays                                     |
| Event for an import into the profile in use | `ActiveProfileChanged`, via a `groupsChangedIn` argument of `ChangeProfiles` | Rename and the other operations keep their exact events                    |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 399 passed, 0 failed (359 at the last recorded run; +40: format
  shape, round-trip, 16 refusal cases, skipped-entry counting, normalisation, missing paths accepted, fresh ids,
  merge rules, `ImportInto`, DI registration, session import events and persistence, page refresh after an import).
