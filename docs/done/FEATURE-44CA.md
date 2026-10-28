# FEATURE-44CA — New application icon

**Item:** FEATURE-44CA — New application icon
**Branch:** `feature/feature-44ca-new-app-icon`
**Run:** feature/2026-10-28-new-icons-release

## Summary

The application's icon is now the new artwork: a rocket in the Soft palette on a dark rounded square. It replaces
Phosphor `RocketLaunch` on a blue square everywhere the old one was:

- `Assets/app.ico`: the new `.ico`, 10 frames (16, 20, 24, 32, 40, 48, 64, 96, 128, 256). It is both the Windows
  `.exe` icon and the window icon on every platform. The names are unchanged, so the csproj and `MainWindow.axaml`
  are untouched.
- `Assets/app-256.png`: the new 256 frame.
- `packaging/linux/icons/`: the seven PNGs replaced, plus `enigma-launchpad-96.png` and `enigma-launchpad.svg`.
- `install.sh` installs eight PNG sizes (96 added) and the SVG as `hicolor/scalable/apps/enigma-launchpad.svg`.
  `uninstall.sh` removes all nine.
- `new-icons-remove-when-done/` is removed. It was never tracked.

## Files / modules touched

**Created**

- `packaging/linux/icons/enigma-launchpad-96.png`
- `packaging/linux/icons/enigma-launchpad.svg`
- `docs/done/FEATURE-44CA.md`

**Modified**

- `src/Enigma.Launchpad.Desktop/Assets/app.ico`, `src/Enigma.Launchpad.Desktop/Assets/app-256.png`
- `packaging/linux/icons/enigma-launchpad-{16,24,32,48,64,128,256}.png`
- `packaging/linux/install.sh`, `packaging/linux/uninstall.sh`
- `README.md`, `docs/RELEASE.md`: the install tables
- `docs/roadmap.md`, `docs/plan/FEATURE-44CA.md`: statuses

**Deleted** (untracked, so not in the diff)

- `new-icons-remove-when-done/`

## Deviations & follow-ups

- **Deviations:** none. The plan was followed as written.
- **On this machine, hicolor's `scalable/apps`** spans 1 to 256 px. So the SVG serves the sizes no PNG has, such as
  20, 40 or 192, and the PNGs keep their own sizes. The comment in `install.sh` says so.
- **Upgrading an install:** running `install.sh` again overwrites the seven old PNGs and adds the two new files. A
  desktop that cached the old icon may show it until its next icon-cache refresh or login. The installer already
  runs `gtk-update-icon-cache` and `kbuildsycoca6`.
- **Follow-up:** the SVG's comment names its export script (`Branding/Apps/export.py`), which lives outside this
  repository. Regenerate the frames there, then copy them here.
- **Documentation sweep:**
  - README: the *Install on Linux* table lists the eight sizes and the scalable SVG; "those four things" became
    "those things";
  - `docs/RELEASE.md`: the same table;
  - nothing else describes the icon.
- Line endings: the SVG is LF without a BOM, per `.gitattributes` (`text=auto eol=lf`).

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental`: 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx`: total 965, 958 passed, 0 failed, 7 skipped (Windows-only).
- The built `Enigma.Launchpad.Desktop.dll` holds the new `.ico`'s bytes and not the old one's.
- Byte checks:
  - each of the 10 frames in the new `.ico` equals its PNG;
  - `app.ico`, `app-256.png` and every PNG under `packaging/linux/icons` equal the new files.
- Installer dry run: the desktop-cache tools were stubbed, and `XDG_DATA_HOME`, `XDG_BIN_HOME` and `XDG_CONFIG_HOME`
  pointed into the scratch directory.
  - `install.sh --from <stub>` printed "Installed 8 icons and a scalable one". Its 9 files equal the new set.
  - `uninstall.sh` printed "Removed 9 icons", and no icon file was left.
  - `bash -n` passes on both scripts. shellcheck is not installed here.
- No test added: no test project covers assets or the shell scripts (plan, *Decisions*).

## Decisions taken at build time

| Question                          | Chosen                                                               | Why                                                                                                  |
|-----------------------------------|----------------------------------------------------------------------|------------------------------------------------------------------------------------------------------|
| How the uninstaller lists icons   | One array: the SVG, then the PNG sizes, removed in a single loop     | One count and one message for the nine files, as before for seven                                    |
| Install tables                    | A separate *scalable* row beside the PNG row                         | Its path differs (`scalable/…svg`); a combined path pattern would be harder to read                  |
| Verifying the installer           | A `--from` stub dry run with the cache tools stubbed                 | A real `kbuildsycoca6` with a scratch `XDG_DATA_HOME` would rebuild the user's KDE menu cache without their apps |
