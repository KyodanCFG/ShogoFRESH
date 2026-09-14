# ShogoFRESH — the feature tour

*Shogo: Mobile Armor Division* came out three weeks before Half-Life, and
history looked the other way. Monolith's anime mecha shooter — one level a
transforming 30-foot mobile armor striding through a city, the next a
corridor fight on foot — never got a sequel, a remaster, or even a port
that runs properly on a machine made this century.

ShogoFRESH is that port, and then some. In March 1999 Monolith released the
game's source code — not the engine, the *game*: weapons, AI, HUD, rules.
ShogoFRESH rebuilds that code on a modern toolchain and wraps it in a
launcher that does everything the 1998 era made painful. If you follow
recompilation projects, this is the same family of work with a friendlier
starting point: official source instead of a decompiler, the original
closed engine kept underneath and worked around rather than replaced. The
game's art, sound, levels and story are untouched — every asset is
Monolith's 1998 original.

You need your own copy (Steam or GOG). The rest is free, and the launcher
and tools are open source.

This page is the tour. [BIBLE.md](BIBLE.md) is the full component-by-
component reference, and the [changelog](../../CHANGELOG.md) records all
300+ releases — not just what changed, but how each change was found.

---

## The 1998 friction, removed

Out of the box, Shogo on modern Windows fails to start without a legacy
input shim, renders stretched 4:3 with a broken field-of-view formula, and
draws a 640×480 HUD on your 4K display. The launcher's one-click setup
handles all of it — every fix applied with a backup and an undo.

Then the game itself gets what it should have had:

- **True widescreen** with a correct FOV calculation, not a stretched image.
- **HUD and text drawn at your resolution**, not upscaled from 640×480 —
  with a scale dial, an ultrawide mode that holds the HUD in a centred
  band, and a crosshair that stays at true centre.
- **Native resolution, borderless and windowed modes**, 32-bit rendering,
  anisotropic filtering, MSAA.
- **Raw mouse input**, mouse wheel binding, two binds per action, and a
  manual reload key — which 1998 had no way to express at all.
- **Reload animations, sounds and equip timing that actually play.** The
  original models disagree on animation name spelling, so half these
  lookups had been failing silently since release day.
- Quality-of-life throughout: autosave on objective (in its own slot, so
  it never eats a manual save), an always-available mission log, JPEG
  screenshots, a profanity filter, a streamer mode that anonymises every
  name on screen, an opt-in background-render mode so alt-tabbing doesn't
  freeze the world.

## Multiplayer that works today

- **A server browser** in the launcher: live player counts, ping, one-click
  join, favourites — plus filters for bots and for fleet (ShogoFRESH
  servers, Classic servers, everything).
- **Hosting from a form**, not a 1998 wizard: rotation, limits, rules,
  blocked weapons, server profiles, a firewall button.
- **A rebuilt dedicated server** with remote console, admin mute, held
  intermission scoreboards, and map rotations that can assign a game mode
  per map.
- **Discovery that survives.** Shogo has already outlived one master
  server. The browser merges several independent sources, servers exchange
  peer lists and introduce each other, and listing on the community
  website is one checkbox. No single site going quiet takes the game down.
- **Server-side validation** against the exploits that plagued 1998 —
  including the classic fire-through-walls check, with tolerances
  inherited from the community's own 2001 anti-cheat, which calibrated
  them on a live tournament server.

## Bots worth shooting at

The headline: **servers are never empty**. Bots hold seats until humans
arrive, hand them over when someone joins, and come back when they leave.
They score, they respawn, they show up on the scoreboard like anyone else.

The 2026 AI overhaul gave them — and the campaign's enemies — actual
character:

- **Personas.** Rushers, snipers, lurkers. A server full of bots plays
  like a server full of different people.
- **Darkness works.** An unlit corner is somewhere to hide: AI sight
  shortens with the target's lighting. This is Monolith's own 1998 stealth
  system, found switched off behind a build flag with an averaging bug —
  fixed and enabled, twenty-eight years late.
- **Sound carries.** Gunfire is heard through walls — muffled, at half
  radius — so a firefight in the next room turns heads instead of being
  politely ignored.
- **They fight like they mean it.** Suppressive fire rattles their aim,
  skilled marksmen lead moving targets, hurt bots run for medkits,
  projectile users hold their range band, and a retreat has a destination
  instead of being a panic in place.
- **Teach them your map.** Walk a route once with the path recorder and
  bots patrol it. Custom maps get competent bots without anyone writing
  waypoint files by hand.

Every system has its own switch, so a purist server can turn any of it off.

## Game modes

- **Deathmatch**, as it ever was.
- **TOWs Out** — every weapon pickup is a rocket launcher, and so is
  everything in your hands.
