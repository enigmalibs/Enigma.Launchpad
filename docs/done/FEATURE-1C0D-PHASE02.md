# FEATURE-1C0D-PHASE02 — Workspaces page

## Summary

A **Workspaces** page, first in the rail and where the launchpad now opens, lists the workspaces of the profile in
use as cards. Each card shows its glyph, its title, its description and what it holds. A live search filters the
cards on title and description. From the page the user can create, edit, duplicate, reorder and delete workspaces,
and every change is saved. Opening a workspace comes with PHASE03.

- **Session:**
  - `ILaunchpadSession.Workspaces` and `WorkspacesChanged`.
  - `AddWorkspace`, `UpdateWorkspace` (keeps the groups), `DuplicateWorkspace` (right after the source, new
    identities through `Profile.Copy`), `DeleteWorkspace`, `CanMoveWorkspace` / `MoveWorkspace`.
  - All go through one `ChangeWorkspaces`: under the lock, then save, then the event outside the lock. A blank
    description is stored as none.
- **Page:**
  - `WorkspacesPageViewModel`: cards rebuilt on `WorkspacesChanged`, `ActiveProfileChanged` and appearing.
  - `SearchText` filters into `Workspaces`.
  - States: `HasWorkspaces`, `IsEmpty`, `HasNoMatch`, `NoMatchText`.
  - Commands: new, edit (saves nothing when unchanged), duplicate (name prompt proposing "X (copy)", checked
    against the other titles), delete (confirmed, naming what is lost), move.
  - `WorkspaceItemViewModel`: a snapshot card with "N groups · M shortcuts" / "Empty".
  - `WorkspacesPageView`: header with New workspace, search box with a magnifier, cards with a ⋯ menu, a no-match
    line, and an empty-state invitation.
- **Form:**
  - `ILaunchpadDialogs.EditWorkspaceAsync(existing, otherTitles)` → `WorkspaceDetails?`.
  - `WorkspaceEditorViewModel`: title and description, checked as typed, with errors shown once edited. The glyph
    is picked from `Glyphs.WorkspaceChoices` (44 glyphs). A glyph the list does not offer is kept, first, so an edit
    never changes it silently.
  - `WorkspaceEditorView`: wrap-panel `ListBox` with tooltips and automation names (`Glyphs.Describe`).
- **Glyphs:** `NewWorkspace` (Briefcase), `WorkspaceChoices`, `Describe`.
- **Rail and DI:** Workspaces first and selected at start; view transient, ViewModel singleton.
- **Bridge safety:** the category pages also re-read on `WorkspacesChanged`, so a workspace added in front or
  moved never leaves them writing into a stale "first workspace".
- **Styles:** `workspaceCard`, `workspaceSummary`, `glyphPicker`.
- **README:** a Workspaces paragraph.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/WorkspacesPageViewModel.cs`
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/WorkspaceItemViewModel.cs`
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/WorkspaceEditorViewModel.cs` (with `WorkspaceDetails`,
  `GlyphOption`)
- `src/Enigma.Launchpad.Desktop/Views/Pages/WorkspacesPageView.axaml` (+ `.cs`)
- `src/Enigma.Launchpad.Desktop/Views/Dialogs/WorkspaceEditorView.axaml` (+ `.cs`)
- Tests: `LaunchpadSessionWorkspaceTests`, `WorkspacesPageViewModelTests`, `WorkspaceEditorViewModelTests`,
  `WorkspacesPageViewTests` (headless).

Modified:
- `src/Enigma.Launchpad.Desktop/Services/LaunchpadSession.cs`, `Services/LaunchpadDialogs.cs`
- `src/Enigma.Launchpad.Desktop/ViewModels/Glyphs.cs`, `ViewModels/MainWindowViewModel.cs`,
  `ViewModels/Pages/CategoryPageViewModel.cs`
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs`, `Themes/Styles.axaml`
- `README.md`
- Tests:
  - `Infrastructure/ScriptedDialogs.cs` (workspace form, `FillWorkspace`).
  - `Infrastructure/WindowHarness.cs`: `LoadedPageAsync` opens the applications page from the workspaces.
  - `MainWindowTests`: the rail and the start page.
  - `TileActionsViewTests`: select by page type.
  - `CategoryPageViewTests`: its helper delegates to the harness.
  - `CompositionRootTests`: one page ViewModel.

## Deviations & follow-ups

- **Cards are not clickable yet.** The plan's "click or Enter opens it" belongs with the drill-in in PHASE03. A
  button that did nothing would be worse than a plain card meanwhile, so the cards are `Border`s with a menu.
- **Start page.** The launchpad opens on Workspaces now, as the main page. Headless tests that need a category page
  open it explicitly through the harness.
- **Glyph count.** 44 glyphs, not "about 30": all are relevant to a workspace's purpose, and the grid fits in the
  dialog's height (`MaxHeight` 196, scrolls beyond).
- Fix budget: 1 cycle. `CategoryPageViewTests` had its own copy of the page-loading helper, which waited for the old
  start page.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx`: succeeded, 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx`: 841 total, 840 passed, 0 failed, 1 skipped (the Unix-only
  resolver test, on this Windows host). 61 tests added.

## Decisions taken at build time

| Question                              | Chosen                                                      | Why                                                                         |
|---------------------------------------|-------------------------------------------------------------|-----------------------------------------------------------------------------|
| Where duplicates' names come from     | `ProfilesPageViewModel.CopyName`, reused                    | Same "X (copy)", "X (copy 2)" rule as profiles                              |
| Search matching                       | Current-culture, case-insensitive `Contains`, trimmed      | Titles are user text; trimming forgives a stray space                       |
| Search with no match                  | A line under the box; the box and New workspace stay        | The user can correct the search or create what they looked for              |
| A glyph outside the offered list      | Kept and offered first                                      | Editing a title must not change an icon from an import or a hand edit       |
| Edit with no change                   | Nothing saved                                               | Same as renaming a group to its own name                                    |
| Category pages during this phase      | Also follow `WorkspacesChanged`                             | They read the first workspace, which a reorder or an add can change         |
