# FEATURE-79F9 — Repository dotfiles

## Summary

The three repository dotfiles left out at hand-over are now tracked:

- `.gitignore` — Visual Studio / dotnet ignore set (GitHub's `VisualStudio.gitignore`, plus Rider `.idea/`).
- `.gitattributes` — every text file LF in the repository and on checkout (`* text=auto eol=lf`); `.bat` / `.cmd`
  CRLF; known binaries (`png`, `ico`, `dll`, `snk`, …) never converted.
- `.editorconfig` — LF, final newline, UTF-8, indentation, and the C# code-style rules that
  `EnforceCodeStyleInBuild` turns into build warnings (errors here).

The tree was checked against them:

- **Nothing ignored is tracked.** `git ls-files -ci --exclude-standard` prints nothing, and no path ever added in
  history matches an ignore rule. After a build, the `bin/` and `obj/` folders of all four projects show as
  ignored.
- **Every text file is LF.** `git ls-files --eol` over the 183 files of this commit: 179 text files
  `i/lf w/lf` (the 174 from hand-over, this item's plan and done files, the 3 dotfiles), 2 empty files and 2
  binaries `-text`. No `crlf`, no `mixed`. Every text file's attribute resolves to `eol=lf`.
  Renormalizing into a throwaway copy of the index changed no tracked file, so no renormalization commit is
  needed.

## Files

Created:
- `.gitignore`
- `.gitattributes`
- `.editorconfig`

The three were already on disk, untracked; only `.gitignore` was edited (final newline added).

## Deviations & follow-ups

- No deviation from the plan. No file had to be untracked or normalized.
- Fix budget: none used. The first build and test run were green.
- `.gitignore` gained a final newline. It is otherwise byte-identical to the `git-repo-hygiene` template, which
  lacks that newline itself. The template is worth fixing at its source.
- Follow-up for existing clones: a Windows clone with `core.autocrlf=true` may show files as modified once it
  pulls this commit. A one-time `git add --renormalize .` there (or a fresh checkout) clears it. Nothing in this
  repository needs it.
- The 3 skipped tests are skipped on Linux, as before this change (559 total on both sides).
- Documentation sweep: nothing became wrong. README's "Build, run, test" and "Solution layout" still hold. No
  edit.
- Line endings: verified as part of this item, see Summary.

## Decisions taken at build time

| Question                                | Chosen                                                                  | Why                                                                                              |
|-----------------------------------------|-------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------|
| How to prove renormalization is a no-op | `git add --renormalize` into a throwaway copy of the index, then diff   | `--dry-run` lists every path whether or not it would change; a scratch index gives real evidence |
| Verify on the index or the working tree | Both: dotfiles staged first, then `git ls-files --eol` and `-ci` checks | The index is what gets committed; the working tree is what a build reads                         |
| `.gitignore` final newline              | Added                                                                   | The committed `.editorconfig` requires it, and git diffs flag its absence                        |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental`, with the committed `.editorconfig` enforced: 0 warnings,
  0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx`: 559 total, 556 passed, 0 failed, 3 skipped. Same as before
  the change.
- `git ls-files -ci --exclude-standard`: empty.
- `git ls-files --eol`: no `crlf` or `mixed` in the index or the working tree; every text attribute `eol=lf`.
- `git status --porcelain --ignored`: `bin/` and `obj/` of the four projects reported `!!`, nothing else.
