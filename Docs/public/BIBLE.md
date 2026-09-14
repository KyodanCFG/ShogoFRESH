# The ShogoFRESH Bible

Everything ShogoFRESH changes, adds or fixes, component by component, with
the reasoning. [FEATURES.md](FEATURES.md) is the tour;
`Launcher/PackageReadme.txt` is the short version that ships to players.

**Last full revision at 0.12.1.** Sections 0–3 describe the foundations,
which have been stable since 0.8; sections 4–8 cover the systems that
arrived after them — game modes, the AI overhaul, localization, the
creative kit, and speedrun support. What individual releases changed is in
the changelog and the release notes.

---

## 0. What ShogoFRESH is, and what it can touch

Shogo runs on LithTech 1.x. The engine executable, `Client.exe`, is closed —
Monolith never released its source and the licence forbids modifying it. What
*was* released, in March 1999, is the game code:

| Binary | What it is | Ours? |
|---|---|---|
| `Client.exe` | the engine | **no** — never modified, never redistributed |
| `CShell.dll` | client game code: HUD, camera, input, effects | yes |
| `Object.lto` | server game code: weapons, AI, rules, physics | yes |
| `CRes.dll` / `SRes.dll` | string resources | yes |
| `FreshSrv.exe` | dedicated server | yes — rebuilt from `ShogoServ/`, installed beside the stock `ShogoSrv.exe` rather than over it |
| `ShogoFRESH.exe` | the launcher | yes — new, WPF/.NET 8 |

Two consequences shape the whole project:

1. **Anything the engine does, we work around rather than fix.** No alpha
   blending, no animation playback-rate control, no way to be told the window
   was restored from an alt-tab.
2. **Game DLLs must live inside a `.rez` archive.** Loose `CShell.dll` or
   `Object.lto` in the game folder are silently ignored. ShogoFRESH ships as
   `ShogoFRESH.rez` containing exactly those four DLLs, so asset mods are
   unaffected.

### The two ruleset gates

Almost every balance decision passes through one of two questions, and
confusing them is the single easiest way to break something:

- **`UseFreshRules()`** — true everywhere, *including the campaign*. Governs
  magazine sizes, reload behaviour, damage falloff, AI reaction. Turned off
  in single player by `ClassicCampaign`.
- **`UseFreshEconomy()`** — multiplayer only. Governs pickups, carry limits,
  respawn behaviour.

The doctrine for Classic, written at the top of `Shared/WeaponDefs.h`:

> **Classic restores the 1998 tuning, never the 1998 defects.**

Classic gives back the original magazines, drops, carry limits, criticals,
AI reaction and gibbing. It never gives back a crash, a silently-failed
animation lookup, or a HUD that vanishes after an alt-tab.

---

## 1. The launcher (`ShogoFRESH.exe`)

WPF on .NET 8. Five tabs. It configures the game by writing the same files
the game already reads — `autoexec.cfg`, `ShogoSrv.cfg`, `dgVoodoo.conf` —
so nothing it does is invisible or unreversible by hand.

### 1.1 Play

- **Server browser** with sortable columns: server, address, map, type,
  players, ping, source, favourite.
- **Discovery is a union of sources**, not a master server (see §3.4). The
  **Source** column shows where each row came from; the launcher queries
  every address itself before showing it online, so an entry from a stranger
  is safe to accept — it is verified before it is displayed.
- **Bot filter**: all / with real players / no bots / populated. A
  bot-filled server looks busy, which is the point of filling it, but it
  makes "find a game with people in it" hard.
- **Fleet filter** beside it: everything / ShogoFRESH servers / Classic
  servers, read from what each server publishes about itself — so a purist
  and a FRESH player can each see their own scene at a glance.
- **Players column** shows the total with the bot count in brackets when the
  server reports one, and sorts on the total.
- Add a server by address, keep favourites (persisted), one-click join.

### 1.2 Host

Writes `ShogoSrv.cfg` and runs `FreshSrv.exe -config -go`. The config keeps
its 1998 name so existing server configs and profiles still work. Var names were
verified against a config written by the shogoservers.com v2.21 wizard, so a
config this launcher writes is one the community's tools understand.

- **Server**: name, UDP port (with an "Allow in Firewall" button that adds
  the inbound rule for the game port *and* the query port), max players, bot
  fill, remote console password, require-ShogoFRESH, list publicly.
- **Round limits**: frag limit, time limit, intermission length.
- **Game variables**: grappling beam, ramming damage, first person view only,
  infinite ammo, critical hits, quick turn, ruleset, map order, random
  pickups, blocked pickups editor, run/missile/respawn/heal scaling, world
  time speed and night colour.
