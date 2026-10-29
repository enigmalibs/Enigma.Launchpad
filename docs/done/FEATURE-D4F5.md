# FEATURE-D4F5 — Release 1.4.0

## Summary

Version **1.4.0**: 1.3.0 with the minor version incremented. The release covers this run:

- FEATURE-424E: packages updated;
- FEATURE-7298: the icon's SVG master in `packaging/icon/`, eight Linux icon sizes and no scalable one;
- FEATURE-7F4A: `app.ico` in the output directory;
- FEATURE-B7DD: publish scripts;
- FEATURE-2961: the About dialog;
- FEATURE-4757: the safety net for unhandled errors.

The in-repo edits are made. The merges, the tag, the pushes and the publish are printed for the user (below), never run.

## Files/modules touched

- Modified: `Directory.Build.props` (`<Version>1.4.0</Version>`)
- Modified: `RELEASENOTES.md` (the `1.4.0 — 2026-10-29` section, above 1.3.0's), `README.md` (the *What's new in 1.4*
  callout), `SECURITY.md` (1.4.x supported; scope names the publish scripts)
- Modified: `docs/roadmap.md`, `docs/plan/FEATURE-D4F5.md`; created `docs/done/FEATURE-D4F5.md`

## Deviations & follow-ups

- **Re-versioned to 1.4.0.** The run was built from 1.2.0 as release 1.3.0. Meanwhile, 1.3.0 had been cut and tagged
  on another machine (FEATURE-44CA, FEATURE-EB8F). When the run was merged on top of it, the user chose to ship it as
  1.4.0. 1.3.0's release notes stay as they are. The 1.4.0 section leaves out what 1.3.0 already shipped: the new
  icon, its 96 px Linux size, and Microsoft.Extensions 10.0.14. It does say that the scalable icon 1.3 installed is
  no longer installed or removed.
- `Directory.Packages.props` is unchanged: `dotnet list package --outdated` re-run on 2026-10-29 found nothing
  outdated after FEATURE-424E.
- `SECURITY.md` *Scope*: "the Linux installer in `packaging/linux/`" became "the Linux installer and the
  publish scripts in `packaging/`". FEATURE-B7DD added scripts the old line did not cover.
- Not done here, as the runbook says: merging into `develop` and `main`, tagging `1.4.0`, pushing, publishing.
- The README's *Settings* paragraph does not mention the About button. It is not wrong, so it was left as it is
  (the callout names Settings → About).

## Build/test evidence

- As 1.3.0, on Windows, before the re-versioning:
  - `dotnet build Enigma.Launchpad.slnx -c Release --no-incremental`: 0 warnings, 0 errors.
    `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 1025 total, 1024 passed, 0 failed, 1 skipped (the
    Unix-only resolver test).
  - `dotnet build Enigma.Launchpad.slnx --no-incremental` (Debug): 0 warnings, 0 errors. `dotnet test` (Debug): the
    same counts.
- As 1.4.0, merged on top of 1.3.0, on Linux:
  - `dotnet build Enigma.Launchpad.slnx -c Release`: 0 warnings, 0 errors.
  - The suite was not re-run: the session that merged the run ran no tests, at the user's request.
  - The built version, read with the runbook's Linux recipe on
    `src/Enigma.Launchpad.Desktop/bin/Release/net10.0/Enigma.Launchpad.Core.dll`:
    `1.4.0+6d77168b4d35c718b826c2d73c327b11da8b380f`, the run branch's tip at build time.
- Nothing in this dev ran `git tag`, `git push`, a merge into `develop` or `main`, `dotnet pack` or a NuGet push.

## Runbook (printed, for the user to run)

1. Pre-flight: `dotnet build Enigma.Launchpad.slnx -c Release`;
   `dotnet test --solution Enigma.Launchpad.slnx -c Release`.
2. `git switch develop && git merge --no-ff feature/2026-10-29-uniform-enigma-apps`, then
   `git switch main && git merge --no-ff develop`.
3. `git tag 1.4.0` (bare, like `1.0.0` to `1.3.0`).
4. `git push origin develop main` and `git push origin 1.3.0 1.4.0`.
5. Publish with `packaging/windows/publish.ps1` (or `publish.bat`) or `packaging/linux/publish.sh`. On Linux,
   install with `packaging/linux/install.sh`.
6. Post-release checks as in `docs/RELEASE.md` §7: the icon in the launcher, About shows 1.4.0 with the tagged
   commit, the shortcuts are intact.

## Documentation sweep

- `RELEASENOTES.md`, `README.md` and `SECURITY.md` are this dev's own edits. `docs/RELEASE.md` is generic in
  `X.Y.Z` and needed none.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Themed sections | *About*, *When something goes wrong*, *Publishing and installing* | What a user meets, in that order; the new icon was 1.3.0's. Then 1.3.0's *Upgrading*, *Dependencies*, *Version* |
| Linux upgrade note | The eight icons replaced; the scalable icon 1.3 installed left behind, with its path to delete | `install.sh` overwrites with `install -D`; neither script knows the SVG any more |
| MSI profile | None | Declined at 1.0.0 (`docs/RELEASE.md`); the run's Windows packaging is the publish scripts |