- **Squishie** — you, on foot, at one-fifth scale, against mechs the size
  of buildings. Type `!squish` in chat and take the fight to their ankles.

Stock, unmodified clients can play all of them — scale, camera and rules
travel over messages a 1998 client already understands.

## Classic, kept honest

Preservation includes the feel. The **Classic ruleset** restores the 1998
balance exactly: the original magazine behaviour driven by the original
animations, the original throw arcs, the original names. The doctrine,
written at the top of the weapons code:

> **Classic restores the 1998 tuning, never the 1998 defects.**

You get 1998's game back. You do not get back its crashes, its vanishing
HUD, or its kill feed naming the wrong weapon.

And compatibility runs **both directions**: stock clients join ShogoFRESH
servers, ShogoFRESH clients join stock servers — where the mod's behaviour
reverts to stock automatically, so nobody has to pick a side and the
existing community keeps its players.

## The campaign

Single player gets the same care: a mission log you can read without
pausing, autosaves that respect your saves, `ClassicCampaign` for the
untouched 1998 tuning, and enemies that inherit the AI overhaul — losing
you in the dark and reacting to the sound of your approach.

For runners, there's a **speedrun kit**: a real-time clock and an in-game
clock (loads and menus free, so runs compare across machines), automatic
splits at every level, quickload counting, cheat tainting, and an
append-only log that doubles as the moderator's validation artifact and a
LiveSplit-watchable file. A registered restart-level key keeps retries
instant and the run clock honest.

## In your language

Every player-visible string exports to a single JSON file, and a finished
translation builds back into the game with one command. Language packs are
plain text files dropped into `Custom\` — no tools, no archives — and an
optional menu-font mode renders the accents the 1998 bitmap fonts never
could. English ships; German and Spanish packs have been verified in play;
the contributor guide is public and the pipeline is waiting for yours.

## Build for it

**[ShogoMAKE](https://github.com/KyodanCFG/ShogoMAKE)** is the creative
kit: build Shogo levels in **TrenchBroom** — the editor the Quake mapping
community already knows — with tutorials that walk real maps, entity and
texture checkers that catch mistakes before the compiler does, working
example maps, and a one-command compile-and-launch loop.

Modding needs no proprietary tools anywhere in the chain. A folder *is* a
mod: loose files in `Custom\` override the game's archives path-for-path.
The launcher opens and extracts the game's own `.rez` archives for
reference art, the texture format is documented and round-trips through
open converters, and the archive format spec is published. Mods can even
carry gameplay manifests — rules a mod may set, with a hard boundary
around what it may never touch (your identity, your network settings,
your moderation).

## Three hundred releases of receipts

The changelog is the audit trail: 300+ releases, each entry recording how
the change was found — which was usually by playing. Along the way,
ShogoFRESH fixed original 1998 bugs that had stood for decades, among
them:

- A chat message containing `%s` could **crash every client that saw it**,
  and the server too — the engine formats text twice, so correct code was
  still unsafe. Player text no longer reaches that path.
- **No Shogo server ever published a working frag limit** — the query
  protocol wrote it under the time limit's key, where the real time limit
  immediately overwrote it.
- The kill feed named **the weapon the killer was holding when the message
  arrived**, not the one that killed.
- Explosions dealt **full damage across their entire radius** — falloff
  with distance now actually falls off.
- Alt-tabbing during a level load came back to **a HUD with no crosshair,
  no health plate, and no explanation**, for the rest of the map.
- "Random" map rotation settled into a **two-map cycle**, because the
  random stream was reseeded on every shot fired.
- A player disconnecting could **crash the dedicated server** minutes
  later, when the AI next looked at where they had been standing.

## How this is built

ShogoFRESH is built by one person with heavy AI assistance, and every
change is verified in play against the real game before it ships. The
game's art is untouched — nothing here is generated art; think film
restoration, where the art is the artifact and the machinery around it got
rebuilt. The receipts are public, and if a project built this way isn't
for you, that's a fair choice: the original game is still there, and these
servers welcome stock clients precisely so nobody has to pick a side.

## Get started

1. **Own Shogo** — Steam or GOG, a few dollars on sale.
2. **[Download ShogoFRESH](https://github.com/KyodanCFG/ShogoFRESH/releases/latest)**,
   unzip anywhere, run the launcher, point it at your install.
3. **Play** — campaign, or the server browser. Want to build levels?
   [ShogoMAKE](https://github.com/KyodanCFG/ShogoMAKE) is the kit.

THIS LEVEL IS NOT MADE BY OR SUPPORTED BY Monolith Productions, or any of
its affiliates and subsidiaries.
