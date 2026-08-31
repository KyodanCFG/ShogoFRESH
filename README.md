# ShogoFRESH

A modernisation of *Shogo: Mobile Armor Division* (Monolith Productions,
1998).

Shogo runs badly on modern Windows: it fails to start without legacy input
support, renders at 4:3 with a broken field-of-view formula, shows a HUD
built for 640×480 on a 4K display, and its multiplayer was tuned for 56k
modems. ShogoFRESH fixes that in one download and adds the things the game
never had.

**It works with your own Steam or GOG copy. No game files are redistributed.**

→ **[Download the latest release](../../releases)**

---

## What ShogoFRESH does

**One-click setup.** Detects your install and applies every compatibility fix
with a backup and an undo for each: the input shim the game needs to start at
all, the graphics wrapper that lets it render on modern hardware, the music
fix, and DirectPlay for multiplayer.

**The game, modernised.** True widescreen with a correct field-of-view
calculation. A HUD built at the size it is drawn instead of upscaled from
640×480, with an ultrawide mode. Native resolution, borderless and windowed
modes, 32-bit rendering, anisotropic filtering and MSAA. Raw mouse input,
mouse and wheel binding, two binds per action, a manual reload key.

**Multiplayer that works today.** A server browser with live player counts and
one-click join. Hosting configured in the launcher rather than a 1998 wizard.
Bots that patrol, score and hold a scoreboard slot, and give the seat up
when a person arrives. Server-side validation against the exploits that plagued the
original.

**Servers stay findable.** Shogo has already outlived one master server. The
browser merges several independent sources and servers introduce each other
directly, so no single site going quiet takes the game with it.

**Original bugs fixed.** Dozens, including some that shipped broken in 1998
and stayed that way.

## What this repository is

The parts of ShogoFRESH that are **entirely original work**:

| | |
|---|---|
| `Launcher/` | the launcher. WPF, .NET 8: server browser, host configuration, settings, mod management, one-click setup |
| `Docs/public/` | the manuals: what ShogoFRESH changes, and how to run a server |
| `Tools/` | the checks that run before every release |

Building things for Shogo — maps, textures, mods, translations — has its own
home: the **Shogo Creative Kit**, released separately. It carries the editor
configuration, the format documentation, the tools, worked examples and
tutorials.
| `CHANGELOG.md` | release history; recent releases carry their notes on the [Releases](../../releases) page |

## Building the launcher

```
dotnet build Launcher/ShogoLauncher/ShogoLauncher.csproj -c Release
```

.NET 8 SDK, Windows. No dependency on the game or on the C++ tree: it builds
and runs standalone, though without a Shogo installation there is not much
for it to configure.

## What is not here

ShogoFRESH also contains modified versions of `CShell.dll`, `Object.lto`,
`CRes.dll`, `SRes.dll` and `ShogoSrv.exe`, built from Monolith's official
Shogo v2.2 source release of March 1999.

**That source cannot be republished.** Its licence is explicit: *"Source Code
is NOT public domain. You may not freely distribute it to any BBS, CD, floppy
or any other media."* A repository is other media.

What the same licence **does** permit is distributing the compiled result for
free, which is what every release here is. So the binaries are public, in
Releases; the launcher and tools are public, here; the modified game source is
not, and will not be.

## Licence

Everything in this repository is MIT licensed; see [LICENSE](LICENSE).

That covers the launcher and the tools. It does **not** cover Shogo itself,
its assets, or Monolith's source release, none of which are here. See
[NOTICE.md](NOTICE.md).

## Credits

ShogoFRESH by KyodanCFG. Built with heavy AI assistance; every change
verified in play against the real game.

THIS LEVEL IS NOT MADE BY OR SUPPORTED BY Monolith Productions, or any of its
affiliates and subsidiaries.

With thanks to Monolith Productions for the game and for releasing its
source; Cristobal for the Stainless Steel widescreen work; elishacloud for
dinputto8; Dege for dgVoodoo2; and NetworkDLS for the community master server
that kept Shogo findable for years.
