# FEATURE-1FA9 — Documents

## Summary

A fourth kind of group: **documents**. Any file, opened with the application the system associates with it, lives in
a documents group in any workspace. There is no documents page.

- **Core:**
  - `ShortcutKind.Document`, written `"document"` in `launchpad.json` and exports.
  - Validation: a full path to an existing file. A folder → "This is a folder, not a file. Add it to a folders group
    instead." The folder rule's message for a file now points at "an applications or a documents group".
  - Launching: a missing file → `TargetMissing`, "The document no longer exists: …". Otherwise it is shell-opened,
    with its own folder as the working directory; `Reveal` shows it in its folder.
  - Import: a document needs a fully qualified path (it need not exist); its arguments are dropped, and a command
    name is refused.
- **Desktop:**
  - Glyph `FileText` (`Glyphs.OfKind`).
  - `ShortcutIcons.CanFind(Document)`: the lookup goes through the program icon source, so the shell's image of the
    file.
  - **Form:**
    - label "File", a placeholder, and Browse… ("Choose a document") naming a nameless shortcut after the file,
      without its extension;
    - no arguments;
    - icon hint "The file's own icon…". `IconHint` is reworded through one `IconSource` ("program's", "website's",
      "file's"), and `KindGlyph` uses `Glyphs.OfKind`.
  - Dialog titles "Add document" / "Edit document".
  - **Tiles:**
    - *Copy path* quick action;
    - Show in folder and *Try to get icon* in the menu; the latter's question names "the file's own icon";
    - no Run with arguments, no Open in terminal.
  - **Workspace:**
    - *Documents* in the New group menu and among the empty workspace's buttons;
    - kind title "Documents", empty group text "No documents in this group yet. Add one with +.";
    - the selection tree uses the document glyph through `Glyphs`.
- **README:** intro, workspaces and open-workspace paragraphs (the kinds, Browse…, the tile buttons), and the export
  kinds list.

## Files

Modified:
- Core:
  - `Shortcuts/{ShortcutKind,Shortcut}.cs`
  - `Validation/ShortcutValidator.cs`
  - `Launching/ShortcutLauncher.cs`
  - `Exchange/ShortcutExchange.cs` (docs)
- Desktop:
  - `ViewModels/Glyphs.cs`
  - `Services/{ShortcutIcons,LaunchpadDialogs}.cs`
  - `ViewModels/Dialogs/ShortcutEditorViewModel.cs`
  - `ViewModels/Pages/{ShortcutViewModel,WorkspacePageViewModel}.cs`
  - `Views/Pages/WorkspacePageView.axaml`
- `README.md`
- Tests:
  - `ShortcutValidatorTests`: document accepted; folder, missing, relative and empty refused; the file-as-folder
    message.
  - `ShortcutLauncherTests`: shell-open in its folder without arguments; missing; reveal.
  - `JsonLaunchpadStoreTests`: a documents group round trip, and `"kind": "document"`.
  - `ShortcutExchangeTests`: read with full paths only and no arguments; kind written.
  - `ShortcutIconsTests`: the document's icon comes from the program icon source.
  - `ShortcutEditorViewModelTests`: fields per kind; browse names without the extension; hint, glyph, folder
    refused.
  - `WorkspacePageKindsTests`: new documents group, kind naming, document tile flags and actions, the Try to get icon
    question.
  - `WorkspacesPageViewTests`: four kinds in the menu and the empty state.
  - `TileActionsViewTests`: Copy path only, headless.
  - `Infrastructure/FakeShortcutIcons.cs`: documents can find icons, like the real service.

## Deviations & follow-ups

- No deviation from the plan.
- Not checked for real: no document was opened and no shell icon read for one on this machine. Opening goes through
  `IProcessStarter`, asserted by its start information. The icon goes through the existing
  `ShellProgramIconReader`, whose Windows test covers programs; any file works the same way in
  `SHCreateItemFromParsingName`.
- Follow-ups (plan: out of scope): "Open with…" another application; a recent-documents source.
- Fix budget: none used. The first build and test run were green.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx`: succeeded, 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx`: 877 total, 876 passed, 0 failed, 1 skipped (the Unix-only
  resolver test, on this Windows host). 17 tests added.

## Decisions taken at build time

| Question                                 | Chosen                                                   | Why                                                                     |
|------------------------------------------|----------------------------------------------------------|-------------------------------------------------------------------------|
| The folder rule's message for a file     | "Add it to an applications or a documents group instead" | A file can now be either                                                |
| Icon wording                             | One `IconSource` word per kind                           | Three kinds with icons found; one sentence, no repeated switches        |
| Kind glyph in the form                   | `Glyphs.OfKind`                                          | The form's own switch would have needed the fourth kind as well         |
| Document arguments in an import          | Dropped                                                  | The form has no arguments field for documents                           |
