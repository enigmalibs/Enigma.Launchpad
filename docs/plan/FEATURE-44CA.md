# FEATURE-44CA — New application icon

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-44ca-new-app-icon`
**Run:** feature/2026-10-28-new-icons-release

## Objective

Replace the application's icon with the new artwork the user dropped, temporarily, in `new-icons-remove-when-done/`:
a rocket in the Soft palette on a dark rounded square, as an `.ico`, ten PNG frames and an SVG master. Then remove
that directory.

## Context & constraints

- **The current icon** is Phosphor `RocketLaunch` on a blue rounded square (FEATURE-5B69-PHASE01). It lives in three
  places:
  - `src/Enigma.Launchpad.Desktop/Assets/app.ico`, 7 frames (16, 24, 32, 48, 64, 128, 256). It is wired twice:
    - as `<ApplicationIcon>`, the Windows `.exe` icon;
    - as `Window.Icon="/Assets/app.ico"` in `MainWindow.axaml`, the running window's icon on every platform;
  - `src/Enigma.Launchpad.Desktop/Assets/app-256.png`: a byte copy of the 256 frame, packed as an Avalonia resource
    (`Assets/**`) but referenced by nothing;
  - `packaging/linux/icons/enigma-launchpad-<N>.png`: the seven frames, byte copies (FEATURE-200F). `install.sh`
    puts them under `hicolor/<N>x<N>/apps/` and `uninstall.sh` removes them, both from `ICON_SIZES`.
- **The new set**, all checked:
  - `enigma-launchpad.ico`: 10 PNG frames: 16, 20, 24, 32, 40, 48, 64, 96, 128, 256;
  - `enigma-launchpad-<N>.png` for those ten sizes, each byte-identical to its `.ico` frame;
  - `enigma-launchpad.svg`, the 256×256 master.
- **The hicolor theme** (`/usr/share/icons/hicolor/index.theme`) has app directories for 16, 22, 24, 32, 36, 48, 64,
  72, 96, 128, 192, 256, 512 and `scalable`. It has none for 20 or 40, so a PNG installed there would never be looked
  up.
- The input directory is untracked: it was never committed, and the run's commits never stage it.

## Steps

1. `Assets/app.ico` ← `enigma-launchpad.ico`; `Assets/app-256.png` ← `enigma-launchpad-256.png`. The names stay, so
   the csproj and `MainWindow.axaml` are unchanged.
2. `packaging/linux/icons/`:
   - the seven `enigma-launchpad-<N>.png` replaced by the new frames;
   - `enigma-launchpad-96.png` added;
   - `enigma-launchpad.svg` added.
3. `install.sh`:
   - `ICON_SIZES` gains 96;
   - the SVG is installed as `hicolor/scalable/apps/enigma-launchpad.svg`;
   - the header comment and the step message are updated.
4. `uninstall.sh`: `ICON_SIZES` gains 96, and the scalable SVG is removed too.
5. Docs: the README's *Install on Linux* table and `docs/RELEASE.md`'s install table say "seven sizes". Both say what
   is installed now.
6. Remove `new-icons-remove-when-done/`.

## Acceptance criteria

- The four icon locations hold the new artwork:
  - `app.ico` is byte-identical to the new `.ico`, with 10 frames;
  - `app-256.png` and every `packaging/linux/icons` PNG are byte-identical to the new frames;
  - the SVG is the new master.
- `install.sh --from <dir>`, dry-run into a scratch `XDG_DATA_HOME`, installs 8 PNGs and the scalable SVG. `uninstall.sh`
  then removes them all. `bash -n` passes on both scripts.
- `dotnet build Enigma.Launchpad.slnx` has zero warnings, and `dotnet test --solution Enigma.Launchpad.slnx` is green.
- `new-icons-remove-when-done/` no longer exists.

## Out of scope

- Loose 20 and 40 PNGs: not hicolor sizes, and already inside the `.ico`.
- Removing the unreferenced `app-256.png`.
- Regenerating the artwork. Its export script, named in the SVG's comment, is not in this repository.
- The version, the release notes: FEATURE-EB8F.

## Decisions taken autonomously

| Question                                     | Chosen                                                                  | Why                                                                                                                         | Alternatives rejected                               |
|----------------------------------------------|-------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------|
| Starting with an untracked input directory   | Proceed. The directory is the spec's input, consumed and then deleted   | The user put it there for this run and asked for its removal. Nothing else is uncommitted                                   | Stopping on the dirty-tree pre-flight               |
| The new sizes 20, 40, 96 and the SVG         | 96 and the SVG installed (`96x96`, `scalable`); 20 and 40 not added     | Only hicolor directories are searched. 20 and 40 have none, and they already ship inside the `.ico`                         | Every PNG in `packaging/`; only the existing seven  |
| File names                                   | `app.ico`, `app-256.png` and `enigma-launchpad-<N>.png` kept            | Nothing that references them moves                                                                                          | Renaming to the new set's `enigma-launchpad.*`      |
| The unreferenced `app-256.png`               | Replaced, kept                                                          | The ask is to replace the icons. Removing an asset is a separate decision                                                   | Deleting it                                         |
| Tests                                        | None added; verified by byte comparison and an installer dry run        | No test project covers assets or shell scripts, and adding one for a file swap would be new infrastructure                  | A test that loads `app.ico`                         |
