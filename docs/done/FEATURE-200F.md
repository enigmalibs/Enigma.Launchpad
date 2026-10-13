# FEATURE-200F — Linux installer

**Item:** FEATURE-200F — Linux installer
**Branch:** `feature/feature-200f-linux-installer`
**Run:** vibe/2026-10-13-linux-installer-release

## Summary

The launchpad installs on Linux with one command, for the current user, after Enigma.GitClient's
`packaging/linux/`.

- `packaging/linux/install.sh`:
  - Builds `src/Enigma.Launchpad.Desktop` in Release for this machine's runtime identifier. The .NET runtime is
    bundled by default.
  - Options: `--framework-dependent`, `--from DIR` (an already-published tree) and `--rid RID`.
  - It refuses root, and refuses empty or relative XDG paths.
  - It publishes into a staging directory beside the install directory and swaps it in. A failure puts the old
    installation back.
  - It then:
    - links `~/.local/bin/enigma-launchpad` to the apphost;
    - installs the seven hicolor icons;
    - writes the desktop entry from its template, substituting `@EXEC@` in bash with the quoted absolute apphost.
      `DOTNET_ROOT` is pinned in the entry when a framework-dependent tree's runtime is where no apphost looks;
    - refreshes the desktop and icon caches, best-effort;
    - notes when the bin directory is off `PATH`.
- `packaging/linux/uninstall.sh`:
  - Removes the application directory, the symlink (only if it points at this installation), the desktop entry and
    the icons.
  - It refuses to remove a directory not named `enigma-launchpad`.
  - It never touches `~/.config/Enigma.Launchpad`.
- `packaging/linux/enigma-launchpad.desktop.in`:
  - `Name=Enigma Launchpad`, `GenericName=Shortcut Launcher`, `Categories=Utility;`.
  - Keywords, `StartupNotify`, `StartupWMClass=Enigma.Launchpad.Desktop`.
- `packaging/linux/icons/enigma-launchpad-<N>.png`: the seven PNG frames of `Assets/app.ico` (16, 24, 32, 48, 64,
  128, 256), byte copies.
- Both scripts are committed with mode 755.
- README:
  - an *Install on Linux* section: what goes where, the options, the sizes, the uninstaller;
  - *Requirements* says the SDK builds and the installer bundles the runtime;
  - *Solution layout* gains `packaging/linux`.

## Files / modules touched

**Created**

- `packaging/linux/install.sh`
- `packaging/linux/uninstall.sh`
- `packaging/linux/enigma-launchpad.desktop.in`
- `packaging/linux/icons/enigma-launchpad-{16,24,32,48,64,128,256}.png`

**Modified**

- `README.md` — *Requirements*, *Install on Linux*, *Solution layout*.

## Deviations & follow-ups

- **`desktop-file-validate` is not available.** It is missing on Windows and in the WSL distro, and installing
  `desktop-file-utils` there needs root.
  - The entry was checked against the Desktop Entry Specification 1.5 by inspection instead:
    - every key is registered;
    - `Utility` is a registered main category;
    - the lists end with `;`;
    - `Exec` is quoted.
  - It carries the same key set as Enigma.GitClient's entry, which passed the validator there.
- **The start from a bare environment was verified only up to Avalonia's Skia initialization.**
  - The installed symlink was started with `env -i` and the bundled runtime ran `Program.Main`.
  - It then aborted in `SkiaSharp` because the WSL distro has no `libfontconfig.so.1`: it is a minimal Arch image,
    `fontconfig` is not installed, and installing it needs root.
  - A desktop installation always carries fontconfig.
  - This proves what the check exists for: the apphost finds its runtime with no `DOTNET_ROOT` and no `PATH`.
  - It does not prove that a window opens. **Follow-up:** after installing on a real Linux desktop, check that the
    launcher entry opens the window.
- **The build path (no `--from`) was run from Git Bash on Windows.**
  - It published a self-contained `linux-x64` tree (an ELF apphost, `libcoreclr.so`, `libSkiaSharp.so`, 115 MB)
    and installed it into a scratch XDG root.
  - On that filesystem `ln -s` makes a copy, so the symlink checks come from the WSL run.
- **The `--framework-dependent` / `DOTNET_ROOT` pinning branch was not exercised.** WSL has no .NET runtime. That
  code is Enigma.GitClient's, verbatim.
- **`StartupWMClass` was not verified against a running window** (no `xprop`). It is Avalonia's default X11 class,
  the entry assembly's name, as in Enigma.GitClient.
- No `FORMER_APP_EXE` in the uninstaller: unlike Enigma.GitClient, this launcher has never had another name.
- Fix budget: none used. The first build and test run were green.
- Documentation sweep: README, as above. No other prose document names the installer.
- Line endings: every new text file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 710 passed, 0 failed, 0 skipped.
- `bash -n` on both scripts, in Git Bash and in WSL — clean.
- WSL (Arch, x86_64), scratch XDG root `/tmp/elp-verify`, published tree from
  `dotnet publish -c Release -r linux-x64 --self-contained true` passed with `--from`:
  - Refused runs: an unknown option, a missing `--from` directory and a relative `XDG_DATA_HOME` each fail and
    write nothing.
  - Install: exit 0. The apphost is executable, the symlink resolves to it, 7/7 icons are in place, and the entry is
    written with `Exec="/tmp/elp-verify/share/enigma-launchpad/Enigma.Launchpad.Desktop"` and no placeholder left.
  - Reinstall: exit 0. A planted stale file is gone, and no staging or `.previous` directory is left.
  - Uninstall: exit 0. All four locations are removed, and the sentinel `launchpad.json` in the scratch config
    directory is untouched.
  - A second uninstall reports "Nothing to remove".
  - A symlink of the same name pointing elsewhere is left alone.
- `sudo` appears only in comments.

## Decisions taken at build time

| Question                           | Chosen                                                                                                  | Why                                                                                 |
|------------------------------------|---------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------|
| Sizes quoted in `--help`/README    | About 115 MB self-contained, 35 MB framework-dependent                                                  | Measured from this app's `linux-x64` publishes, not copied from Enigma.GitClient    |
| Closing hint                       | `Run it from a terminal with: enigma-launchpad`                                                         | The launchpad takes no argument, unlike the git client's `<repository>`             |
| The git note at the end            | Dropped                                                                                                 | The launchpad does not need git                                                     |
| `Comment` and `Keywords`           | "Your applications, folders and websites, one click away"; the three kinds, shortcut, launcher, profile | What the README's first line says; the words someone types into a launcher's search |
| README layout table                | A `packaging/linux` row                                                                                 | The table lists every top-level area of the repository                              |
| How the published tree reached WSL | Published on Windows, passed with `--from`                                                              | WSL has no SDK; `--from` is the installer's own path for a tree published elsewhere |
