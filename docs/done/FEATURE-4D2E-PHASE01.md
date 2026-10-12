# FEATURE-4D2E-PHASE01 — Profile document model

## Summary

The launchpad document now holds **profiles**. `launchpad.json` is written in format **version 2** —
`{ version, activeProfileId, profiles: [ { id, name, applications, directories, urls } ] }` — and a version 1
file (the three categories at the root) is still read: its groups become one profile named "Default", in use, and
the next save writes it back as version 2.

- `Profile` (record): `GroupsOf` / `With` per category, `Create(name)`, `Duplicate(name)` (fresh identities for
  the profile, its groups and its shortcuts), `CountGroups()` / `CountShortcuts()`.
- `LaunchpadData` (record): `ActiveProfileId`, `Profiles`, `ActiveProfile`; `GroupsOf` / `With` act on the
  profile in use, so the session and the category pages work unchanged; profile operations `AddProfile`,
  `RenameProfile`, `RemoveProfile` (never the last; removing the one in use activates the one that took its
  place, else the one before), `CanMoveProfile` / `MoveProfile`, `WithActiveProfile`, `FindProfile`;
  `Normalised()` repairs a hand-edited document (no profile → "Default"; unknown active identity → first).
  `Empty` is now one empty "Default" profile.
- `JsonLaunchpadStore`: reads the version first, then the matching shape; versions other than 1 and 2, a missing
  field, or a `null` profile / group / shortcut are refused and moved aside as before.
- `IShortcutValidator.ValidateProfileName` — same rules as group names, its own duplicate message.

The Desktop application compiles and behaves as before on the profile in use; switching comes in PHASE02.

## Files

Created:
- `src/Enigma.Launchpad.Core/Shortcuts/Profile.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Shortcuts/ProfileTests.cs`

Modified:
- `src/Enigma.Launchpad.Core/Shortcuts/LaunchpadData.cs` — version 2 shape and profile operations.
- `src/Enigma.Launchpad.Core/Persistence/JsonLaunchpadStore.cs` — version probe, v1 migration, well-formedness
  over profiles, normalisation.
- `src/Enigma.Launchpad.Core/Validation/IShortcutValidator.cs`, `ShortcutValidator.cs` — profile names (shared
  unique-name rule).
- `tests/Enigma.Launchpad.Core.UnitTests/Shortcuts/LaunchpadDataTests.cs` — rewritten for profiles.
- `tests/Enigma.Launchpad.Core.UnitTests/Persistence/JsonLaunchpadStoreTests.cs` — v2 format, migration,
  normalisation, new unreadable cases.
- `tests/Enigma.Launchpad.Core.UnitTests/Validation/ShortcutValidatorTests.cs` — profile names.
- `tests/Enigma.Launchpad.Desktop.UnitTests/LaunchpadSessionTests.cs` — category reads through `GroupsOf`.

## Deviations & follow-ups

- `LaunchpadData.ActiveProfile` is a property marked `[JsonIgnore]` (System.Text.Json would otherwise write the
  profile a second time). The rest of the model stays attribute-free.
- `LaunchpadData.Of(profile)` was added as the one way to build a single-profile document (used by `Empty` and the
  v1 migration) — not in the plan, but it keeps both on one path.
- A v1 file's migrated profile gets a fresh identity on every load until the first save writes it as v2; nothing
  refers to that identity across loads, so it is harmless.
- No documentation sweep edit: nothing user-facing changed yet (README updated in PHASE03 with the profiles UI).
- Line endings: new files are LF, per `.gitattributes`.

## Decisions taken at build time

| Question                              | Chosen                                                           | Why                                                                     |
|---------------------------------------|------------------------------------------------------------------|-------------------------------------------------------------------------|
| How the store picks a shape           | Read `{ version }` first, then deserialize v1 or v2              | Strict required-field checks still apply to each shape                  |
| Out-of-range move / unknown profile   | Exceptions from the model; callers check `CanMoveProfile` first  | Programming errors surface; the UI only offers valid moves              |
| `ActiveProfile` with no profile       | `InvalidOperationException`                                      | Loaded documents are always normalised; only a hand-built one can hit it |
| Profile counts                        | Methods (`CountGroups`, `CountShortcuts`), not properties        | Properties would be serialized into the file                            |
| Unique-name rule                      | One private helper for groups and profiles, distinct messages    | Same rule, user-facing wording per noun                                 |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 317 passed, 0 failed (271 before; +46: profile operations,
  duplication, v1 migration and write-back, normalisation, unreadable v2 shapes, profile-name validation).
