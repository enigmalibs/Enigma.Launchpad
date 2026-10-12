# FEATURE-79F9 — Repository dotfiles

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-79f9-repo-dotfiles`
**Run:** feature/2026-10-12-repo-dotfiles

## Objective

Commit the three repository dotfiles left out when the repo was handed over — `.gitignore`, `.gitattributes`
and `.editorconfig` — and prove two things on the result:

- no tracked file is one the `.gitignore` would ignore;
- every text source file is LF, with no CRLF, and `.gitattributes` keeps it that way.

## Context & constraints

- The three files sit untracked at the root of `develop` @ `1000dc2`. They are the only uncommitted changes.
- `.gitignore` and `.gitattributes` are byte-identical to the house `git-repo-hygiene` templates (GitHub's
  `VisualStudio.gitignore` plus Rider `.idea/`; `* text=auto eol=lf`, `.bat`/`.cmd` CRLF, known binaries). The
  `.editorconfig` is byte-identical to the `dotnet-solution-config` template.
- `.gitignore` has no final newline. The `.editorconfig` asks for one on every file (`insert_final_newline`).
- `Directory.Build.props` sets `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild`. Once `.editorconfig` is
  committed, its warning-level style rules are part of every build on every clone.
- State on `develop` @ `1000dc2`, with the dotfiles in the working tree:
  - 178 tracked files: 174 `i/lf w/lf`, 2 empty (`RELEASENOTES.md`, `docs/done/.gitkeep`), 2 binary
    (`app-256.png`, `app.ico`). No CRLF, no mixed endings, no UTF-8 BOM, no missing final newline, no
    trailing whitespace.
  - `git ls-files -ci --exclude-standard` is empty. No path ever added in history is ignored either.
  - After a build, `bin/` and `obj/` of all four projects show as ignored.
  - `dotnet build --no-incremental`: 0 warnings. `dotnet test`: 559 total, 556 passed, 3 skipped (on Linux).

## Design

- Commit the three files as they are, except for adding a final newline to `.gitignore`.
- Untrack nothing: no tracked file matches an ignore rule. If the verification at build time finds one, it is
  removed from the index with `git rm --cached` (the file stays on disk) in the same commit.
- Normalize nothing: every text file is already LF in the index and the working tree, so
  `git add --renormalize` would be a no-op. The committed `.gitattributes` enforces LF from now on, Windows
  checkouts included.

## Steps

1. Add the missing final newline to `.gitignore`.
2. Verify the ignore rules against the tree: `git ls-files -ci --exclude-standard` is empty, and `bin/` / `obj/`
   are reported as ignored after a build.
3. Verify line endings with the committed attributes: `git ls-files --eol` shows every text file as
   `i/lf w/lf attr/… eol=lf`, and binaries as `-text`. The three dotfiles themselves contain no CR.
4. `dotnet build Enigma.Launchpad.slnx --no-incremental` (0 warnings, code style enforced) and
   `dotnet test --solution Enigma.Launchpad.slnx`.
5. Commit the three dotfiles together with the roadmap, plan and done records.

## Acceptance criteria

- `.gitignore`, `.gitattributes` and `.editorconfig` are tracked, LF, UTF-8 without BOM, with a final newline.
- `git ls-files -ci --exclude-standard` prints nothing.
- `git ls-files --eol` shows no `crlf` or `mixed` in the index or the working tree. Every text file's attribute
  resolves to `eol=lf`, and `.png` / `.ico` resolve to `-text`.
- Build 0 warnings; suite green.

## Out of scope

- Changing the content of the ignore, attribute or editor rules (beyond the final newline).
- Rewriting history. Nothing ignored was ever committed anyway.
- CI, hooks or scripts that enforce line endings.
- Code-style fixes in the C# sources: the build is already clean under the `.editorconfig`.

## Decisions taken autonomously

| Question                                                      | Chosen                                                        | Why                                                                                                          | Alternatives rejected                                    |
|---------------------------------------------------------------|---------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------|----------------------------------------------------------|
| Pre-flight: start with three untracked files in the tree?     | Yes, treating them as the run's declared inputs               | They are exactly and only the files the spec names. Stopping would force a commit on `develop` or a stash    | Stop with "dirty working tree"                           |
| Keep the dotfiles' content or rewrite it?                     | Keep it; only add `.gitignore`'s missing final newline        | Already the house templates; the repo's own `.editorconfig` requires the final newline                       | Rewrite from scratch; commit `.gitignore` byte-identical |
| Tracked files the `.gitignore` would ignore                   | None found, so nothing untracked                              | `git ls-files -ci --exclude-standard` is empty and history is clean                                          | Purge history                                            |
| Line endings                                                  | Commit `.gitattributes` and verify; no renormalization commit | Every text file is already LF, so `--renormalize` changes nothing. The spec asks for LF, so this is in scope | Separate renormalization commit                          |
| `.editorconfig` vs the zero-warnings build                    | No code change                                                | Build with it in place is 0 warnings under `EnforceCodeStyleInBuild`                                         | Lower rule severities                                    |
| Explicit `.gitattributes` lines for `axaml`, `csproj`, `slnx` | Not added                                                     | `* text=auto eol=lf` already covers them; keep the house template                                            | List every extension                                     |
| README note on line endings or dotfiles                       | None                                                          | Nothing in the README becomes wrong; would be gold-plating                                                   | Add a "Contributing" section                             |
| Item type and size                                            | `FEATURE`, single phase                                       | Adds repository configuration; one small reviewable commit                                                   | `BUG`; one phase per file                                |
