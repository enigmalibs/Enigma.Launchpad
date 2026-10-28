# Release notes

## 1.3.0 — 2026-10-28

A minor release:

- a **new application icon**: a rocket on a dark rounded square;
- on Linux, the installer adds a **96 px size and a scalable icon**.

### Application icon

- **The icon is a rocket in soft blues and green on a dark rounded square.** It replaces the launch glyph on a blue
  square, everywhere the application shows one:
  - the window and the taskbar, on every platform;
  - the `.exe` in Explorer, on Windows;
  - the application launcher, on Linux.
- **The `.ico` holds ten sizes**, from 16 to 256 pixels, against seven before. The 20, 40 and 96 pixel sizes
  give a scaled display a sharp frame.

### Install on Linux

- **Eight sizes and a scalable one.** `install.sh` puts the icon under `~/.local/share/icons/hicolor`:
  - 16, 24, 32, 48, 64, 96, 128 and 256 pixels as PNGs (96 is new);
  - an SVG under `scalable/apps`, also new, for any size the PNGs do not have.
- **`uninstall.sh` removes all nine.**

### Upgrading from 1.2

- **Nothing is converted.** `launchpad.json`, `settings.json` and export files are as 1.2 left them, and 1.2 still
  opens them.
- **On Linux, run `packaging/linux/install.sh` again.** It replaces the application and the icons in place. A desktop
  that cached the old icon may show it until its next refresh, or until you log in again.

### Dependencies

- **Microsoft.Extensions**, 10.0.13 → **10.0.14**, a .NET servicing update:
  - DependencyInjection.Abstractions;
  - Logging.Abstractions;
  - Hosting;
  - DependencyInjection, in the tests.
- **The Avalonia set is held back at 12.1.1**, with Enigma.Avalonia.Desktop **1.2.0**. Avalonia 12.1.4 and
  Enigma.Avalonia.Desktop 1.3.0 are out, but the set moves as a whole and in a release of its own.

### Version

- **1.3.0** is a minor release under Semantic Versioning. It changes the application's look and the Linux install,
  not its data: 1.2 and 1.3 read each other's files.

## 1.2.0 — 2026-10-15

A minor release:

- **group columns**: a group takes the full line of a workspace, two thirds or one third of it, and groups whose
  widths fit together share a line;
- a group can be **dragged beside another one**, onto its line.

### Group columns

- **A group's menu has a Width submenu**, after *Rename…*: *Full width*, *Two thirds* and *One third*. The current
  width is checked, and a change is saved.
- **Groups are laid out in lines of three columns, in their order.** A group that does not fit in what is left of a
  line starts the next one:
  - three ⅓ groups, a ⅔ and a ⅓, or a ⅓ and a ⅔ share a line;
  - a full-width group is alone on its line.
- **Cards on a shared line** sit at its top, each at its own height, so a folded group stays a header.
- **In a narrow window** — when a third of the line cannot hold a tile — every group takes the whole line, in order.
  The widths are kept, and come back when the window is wide enough again.
- **New groups take the full line**, as do all the groups you had: a workspace looks as it did until you change a
  width.

### Drag and drop

- **Drop a group beside another one.** Over a card narrower than the line, the group lands on the side of the card
  the pointer is over; over the empty end of a line, after its last card. Over a card that takes the whole line, the
  upper half is before it and the lower half after it, as before.
- **The dashed gap is as wide as the dragged group**: beside a card when it fits on that line, on a line of its own
  otherwise. The cards after it make room, and the gap holds still while they move.
- **The copy of a dragged group is as wide as its card** and follows the pointer both ways, inside the list. A
  full-width group's copy still moves up and down only.
- The rules are unchanged:
  - groups reorder whatever their kind;
  - a tile goes only to a group of its own kind;
  - dropping an item where it already is moves nothing.

### Changes

- **Move earlier and Move later.** In a group's menu, *Move up* and *Move down* are now *Move earlier* and *Move
  later*, with left and right arrows, as on tiles: in columns, the next group may sit beside a group rather than
  below it.
- **Narrow cards.** A long group name is trimmed with an ellipsis, with its count right after it. The sentence of an
  empty group wraps.
- **Export files carry the widths** that are not full: `twoThirds`, `oneThird`.
  - An import gives a group it adds its width; a group merged into one you have keeps yours.
  - A width this version does not know is read as the full width, and nothing is skipped for it.

### Upgrading from 1.1

- **Nothing is converted.** `launchpad.json` stays at format version 3:
  - each group gains a `width`;
  - a group without one, as 1.1 writes them all, takes the full line;
  - no copy of the file is made.
- **`settings.json` is unchanged.**
- **Export files stay at format version 2.** 1.1's exports import into 1.2 at full width. 1.2's import into 1.1,
  which ignores the widths.
- **Going back to 1.1:**
  - 1.1 opens a file 1.2 wrote, and shows every group at full width;
  - its next save writes the groups without their widths, so they are gone if you come back to 1.2.

### Dependencies

- None moved: only the Avalonia set is outdated.
- The Avalonia set is still held back at **12.1.1** with Enigma.Avalonia.Desktop 1.2.0, which is still its latest.
  12.1.4 is out, but the set moves as a whole.

### Version

- **1.2.0** is a minor release under Semantic Versioning. It adds group columns and dragging a group beside another
  one. The file format is unchanged: a 1.1 file opens as it was, and 1.1 still opens a 1.2 file.

## 1.1.0 — 2026-10-14

A minor release:

- **workspaces** take the place of the Applications, Directories and URLs pages. Each is a collection of its own,
  with a title, a description and a glyph, holding groups of every kind side by side. Your shortcuts move into one on
  their own;
