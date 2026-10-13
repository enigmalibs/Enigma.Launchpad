# Release notes

## 1.0.0 — 2026-10-13

The first release: a desktop launcher for your applications, folders and websites. Each kind is a page of its own,
the shortcuts are arranged in groups you shape, and everything is kept in one readable file. It runs on Windows,
Linux and macOS from the same code.

### Shortcuts and groups

- Three pages in the navigation rail — **Applications**, **Directories** and **URLs** — each holding its shortcuts in
  groups you create, rename, fold, reorder and delete.
- Add a shortcut with a group's **+**. **Browse…** picks a program or a folder.
- The form takes a name, a target, default arguments for an application, an optional description of up to 200
  characters, and an icon.
- It refuses a blank name, a relative path, or an address other than http or https, and says why under the field.
- Drag a group by its grip, or a tile, to reorder them. Drop a tile on another group of the same page to move it
  there.

### Tiles

- Click a tile, or press Enter on it, to open it.
- Right-click it to:
  - edit it;
  - try to get its icon again;
  - copy its target;
  - show it in its folder;
  - move it to another group;
  - delete it.
- Hover a tile, or give it the keyboard focus, for its own buttons:
  - on an application, *Run with arguments…*, which starts it once with other arguments than its default ones,
    without changing the shortcut;
  - on a folder, *Open in terminal* and *Copy path*;
  - on a link, *Copy URL*.
- A shortcut's description shows on its tile, under the icon and the name.

### Icons

- An application tile shows the program's own icon, on Windows.
- A link tile shows the website's icon. The launchpad finds it by asking the site itself — its home page, the web
  app manifest it links and its icon files. It never fetches the address the shortcut points to, sends no cookies,
  and asks each site at most once a week.
- Icons found are cached, so a new shortcut to a site seen before gets its icon at once. A site that cannot be
  reached keeps the icon it had.
- An icon can also be an image you choose in the form.
- Every icon is kept with its shortcut, so it is back at the next start without asking the system or the site
  again.

### Profiles

- Profiles keep separate sets of shortcuts — say *Work* and *Home* — each with its own applications, directories and
  URLs.
- Pick the profile in use from the **Profile** list at the top of the window.
- Create, duplicate, rename, reorder, use or delete profiles on the **Profiles** page.

### Export and import

- *Export…* writes the groups and shortcuts you tick to a small JSON file. It holds names, targets, arguments,
  descriptions and icons.
- *Import…* adds the ones you tick to a profile:
  - a group named like an existing one receives its shortcuts there;
  - shortcuts it already holds are skipped, so importing a file twice adds nothing.
- A file that is not an export, or is damaged, is refused with the reason.
- An entry the form would refuse is left out, and the import says how many were. An icon that is not a PNG image, or
  is over 256 KB, is dropped and its shortcut kept.

### Settings and your data

- The **Settings** page chooses the theme — light, dark, or following the system — and whether the launchpad
  minimizes itself once a shortcut is opened. It shows where your data lives and opens that folder.
- Everything is saved after every change, atomically, to `launchpad.json` in your per-user application-data folder,
  with your preferences beside it in `settings.json`.
- A file that cannot be read is kept aside as `*.corrupt-<timestamp>.json` and never overwritten.

### Installing

- On Linux, `packaging/linux/install.sh` builds and installs the application for you — no root, nothing outside your
  home directory. You get **Enigma Launchpad** in your application launcher, an `enigma-launchpad` command, and its
  icon.
- The .NET runtime is bundled by default; `--framework-dependent` uses an installed one instead.
- Run it again to upgrade. `packaging/linux/uninstall.sh` takes it all away and leaves your shortcuts and settings
  alone.

### Compatibility

- Windows 10/11, Linux (X11) and macOS, from the same code. Programs' own icons are read on Windows only. Elsewhere
  an application tile shows its kind's icon, or the image you chose.
- .NET 10: bundled with the application by the Linux installer, or installed, for a framework-dependent build.
- Data files from the development builds (0.1.0) load as they are. A file written before profiles existed is read
  into a single *Default* profile.

### Version

- Initial release: **1.0.0**.