- **Map rotation** from the full retail list (8 mech + 12 on-foot) plus any
  `Custom\*.dat`.
- **Server mods**: `Custom\*.rez` checkboxes.
- **Discovery**: registration URL and peer list, with "Fill from browser".
- **Server profiles**: named copies of `ShogoSrv.cfg`, so anything
  hand-edited into it survives too. Import/export.

Values that exceed a hard limit are clamped **and reported in the status
bar** — silently rewriting what somebody typed is how "I set bot fill to 60
and it ignored me" happens.

### 1.3 Mods

Lists `Custom\*.rez`, toggled by renaming the file. Rez files that contain
*game code* are flagged, because those override ShogoFRESH unless ShogoFRESH
is set to take priority — which is a checkbox here. Load order is
`-rez` last-wins; ShogoFRESH loads after `Custom\` by default.

### 1.4 Keybinds

Full rebinding for keyboard and mouse, including wheel. Two shipped layouts
plus whatever the player makes.

The hard part is not the UI. **Engine actions come from `AddAction <name>
<id>` lines in `autoexec.cfg`**, written in 1998 and listing only the actions
that existed then. A key bound to an unregistered action fails outright and
shows as unassigned. New actions (reload, holster, mission log) have to be
declared in the shipped `defkeybd.cfg` *and* registered at runtime for
existing installs. This is why the manual reload key never worked before.

### 1.5 Settings

Player identity, input, audio, gameplay, display, dgVoodoo output, advanced
launch flags, and the game installation path (Steam and GOG detected from the
registry).

Gameplay holds the client-side rules: gore, screen flash, chat sound, auto
third-person in vehicle mode, weapon auto-switch mode, HUD width band, HUD
number size, kill feed style, profanity filter, streamer mode, Classic 1998
campaign.

### 1.6 Game Setup

Detect / apply / undo for the five things a fresh Shogo install needs:

| Fix | What it does |
|---|---|
| DirectPlay | the Windows feature Shogo's multiplayer needs |
| `dinputto8` | DirectInput 1–7 shim, for mouse input on modern Windows |
| `dgVoodoo` | Direct3D wrapper — this is what makes the game render at all |
| AM18 | the music fix |
| ShogoFRESH | the mod itself, plus recommended defaults |

The two cards ShogoFRESH itself updates over time float to the top once
initial setup is done, and detect *updates*, not just presence.

### 1.7 Update notice

Checks GitHub Releases; a dismissible banner above the tabs.

---

## 2. The game client (`CShell.dll`)

### 2.1 Presentation

- **Widescreen** (the Stainless Steel layer, ported): correct FOV and
  aspect handling instead of a stretched 4:3 image.
- **HUD and text drawn at screen resolution**, not upscaled from 640×480 —
  the difference between crisp and mushy at 1440p.
- **`HudScale`** (0 = scale with display), **`HudAspect`** (hold the HUD
  inside a centred 1.333/1.777/2.333 band, for ultrawide — the crosshair
  always stays at true centre), **`HudTextShadow`**, **`HudNumberY`**,
  **`FovX`**.
- Ammo counter reads `12/48`. Magazine and reserve are both shown, because
  ShogoFRESH has magazines that matter.
- **Cutscene bars sized from the portrait** rather than fixed, so the top bar
  clears the profile picture and there are no side bars.
- **Scoreboard** rebuilt at HUD scale, in columns, with deaths and a match
  header (server name, map, limits).

### 2.2 Effects

- **Explosion scorch marks** (`ExplosionScorch`, default on). The first
  attempt used a dark dynamic light; the engine's lights are *additive*, so a
  dark light can never darken anything. It is a decal.
- Per-surface debris, heavier bullet impacts, Spider fireballs, shockwave
  rings, sniper impact light, impact glow as a sprite.
- **`MuzzleFlashScale`** (default 1.6; 1.0 restores the 1998 sizes).
- Car explosions no longer throw their smoke away from the wreck.
- **Footsteps charge for ground covered**, not just for animation keyframes,
  plus an early first step — so walking sounds like walking.

### 2.3 Weapons, client side

- **Reload animations, sounds, dips and equip windows.** Some PV models
  spell their animation names differently (`reload1`, `Selectm`), so those
  lookups had been silently failing since 1998 — genuine spelling
  differences, not casing: the engine's lookup turned out to be
  case-insensitive (fact 4 in §9). `ResolveWeaponAni()` tries the known
  spellings and reports which answered under `WeaponDebug 1`.
- Weapons with no reload animation **dip out of frame** instead, so the pause
  is legible rather than a frozen model.
- **Holster** by key or by volume, with animations, and no phantom firing
  while holstered.
- **Weapon auto-switch** is a client preference sent to the server on world
  entry: never / if new / if better (stock) / always. Holding nothing, or
  only a melee weapon, always switches.
- **Over-the-shoulder chase camera**, with configurable distance and
  rotation.
- **Zoom sensitivity that scales with magnification** by default, so every
  weapon's zoom feels like the same mouse — 1998 divided sensitivity by ten
  for *every* zoom regardless of FOV, the sniper's correct value applied to
  weapons it was wrong for. A fixed percentage is available for players who
  want a constant.
- **View-model nudges**, global and per weapon, added on top of Monolith's
  own placement table — plus a single off-switch that shows the stock table
  underneath, for honest A/B comparisons.

### 2.4 HUD feeds and chat

The feeds were rebuilt around **what a message means**, not who owns it —
pickups, kills and system notices are three different things and now look it.

- Kill feed with three styles: ammo icon, weapon icon, or text. In text
  style the weapon name is `#FF5555`. Environment deaths read `<victim>
  died` with the victim on the left.
- Chat at bottom centre, with a drop shadow, and a chat sound that can be
  silenced.
- Chat and the corner feeds **clear on a level change**, so bot "left the
  game" lines do not carry into the next map.
- The pickup feed hides while a transmission is playing.

### 2.5 Single player

- **`MissionLog`**: an objective readout, top right, always available, with a
  bindable full log that shows the last transmission text alongside — and no
  pause, so it can be read during play.
- "OBJECTIVE UPDATED" blink, frozen behind an opening cutscene.
- **`AutoSaveOnObjective`** with a **separate autosave slot**, so an
  automatic save never eats a manual one.
- **`ClassicCampaign`** — see §0.

### 2.6 Quality of life

- **`BackgroundRender`** (opt-in, default off) keeps the client rendering and
  connected while its window is behind another application. The engine's
  window is subclassed from `CShell.dll` via `GetEngineHook("HWND", …)`;
  deactivation is hidden from the engine and **input is gated on the real
  focus state**, or keys pressed in another application would drive the game.
- **HUD surfaces rebuild when missing**, not once per world. Surface creation
  fails *silently* while the game is in the background, so a level that
  loaded during an alt-tab came back with no plate art, no crosshair and no
  air meter for the entire map. There is no restore notification to hook, so
  rebuild-if-missing beats rebuild-on-event.
- **Profanity filter** (`ProfanityFilter`, default on — *including when the
  var is missing*, so an install the launcher never touched is still
  filtered). Display-side only: what reaches the wire is untouched and other
  players see whatever was typed. Whole-word matching, so Scunthorpe is
  safe; leetspeak folded (`0→o`, `3→e`, `$→s`…).
- **Streamer mode** (`StreamerMode`, default off). Three problems, one
  switch: chat HUD hidden and its chirp silenced; other players replaced with
  generated names that stay **stable for the whole session including across
  map changes**, keyed on the engine's client id; and your own name
  randomised on every connect. The name you typed is never overwritten, so
  turning the mode off restores it. Aliases are dropped on leaving, so
  reconnecting reshuffles — which is what stops a viewer following names
  between sessions.
- **Licence attribution** on the opening screen: author, contact, and the
  Monolith notice, as EULA clause 8(c)(iv) requires.

### 2.7 Diagnostics

Not scattered prints — **channels of one facility** (`Shared/FreshDebug.h`).
`FreshDebugOn(FRESHDBG_X)` / `FreshDebugPrint(FRESHDBG_X, …)`, one
implementation per DLL. Adding a channel is one `#define`; setting
`FRESH_DIAGNOSTICS` to 0 compiles all of them out, which replaces "remember
to delete the debug prints" with a single release decision.

