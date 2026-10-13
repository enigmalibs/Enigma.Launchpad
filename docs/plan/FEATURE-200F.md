# FEATURE-200F — Linux installer

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-200f-linux-installer`
**Run:** vibe/2026-10-13-linux-installer-release

## Objective

Make the launchpad installable on Linux with one command, the way Enigma.GitClient is: the application in
`~/.local/share`, a launcher symlink in `~/.local/bin`, a desktop entry named **Enigma Launchpad** with its icon in
the hicolor theme — and an uninstaller that takes it all away again, leaving the user's shortcuts alone.

## Context & constraints

- **The reference** is Enigma.GitClient's `packaging/linux/` (its FEATURE-B4C0, as it stands today): `install.sh`
  (bash, `set -euo pipefail`, refuses root, `--from DIR`, `--framework-dependent`, `--rid RID`, `--help`; publishes
  the app in Release — self-contained by default — into a staging directory beside the install directory and swaps
  it in, rolling back in the EXIT trap; symlinks the apphost; installs the hicolor icons; writes the desktop entry
  from a `.desktop.in` template by substituting `@EXEC@` in bash, not sed; pins `DOTNET_ROOT` in `Exec` when a
  framework-dependent tree's runtime is not where an apphost looks; refreshes the desktop and icon caches
  best-effort; notes when the bin directory is off `PATH`), `uninstall.sh` (removes exactly those four locations,
  never the user's configuration, refuses to remove a directory whose name is not the app id, leaves alone a
  symlink pointing elsewhere), and `icons/<app id>-<N>.png`.
- The app is `src/Enigma.Launchpad.Desktop`, apphost `Enigma.Launchpad.Desktop`, window title "Enigma Launchpad".
  It takes no command-line argument of its own.
- The user's data — `launchpad.json`, `settings.json`, the `icons` cache — lives in
  `$XDG_CONFIG_HOME/Enigma.Launchpad` (`~/.config` by default; `Environment.SpecialFolder.ApplicationData`).
- `Assets/app.ico` holds seven PNG frames: 16, 24, 32, 48, 64, 128 and 256 px.
- `.gitattributes` already declares `*.sh text eol=lf` and `*.png binary`.
- The README's *Requirements* names the SDK only, and has no install section.
- No Linux machine here; an Arch Linux WSL distro is available, without `dotnet` and without
  `desktop-file-validate`. `dotnet publish -r linux-x64` runs on Windows.

## Steps

1. `packaging/linux/icons/enigma-launchpad-<N>.png` — the seven frames of `app.ico`, byte copies.
2. `packaging/linux/enigma-launchpad.desktop.in` — `Type=Application`, `Version=1.5`, `Name=Enigma Launchpad`,
   `GenericName=Shortcut Launcher`, a `Comment`, `Exec=@EXEC@`, `Icon=enigma-launchpad`, `Terminal=false`,
   `Categories=Utility;`, `Keywords=`, `StartupNotify=true`, `StartupWMClass=Enigma.Launchpad.Desktop`.
3. `packaging/linux/install.sh` — GitClient's installer with this application's id, apphost, project, sizes,
   icon count and wording; no git requirement.
4. `packaging/linux/uninstall.sh` — likewise; it names `~/.config/Enigma.Launchpad` as left untouched. No former
   launcher name to recognise.
5. Both scripts mode 755 in the index.
6. `README.md` — an *Install on Linux* section (what goes where, the options, the uninstaller), and the
   *Requirements* corrected for the installer's default (the SDK builds; the runtime is bundled).

## Acceptance criteria

- `bash -n` parses both scripts.
- The desktop entry carries only keys and categories the freedesktop specification registers, `Name` "Enigma
  Launchpad", and an `Exec` that is the absolute installed apphost, quoted.
- An install from a published `linux-x64` tree into a scratch XDG root (WSL): the install directory holds the
  apphost with its executable bit, the symlink resolves to it, the seven icons are in their hicolor directories, the
  entry is written; the installed application starts from a bare environment (`env -i`).
- The build path (no `--from`) publishes a self-contained tree and installs it.
- Running the installer again replaces the installation and leaves no stale file or staging directory.
- `uninstall.sh` removes the four locations, leaves a scratch configuration directory untouched, and reports
  "nothing to remove" when run again.
- Neither script uses `sudo` or writes outside the XDG directories.
- Build clean with zero warnings; the whole suite green.

## Out of scope

- `.deb`, `.rpm`, Flatpak, AppImage, a system-wide install.
- Windows and macOS packaging.
- Translations of the desktop entry.

## Decisions taken autonomously

| Question                       | Chosen                                                          | Why                                                                                                   | Alternatives rejected                               |
|--------------------------------|-----------------------------------------------------------------|-------------------------------------------------------------------------------------------------------|-----------------------------------------------------|
| "Like done in Enigma.GitClient" | Its current scripts, template and icon layout, adapted          | The draft asks for exactly that, and those scripts carry lessons (the runtime lookup, the staged swap) already paid for | A new design; `.deb` / AppImage packaging          |
| Publish default                | Self-contained, `--framework-dependent` as the opt-in           | A framework-dependent launcher entry silently does nothing when the runtime is in `~/.dotnet`         | Framework-dependent by default                      |
| Application id                 | `enigma-launchpad`                                              | Mirrors `enigma-git-client` and the window title                                                      | `enigma.launchpad`; a reverse-DNS id (none exists)  |
| `Name` / `GenericName`         | "Enigma Launchpad" / "Shortcut Launcher"                        | The window title; the freedesktop spec wants a generic name ("Web Browser"), and nothing asks otherwise | Both "Enigma Launchpad" (GitClient's draft-specific choice) |
| `Categories`                   | `Utility;`                                                      | The registered main category that fits a launcher                                                     | `System;`; `Utility;Core;`                          |
| Icon sizes                     | All seven frames, 16 to 256 px                                  | `app.ico` carries a 24 px frame and hicolor has the directory; a panel at 24 px then gets the drawn frame, not a scaled one | GitClient's six                                     |
| `Exec`                         | The absolute installed apphost, quoted, no field code           | Does not depend on `~/.local/bin` being on the launcher's `PATH`; the app takes no file argument      | `Exec=enigma-launchpad`; `%U`                       |
| How it is verified             | `bash -n`, entry checked against the spec, real install / reinstall / uninstall in WSL against a scratch XDG root, a start from `env -i` | The Definition of Done for scripts, without a shell-test framework the project does not have | A `bats` suite; installing `desktop-file-utils` system-wide (needs root) |
