# FEATURE-059B — Shortcut descriptions

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-059b-shortcut-descriptions`
**Run:** vibe/2026-10-13-icons-descriptions-tile-actions

## Objective

Every shortcut — application, directory or URL — can carry an optional **description**, entered in its form and
shown on its tile under the icon row (the icon, the name and the subtitle).

## Context & constraints

- `Shortcut(Id, Name, Target, Arguments = null)` is a Core record serialized as-is in `launchpad.json`
  (format v2, camelCase, `RespectRequiredConstructorParameters`): an optional constructor parameter is optional in
  the file, so older files still load, and older builds skip the unknown field.
- The form is `ShortcutEditorViewModel` / `ShortcutEditorView`; errors show under a field once it was edited.
- Tiles are fixed-size buttons (`Button.tile`, 248 × 62) in a `WrapPanel`; a row's items stretch to the tallest.
- Exports (`ShortcutExchange`, format v1) carry name, target and arguments; an imported entry the form would refuse
  is left out and counted.
- `Profile.Import` decides "same shortcut" by name, target and arguments — unchanged.

## Design

- Core: `Shortcut` gains `string? Description = null` (after `Arguments`). `IShortcutValidator.ValidateDescription`
  — optional; at most `ShortcutValidator.MaxDescriptionLength` (200) characters once trimmed.
- Export: `ExportShortcut.Description` written when not blank; read trimmed, blank → none; one over the limit makes
  the entry refused (counted as skipped), like any field the form would refuse.
- Form: a **Description (optional)** field after the arguments, single line wrapping, with its error under it;
  `ToShortcut` stores it trimmed, or `null` when blank.
- Tile: `ShortcutViewModel.Description` / `HasDescription`; a second row under the icon row spanning the tile,
  at most two lines, trimmed with an ellipsis. The tile's tooltip adds the full description on its own line.
  `Button.tile` gets `MinHeight` instead of `Height`, and its content aligns to the top with padding that keeps a
  description-less tile exactly as tall and centred as today.

## Steps

1. Core: `Shortcut.Description`; `ValidateDescription` + constant; `ShortcutExchange` write/read.
2. Desktop: editor field + validation; `ShortcutEditorView` row.
3. Tile: ViewModel properties, tooltip, view row, styles (`TextBlock.tileDescription`, tile sizing).
4. Tests: validator; store round trip (with and without a description, an older file without the field);
   exchange round trip, blank and over-long descriptions; `Profile.Duplicate`/`Import` keep it; editor
   (validation, trimming, null when blank, existing description loaded); tile ViewModel; headless tile shows the
   description only when there is one.
5. README: the export paragraph mentions descriptions.

## Acceptance criteria

- The three forms have a Description field; a description over 200 characters is refused with a message.
- A tile with a description shows it under the icon row; one without looks as before.
- Descriptions survive a restart, a profile duplicate, and an export/import.
- 0 warnings; suite green.

## Out of scope

- Multi-line (newline) descriptions, rich text, search by description.
- Descriptions in the export/import selection tree.

## Decisions taken autonomously

| Question                       | Chosen                                                              | Why                                                                         | Alternatives rejected                        |
|--------------------------------|---------------------------------------------------------------------|-----------------------------------------------------------------------------|----------------------------------------------|
| Length limit                   | 200 characters, validated like the name                             | A tile shows two lines; a limit keeps the file and the tile sane            | No limit; a hard `MaxLength` with no message |
| Tile layout                    | Second row under the icon row, ≤ 2 lines, ellipsis; full text in tooltip | What the draft asks; rows of tiles stay aligned                         | Third line in the text column; fixed tall tiles |
| Exported                       | Yes, optional field, format stays v1                                | An export that drops user text loses data; older builds skip the field      | Leave descriptions out of exports            |
| Over-long imported description | Entry refused and counted                                           | The existing rule: an entry the form would refuse is left out               | Truncate silently                            |
| Item shape                     | Single phase                                                        | One reviewable commit across model, form and tile                           | Split model / UI                             |