`WeaponDebug`, `AnimDebug`, `HudDebug`, `FocusDebug`, `ProjDebug`,
`StoryDebug`.

---

## 3. The game server (`Object.lto` + `FreshSrv.exe`)

### 3.1 Weapons and the ammo economy

- **Magazines that matter.** ShogoFRESH magazine sizes across the board
  (shotgun 1 → 8, and so on), per-weapon reload times, reload on draw when
  the magazine is empty.
- **The reload bug that took four attempts.** `CPlayerObj` calls
  `CWeapon::Fire()` **directly** and never `UpdateWeapon()` — and
  `UpdateFiring`/`UpdateNonFiring` are the only code that advances
  `m_eState`. So for the *player's* weapon, `W_RELOADING` and `W_SELECT` are
  set and never cleared. Stock never noticed because nothing tested them; the
  moment ShogoFRESH tested them, the weapon stopped working after one reload.
  The fix is structural rather than a hazard to remember: `GetState()` returns
  `W_IDLE` unless the state is actually advancing, and reloading is a
  **deadline**, which needs nothing from the state machine and cannot outlive
  its own duration.
- **Blocklists**: `BlockWeapons <ids>` and `BlockItems <names>`. The level is
  rebuilt with same-tier replacements, so its layout survives.
- **`RandomPickups 0-4`**: none / weapons / items / both separately / both
  pooled.
