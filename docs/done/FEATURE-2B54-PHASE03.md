# FEATURE-2B54-PHASE03 — Profiles page export & import

## Summary

Each profile card's menu now offers **Export…** and **Import…**, between Duplicate… and the moves.

- **Export…** (disabled for a profile with no group) opens the selection dialog on the profile — "Export “Work”",
  confirm "Export…" — then a save picker proposing `Work shortcuts.json` (JSON filter, overwrite confirmed by the
  picker), writes the ticked part, and reports it in the info bar: "1 group · 1 shortcut written to <path>."
- **Import…** opens a JSON picker, reads the file and, when it holds something, opens the selection dialog on its
  content — "Import into “Home”", saying where it comes from, how merging works and, if any, how many entries were
  left out — then imports the ticked part into that profile and reports what was added ("3 shortcuts and 1 new
  group added.", or "Everything ticked was already there."). Importing into the profile in use refreshes the
  category pages (PHASE01).
- Failures change nothing and say why: a refused file ("This file cannot be imported" + the reader's reason), an
  unreadable file, an export that cannot be written (error info bar); a file with nothing valid in it ("Nothing to
  import", warning). Cancelling at any step does nothing.
- Seams: `IPathPicker.PickJsonFileToOpenAsync` / `PickJsonFileToSaveAsync` (over the Enigma file dialog service);
  `IUserNotifications.ShowSuccess` (the info bar's Success severity).

## Files

Created:
- `tests/Enigma.Launchpad.Desktop.UnitTests/ProfileExportImportTests.cs`

Modified:
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ProfilesPageViewModel.cs` — export and import flows, the export
  file name, the "added" wording; takes `IShortcutExchange`, `IPathPicker`, `IUserNotifications`.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ProfileItemViewModel.cs` — `ExportCommand`, `ImportCommand`.
- `src/Enigma.Launchpad.Desktop/Views/Pages/ProfilesPageView.axaml` — the two menu items.
- `src/Enigma.Launchpad.Desktop/Services/PathPicker.cs` — JSON open / save pickers.
- `src/Enigma.Launchpad.Desktop/Services/UserNotifications.cs` — `ShowSuccess`.
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FakePathPicker.cs`, `RecordingNotifications.cs` — the new
  members.
- `tests/Enigma.Launchpad.Desktop.UnitTests/ProfilesPageViewModelTests.cs` — the new constructor;
  `ProfilesPageViewTests.cs` — the card menu.
- `README.md` — documentation sweep (see below).

## Deviations & follow-ups

- The export dialog's confirm button reads "Export…" rather than the plan's "Export": a save picker follows it, and
  the ellipsis says so.
- Documentation sweep: `README.md` — a paragraph on export and import after the profiles paragraph; an "Export
  files" section under "Your data" with the format and the reading rules; the Core row of "Solution layout" now
  names export (its table re-padded).
- Fix budget: none used — the first build and test run were green.
- Follow-up suggestion (out of scope): importing a file as a new profile in one step, and exporting several
  profiles to one file.
- Line endings: new files are LF, per `.gitattributes`.

## Decisions taken at build time

| Question                              | Chosen                                                   | Why                                                                    |
|---------------------------------------|----------------------------------------------------------|------------------------------------------------------------------------|
| Order of the export steps             | Tick first, then choose the file                         | The file dialog comes last, as in "Save as"; cancelling early is cheap |
| Success message for an export         | Counts and the full path                                 | The user knows what was written and where                              |
| Counting what an import added         | Profile counts just before and after `ImportIntoProfile` | Merged and skipped entries are reflected exactly                       |
| Import with nothing valid in the file | Warning, no dialog                                       | An empty tree would be a dead end                                      |
| Invalid file-name characters          | Replaced by `_`, per `Path.GetInvalidFileNameChars()`    | The picker always gets a usable proposal on this platform              |
| Menu icons                            | `Export` / `DownloadSimple`                              | Out of and into the launchpad, at a glance                             |
| Where the flows live                  | `ProfilesPageViewModel`, like the other card commands    | The cards stay snapshots; the page asks and hands over to the session  |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors (no `AVLN` XAML warnings).
- `dotnet test --solution Enigma.Launchpad.slnx` — 434 passed, 0 failed (413 before; +21: export writes the ticked
  part and reports where, cancelling in the dialog or the picker, disabled for an empty profile, write failure;
  import merges the ticked part into the chosen profile and reports what was added, re-import adds nothing,
  left-out entries mentioned, refused / unreadable / empty files, cancelling; file-name and wording theories;
  headless — the card menu's entries in order).
