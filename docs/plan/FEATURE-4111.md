# FEATURE-4111 — Expand all selection tree items

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-4111-expand-selection-tree`
**Run:** vibe/2026-10-12-expand-selection-tree

## Objective

In the export and import dialogs, the checkbox tree always opens with **every item expanded** — categories and
groups — so every shortcut is visible without unfolding anything.

## Context & constraints

- Both dialogs are `ShortcutSelectionViewModel` (FEATURE-2B54 PHASE02), shown by
  `ILaunchpadDialogs.SelectShortcutsAsync`; one change covers export and import.
- `SelectionNodeViewModel.IsExpanded` is bound two-way to `TreeViewItem.IsExpanded`
  (`ShortcutSelectionView.axaml`). Today `ForCategory` opens the categories only; groups start folded.
- The tree is capped at 340 px and scrolls, so a fully open long list stays inside the dialog.
- A dialog builds a fresh tree each time it opens: nothing is remembered between openings.

## Design

- Every line starts expanded: `IsExpanded` defaults to `true`, and the categories-only special case in
  `ForCategory` goes. A shortcut line has nothing under it, so the flag has no effect there.
- The fold arrows keep working while the dialog is open; the next dialog starts fully open again.
- Doc comments describing "categories open, groups folded" are updated.

## Steps

1. `SelectionNodeViewModel`: `IsExpanded` starts `true`; `ForCategory` no longer sets it; docs updated.
2. `ShortcutSelectionViewModel` remarks updated.
3. Tests: the ViewModel test asserts every line starts expanded (categories, groups, shortcuts); the headless dialog
   test sees the shortcuts' checkboxes on opening.

## Acceptance criteria

- Opening the export or the import dialog shows every category, group and shortcut without unfolding anything.
- 0 warnings; suite green.

## Out of scope

- Removing the fold arrows, or remembering a fold between openings.
- Expand all / collapse all buttons.

## Decisions taken autonomously

| Question                         | Chosen                                                      | Why                                                                      | Alternatives rejected                  |
|----------------------------------|-------------------------------------------------------------|--------------------------------------------------------------------------|----------------------------------------|
| What "all the tree items" covers | Categories and groups — every line with lines under it      | Shortcuts are leaves; nothing else can be expanded                       | Categories only (today's behaviour)    |
| Meaning of "always"              | Every opening starts fully expanded; folding still possible | Honours the request on every opening without taking a control away       | Remove the fold arrows; remember folds |
| Mechanism                        | `IsExpanded` defaults to `true` on every node               | One rule for every level; no special case left in the builder            | Set it per level in `ForCategory`      |
| Large profiles                   | Rely on the existing 340 px cap and scrolling               | Already in place; a launchpad holds hundreds of shortcuts at most        | Fold when over a threshold             |
| Earlier records                  | FEATURE-2B54 plan and done docs left as written             | They record what was built then; this item records the change            | Rewrite them                           |
| Item type                        | `FEATURE`, single phase                                     | A deliberate behaviour change, not a defect; one small reviewable commit | `BUG`                                  |
