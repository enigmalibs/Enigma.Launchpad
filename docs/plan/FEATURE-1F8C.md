# FEATURE-1F8C — Commands as application targets

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-1f8c-command-targets`
**Run:** vibe/2026-10-14-workspaces-documents-commands

## Objective

An application shortcut's target can be a **full program path** (as today) **or a command** that the system finds
through the `PATH` environment variable — `notepad`, `code`, `git`, `firefox` — so a tool installed on the `PATH` is
usable without knowing where it lives.

## Context & constraints

- `ShortcutValidator.ValidateApplication` accepts only a fully qualified path to an existing file (or a `.app` bundle
  on macOS). `TryReadPath` refuses anything relative with "Enter a full path, starting from the drive or the root."
- `ShortcutLauncher.CheckTarget` refuses an application whose file no longer exists; `BuildLaunchStartInfo` starts
  it through the shell, with its own folder as the working directory. `Reveal` shows the file in its folder.
- `ShortcutExchange.ReadTarget` accepts a fully qualified path for applications and directories (it need not exist).
- `ShortcutIcons.FindAsync(Application, target)` asks `IProgramIcons` for the icon of the target path.
- `ShortcutEditorViewModel`: target label "Program", placeholder `C:\Program Files\App\app.exe` / `/usr/bin/app`.
- Core holds no Avalonia reference; seams are interfaces registered in `AddLaunchpadCore`.

## Design

### Command resolution (Core, `Launching`)

- `ICommandResolver`:
  - `string? Resolve(string command)` — the full path of the program a command names, or `null`.
- `PathCommandResolver` — the default implementation.
  - Searches the directories of `PATH`, in order. The **current directory is never searched**, so a program
    planted in a working directory cannot shadow a real one.
  - **Windows:**
    - The process's `PATH` is followed by the machine and user `PATH` read fresh (`EnvironmentVariableTarget`), so a
      tool installed while the launchpad runs is found. Duplicates are skipped.
    - A name without one of the `PATHEXT` extensions is tried with each of them (default
      `.COM;.EXE;.BAT;.CMD`); a name that has one is tried as it is.
  - **Elsewhere:** only a file with an execute permission bit counts.
  - Empty and relative `PATH` entries are skipped. Environment variables in Windows entries are expanded.
- `PathCommandResolver.IsCommandName(string)`:
  - A trimmed, non-empty text that is not fully qualified, holds no directory separator (`/`, and `\` on
    Windows), and no character a file name cannot hold.
- An internal constructor takes the directories, extensions and platform, for tests.
- Registered as a singleton in `AddLaunchpadCore`.

### Validation

- `ShortcutValidator` gets a public constructor taking `ICommandResolver`; the parameterless one keeps using
  `PathCommandResolver`.
- Application target rules:
  - Empty → "Enter a path or a command."
  - Fully qualified → today's rules (an existing file, or a bundle on macOS).
  - A command name that resolves → **valid, stored as typed (trimmed)**, never replaced by the resolved path, so it
    follows `PATH` changes and travels to other machines.
  - A command name that does not resolve → "No program called “x” was found on the PATH." When the text contains a
    space, add "Arguments go in Default arguments."
  - Anything else (a relative path) → "Enter a full path, or a command found on the PATH."
- Directory targets keep refusing relative paths, as today.

### Launching

- `ShortcutLauncher` takes `ICommandResolver`.
- An application whose target is a command name is resolved first:
  - Not found → `TargetMissing`, "No program called “x” was found on the PATH."
  - Found → the resolved path is started through the shell, with the user's **home folder** as working directory —
    as a desktop launcher or a terminal would. Default arguments apply as today.
- `Reveal` shows the resolved program.
- A full-path target behaves exactly as today.

### Import

- `ShortcutExchange.ReadTarget` accepts a command name for an application, not resolved: the file may come from
  another machine, and the launcher checks every target when it opens it.

### Desktop

- `ShortcutIcons` takes `ICommandResolver`. `FindAsync` / `RefreshAsync` resolve a command — off the UI thread — and
  look up the resolved program's icon; an unresolved command finds none.
- `ShortcutEditorViewModel`:
  - Target label "Program or command".
  - Placeholder `C:\Program Files\App\app.exe or notepad` (Windows) / `/usr/bin/app or firefox`.
  - A hint under the field for applications: "The full path of a program, or a command found on the PATH, such as
    notepad." (`firefox` outside Windows.)
- README: the shortcut paragraph mentions commands.

## Phases

Single phase.

**Status:** TODO

Steps:
1. Core: `ICommandResolver`, `PathCommandResolver`, DI registration.
2. Validator and launcher changes; exchange `ReadTarget`.
3. Desktop: `ShortcutIcons` resolution; editor label, placeholder and hint.
4. Tests:
   - Resolver: temp-directory `PATH` — found, `PATHEXT` tried, explicit extension, not found, relative and empty
     entries skipped, `IsCommandName` cases.
   - Validator (fake resolver): command accepted and stored as typed; unresolved; relative path refused; a space
     hint.
   - Launcher: a command starts the resolved path in the home folder with its arguments; an unresolved command is
     `TargetMissing` and starts nothing; reveal resolves.
   - Exchange: a command is imported.
   - Icons: a command's icon comes from the resolved path.
   - Editor: label and hint.
   - Core DI registration.
5. README.

Acceptance criteria:
- Typing `notepad` (or any program on the `PATH`) as an application's target is accepted, stored as `notepad`, and
  opens that program; one that isn't on the `PATH` is refused in the form and reported when launched.
- Full-path targets behave exactly as before.
- 0 warnings; suite green.

## Out of scope

- The Windows *App Paths* registry (names the Run dialog knows but the `PATH` does not).
- Splitting a typed command line into a program and arguments.
- Environment variables inside full paths (`%LOCALAPPDATA%\…`).

## Decisions taken autonomously

| Question                          | Chosen                                                                | Why                                                                              | Alternatives rejected                                     |
|-----------------------------------|-----------------------------------------------------------------------|----------------------------------------------------------------------------------|-----------------------------------------------------------|
| What is stored for a command      | The command as typed                                                  | Follows `PATH` changes and other machines; it is what the user asked for          | Storing the resolved full path                            |
| Where commands are looked up      | `PATH` (+`PATHEXT` on Windows), never the current directory           | "Recognized because in user/system environment variables"; no hijack by CWD      | Shell-executing the bare name; App Paths registry          |
| Windows `PATH` freshness          | Process + current machine + user `PATH`                               | A tool installed while the launchpad runs is found without a restart             | Process `PATH` only                                       |
| Working directory of a command    | The user's home folder                                                | What desktop launchers and terminals use for a bare command                      | The resolved program's folder                             |
| Arguments typed in the target     | Refused, with a hint to use Default arguments                        | The field holds one program; arguments already have their own field              | Splitting the text on the first space                     |
| Imported commands                 | Accepted unresolved                                                   | Same rule as paths: the file may come from another machine                       | Resolving on import                                       |
| Item shape                        | One phase                                                             | One seam, small changes across validator, launcher, icons, form                  | Separate Core and Desktop phases                          |
