# FEATURE-2DA2 — Tile quick actions

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-2da2-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-13-icons-descriptions-tile-actions

## Objective

Buttons inside the tiles:

- **Applications** — *Run with arguments…*: a form, pre-filled with the shortcut's default arguments, then the
  program runs with what was typed. A click on the tile still runs it as today.
- **Directories** — *Open in terminal* and *Copy path*.
- **URLs** — *Copy URL*.

The form's "Arguments" become **default arguments**: used by a click, and the starting value of Run with
arguments…, where they can be changed for one run without changing the shortcut.

## Context & constraints

- A tile is a `Button.tile` whose command opens the shortcut; the context menu holds the other actions. Drag starts
  from a press anywhere on a tile (`CategoryPageView`, tunnelling handlers).
- `IShortcutActions` opens, reveals and copies, reporting failures through `IUserNotifications`; `Open` minimizes
  the window when the setting says so. `IShortcutLauncher` (Core) builds `ProcessStartInfo`s and starts them
  through `IProcessStarter`, per platform (`DesktopPlatform`).
- `CopyTargetCommand` already copies the target.

## Design

### Action strip (PHASE01)

- Inside the tile, top-right of the icon row, a `StackPanel.tileActions` of small ghost buttons
  (`Button.tileAction`), hidden until the tile is hovered or holds keyboard focus (`:pointerover`,
  `:focus-within`), overlaying the end of the text with the hover background so nothing reflows.
- Directories: **Copy path**; URLs: **Copy URL** — both `CopyTargetCommand`. Every button has a tooltip and an
  automation name. A press on an action button never starts a drag.
- `ShortcutViewModel.CanCopyFromTile` (directories, URLs) and `CopyTargetLabel`.

### Run with arguments (PHASE02)

- `ILaunchpadDialogs.PromptArgumentsAsync(name, defaultArguments)` → the typed arguments, empty for none, `null`
  when cancelled; `ArgumentsDialogViewModel` / `ArgumentsDialogView` (one field, pre-filled, Run / Cancel).
- Page `RunWithArgumentsAsync` opens `shortcut with { Arguments = typed }` through `IShortcutActions.Open`; nothing
  is saved. `RunWithArgumentsCommand` / `CanRunWithArguments` (applications); tile button and context menu item.
- Form label **Default arguments (optional)** and a hint explaining both uses.

### Open in terminal (PHASE03)

- `IShortcutLauncher.OpenInTerminal(Shortcut)`: a missing folder → `TargetMissing`; candidates per platform, each
  started with the folder as its **working directory** (never on a command line), the next tried when one cannot be
  found (`Win32Exception`); none → `Failed`, worded for the user.
  - Windows: `wt.exe -d .`, then `cmd.exe`.
  - macOS: `open -a Terminal <path>` (argument list).
  - Linux: `x-terminal-emulator`, `gnome-terminal`, `konsole`, `xfce4-terminal`, `xterm`.
- `IShortcutActions.OpenInTerminal` reports and minimizes like `Open`. `OpenInTerminalCommand` /
  `CanOpenInTerminal` (directories); tile button and context menu item.

## Phases

### PHASE01 — Action strip & copy buttons

**Status:** DONE

Steps:
1. ViewModel properties; tile markup; styles.
2. `CategoryPageView`: no drag from an action button.
3. Tests: ViewModel (which kinds copy from the tile, labels); headless (directory and URL tiles have their copy
   button, application tiles none; the strip is hidden until hover/focus; clicking it copies and does not open).

Acceptance criteria:
- Directory tiles copy their path, URL tiles their address, from a button inside the tile.
- 0 warnings; suite green.

### PHASE02 — Run with arguments

**Status:** DONE

Steps:
1. Arguments dialog (ViewModel, view, `ILaunchpadDialogs` method).
2. Page method, ViewModel command; tile button and menu item for applications.
3. Editor label and hint.
4. Tests: dialog ViewModel; page (pre-filled with the defaults, typed arguments used, empty runs without, cancel
   runs nothing, the shortcut is not changed or saved); headless (button on application tiles only).

Acceptance criteria:
- Run with arguments… runs the program with the arguments typed, pre-filled with its defaults; a click still uses
  the defaults.
- 0 warnings; suite green.

### PHASE03 — Open in terminal

**Status:** DONE

Steps:
1. Core launcher: candidates per platform, fallback chain, results.
2. `IShortcutActions.OpenInTerminal`.
3. ViewModel command; tile button and menu item for directories.
4. Tests: start infos per platform (working directory, arguments, no shell parsing of the path); fallback to the
   next candidate; all missing → `Failed`; missing folder → `TargetMissing`; actions report and minimize;
   headless (button on directory tiles only).

Acceptance criteria:
- Open in terminal opens a terminal in the folder on Windows (Windows Terminal, else Command Prompt), macOS and
  Linux, or says why it could not.
- 0 warnings; suite green.

## Out of scope

- A setting to choose the terminal; running as administrator; copy buttons on application tiles.
- Remembering arguments typed in Run with arguments….

## Decisions taken autonomously

| Question                      | Chosen                                                                | Why                                                                         | Alternatives rejected                           |
|-------------------------------|-----------------------------------------------------------------------|-----------------------------------------------------------------------------|-------------------------------------------------|
| Click with stored arguments   | Unchanged: runs with them (now "default arguments")                   | "Clicking … that's what I want"; existing shortcuts keep working            | Click runs without arguments                    |
| Run with arguments… input     | Pre-filled with the defaults; one-off; empty = none                   | "Default value but not forced"                                              | Saving the typed arguments back                 |
| Buttons' visibility           | Shown on hover or keyboard focus, overlaying the text end             | Tiles are 248 px wide; permanent buttons would trim every name              | Always visible; a separate column               |
| Also in the context menu      | Yes (Run with arguments…, Open in terminal)                           | Keyboard path (Shift+F10) and discoverability                               | Tile buttons only                               |
| Terminal per platform         | Windows Terminal → cmd; Terminal.app; x-terminal-emulator → known ones | Works out of the box; the path is a working directory, never parsed        | A settings field for a custom command           |
| Minimize after launch         | Applies to Run with arguments and Open in terminal                    | Both open something outside the launchpad, like a click                     | Never minimize for them                         |
| Copy feedback                 | None, as "Copy target"                                                | Consistent with the existing copy                                           | A notification per copy                         |
| Copy on application tiles     | Not added                                                             | Not asked; the context menu has Copy target                                 | Copy path on every tile                         |
| Item shape                    | Three phases (strip + copies, arguments, terminal)                    | Each one reviewable commit; the strip lands first                           | One item per category                           |
