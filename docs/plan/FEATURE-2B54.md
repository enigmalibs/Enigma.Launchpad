# FEATURE-2B54 — Profile import / export

**Status:** IN PROGRESS
**Type:** FEATURE
**Branch:** `feature/feature-2b54-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-12-profile-import-export

## Objective

From the **Profiles** page, export a profile's applications, directories and URLs — its groups and their
shortcuts — to a **JSON** file, and import such a file into a profile. Both directions show a **checkbox tree**
(category → group → shortcut) so the user chooses exactly what is exported and what is imported.

## Context & constraints

- `launchpad.json` is format v2 (profiles). It is **not** changed by this feature.
- `Profile` holds three categories of `ShortcutGroup`s; group names are unique per category, ignoring case; names
  are 1–100 characters (`ShortcutValidator.MaxNameLength`); URLs are http/https only (`UrlNormalizer`).
- `ILaunchpadSession` owns the document; profile operations go through `ChangeProfiles`, which raises
  `ProfilesChanged` and, when the profile in use changed, `ActiveProfileChanged`.
- **Category pages cache their groups** and re-read them only on `ActiveProfileChanged`: an import into the
  profile in use must raise it, or the next edit on a page would write its stale groups over the import.
- `IPathPicker`, `ILaunchpadDialogs`, `IUserNotifications` are the seams ViewModels are tested through
  (`FakePathPicker`, `ScriptedDialogs`, `RecordingNotifications`).
- The dialog host is capped at 640 × 640 (`MainWindow.axaml`): the tree must scroll inside the dialog.
- The launcher re-checks every target when it opens it, so an imported path that does not exist on this machine
  is reported at launch, not at import.

## Design

### Exchange format (version 1)

```json
{
  "format": "enigma-launchpad-shortcuts",
  "version": 1,
  "profile": "Work",
  "applications": [
    {
      "name": "Dev tools",
      "shortcuts": [
        { "name": "VS Code", "target": "C:\\Tools\\Code\\Code.exe", "arguments": "--new-window" }
      ]
    }
  ],
  "directories": [],
  "urls": [
    { "name": "Docs", "shortcuts": [ { "name": "Avalonia", "target": "https://docs.avaloniaui.net" } ] }
  ]
}
```

- camelCase, indented, UTF-8 without BOM, written atomically (`AtomicFile`); `arguments` only when present.
- No ids and no fold state: an export is an interchange document, not a copy of the store.
- `profile` is informational (the source profile's name, shown when importing).
- Reading: comments and trailing commas tolerated; a missing category list reads as empty.

### Reading rules

- **Refused as a whole** (with a reason for the user): not JSON, `format` missing or different, `version` other
  than 1, a `null` group / shortcut, a missing required field.
- **Left out and counted** (the rest is offered): a group whose name is empty or too long (with its shortcuts), a
  shortcut whose name is empty or too long, an application or directory whose path is empty or not fully
  qualified, a URL that `UrlNormalizer` refuses. Names are trimmed; URLs normalised; empty arguments dropped;
  arguments on directories and URLs dropped.
- Paths need **not** exist on this machine.
- Every group and shortcut read gets a new identity.

### Merge (import into a profile)

- Per category, in file order: a group whose name matches an existing group (trimmed, ignoring case) receives the
  shortcuts at its end; otherwise it is appended as a new, unfolded group.
- A shortcut identical to one already in the receiving group — same name and target (ignoring case) and same
  arguments — is skipped, so importing the same file twice adds nothing.
- Everything added gets a new identity, whatever the source.
- `Profile.Import(Profile source)` returns the merged profile; counts of what was added are the difference in
  `CountGroups()` / `CountShortcuts()`.

### Core API (`Enigma.Launchpad.Core.Exchange`)

- `IShortcutExchange` / `ShortcutExchange` (singleton, uses `IShortcutValidator` for names):
  - `Task ExportAsync(string path, Profile profile, CancellationToken)` — writes the profile's groups.
  - `Task<ShortcutImport> ReadAsync(string path, CancellationToken)` — throws `InvalidDataException` for a refused
    file; I/O errors propagate.
- `ShortcutImport(Profile Content, int SkippedEntries)` — the content as a profile (named after the file's
  `profile`, or "Imported"), and how many entries were left out.
- `LaunchpadData.ImportInto(Guid profileId, Profile source)`.

### Session

- `ILaunchpadSession.ImportIntoProfile(Guid id, Profile content)` — saved like every change; raises
  `ProfilesChanged`, and `ActiveProfileChanged` when the target is the profile in use (its value changed), so the
  category pages re-read their groups.

### Selection dialog

- `ShortcutSelectionViewModel` over a `Profile`: one node per non-empty category, then its groups, then their
  shortcuts (name + target). Three-state checkboxes: checking a node checks everything under it; a parent shows
  indeterminate when partly checked. Everything starts checked; categories open, groups folded.
- `HasSelection` (confirm enabled only then); a summary line ("2 groups · 5 shortcuts selected");
  `ToProfile()` returns the profile filtered to the selection — a group with shortcuts is kept when any of them is
  checked, an empty group when it is checked.
- `ILaunchpadDialogs.SelectShortcutsAsync(title, message, confirmText, content)` → the filtered profile, or
  `null` when cancelled.
- `ShortcutSelectionView`: message, a `TreeView` of checkboxes capped in height (scrolls), the summary.

### Profiles page

- Card menu: after Duplicate…, a separator, **Export…** (disabled for a profile with no group) and **Import…**.
- Export: selection dialog ("Export “Work”", confirm "Export") → save picker (`Work shortcuts.json`, JSON filter;
  invalid file-name characters replaced) → write → success info bar ("3 groups · 12 shortcuts exported to …").
- Import: open picker (JSON filter) → read → refused file: error info bar; nothing valid: warning → selection
  dialog ("Import into “Work”", the source profile and the count of left-out entries in its message, confirm
  "Import") → `ImportIntoProfile` → success info bar with what was added (or that everything was already there).
- `IPathPicker` gains `PickJsonFileToOpenAsync(title)` / `PickJsonFileToSaveAsync(title, suggestedFileName)`;
  `IUserNotifications` gains `ShowSuccess(title, message)`.
- I/O failures (`IOException`, `UnauthorizedAccessException`) show an error and change nothing.

## Phases

### PHASE01 — Export format, merge & import

**Status:** DONE

Steps:
1. `Enigma.Launchpad.Core.Exchange`: the format records, `IShortcutExchange` / `ShortcutExchange` (export, read,
   reading rules), `ShortcutImport`; registered in `AddLaunchpadCore`.
2. `Profile.Import(Profile source)` and `LaunchpadData.ImportInto(Guid, Profile)` with the merge rules.
3. `ILaunchpadSession.ImportIntoProfile` raising `ActiveProfileChanged` for the profile in use.
4. Tests: export JSON shape (no ids, no fold state, `arguments` only when set); round-trip; refused files (not
   JSON, wrong format, version 2, null entry); skipped entries counted (bad names, relative path, `ftp:` URL);
   URL normalised; fresh ids; merge into a same-named group, new groups appended, duplicates skipped, idempotent
   re-import, fresh ids on merge; session import persisted, `ActiveProfileChanged` only for the profile in use, a
   loaded category page shows the imported groups; DI registration.

Acceptance criteria:
- A profile exported and imported into an empty profile reproduces its groups and shortcuts (new identities).
- Importing into the profile in use updates the open category pages; a later edit there keeps the imported groups.
- 0 warnings; suite green.

### PHASE02 — Shortcut selection dialog

**Status:** TODO

Steps:
1. `ShortcutSelectionViewModel` + node ViewModel (three-state cascade up and down, defaults, summary,
   `HasSelection`, `ToProfile()`).
2. `ShortcutSelectionView` (TreeView of checkboxes, capped height) and `ILaunchpadDialogs.SelectShortcutsAsync`
   (confirm enabled with the selection); `ScriptedDialogs` answers it.
3. Tests: initial state; unchecking a shortcut makes its group and category indeterminate; checking a category
   checks everything; an empty group is kept only when checked; `ToProfile()` keeps order and only the selection;
   nothing selected → `HasSelection` false; headless — the tree shows the categories with checkboxes.

Acceptance criteria:
- The dialog shows the content as a checkbox tree whose parents follow their children, and returns exactly the
  checked part.
- 0 warnings; suite green.

### PHASE03 — Profiles page export & import

**Status:** TODO

Steps:
1. `IPathPicker` JSON open/save; `IUserNotifications.ShowSuccess`; fakes updated.
2. `ProfileItemViewModel.ExportCommand` / `ImportCommand`; `ProfilesPageViewModel` export and import flows.
3. Card menu items with icons.
4. Tests: export writes the selected part to the picked file and reports it; cancel at either step writes
   nothing; export disabled for an empty profile; import merges the selection into the chosen profile and reports
   the counts; refused file → error, nothing changed; nothing valid → warning; cancel → nothing changed; I/O error
   on write → error; headless — the card menu offers Export… and Import….
5. README: export / import and the format.

Acceptance criteria:
- A user can export any part of a profile to a JSON file and import any part of such a file into any profile,
  choosing with checkboxes both ways.
- 0 warnings; suite green.

## Out of scope

- Exporting several profiles in one file, or importing a file as a new profile in one step (create the profile,
  then import into it).
- Replacing a profile's content on import; renaming colliding groups.
- Exporting the fold state or settings; drag-and-drop of files; a custom file extension.

## Decisions taken autonomously

| Question                        | Chosen                                                    | Why                                                                   | Alternatives rejected                     |
|---------------------------------|-----------------------------------------------------------|-----------------------------------------------------------------------|-------------------------------------------|
| Unit of a file                  | One profile's content: categories → groups → shortcuts    | Matches "apps / directories / urls, subgroups and items" on a profile | All profiles in one file; one category    |
| Import target                   | The profile whose card was used, merged                   | Never destroys data; a new profile is one click away                  | Always a new profile; replace the content |
| Same-named group on import      | Receives the shortcuts                                    | Keeps group names unique per category, as the model requires          | "Tools (2)"; replace the group            |
| Identical shortcut on import    | Skipped                                                   | Re-importing a file is harmless                                       | Duplicated                                |
| Identities                      | Never exported; new ones on import                        | Ids are internal; two profiles must never share one                   | Export and reuse ids                      |
| JSON shape                      | Own versioned document with a `format` marker             | A stable interchange contract, independent of `launchpad.json`        | Reuse the store's v2 shape                |
| Fold state                      | Not exported                                              | A view preference, not content                                        | Export `isCollapsed`                      |
| Checkbox UI                     | One three-state tree dialog for both directions           | Same mental model both ways; parents summarise their children         | Flat list; group-level checkboxes only    |
| Initial selection               | Everything checked; categories open, groups folded        | The common case is "all"; large profiles stay readable                | Nothing checked                           |
| Entry points                    | Export… / Import… in each profile card's menu             | Both act on one profile; the menu already holds its actions           | Page header buttons; Settings page        |
| Broken or foreign file          | Refused as a whole with a reason                          | Nothing half-understood is imported                                   | Best-effort import                        |
| Invalid entries in a valid file | Left out and counted in the dialog                        | One bad URL does not block the rest; the user is told                 | Refuse the file; import them anyway       |
| Paths missing on this machine   | Accepted                                                  | Files move between machines; the launcher checks targets at launch    | Require existence                         |
| URL schemes                     | http / https only, normalised                             | Same rule as typed URLs; no shell handler smuggled in                 | Accept any scheme                         |
| Size limit on import            | None                                                      | A user-picked local file; `launchpad.json` has none either            | A fixed cap                               |
| Feedback                        | Info bar: success with counts, warning, error             | The app's existing notification channel                               | Message dialogs                           |
| Suggested file name             | `<Profile> shortcuts.json`, JSON filter                   | Says what it is; "a json format" as asked                             | A custom `.launchpad` extension           |
| Pages after importing in use    | `ActiveProfileChanged` raised; pages re-read their groups | The profile in use's value changed; one listener, already correct     | A new event                               |
| Empty profile                   | Export disabled                                           | Nothing to choose from                                                | Export an empty file                      |
| Phasing                         | Format & merge → selection dialog → page wiring           | Each phase reviewable on its own, the core first                      | One dev                                   |