- a fourth kind of shortcut, **documents**: any file, opened with the application your system opens it with;
- an application can be a **command** found on the `PATH` — `code`, `git`, `notepad` — instead of a full path;
- dragging in a workspace shows **where the item will land**, and a copy of it follows the pointer.

### Workspaces

- **The Workspaces page** is first in the navigation rail, and the launchpad opens on it. It shows the profile's
  workspaces as cards:
  - each card has its glyph, its title, its description, and how many groups and shortcuts it holds;
  - the search box above them filters the cards by title and description.
- **New workspace** asks for a title, a description of up to 200 characters and a glyph, one of 44. A title is
  required and must differ from the profile's other workspaces, ignoring case.
- **A card's ⋯ menu:**
  - *Open*;
  - *Edit…*;
  - *Duplicate…*, which proposes "*title* (copy)";
  - *Move up* and *Move down*;
  - *Delete…*, which asks first and says what goes with the workspace.
- **Open a workspace** by clicking its card, or pressing Enter on it. It takes the place of the list. The arrow at
  the top left, or Alt+Left, goes back.
- **In a workspace, groups of every kind sit side by side.**
  - **New group** asks which kind it holds: applications, folders, links or documents. An empty workspace offers one
    button per kind.
  - A group's name must differ only from the workspace's other groups of the same kind.
  - **Move to workspace…**, in a group's menu, moves it with its shortcuts to another workspace of the profile. It is
    refused when that workspace already has a group of the same name and kind.

### Documents

- **A documents group** holds files of any type. Clicking a tile, or pressing Enter on it, opens the file with the
  application your system uses for it.
- **The form** asks for the file. Browse… picks one, and names a nameless shortcut after the file, without its
  extension. A folder is refused; it belongs in a folders group.
- **The tile** has *Copy path* among its buttons, and *Show in folder* and *Try to get icon* in its menu. On Windows it
  shows the file's own icon.
- A file that no longer exists is not opened; the launchpad says so.

### Commands as applications

- **An application's target can be a command:** a program's name, such as `code`, `git` or `notepad`, with no folder.
  The form's field reads *Program or command*.
- **It is kept as you typed it** and looked up on the `PATH` each time it is opened. So it follows the `PATH`, and
  works on another machine that has the program.
- **The form checks it.** A name that nothing on the `PATH` answers is refused and named. When the text has a space,
  the form adds that arguments go in *Default arguments*.
- **Where it runs:** a command starts in your home folder. The current folder is never searched for it, so a program
  dropped wherever the launchpad runs cannot stand in for the one you meant.
- **On Windows:**
  - the `PATHEXT` extensions are tried;
  - the user's and the machine's `PATH` are read afresh each time, so a tool installed while the launchpad runs is
    found without restarting it.
- **Elsewhere,** only a file that may be executed counts.
- *Show in folder* and the program's icon follow the command to the program it finds.

### Drag and drop

- **A dashed slot opens where the item will land.** The groups below it, or the tiles after it, move to make room,
  and the slot holds still while they move.
- **A copy of what you drag follows the pointer:**
  - a tile is carried whole, from where you took hold of it;
  - a group is carried as its header, up and down;
  - the item itself stays faded in its place.
- **Dropping an item where it already is** moves nothing, and is no longer refused: a drag no longer starts on a
  refusal cursor.
- The rules are unchanged:
  - groups reorder whatever their kind;
  - a tile goes only to a group of its own kind.

### Changes

- **The Applications, Directories and URLs pages are gone.** The rail holds **Workspaces**, with **Profiles** and
  **Settings** at its foot. Your groups are in a workspace (see *Upgrading from 1.0*).
- **Folders and links.** A group of directories is a *folders* group, and a group of URLs a *links* group.
- **The Profiles page** sums a profile up as its workspaces and shortcuts.
- **Export and import carry workspaces:**
  - *Export…* writes the workspaces, groups and shortcuts you tick, each group with its kind;
  - *Import…* adds a workspace titled like an existing one into it. A group named like one of that workspace's groups
    of the same kind receives its shortcuts there.

### Upgrading from 1.0

- **Your shortcuts move into a workspace on their own.** At the first start:
  - each profile's groups go into one workspace called *Shortcuts* — applications first, then folders, then links —
    each group keeping its kind;
  - a profile with no groups gets no workspace.
- **`launchpad.json` moves to format version 3.**
  - The first time 1.1 reads the 1.0 file, it copies it to `launchpad.v2.backup.json` beside it. That copy is never
    replaced.
  - The file itself is saved in the new format at the next change.
- **Export files written by 1.0 still import,** into one *Shortcuts* workspace. 1.1 writes exports in format
  version 2, which 1.0 refuses.
- **`settings.json` is unchanged.**
- **Going back to 1.0:**
  - 1.0 cannot read a version 3 file. It would keep it aside as `launchpad.corrupt-<timestamp>.json` and start empty;
  - to go back, quit the launchpad and copy `launchpad.v2.backup.json` over `launchpad.json` before starting 1.0;
  - what you changed in 1.1 is not in that copy.

### Dependencies

- Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions and
  Microsoft.Extensions.Hosting 10.0.12 → 10.0.13.
- Microsoft.Extensions.DependencyInjection 10.0.12 → 10.0.13 and xunit.v3 4.0.1 → 4.0.2 (the test projects only).
- The Avalonia set is still held back at **12.1.1** with Enigma.Avalonia.Desktop 1.2.0. 12.1.4 is out, but the set
  moves as a whole.

### Version

- **1.1.0** is a minor release under Semantic Versioning. It adds workspaces, documents and commands, and makes drag
  and drop clearer. Workspaces replace the three category pages, but they hold the same groups: nothing you saved is
  lost, and 1.0's export files still import.

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
