# ShogoFRESH — the console variables

Every client-side setting ShogoFRESH adds, in one place. The launcher's
Settings tab covers the common ones with a UI; this page is for the rest,
and for people who like knowing what a switch actually does.

Open the console in game with the backquote/tilde key (left of `1`), type
the variable and a value, Enter. Names are case-insensitive. Settings
persist in `autoexec.cfg`, which the launcher also reads and writes.

Two rules save a lot of confusion:

- **Server variables need `serv` in front** when typed at the game
  console — `serv DamageDebug 1` — and that only works in single player
  or a game you are hosting. On a dedicated server, set them at the
  server's own console, in `ShogoSrv.cfg`, or over rcon. The full server
  set is in [SERVER-GUIDE.md](SERVER-GUIDE.md).
- **Where a message prints decides where you read it.** Client-side
  output appears in your console. Server-side output goes to the server —
  in single player that's the same console, but on a dedicated server
  it's the server window and its log, and your own console shows nothing.

---

## Display and HUD

| Variable | What it does |
|---|---|
| `FovX` | horizontal field of view |
| `HudScale` | HUD size; 0 (default) scales with your display |
| `HudAspect` | hold the HUD inside a centred band — 1.333, 1.777 or 2.333 — for ultrawide monitors; the crosshair stays at true centre |
| `HudTextShadow` | drop shadow behind HUD text |
| `HudNumberY` | fine vertical nudge for the HUD figures |
| `CutsceneHeight` | cutscene picture height, overriding the automatic sizing |
| `MenuFont` | 1 = draw menus in a modern font instead of the 1998 bitmap art — the only way a menu can show accented characters. Takes effect next time you enter a menu screen. Default 0, the 1998 look |
| `MuzzleFlashScale` | muzzle flash size; default 1.6, 1.0 = the 1998 sizes |
| `MarkClip` | clip decals to the surface they sit on, so a scorch doesn't overhang a step (default 1) |
| `MarkBright` | decal brightness 0–1, default 0.78 |
| `ExplosionScorch` | lingering scorch mark where an explosion went off (default on) |

## Camera

| Variable | What it does |
|---|---|
| `ThirdPersonDist` / `ThirdPersonHeight` / `ThirdPersonShoulder` | the chase camera: how far back, up, and over. Defaults 70 / 28 / 40. Tune distance and height together — pushing distance out alone flattens the view into the player's back |
| `ThirdPersonPitch` | how far the chase camera may look up and down, in degrees. Default 70, capped at 89. Stock hardcoded 45 |
| `ZoomSensitivity` | mouse feel while zoomed. **0 (default) scales with the magnification**, so every weapon's zoom feels like the same mouse. 1–100 is a fixed percentage of normal aim instead — 100 is 1:1, 10 is the 1998 behaviour |

## View model (your own screen only — nobody else sees these)

| Variable | What it does |
|---|---|
| `ViewModelX` / `ViewModelY` / `ViewModelZ` | nudge the first-person weapon right / up / forward, added on top of the original placement. Negative Z pulls it closer, negative Y drops it |
| `ViewModelX14` etc. | the same three axes for one weapon, weapon id on the end — a per-weapon correction added to the global |
| `ViewModelOff` | 1 ignores every nudge and shows the original table underneath, for honest A/B against stock |

## Gameplay

| Variable | What it does |
|---|---|
| `ClassicCampaign` | 1 = the 1998 single-player tuning, exactly: magazines, drops, carry limits, criticals, AI reaction. Fixes and presentation stay |
| `Gore` | 0 off, 1 realistic (machines spark, people bleed), 2 full — the 1998 behaviour and the default |
| `AutoSaveOnObjective` | quicksave when an objective completes, into its own slot so it never eats a manual save (single player, default on) |
| `MissionLog` | objective readout, top right (single player) |
| `KillFeedStyle` | multiplayer kill feed: 0 ammo icon, 1 weapon icon, 2 text (default) |
| `MusicInMultiplayer` | 1 restores level music in multiplayer worlds; off by default for stability. The campaign is untouched |
| `EnemyHighlight` | single player only: enemies stay legible in unlit rooms (default off) |

## Comfort and privacy

| Variable | What it does |
|---|---|
| `BackgroundRender` | 1 = keep rendering and stay connected while the game window is behind another application. Input is ignored while unfocused. Default off |
| `ProfanityFilter` | star out profanity in chat and names, on your screen only — the wire is untouched. Default on |
| `StreamerMode` | anonymise a session for broadcast: chat hidden and silenced, other players given stable generated aliases, your own name randomised per connect. Turning it off restores everything. Default off |
| `ScreenshotJpeg` | convert F8 screenshots to JPEG in `Save\screenshots` (default 1; 0 keeps raw BMPs beside the game exe) |
| `ScreenshotQuality` | JPEG quality 1–100, default 90 |

## Speedrunning

