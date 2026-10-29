# FEATURE-7298 — New application icon

## Summary

The application icon is now the new LP-A2 artwork (Rocket, *Soft* palette) from
`Branding/Apps/final/launchpad`, everywhere the repository carries it:

- `app.ico` holds ten 32-bit PNG frames (16, 20, 24, 32, 40, 48, 64, 96, 128, 256), up from seven;
- `app-256.png` is its 256 frame;
- the SVG master is in the new `packaging/icon/`;
- the Linux installer installs and removes eight PNG sizes, and no scalable SVG.

A new `AppIconTests` pins the frames and the copies.

## Files/modules touched

- Unchanged, already this artwork byte for byte (FEATURE-44CA): `src/Enigma.Launchpad.Desktop/Assets/app.ico`,
  `src/Enigma.Launchpad.Desktop/Assets/app-256.png`, `packaging/linux/icons/enigma-launchpad-{16,24,32,48,64,96,128,256}.png`
- Created: `packaging/icon/enigma-launchpad.svg`
- Deleted: `packaging/linux/icons/enigma-launchpad.svg`
- Modified: `packaging/linux/install.sh`, `packaging/linux/uninstall.sh`
- Modified: `README.md`, `docs/RELEASE.md`
- Created: `tests/Enigma.Launchpad.Desktop.UnitTests/AppIconTests.cs`
- Modified: `docs/roadmap.md`, `docs/plan/FEATURE-7298.md`; created `docs/done/FEATURE-7298.md`

## Deviations & follow-ups

- Built on 1.2.0, then merged on top of 1.3.0, where FEATURE-44CA had already put this same artwork in place:
  `app.ico`, `app-256.png` and the eight Linux PNGs are byte for byte the Branding files, so step 1's copies
  changed only the SVG master.
- FEATURE-44CA had kept the master as `packaging/linux/icons/enigma-launchpad.svg` and installed it as
  `hicolor/scalable/apps/enigma-launchpad.svg`. The user chose to drop the scalable icon when merging: the master
  moves to `packaging/icon/`, and `install.sh` and `uninstall.sh` handle the eight PNGs only, as GitClient's do.
  `README.md` and `docs/RELEASE.md` lose the scalable row.
- Follow-up: on a machine where 1.3.0 was installed, `uninstall.sh` now leaves
  `~/.local/share/icons/hicolor/scalable/apps/enigma-launchpad.svg` behind. The 1.4.0 release notes say so.
- The Branding SVG has CRLF line endings. The repository's `* text=auto eol=lf` stores it as LF, so its
  content is verbatim and only the line endings are normalised.

## Build/test evidence

- Before the copies, the Branding `.ico`'s ten frames were checked byte for byte against
  `png/enigma-launchpad-N.png`, and every copy afterwards with `cmp`.
- `bash -n` passes on `install.sh` and `uninstall.sh`. Both keep mode `100755`.
- `dotnet build Enigma.Launchpad.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 968 total, 967 passed, 0 failed, 1 skipped (the
  Unix-only resolver test). 3 tests added:
  - `TheIcon_HoldsTenSquare32BitPngFramesFrom16To256`;
  - `TheLargeImage_IsTheIconsLargestFrameByteForByte`;
  - `TheLinuxIcons_AreTheIconsFramesByteForByteInEverySizeTheInstallerInstalls` (both scripts list the same
    eight sizes).

## Documentation sweep

- `README.md`: the install table lists the eight sizes and no scalable icon (four things for `uninstall.sh` to
  remove), and the layout table gains `packaging/icon`.
- `docs/RELEASE.md` §6: the install table lists the eight sizes and no scalable icon.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Test name for `app-256.png` | "the large image", not "the About image" | The About dialog does not exist yet (FEATURE-2961) |
| An explicit size list in the Linux test | Asserted as well as the two scripts agreeing | It catches the two scripts drifting together |
