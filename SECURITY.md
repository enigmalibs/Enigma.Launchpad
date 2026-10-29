# Security Policy

Enigma.Launchpad is a desktop launcher that starts the programs, opens the folders and visits the websites you give
it, fetches those websites' icons over the network, and imports shortcut files that may come from another machine.
A defect in it could start something you did not choose, reach a site you did not expect, or let a crafted file
change what your shortcuts do. Vulnerability reports are taken seriously and handled with priority.

## Supported versions

Security fixes are provided for the latest released version. Enigma.Launchpad follows
[Semantic Versioning](https://semver.org/), and users are encouraged to stay current with the newest release.

| Version | Supported          |
|---------|--------------------|
| 1.4.x   | :white_check_mark: |
| 1.3.x   | :x:                |
| 1.2.x   | :x:                |
| 1.1.x   | :x:                |
| 1.0.x   | :x:                |

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions, or pull requests.**
Public disclosure before a fix is available puts every user at risk.

Instead, use **GitHub's private vulnerability reporting**:

1. Go to the repository's **Security** tab.
2. Select **Report a vulnerability** to open a private advisory.
3. Include as much detail as you can — the affected version, the component involved, a description of the issue,
   and, where possible, a minimal reproduction and its impact.

This keeps the report private between you and the maintainers while it is triaged and fixed.

## What to expect

- Your report will be acknowledged and triaged as promptly as possible.
- The issue will be investigated and, once confirmed, a fix prepared and released.
- Coordinated disclosure is preferred: please allow a reasonable period for a fix to ship before any public
  discussion of the vulnerability.
- Your contribution will be credited in the resulting advisory unless you ask to remain anonymous.

## Scope

The following are in scope:

- the way Enigma.Launchpad starts programs, folders, terminals and addresses;
- the way it finds and fetches website icons;
- the way it reads imported export files and the images they carry;
- the way it reads and writes the files in its data directory;
- the Linux installer and the publish scripts in `packaging/`.

Issues rooted in Avalonia, SkiaSharp or the .NET runtime should also be reported to those projects.