- **`InfiniteAmmo`**, with TOW, grenades, Red Riot, Juggernaut and Spider
  excluded on purpose.
- Single player: endless starting sidearms, bounded enemy ammo drops, health
  and armour drop chances, reserve ceilings tightened against real magazine
  sizes.
- **The grenade launcher reworked**: under FRESH it fires a tumbling 3D
  shell instead of the 1998 sprite, with a real fuse, bounces that bleed
  speed, and a fairness constant — a grenade that has bounced deals reduced
  self-damage, always, because fairness rules that vary per server get
  discovered the hard way.
- **Thrown mines with a lifecycle**: an arming delay (a mine at your own
  feet cannot kill you while it settles), a proximity radius deliberately
  shorter than the blast, a self-destruct fuse counted from landing, and a
  per-player budget where placing one more detonates your oldest rather
  than refusing the throw.
- **Live tuning dials** for throw arcs, fuses, per-weapon fire/equip/reload
  timing — server variables meant to be dialled in while playing and then
  retired into the table. None of them apply under the Classic ruleset:
  Classic is 1998, artwork and stock tables included.

### 3.2 Damage and scoring

- **Explosion falloff fixed.** Radius damage applied at full strength across
  the whole radius; it now falls off with distance, down to a floor.
- **`CriticalHits` default off in multiplayer.** A 5% roll that doubles a hit,
  with no tell and nothing either player can do about it, decides duels
  invisibly. It was always live in 1998 deathmatch.
- **Kill attribution fixed.** The feed reported the weapon the killer was
  *holding when the message arrived*, not the weapon that killed. The weapon
  id now rides on `MID_PLAYER_FRAGGED`, encoded as `id+1` so that zero means
  "unknown" rather than "the first weapon".
- **Environment kills**: a death with no weapon behind it is attributed to
  the environment, which also covers suicides and drowning.

### 3.3 Bots

- **Player bots** (`BotAdd <n>`, up to 48) score, hold a scoreboard slot,
  patrol the level, and respawn into the same slot.
- **NPC bots** (`BotAddNpc <n>`) are target practice: no scoreboard presence,
  shown as "Enemy" in the kill feed.
- **`BotFill <n>`** keeps a standing target and **hot-seats**: a bot stands
  down whenever a real player needs the slot and comes back when one leaves.
- **`BotTag`** advertises the bot count in the server name, so the browser can
  show it even to tools that do not understand the `bots` field.
- **`BotRemove 1`** clears all; `Players 1` lists everyone with their kick id;
  `Kick <id>` disconnects one.
- What the bots are like to fight — personas, senses, suppression, the
  waypoint graph and the route recorder — is §5: the AI overhaul applies
  to them in full.

### 3.4 Discovery without a master server

Shogo has already outlived one master server (shogo-mad.com) and now relies
on a second. If finding servers depends on one site, the day that site stops
answering, every server still running becomes unfindable — even though
nothing is wrong with them.

- **`FreshSrv.exe` answers a `peers` query** with the servers it knows about,
  and **announces itself** to everyone in its own list, so learning is
  two-way and reaching *one* live server discovers the rest.
- Peers come from `peers.txt` beside the executable or `Peers` in the config;
  configured peers never expire, learned ones age out after six hours.
- Rate limited (`MayAnswer`, one answer per address every couple of seconds,
  shared across query types) so the port cannot be used for reflected
  amplification.
- `WebRegUrl` for master-server registration — **blank by default**, because
  the stock default pointed at shogo-mad.com and spent a request per cycle on
  a host that has not answered for years.
- **`SyncShogoServers`** checks the server in to shogoservers.com's JSON
  registration — the format the community master server itself speaks,
  confirmed against its published spec. A stable per-install identity means
  restarts *update* the server's row instead of littering new ones. **Off by
  default**, because it puts the server's address on a public web page
  rather than in the in-game browser; the launcher's Host tab makes opting
  in one visible checkbox ("List on shogoservers.com").
- **A server the master site vouches for can be joined even when its UDP
  probe goes unanswered.** Joins travel over TCP and queries over UDP, so a
  server reachable for the one can be silent on the other — a
  site-heartbeat row is offered for joining instead of being hidden behind
  a dead probe.

