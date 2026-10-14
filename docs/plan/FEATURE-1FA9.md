# FEATURE-1FA9 — Documents

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-1fa9-documents`
**Run:** vibe/2026-10-14-workspaces-documents-commands

## Objective

Shortcuts to **documents** — any file, opened with the application the system associates with it — as a fourth
kind of group in a workspace. There is no documents page: documents live in workspaces (FEATURE-1C0D).

## Context & constraints

- Built after FEATURE-1C0D: groups carry a `ShortcutKind`, a workspace's **New group** menu lists the kinds, and tiles
  take their kind from their group.
- `ShortcutKind` is serialized as a camelCase string in `launchpad.json` (v3) and in exports (v2).
- `ShortcutLauncher` opens everything through the shell; a document handed to the shell opens in its associated
  application.
- `ShellProgramIconReader` (Windows) asks the shell for a file's image, which works for any file — a document gets
  its type's (or its own) icon.

## Design

- `ShortcutKind.Document` — "A file, opened with the application the system associates with it."
- **Validation:**
  - A full path to an existing file. Relative or empty paths get the existing messages.
  - A folder → "This is a folder, not a file. Add it to a folders group instead."
  - The application rule for a folder keeps pointing at folders groups.
- **Launching:**
  - A missing file → `TargetMissing`, "The document no longer exists: …".
  - Otherwise the file is started through the shell, with its own folder as the working directory, so the
    application's File › Open starts there.
  - `Reveal` shows it in its folder.
- **Import / export:**
  - `"kind": "document"`. The target must be a fully qualified path; it need not exist.
  - No arguments.
- **Icons:**
  - `IShortcutIcons.CanFind(Document)` is true, and the lookup goes through the program icon source (the shell's
    image of the file).
  - A missing file, or a platform without a reader, finds none, as for programs.
- **Form** (`ShortcutEditorViewModel`):
  - Label "File"; placeholder `C:\Users\you\Documents\report.docx` / `/home/you/Documents/report.pdf`.
  - **Browse…** picks a file ("Choose a document") and names a nameless shortcut after it, without its extension.
  - No arguments. Icon hint "The file's own icon, found automatically." — the wording of `IconHint` and of
    *Try to get icon*'s question uses "file".
  - Dialog titles "Add document" / "Edit document".
- **Tiles:**
  - Glyph `FileText`.
  - Quick action **Copy path**.
  - Context menu: Open, Edit…, Try to get icon, Copy target, Show in folder, moves, Delete…
  - No Run with arguments, no Open in terminal.
- **Workspace:**
  - The New group menu gains **Documents**. The group card glyph and the empty text read "No documents in this group
    yet. Add one with +."
- **Selection dialog:** document groups and shortcuts appear with the document glyph.
- README: documents in the shortcut and workspace paragraphs, and the export example.

## Phases

Single phase.

**Status:** DONE

Steps:
1. Core: kind, validator, launcher, exchange.
2. Desktop: icons, form, tile flags and glyph, New group menu, group glyph and empty text, selection glyph, dialog
   titles.
3. Tests:
   - validator (file accepted; folder, relative and missing refused);
   - launcher (opens through the shell in its folder; missing → `TargetMissing`; reveal);
   - storage and export round trip of a document group;
   - icons can be found;
   - form (label, browse names it, no arguments);
   - workspace (new documents group, tile flags);
   - headless (Copy path on a document tile).
4. README.

Acceptance criteria:
- A workspace can hold a group of documents; clicking a document opens it in its default application, and its
  tile copies its path and shows it in its folder.
- Documents survive save, reload, export and import.
- 0 warnings; suite green.

## Out of scope

- "Open with…" another application; recent-documents integration; document previews beyond the shell icon.
- A documents page (explicitly not wanted).

## Decisions taken autonomously

| Question                       | Chosen                                                       | Why                                                                         | Alternatives rejected                       |
|--------------------------------|--------------------------------------------------------------|-----------------------------------------------------------------------------|---------------------------------------------|
| What a document is             | Any existing file, opened with its default application       | "Manage documents" — the shell's association is the natural opener          | A list of allowed extensions                |
| Working directory              | The document's folder                                        | The opening application's File › Open starts beside it                      | No working directory                        |
| Icon                           | Found from the file via the shell, like programs             | The existing reader already returns a file's image on Windows               | Glyph only                                  |
| Name from Browse…              | The file name without its extension                          | Consistent with applications; the full path is in the subtitle               | The full file name                          |
| Tile quick actions             | Copy path                                                    | Same as folders; Show in folder stays in the menu                           | Show in folder on the tile too              |
| Item shape                     | Separate item after workspaces, one phase                    | Its own feature in the request; needs typed groups to have a place          | A phase of FEATURE-1C0D                     |
