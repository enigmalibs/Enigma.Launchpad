# FEATURE-0BB1-PHASE01 — Group width in model & files

**Item:** FEATURE-0BB1 — Group columns
**Phase:** PHASE01 — Group width in model & files
**Branch:** `feature/feature-0bb1-phase01-group-width-model`
**Run:** vibe/2026-10-15-group-columns

## Summary

A group now has a width: the full line (the default), two thirds or one third. This phase keeps the width
everywhere a group is stored or copied. Nothing on screen changes yet: PHASE02 draws the columns and adds the menu.

- **The model:**
  - `GroupWidth { Full, TwoThirds, OneThird }`, with `Full` the zero value;
  - `ShortcutGroup` gains `Width = GroupWidth.Full` as its last parameter;
  - the `Columns` extension property maps a width to how many of a line's three columns it takes (3, 2, 1).
- **`launchpad.json`:**
  - each group is written with `"width": "full" | "twoThirds" | "oneThird"`;
  - a file without the field reads every group at full width, and version 1 and 2 files do too;
  - the format stays version 3. An older build skips the unknown member;
  - an unknown width makes the file unreadable, as an unknown kind does: it is moved aside, with nothing lost.
- **Export files:**
  - a group's `width` is written only when it is not the full width, so a file without columns is unchanged;
  - on import, the width is read ignoring case. A missing, empty or unknown value reads as full width, and nothing
    is skipped for it;
  - the format stays version 2.
- **Import merge (`Profile.Import`):**
  - a group the import adds keeps its width;
  - a group merged into an existing one keeps the existing group's width, like its fold state.
  - `Profile.Duplicate` already copied the width (it copies with `with`); a test now covers it.
- **The open workspace:** `GroupViewModel` reads the width and writes it back in `ToModel()`. Without this, the next
  save of a page would reset every width to full.

## Files touched

- Created:
  - `src/Enigma.Launchpad.Core/Shortcuts/GroupWidth.cs`
  - `src/Enigma.Launchpad.Core/Shortcuts/GroupWidthExtensions.cs`
- Modified:
  - `src/Enigma.Launchpad.Core/Shortcuts/ShortcutGroup.cs` — the `Width` parameter.
  - `src/Enigma.Launchpad.Core/Shortcuts/Profile.cs` — `Import` keeps the width of a group it adds.
  - `src/Enigma.Launchpad.Core/Exchange/ShortcutExchange.cs` — `ExportGroup.Width`, `WidthName`, `ReadWidth`.
  - `src/Enigma.Launchpad.Desktop/ViewModels/Pages/GroupViewModel.cs` — `Width`, read and written back.
  - `README.md` — the *Export files* section lists the widths and how an unknown one is read.
  - Tests:
    - `tests/Enigma.Launchpad.Core.UnitTests/Persistence/JsonLaunchpadStoreTests.cs`
    - `tests/Enigma.Launchpad.Core.UnitTests/Shortcuts/ProfileTests.cs`
    - `tests/Enigma.Launchpad.Core.UnitTests/Exchange/ShortcutExchangeTests.cs`
    - `tests/Enigma.Launchpad.Desktop.UnitTests/WorkspacePageGroupsTests.cs`

## Decisions taken at build time

| Question                                  | Chosen                                                                          | Why                                                                                                                   | Alternatives rejected                             |
|-------------------------------------------|---------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------|---------------------------------------------------|
| Where the width-to-columns mapping lives  | A C# 14 extension property, `GroupWidthExtensions.Columns`, with `LineColumns`  | House extension convention; it is a reading of the enum, not part of the record                                       | A method on `ShortcutGroup`; enum values = spans  |
| The view model in this phase              | `GroupViewModel` carries `Width` through (read-only)                            | `ToModel()` rebuilt the group from its parts; without this, a page save would drop widths set by import or by hand    | Waiting for PHASE02                               |
| An integer `width` in `launchpad.json`    | Refused, like an integer `kind`                                                 | The store's enum converter refuses integers for every enum; one rule                                                  | Accepting integers for widths only                |

## Deviations & follow-ups

- **One step taken from PHASE02.** `GroupViewModel` already reads and writes the width: see *Decisions*. Making it
  settable, and the `Is…` flags, stay in PHASE02.
- **Fix budget:** 0 cycles. The first build and test run was green.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx`: **925 total, 924 passed, 0 failed, 1 skipped** (the Unix-only
  resolver test on this Windows host). The baseline was 912.
- Tests added or extended:
  - `JsonLaunchpadStoreTests`:
    - the round trip and the written JSON cover all three widths;
    - a group without a width reads as full;
    - version 2 groups read as full;
    - `"width": "half"` and `"width": 2` move the file aside.
  - `ProfileTests`:
    - `Duplicate` and an import into an empty profile keep widths (the description now includes them);
    - a merge keeps the existing group's width and gives an added group its own.
  - `ShortcutExchangeTests`:
    - only widths that are not full are written;
    - the export/read round trip keeps widths;
    - the width is read ignoring case, and full is used for a missing, empty, null or unknown value, with nothing
      skipped.
  - `WorkspacePageGroupsTests.Saving_KeepsEachGroupsWidth`.