### 3.5 The query protocol

GameSpy v1 over UDP, on the game port + 149.

- `\info\` — now also publishes `bots`.
- `\rules\` — now publishes `mod` and `ruleset`, and **`fraglimit` under the
  right key**: it had been written as `timelimit`, whose second write
  overwrote the real time limit. No Shogo server has ever published a working
  frag limit.
- `\players\` — bots marked with a `bot_N` field.
- `\peers\`, `\announce\` — the discovery pair above.
- `\rcon\` — see §3.7.

Everything appended is appended, because **stock clients stop reading after
the fields they know**.

### 3.6 Match flow

- **Intermission** (`Intermission <sec>`, default 15, max 60, 0 = the stock
  instant switch). The scoreboard is held between maps and the next map is
  announced on it. Chat stays live. The freeze is enforced where each frozen
  thing lives: ShogoFRESH clients lock their own input, `HandleDamage`
  refuses damage so nothing changes the final score (which also covers stock
  clients), fire and weapon-sound messages are dropped, and bots stand down.
- **`MapOrder 0/1/2`**: in order, random, or random alternating mech and
  on-foot. Random used to settle into a two-map cycle, because `Weapon.cpp`
  reseeds the global `rand()` stream on *every shot* to keep bullet spread in
  step across the network — pinning it to one of 254 states. Level-start
  decisions use `FreshRandom()` instead.

### 3.7 Remote console

`RconPassword` in `ShogoSrv.cfg` enables it. The server runs the command and
replies with whatever the console printed.

**Blank disables the feature outright** — an rcon query to a server not using
it gets no reply at all, so nothing advertises that the capability exists.

There are two doors. **From inside the game** — `RconPassword "…"` once,
then `Rcon "<var> <value>"` — the reply arrives as chat, and the password
only ever travels to a server you are already connected to. That is the one
to prefer. **From outside**, the query port answers `\rcon\<pw>\<cmd>`, for
external tooling; off-the-shelf rcon clients do not speak this protocol
(they speak Source, Quake or Battlefield rcon), so anything used here has to
send a raw UDP string.

A command is `<var> <value>`, which is not a limitation: every
administrative command in ShogoFRESH is a server console var. That
includes moderation — `Mute <id> [minutes]` (an expiry, not a flag, so it
ends on its own; blocks chat commands too, so it cannot be worked around
with a vote), `Unmute`, `Kick`, and `NextLevel` to end the level now, the
same lever as the server window's own button so the two cannot drift.

**The password crosses the network in plaintext and always will.** GameSpy v1
is unencrypted UDP text with nowhere to put a challenge-response, and it has
to keep working for the stock server browsers. Use a password you do not use
anywhere else. Wrong passwords are rate limited through the same limiter the
peer exchange uses.

### 3.8 Anti-cheat and the mixed-client problem

Stock (non-ShogoFRESH) clients can join a ShogoFRESH server, and most rules
bind them automatically because those rules live in `Object.lto`: reload
pauses, the intermission freeze, blocked weapons, the ammo economy, the
ruleset.

Two do not, because they are decisions the client makes about its own input
and its own camera:

| Rule | What a stock client keeps |
|---|---|
| `QuickTurn 0` | the instant 180 |
| `FirstPersonOnly 1` | the chase camera |

That is backwards — it rewards *not* installing the mod. Three levers close
it:

- **`RequireFresh`** (default off) — reliable. A ShogoFRESH client announces
  itself by sending `MID_FRESH_PREFS`, which stock clients do not know
  exists; anyone who has not by ~20 s after entering the world is
  disconnected with a message saying why. Right for organised matches, wrong
  for a public server: the player base is small enough that turning away
  somebody who owns the game costs more than the advantage does.
- **`QuickTurnCheck`** (default off) — a heuristic. The server watches the
  rotation every client already sends and looks for half a circle arriving
  out of near-stillness. Three inside twenty seconds and fire is refused for
  a moment; the turn still happens, the shot it was for does not. It is off
  by default because a stock client sends rotation at **7 Hz** — ShogoFRESH
  raises its own to 30, but ShogoFRESH clients are exempt anyway — and at
  that rate a real fast spin can look the same — so detections print on the `WeaponDebug`
  channel whether or not the check is armed, and an operator watches their
  own server before arming it.
- **`FirstPersonCheck`** (default **on**) — not a heuristic. Every client
  states which view it is in on every update, because the server needs to
  know. A stock client firing from chase view has the shot refused and is
  told, every few seconds, that the server is first person only — because
  that is something they can put right in one keypress, unlike the 180.

ShogoFRESH clients are exempt from both checks: they cannot perform either
action there, so any detection from one would be a false positive by
definition.

- **`SkinnyCheck`** (default off) — reverse line of sight, the classic
  "firing through walls". A hacked client cannot move the player through a
  wall, but it can claim to be firing from the far side of one; the server
  traces from where it has the player to the muzzle the client claims and
  refuses the shot if a wall is in between. `FirePosCheck` bounds how *far*
  the claimed muzzle may be, this bounds what may be *between* — a metre
  through a wall and a metre down a corridor are the same distance.

  **Its tolerances are inherited from the 2001 community anti-cheat rez**,
  which shipped this check, found its false positives on a live tournament
  server, and wrote them down: surfaces you can legitimately shoot through
  are exempt, a blocker right at the muzzle is a corner being leaned past
  rather than a wall, and there is an allowance of sixteen infractions per
  life before it acts. That last number is somebody else's measurement and is
  better evidence than anything available here.

  What it does **not** do is what the 2001 rez did — kill the offender and
  name them in a file. Same objection as every other execution penalty: it is
  invisible to everyone else, reads as a bug to the person it happens to, and
  does not stop them. The shot is taken, which is the thing the exploit was
  for.

Also present, both off by default and both awaiting calibration:
**`FireRateCheck`** (token bucket on shot cadence) and **`FirePosCheck`**
(maximum distance between the claimed fire position and the player).

---

## 4. Game modes

`GameMode <n>` in the server config, and a rotation entry may be written
`world:mode` to override it for one map — so one dial covers an all-arena
server, a mixed rotation, and a one-off.

- **0 — Deathmatch**, unchanged.
- **1 — TOWs Out.** Every weapon pickup becomes a rocket launcher and
  everyone spawns holding one. No economy, no tiers, just rockets.
- **2 — Squishie.** On mech maps, `!squish` in chat respawns you ON FOOT at
  one-fifth scale; `!mech` climbs back in. Scale, camera offset and gore all
  travel over stock wires, so **unmodified 1998 clients can play it** — and
  NPC bots go squishie too. `SquishScale` tunes the size, applied on the
  next respawn so a value can be tried without a map change; eye height
  scales with it rather than being a separate dial.

Two supporting mechanisms, both rules-neutral by design:

- **`DeathSpectate`** (default off) gives a dead player a free-fly camera
  until respawn. Every client is ghosted server-side; only ShogoFRESH
  clients get the camera, because movement is client-authoritative. It
  exists as the mechanism queue-style modes need, not as a deathmatch rule.
- **`StartHolstered`** starts the weapon put away — on a per-level list, or
  everywhere — but never forced: the player may draw at will.

---

## 5. The AI overhaul

Fourteen commits shipped together in 0.12.0. All of it is FRESH-gated —
**the Classic ruleset keeps 1998's AI untouched** — and every system has
its own server switch, so any of it can be turned off per server.

### 5.1 Senses

- **Darkness shortens sight** (`AiLightVision`). The target's lighting
  scales AI visible range between 12.5% and 100%, so an unlit corner is
  somewhere to hide. This is **Monolith's own 1998 stealth system**,
  shipped behind a build flag that was set nowhere — with an averaging bug
  that read even brightly lit targets as dim, which is likely why it was
  switched off. Fixed and enabled. It also gates the fire solution: a
  target lost to darkness is shot at where it was last *lit*, or not at
  all, instead of being lasered through the dark it is hiding in. Single
  player + FRESH only: multiplayer bots stand in for players, and a player
  cannot be blinded by a dark corner.
- **Fire is heard through walls** (`AiWallHearing`) at half its sound
  radius — muffled, not mute; a silenced shot carries an eighth. Stock's
  own comment stated the rule ("can't hear through walls") for weapon fire
  while the identical check on impact sounds was commented out by Monolith
  themselves, so explosions always carried; this brings the fire sound to
  the rule the impact already followed. A heard target still cannot be
  *shot* through the wall.
- **Footsteps are heard**, scaled by movement, and multiplayer bots track
  **scent trails** — a cold trail is followed to where you were, not to
  where you are.

### 5.2 Behaviour

- **Suppression** (`AiSuppression`): near-miss fire rattles AI — roughly
  two marksmanship grades of extra scatter plus an immediate dodge,
  stacking to four seconds. Fed from the one impact site every bullet and
  blast passes through, shooter exempt from its own noise. Suppressive
  fire against a dug-in enemy is a real tactic: a rattled expert shoots
  like a mediocre trooper, not like nobody.
- **Skilled marksmen lead moving targets**; lower grades shoot where you
  are and miss where you will be.
- **Projectile users hold their range band** instead of closing to melee
  with a rocket launcher; targeting prefers the nearest threat.
- **Hurt AI self-care**: a bot low on health breaks for a medkit it knows
  about rather than trading down a lost fight.
- **Retreats have destinations.** Falling back means falling back *to*
  somewhere, instead of the stock panic-in-place.
- **Squad callouts**: bots announce contacts to nearby allies, so one
  spotting you can mean several converging.
- **Personas** (multiplayer bots): rushers, snipers, lurkers — each scales
  aggression, preferred range, and how hard suppression lands, so a full
  server plays like different people rather than one bot copied twelve
  times.

### 5.3 The waypoint graph, and teaching it

Bots move on a waypoint graph. On retail maps it is built for them; on any
map, **the path recorder teaches routes by walking them** — walk the route
once and bots patrol it. A custom map gets competent bots without anyone
hand-placing navigation.

Along the way the overhaul closed five stock AI defects, among them the
metronomic burst cadence (every enemy firing on the same clock), permanent
panic states, and broken target memory.

---

## 6. Localization

The blocker was never the strings — it was knowing which of the game's
**two text systems** draws what (engine fact 19). Windows fonts draw the
HUD, chat, transmissions and briefings, and render accents fine; the 1998
bitmap strips draw the menus and stop silently at ASCII 126.

What ShogoFRESH does with that:

- **One JSON of every player-visible string.** An export tool generates it
  from the game's resources; a build tool turns a translated copy back
  into compilable resources. The English resources stay the source of
  truth, and an automated check asserts the JSON still describes them and
  that the round trip is byte-exact.
- **Language packs are loose text files** in `Custom\Strings\` — no tools,
  no archives. A pack declares its own language in its first lines, which
  is also how ownership is decided: applying a new language removes the
  files the old one owned, including packs it finds rather than installed.
- **`MenuFont 1`** draws the menus in a Windows font instead of the bitmap
  strips — Monolith's own localization fallback, taken as an opt-in. It is
  the only way a menu ever renders an accent. Default off: the 1998 look.
- **Menu identifiers are accent-folded**, so a translated menu still
  matches the code that looks items up by name.

English ships; German and Spanish packs are verified in play. The
contributor guide — export, translate, build, test — is published with the
creative kit (LOCALIZATION.md there).

---

## 7. Modding and the creative kit

The rule that shapes everything here: **a folder is a mod.** A directory
mounts into the engine's file tree at the root (engine fact 17), and a
mounted file overrides the same path inside an archive — verified in play,
side by side. So a texture, model or sound mod is loose files in
`Custom\`, and no proprietary tool appears anywhere in the chain:

- **The launcher opens and extracts the game's own `.rez` archives**
  (Mods tab → Open Archive…), which is how a modder gets reference art
  out without the SDK tool the licence forbids redistributing.
- **The texture format is documented** (DTXFORMAT.md) and round-trips
  through open converters, byte-exact. **The archive format is documented**
  (REZFORMAT.md) and the release build writes archives with our own
  writer, verified byte-for-byte against the original tool's output.
- **Mod manifests** may set gameplay rules (`ModRules`, default on) with a
  hard boundary: a mod may set *rules*, never the server's identity, its
  network settings, or its moderation. `FreshMods 0` ignores manifests
  entirely; `ModDebug` prints found/parsed/applied/refused.
- Packed mods beat loose files (last-wins), so loose is the easy path,
  not the winning one — and rez files containing *game code* are flagged
  in the launcher, because those override ShogoFRESH itself.

**ShogoMAKE** (github.com/KyodanCFG/ShogoMAKE) is the mapping half:
TrenchBroom configuration for Shogo, the entity definitions, tutorials
that walk real single-player and multiplayer maps, entity and texture
checkers that catch mistakes before the compiler does, example maps, and
a one-command compile-and-launch loop. The format documents above are
published with it, beside the tools they describe.

---

## 8. Speedrunning and capture

- **`SpeedrunTimer 1`** (single player): LVL / IGT / RTA, top centre. RTA
  is wall time and never stops; IGT counts only in-world, unpaused,
  playing frames — loads and menus are free, which is what makes runs
  comparable across machines. A run arms at a NEW GAME load, splits at
  every level transition, notes every quickload, is tainted (`*`) at the
  one choke point every cheat code passes through, and ends at the
  campaign's final world.
- **The log** (`%APPDATA%\ShogoFRESH\Logs\speedrun.log`) is append-only at
  a stable path: simultaneously the validation artifact a moderator reads
  and a file a LiveSplit watcher can follow.
- **`RestartLevel`** is a registered, deliberately unbound action —
  instant retries that reset the level clock and keep the run clock honest
  about attempts.
- **Capture**: the engine's F8 screenshot is converted to JPEG in
  `Save\screenshots` (`ScreenshotJpeg`, quality dial included), and
  **streamer mode** (§2.6) anonymises a session for broadcast.
- **Demo recording**: the engine has carried `Record` / `PlayDemo` console
  commands since 1998, registered and apparently never typed by anyone.
  Status: honest — untested, with a written test protocol; not yet a
  supported feature.

---

## 9. Engine facts that cost real time to learn

These are the ones that bit hardest. The full set lives in the project's
internal engineering notes (twenty-four facts and counting); this is the
short list, because every one of them explains a bug that looked
impossible.

1. **The server weapon state machine does not advance for the player** (§3.1).
2. **`-rez` is last-wins, and game DLLs must be inside a rez.**
3. **Engine actions come from `AddAction` lines written in 1998.** A key bound
   to an unregistered action fails outright.
4. **`GetAnimIndex` is case-insensitive** — settled by reading the engine's
   comparator, correcting what this list used to claim. Only genuine
   spelling differences between the models matter (`Reload` vs `reload1`),
   and those are what the animation-name resolver exists for.
5. **There is no animation playback-rate control.** An animation can be cut
   short, never sped up.
6. **`rand()` is unusable for game logic** — reseeded every shot.
7. **Client surfaces belong to the world that was loading when they were
   created**, and creation fails *silently* in the background.
8. **`CreateObjectProps()` fires `MID_PRECREATE` with `PRECREATE_STRINGPROP`**,
   which calls `Setup()` — zeroing the respawn delay for anything created at
   runtime.
9. **Extend the protocol by appending.** Reading past the end returns zero
   **for integer reads only** — `ReadFromMessageFloat` past the end returns
   something that casts to `INT_MIN`. That is what put `-2147483648` in the
   scoreboard's deaths column for every bot.
10. **`LauncherPrefs.Save()` only runs from the Settings tab**, so a pref
    bound from another tab appears to work and then reverts.
11. **`GetAlignement()` has no `ROGUE` case**, so ROGUE hates everything
    including itself.
12. **The client identifies itself by *sending* `MID_FRESH_PREFS`** — the
    arrival is the proof, not any field inside it.
13. **Only two rules cannot be enforced server-side**, and one of those two is
    still *detectable* even though it is not preventable.
14. **Clients send rotation at `CSendRate`, default 7 Hz** — ~143 ms between
    samples, which bounds any server-side reasoning about how fast someone
    turned. ShogoFRESH raises its own clients to 30; stock clients stay at 7.
15. **The engine's window can be subclassed from `CShell.dll`** — this is not
    binary work.
16. **The engine formats console text TWICE**, so `CPrint("%s", text)` —
    textbook-correct usage — is still unsafe, and a `%s` typed in chat could
    crash the server and every client that saw it. Nothing calls the engine's
    print directly any more; one wrapper strips `%` from anything a player
    can type, and an automated check keeps it that way.
17. **A directory mounts like a rez, at the file-tree root.** That is how
    loose maps have ever reached a client, why custom worlds load by bare
    name, and why a folder of loose files is a complete mod.
18. **An object handle outlives the object, and the engine will not say so.**
    A non-null handle proves nothing; anything held across frames is
    validated against the live object list or released on destruction. A
    disconnect crash that struck minutes late taught this one.
19. **There are two text systems**: Windows fonts draw the HUD, chat and
    briefings; 1998 bitmap strips draw the menus, and their glyph tables
    stop at ASCII 126 — a character past that vanishes without a trace.
    Localization lives or dies on knowing which system draws what (§6).
20. **1998 had magazines and an automatic reload all along** — the shotgun's
    pump and the grenade launcher's re-arm ARE reload animations playing
    after every round. The Classic ruleset's timing is driven by those
    animations, exactly as stock was, rather than by any modern table.

---

## 10. Provenance and licence

Built from Monolith Productions' official Shogo v2.2 source release (March
1999). Distributed free, as clause 8(c)(v) requires.

- `Client.exe` is neither modified nor renamed nor redistributed (8(c)(ii)).
- No modified Monolith assets are shipped.
- The creators' names and contact addresses, with the required notice, appear
  on the opening screen and in every online description (8(c)(iv)):

  > THIS LEVEL IS NOT MADE BY OR SUPPORTED BY Monolith Productions, or any of
  > its affiliates and subsidiaries.

- The source itself is **not** public domain under clause 8(b), which
  constrains where it can be published.
