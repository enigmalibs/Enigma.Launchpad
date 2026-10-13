# Enigma.Launchpad

A cross-platform desktop launcher for your **applications**, **directories** and **URLs**, built with
[Avalonia 12](https://avaloniaui.net/), [Enigma.Avalonia.Desktop](https://www.nuget.org/packages/Enigma.Avalonia.Desktop)
and [Enigma.Icons.Avalonia](https://www.nuget.org/packages/Enigma.Icons.Avalonia).

Each of the three categories is a page in the navigation rail, where your shortcuts are arranged in
groups you create, rename, fold, reorder and delete. Add a shortcut with a group's **+** (Browse… picks a
program or a folder); click its tile, or press Enter, to open it; right-click it to edit it, try to get its
icon again, copy its target, show it in its folder, move it or delete it. Drag a group by its grip, or a tile, to
reorder them; drop a tile on another group of the same category to move it there.

**Profiles** keep separate sets of shortcuts — say *Work* and *Home* — each with its own applications,
directories and URLs. Choose the profile in use from the **Profile** list at the top of the window; create,
duplicate, rename, reorder, use or delete profiles on the **Profiles** page, at the foot of the rail or from the
button beside the list.

**Export and import** move shortcuts between profiles and machines. In a profile's menu on the Profiles page,
*Export…* writes the groups and shortcuts you tick to a JSON file, and *Import…* reads such a file and adds the
ones you tick to that profile: a group named like one the profile already has receives its shortcuts there, and
shortcuts the group already holds are skipped, so importing a file twice adds nothing.

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

It holds every profile and remembers the one in use. A file written before profiles existed is read into a
single *Default* profile, and saved in the current format on the next change.

Each shortcut keeps its icon in it too, as a small PNG image: the image you chose in its form, or a program's or a
website's icon, saved there the first time it is found and shown, so it is back at the next start without asking
the system or the site again.

The file is written atomically. If it ever cannot be read, it is kept as
`launchpad.corrupt-<timestamp>.json` beside the original, and the launchpad starts empty; nothing is overwritten.

Your preferences are kept beside it in `settings.json`. An unreadable settings file is likewise kept as
`settings.corrupt-<timestamp>.json`, and the defaults are used.

Website icons are also cached beside it, in an `icons` folder, so a new shortcut to a site seen before gets its
icon at once. To find one, the launchpad asks the site itself — its home page and its icon files, never the address
a shortcut points to, and without cookies — at most once a week per site; a site that cannot be reached keeps the
icon it had. The folder is only a cache: deleting it is safe.

### Export files

An export is a small, readable JSON document of its own — not a copy of `launchpad.json` — holding names,
targets, arguments, descriptions and icons, but no internal identities and no folded state:

```json
{
  "format": "enigma-launchpad-shortcuts",
  "version": 1,
  "profile": "Work",
  "applications": [
    { "name": "Dev tools", "shortcuts": [ { "name": "VS Code", "target": "C:\\Tools\\Code\\Code.exe", "arguments": "--new-window", "description": "The editor" } ] }
  ],
  "directories": [],
  "urls": [
    { "name": "Docs", "shortcuts": [ { "name": "Avalonia", "target": "https://docs.avaloniaui.net", "icon": { "png": "iVBORw0KGgo…", "isCustom": false } } ] }
  ]
}
```

A file that is not such an export, or is damaged, is refused with the reason. Inside one, an entry the shortcut
form would refuse — a blank name, a relative path, an address other than http or https, a description over 200
characters — is left out, and the import says how many were. Paths need not exist on the machine importing them.
An icon is a PNG image in base64; one that is not, or is over 256 KB, is dropped and its shortcut kept.

## Requirements

- .NET SDK 10.0.100 or later (pinned in `global.json`, rolls forward to newer feature bands)
- Windows 10/11, Linux (X11) or macOS

## Build, run, test

```bash
dotnet build Enigma.Launchpad.slnx
dotnet run --project src/Enigma.Launchpad.Desktop
dotnet test --solution Enigma.Launchpad.slnx
```

## Solution layout

| Path                                       | What it is                                                         |
|--------------------------------------------|--------------------------------------------------------------------|
| `src/Enigma.Launchpad.Core`                | Model, validation, persistence, export and launching — no Avalonia |
| `src/Enigma.Launchpad.Desktop`             | The Avalonia application: composition root, ViewModels, Views      |
| `tests/Enigma.Launchpad.Core.UnitTests`    | xUnit v3 tests for the core library                                |
| `tests/Enigma.Launchpad.Desktop.UnitTests` | xUnit v3 tests for the app, including headless Avalonia tests      |
| `docs/`                                    | Roadmap, plans and completion records                              |

## License

MIT — see [LICENSE.md](LICENSE.md).
