# Enigma.Launchpad

A cross-platform desktop launcher for your **applications**, **folders**, **links** and **documents**, built with
[Avalonia 12](https://avaloniaui.net/), [Enigma.Avalonia.Desktop](https://www.nuget.org/packages/Enigma.Avalonia.Desktop)
and [Enigma.Icons.Avalonia](https://www.nuget.org/packages/Enigma.Icons.Avalonia).

> **What's new in 1.1** — workspaces in place of the three category pages, documents as a fourth kind of shortcut,
> commands found on the `PATH` as applications, and drag and drop that shows where an item will land. Your shortcuts
> move into a workspace on their own. See [RELEASENOTES.md](RELEASENOTES.md).

**Workspaces**, the main page of the navigation rail and where the launchpad opens, are a profile's own collections
of shortcuts: each has a title, a line saying what it is for and a glyph, and holds groups of applications, folders,
links and documents, side by side. Their page shows them as cards, with a search over titles and descriptions; create a
workspace with **New workspace**, and edit, duplicate, reorder or delete one from its card's menu. The shortcuts you
had before workspaces are in one called *Shortcuts*.

Click a card, or press Enter on it, to open its workspace in place of the list; the arrow at the top left (or
Alt+Left) goes back. In a workspace, your shortcuts are arranged in groups you create, rename, fold, reorder and
delete. Every group holds one kind of shortcut, chosen when it is created from **New group**: applications, folders,
links or documents — any file, opened with the application your system opens it with. A group's menu can also move
it, with its shortcuts, to another workspace — unless that one already has a group of the same name and kind. Add a
shortcut with a group's **+** (Browse… picks a program, a folder or a document); click its tile, or press Enter, to
open it; right-click it to edit it, try to get its icon again, copy its target, show it in its folder, move it or
delete it. Hover a tile, or give it the keyboard focus, for its own buttons: *Run with arguments…* on an application,
which starts it once with other arguments than its default ones; *Open in terminal* and *Copy path* on a folder;
*Copy path* on a document; *Copy URL* on a link. Drag a group by its grip, or a tile, to reorder them; drop a tile on
another group of the same kind to move it there.

An application is a program's full path, or a **command** found on the `PATH` — `notepad`, `code`, `git` — kept as
you typed it and looked up each time it is opened, so it follows the `PATH` and works on another machine that has
it. A command starts in your home folder; the current folder is never searched. On Windows, the `PATHEXT` extensions
are tried, and the user's and the machine's `PATH` are read afresh, so a tool installed while the launchpad runs is
found without restarting it.

**Profiles** keep separate sets of shortcuts — say *Work* and *Home* — each with its own workspaces. Choose the
profile in use from the **Profile** list at the top of the window; create,
duplicate, rename, reorder, use or delete profiles on the **Profiles** page, at the foot of the rail or from the
button beside the list.

**Export and import** move shortcuts between profiles and machines. In a profile's menu on the Profiles page,
*Export…* writes the workspaces, groups and shortcuts you tick to a JSON file, and *Import…* reads such a file and
adds the ones you tick to that profile: a workspace titled like one the profile already has receives its groups
there, a group named like one of that workspace's groups of the same kind receives its shortcuts there, and shortcuts
the group already holds are skipped, so importing a file twice adds nothing.

**Settings**, at the foot of the rail, chooses the theme — light, dark, or following the system — and whether
the launchpad minimizes itself once a shortcut is opened; it also shows where your data lives and opens that
folder.

## Your data

Everything is saved after every change to `launchpad.json`, a readable JSON file in your per-user
application-data folder:

| Platform | Location                                                                              |
|----------|---------------------------------------------------------------------------------------|
| Windows  | `%APPDATA%\Enigma.Launchpad\launchpad.json`                                           |
| Linux    | `$XDG_CONFIG_HOME/Enigma.Launchpad/launchpad.json` (`~/.config` by default)           |
| macOS    | `Enigma.Launchpad/launchpad.json` under the folder .NET resolves as `ApplicationData` |

It holds every profile — each with its workspaces, their groups and their shortcuts — and remembers the one in use.
A file written before workspaces existed is read with each profile's groups in one *Shortcuts* workspace, every
group keeping the kind of the page it was on; a file written before profiles existed is read the same way into a
single *Default* profile. Such a file is first copied, once, to `launchpad.v<N>.backup.json` beside it — never
replaced afterwards — and saved in the current format on the next change.

Each shortcut keeps its icon in it too, as a small PNG image: the image you chose in its form, or a program's or a
website's icon, saved there the first time it is found and shown, so it is back at the next start without asking
the system or the site again.

The file is written atomically. If it ever cannot be read, it is kept as
`launchpad.corrupt-<timestamp>.json` beside the original, and the launchpad starts empty; nothing is overwritten.

Your preferences are kept beside it in `settings.json`. An unreadable settings file is likewise kept as
`settings.corrupt-<timestamp>.json`, and the defaults are used.

Website icons are also cached beside it, in an `icons` folder, so a new shortcut to a site seen before gets its
icon at once. To find one, the launchpad asks the site itself — its home page, the web app manifest it links and its
icon files, never the address a shortcut points to, and without cookies — at most once a week per site; a site that
cannot be reached keeps the icon it had. The folder is only a cache: deleting it is safe.

### Export files

An export is a small, readable JSON document of its own — not a copy of `launchpad.json` — holding workspaces
(titles, descriptions and glyphs), their groups (names and kinds: `application`, `directory`, `url`, `document`) and the
groups' shortcuts (names, targets, arguments, descriptions and icons), but no internal identities and no folded
state:

```json
{
  "format": "enigma-launchpad-shortcuts",
  "version": 2,
  "profile": "Work",
  "workspaces": [
    { "title": "Dev", "description": "Everything for coding", "icon": "Code", "groups": [
      { "name": "Dev tools", "kind": "application", "shortcuts": [ { "name": "VS Code", "target": "C:\\Tools\\Code\\Code.exe", "arguments": "--new-window", "description": "The editor" } ] },
      { "name": "Docs", "kind": "url", "shortcuts": [ { "name": "Avalonia", "target": "https://docs.avaloniaui.net", "icon": { "png": "iVBORw0KGgo…", "isCustom": false } } ] }
    ] }
  ]
}
```

A file of the earlier format — version 1, with `applications`, `directories` and `urls` lists from before
workspaces — is still read, into one *Shortcuts* workspace. A file that is not such an export, or is damaged, is
refused with the reason. Inside one, an entry the forms would refuse — a blank name or title, a relative path, an
address other than http or https, a description over 200 characters, a group of a kind this version does not know —
is left out with everything in it, and the import says how many entries were. Paths need not exist on the machine
importing them, and an application's command need not be on its `PATH`.
An icon is a PNG image in base64; one that is not, or is over 256 KB, is dropped and its shortcut kept.

## Requirements

- .NET SDK 10.0.100 or later to build (pinned in `global.json`, rolls forward to newer feature bands). The Linux
  installer bundles the .NET runtime with the application by default; a framework-dependent build needs the .NET 10
  runtime instead
- Windows 10/11, Linux (X11) or macOS

## Build, run, test

```bash
dotnet build Enigma.Launchpad.slnx
dotnet run --project src/Enigma.Launchpad.Desktop
dotnet test --solution Enigma.Launchpad.slnx
```

## Install on Linux

`packaging/linux/install.sh` builds the application from this repository and installs it for you — no root, no
`sudo`, nothing outside your home directory — so that **Enigma Launchpad** is in your application launcher:

```bash
./packaging/linux/install.sh
```

| What                                     | Where (`XDG_DATA_HOME` and `XDG_BIN_HOME` are honoured)               |
|------------------------------------------|-----------------------------------------------------------------------|
| The application                          | `~/.local/share/enigma-launchpad`                                     |
| A launcher command, `enigma-launchpad`   | `~/.local/bin/enigma-launchpad`                                       |
| The desktop entry                        | `~/.local/share/applications/enigma-launchpad.desktop`                |
| The icon, in seven sizes                 | `~/.local/share/icons/hicolor/<N>x<N>/apps/enigma-launchpad.png`      |

By default the .NET runtime is bundled with the application (about 115 MB), so it runs whatever is installed on the
machine. `--framework-dependent` builds against an installed .NET 10 runtime instead (about 35 MB); `--from DIR`
installs a directory you published yourself, and `--rid` picks another runtime identifier. Run the installer again
to upgrade in place. `./packaging/linux/uninstall.sh` removes those four things and nothing else: your shortcuts,
profiles and settings in `~/.config/Enigma.Launchpad` are left as they are.

## Solution layout

| Path                                       | What it is                                                         |
|--------------------------------------------|--------------------------------------------------------------------|
| `src/Enigma.Launchpad.Core`                | Model, validation, persistence, export and launching — no Avalonia |
| `src/Enigma.Launchpad.Desktop`             | The Avalonia application: composition root, ViewModels, Views      |
| `tests/Enigma.Launchpad.Core.UnitTests`    | xUnit v3 tests for the core library                                |
| `tests/Enigma.Launchpad.Desktop.UnitTests` | xUnit v3 tests for the app, including headless Avalonia tests      |
| `packaging/linux`                          | The Linux installer and uninstaller, the desktop entry, the icons  |
| `docs/`                                    | Roadmap, plans, completion records and the release runbook         |

## License

MIT — see [LICENSE.md](LICENSE.md).