| Variable | What it does |
|---|---|
| `SpeedrunTimer` | 1 = the speedrun clock, top centre (single player): level time, in-game time, real time, with a `*` if a cheat tainted the run. IGT counts only in-world, unpaused, playing frames — loads and menus are free, so runs compare across machines. A run arms at a NEW GAME load with the variable on, splits at every level, notes quickloads, and appends everything to `%APPDATA%\ShogoFRESH\Logs\speedrun.log` — readable by a moderator, watchable by LiveSplit |
| `RestartLevel` | a bindable action (deliberately unbound by default): instant level restart with honest run accounting. Bind it in the launcher's Keybinds tab |
| `Record <world> <file>` / `PlayDemo <file>` | the engine's own demo record/playback, registered since 1998. **Status: experimental and untested** — try it, but don't build a run archive on it yet |

## In-game admin (any ShogoFRESH client, with the server's password)

Set `RconPassword` once at your console to match the server's, then
`Rcon "<command>"` — the reply arrives as chat. Everything an admin can
do is a server variable, so anything in the server guide works here:
`Rcon "NextLevel"`, `Rcon "Mute 3 10"`, `Rcon "Kick 4"`,
`Rcon "TimeLimit 20"`. Details and cautions in
[SERVER-GUIDE.md](SERVER-GUIDE.md).

## Diagnostics

Turn on a channel, reproduce the problem, read the trace. Attach it when
reporting a bug and you've done half the diagnosis.

| Variable | Side | What it traces |
|---|---|---|
| `WeaponDebug` | both | weapon, ammo and animation lifecycle |
| `HudDebug` | client | HUD surface lifecycle — for missing panels |
| `CamDebug` | client | the chase camera once a second, including asked-vs-got distance (they disagree when a wall pulled the camera in) |
| `ExplosionDebug` | client | 1 draws a wireframe sphere tracking the true damaging radius of each blast (the fireball you see is drawn about twice that size); 2 adds a calibration cube. `ExplosionDebugTime` holds the peak, default 10 s |
| `FocusDebug` | client | window activation and the background-render hook |
| `ModDebug` | both | mod manifests: found, parsed, applied, refused |
| `MapDebug` | client | which directories were searched for custom levels |
| `AnimDebug` | server | character animation transitions and footsteps |
| `DamageDebug` | server | every damage event with the arithmetic shown — needs `serv DamageDebug 1` |
| `ProjDebug` | server | how each projectile left the world |
| `StoryDebug` | server | level triggers, transmissions, dialogue queue |
| `AiDebug` | server | AI sight cuts from darkness, through-wall hearing, suppression — needs `serv AiDebug 1` |

Server-side channels can't be switched on from a client connected to a
dedicated server — deliberately, so a guest can't spam a host's console.
Use `Rcon`, the server console, or `ShogoSrv.cfg`.

One curiosity worth knowing: `LightTest <n>` lays n dynamic lights in a
row for 30 seconds. The engine has ten dynamic light slots for the whole
world — ask for twenty and count the pools.

## Weapon tuning dials

These are **server** variables — in single player or a hosted game,
`serv` in front works (`serv NadeFuse 2`); on a dedicated server use its
console, config or rcon. Every one of them defaults to 0, meaning "use
the built-in table", so an untouched server behaves exactly as shipped.
**None of them apply under the Classic ruleset**, where 1998 decides
everything. Weapon ids go on the end where a dial is per-weapon —
`Fire14` is the shotgun.

| Variable | What it does |
|---|---|
| `Fire<id>` | seconds between shots for one weapon (0.05–5.00). Can only make a weapon FASTER — it cuts the fire animation short, and a value longer than the animation does nothing |
| `Equip<id>` | seconds from draw to first shot (0.05–5.00) |
| `Reload<id>` | reload seconds for one weapon (0.10–5.00). For the shotgun this is the whole shell-by-shell reload |
| `NadeVelocity` / `NadeAngle` / `NadeDrop` | the grenade launcher's throw: speed (100–4000), elevation (−45–60°), release drop |
| `NadeFuse` / `NadeBounce` | grenade fuse in seconds from launch (0.5–10), and speed kept per bounce (0.05–0.95) |
| `MineVelocity` / `MineAngle` / `MineDrop` | the sticky mine launcher's throw, same clamps as the grenade's |
| `MineArm` / `MineRange` / `MineFuse` | seconds after landing before a mine is live (0.5–15), its trigger radius (20–400, deliberately shorter than the blast), and its self-destruct fuse from landing (5–300 s) |
| `MineLimit` | live mines one player may have out (1–16); one more detonates their oldest instead of refusing the throw |
| `FallDamage` / `FallThreshold` | what falling costs, and how far you fall for free. These two also have a launcher UI on the Host tab |
| `SquishScale` | how small a Squishie is (0.08–1.00, default 0.2); applies on the next respawn |
