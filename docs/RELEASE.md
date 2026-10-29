# Release runbook

Reusable checklist for cutting a new **Enigma.Launchpad** version.

Replace `X.Y.Z` with the version being released (e.g. `1.1.0`) throughout. The version lives in
`Directory.Build.props` (`<Version>`) and covers the whole solution: `Enigma.Launchpad.Core` and
`Enigma.Launchpad.Desktop` ship as one application, so they never carry separate numbers.

**Nothing here is published to NuGet.** Neither project has a `PackageId`, so there is no `pack` step, no
`nuget push`, and no package metadata to keep in order. A release produces two things: a tag on `main`, and an
installed application for whoever wants one. Steps 5 and 6 replace what would be the pack-and-push steps for a
library.

## 1. Pre-release checks

Run from the repository root, on the branch that will be merged:

- [ ] `<Version>X.Y.Z</Version>` set in `Directory.Build.props`.
- [ ] `RELEASENOTES.md` has a top `X.Y.Z` section, dated, describing the release (newest first).
- [ ] The README's *what's new* callout reflects `X.Y.Z`.
- [ ] Clean, warning-free build:
      ```bash
      dotnet build Enigma.Launchpad.slnx -c Release
      ```
- [ ] Full test suite green:
      ```bash
      dotnet test --solution Enigma.Launchpad.slnx -c Release
      ```
- [ ] The build carries the version. The application does not show it, so read it off the build: the version and
      the commit the build was cut from, `X.Y.Z+<sha>`.
      - On Linux (`strings` is in binutils):
        ```bash
        strings src/Enigma.Launchpad.Desktop/bin/Release/net10.0/Enigma.Launchpad.Core.dll | grep -m1 -o 'X\.Y\.Z+[0-9a-f]*'
        ```
      - On Windows:
        ```powershell
        (Get-Item src/Enigma.Launchpad.Desktop/bin/Release/net10.0/Enigma.Launchpad.Core.dll).VersionInfo.ProductVersion
        ```

## 2. Merge into `develop`, then into `main`

Work is integrated on `develop`; `main` carries the releases:

```bash
git switch develop
git merge --no-ff <release-branch>
git switch main
git merge --no-ff develop
```

## 3. Tag the release

The first release sets the convention: **bare `X.Y.Z`**, no `v` prefix. Run `git tag` before tagging a later release
and match what you find.

```bash
git tag X.Y.Z
```

## 4. Push

```bash
git push origin develop main
git push origin X.Y.Z
```

## 5. Publish the application

A release build for a specific platform, with the .NET runtime bundled so the result runs on a machine that has no
.NET installed:

```bash
dotnet publish src/Enigma.Launchpad.Desktop -c Release -r linux-x64 --self-contained true -o ./artifacts
```

Substitute the runtime identifier for the target platform — `linux-arm64`, `win-x64`, `osx-arm64`.

**Always pass `-r`.** A publish with no runtime identifier carries the Skia and HarfBuzz natives for *every*
platform in a `runtimes/` tree. With one, the natives sit beside the assemblies, and the result is about 115 MB
self-contained, or 35 MB against an installed runtime (`--self-contained false`).

## 6. Install it

On Linux, the installer does the publish and the installation in one step, and is what puts the application in the
desktop launcher:

```bash
packaging/linux/install.sh              # --help lists the options
```

It writes only under `$HOME`:

| What                                    | Where                                                              |
|-----------------------------------------|--------------------------------------------------------------------|
| the application                         | `~/.local/share/enigma-launchpad`                                  |
| the launcher symlink                    | `~/.local/bin/enigma-launchpad`                                    |
| the desktop entry, "Enigma Launchpad"   | `~/.local/share/applications/enigma-launchpad.desktop`             |
| the icon, eight sizes                   | `~/.local/share/icons/hicolor/<N>x<N>/apps/enigma-launchpad.png`   |

`packaging/linux/uninstall.sh` removes exactly those, and leaves `~/.config/Enigma.Launchpad` alone: the shortcuts,
the profiles, the settings and the icon cache.

Windows and macOS have no installer: publish as in step 5 and run the result.

## 7. Post-release verification

- [ ] **Enigma Launchpad** appears in the application launcher, with its icon, and starts when clicked. A launcher
      starts a process from a bare environment, so this is not the same test as starting it from a terminal — check
      the launcher, not just the shell.
- [ ] The installed build carries `X.Y.Z+<sha>` naming the tagged commit (the step 1 recipe, pointed at
      `~/.local/share/enigma-launchpad/Enigma.Launchpad.Core.dll`).
- [ ] Your shortcuts are all there after the upgrade, and a shortcut of each kind opens.
- [ ] `git tag` lists `X.Y.Z` on the merge commit on `main`, and the tag is on the remote.

## Why there is no MSI profile

`dotnet-release` offers a WixSharp MSI profile for an app release. It was declined for 1.0.0: the release's
packaging is the Linux installer. When a Windows installer is wanted, generate the **first** profile then. Its
`upgradeCode` is created once and reused verbatim in every later version; its `productId` is new each time.
