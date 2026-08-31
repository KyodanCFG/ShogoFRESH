# ShogoFRESH — changelog

What shipped in each release, one entry per version — up to 0.9.39, where
this file stops. Later releases carry their notes on the
[Releases](../../releases) page instead.

ShogoFRESH is built from Monolith's official Shogo v2.2 source release (March
1999). `Client.exe` is closed and unmodifiable, so everything here is the game
DLLs (`Object.lto`, `CShell.dll`, `CRes.dll`, `SRes.dll`), the dedicated
server (`FreshSrv.exe`), and the launcher.

---

## 0.9.39 — 2026-08-05

**Fixes**
- **0.9.38 disabled every mod you already had.** On upgrading there is no
  `seen-mods.txt`, so the first scan decided *everything* was new and turned
  the lot off — nine archives at once on the reporter's install, which then
  vanished from the Host tab's rez list in the same moment.

  **The first scan now records and changes nothing.** "New" can only mean new
  relative to a list that exists; that is the only kind of new observable from
  the outside. A file appearing after that is still disabled on sight, which
  was the point.

- **The Host tab lists disabled archives too.** The Mods tab's on/off is a
  **client** decision — which archives your own game launches with. The Host
  tab's list is a **server** decision, and a dedicated server is usually not
  even the same machine. Hiding a mod from the host because the local client
  has it switched off conflates two unrelated questions, and it is how the
  list emptied. The name written into `ShogoSrv.cfg` is always the real
  archive name, because `.rez.off` is not loadable.

---

## 0.9.38 — 2026-08-05

**Launcher**
- **The Mods tab lists archives only.** Loose `.dat` maps in `Custom\` are not
  mods — they are already offered by the map rotation and the in-game level
  menus, and listing them a third time as something to enable or disable
  invited a toggle that means nothing.
- **A newly-seen mod arrives disabled.** Dropping a `.rez` into `Custom\` and
  having it silently join the next launch is how a load order changes without
  anybody deciding it did: `-rez` is last-wins, and a mod carrying game code
  can take over the whole install. Turning something on is a choice, and it
  should be made in the launcher rather than by the filesystem.

  "Seen" is remembered in `%APPDATA%\ShogoFRESH\seen-mods.txt`, so this
  happens once per file rather than on every scan — a mod you then enable
  stays enabled, because it is already known. An unreadable list makes
  everything look new, which is the safe direction to fail in.

---

## 0.9.37 — 2026-08-05

**Additions**
- **Admins can mute.** `Rcon "Mute <id> [minutes]"`, `Rcon "Unmute <id>"`.
  Default 10 minutes, capped at 120 — a mute measured in days is a ban nobody
  wrote down. Ids are the ones on the scoreboard.

  This is the first rung of the ladder `MODERATION.md` has argued for since it
  was written (notice → mute → kick → temp ban). Its complaint was that the
  tools "do nothing or they kill you", and the common case is a person having a
  bad night rather than a cheater.

  Three choices worth keeping when the rest of the ladder gets built:

  - **The server still sees and logs a muted player's chat.** Mute takes away
    an audience, not a record — an admin has to be able to judge whether it
    worked, whether it was fair, and when it can come off.
  - **Only the muted player is told.** A punishment with an audience escalates
    exactly the thing the cheapest rung exists to defuse. They are told on
    every attempt, too: shouting into a void is how a bad night gets worse.
  - **Chat commands are blocked as well**, so being unable to speak while still
    able to call a `!votekick` is not a loophole with an obvious first user.

  It holds an **expiry**, not a flag, so `MODERATION.md`'s "everything expires"
  is structural rather than something an admin has to remember. Per-session: a
  reconnect clears it, which is acceptable for this rung — somebody who
  reconnects to evade a mute has told you to reach for the next one.

  This also makes the MOTD's "Admins can mute, kick and ban" true, which is the
  better of the two ways to fix a false claim.

**Fixes**
- **Holstering while zoomed left the HUD hidden** — my regression from 0.9.36.
  `m_bDrawHud` was set in exactly one place, inside `HandleZoomChange()`, which
  only runs when a weapon change carries a zoom state. Every *other* way of
  ending a zoom left the HUD off, including two pre-existing paths nobody had
  hit yet (dying while zoomed, and an external camera).

  The HUD is now **derived** from the zoom state every frame rather than set by
  whichever path remembers to. Nothing else owns that flag — the only other
  writes are two "back to normal" resets — so no future path that clears a zoom
  has to know about it.

---

## 0.9.36 — 2026-08-04

**Fixes**
- **The zoom survived being holstered.** Draw the sniper rifle after putting
  something else away and you were already looking down its scope, without
  having asked. Zoom is weapon state and nothing cleared it: `HolsterBack` is
  not a weapon change, so it never reached the code that would. Cleared on the
  way **in** instead — a holstered weapon has no field of view to be wrong
  about, so the state is already right whatever happens next.
- **The message of the day appeared in single player.** There is no server
  there in any sense a player would recognise; it was the game talking to
  itself.

**Message of the day**
- **The HUD stands down while it is up** — pickup feed, kill feed, stats and
  chat. Chat had to be gated in its own manager: it draws from `CMessageMgr`,
  so the quiet flags the rest of the HUD honours never reached it. Lines are
  held rather than dropped and appear the moment the notice is dismissed.
- **Body text is smaller** (design height 10, unbolded) and no longer borrows
  the header's font. At the header's size it read as a series of announcements
  and ran out of screen after a dozen lines.
- **The client-side profanity filter covers it.** Same argument as chat and
  player names: whether a stranger's words are starred out is the *reader's*
  setting, not the writer's, and a server notice is a stranger's words like
  any other.
- **Every server says something.** A default ships two ways: the launcher
  seeds `%APPDATA%\ShogoFRESH\motd.md` if there is not one already — that
  folder is never touched by extracting the zip, so an edited file survives
  updates — and the game carries a compiled-in default for the case where
  somebody deletes it. A `motd.md` in the zip payload was rejected: it would
  overwrite an admin's rules on every update, and losing those to an upgrade
  is a far worse failure than a plain default.

---

## 0.9.35 — 2026-08-04

**Fixes**
- **`motd.md.example` never shipped, though 0.9.33 said it did.** I wrote it
  into `Launcher/Redist/shogofresh/`, which is a **staging** folder rebuilt on
  every package: `prepare-redist.ps1` carries an explicit `$shipped` allow-list
  and deletes anything else, on the stated principle that the payload should be
  *"what we mean to ship, not the union of everything we ever shipped"*. The
  script did exactly its job and printed `removing stale motd.md.example` while
  doing it — I did not read its output.

  The example now lives in `Launcher/Payload/`, a tracked source folder beside
  `Launcher/Fonts/`, and packaging copies it in and says so. Verified by
  reading the file list out of the built zip rather than by assuming.

  It ships as `.example` and never as a live `motd.md`: an installer dropping
  one in would have a server announcing our words as its own. Renaming it is
  also the moment an admin reads it.

---

## 0.9.34 — 2026-08-04

**Fixes**
- **"Max players" and the "Raw mouse input" controls were missing from the
  launcher.** Both caused by one careless edit of mine in 0.9.30: moving the
  zoom row used a **generic** anchor — `<StackPanel Grid.Row="2"
  Grid.Column="1" Orientation="Horizontal">` — replaced once. That string is
  not unique, and the first match in the file is in the **Host** tab, not the
  Input group.

  So the Max players text box was pushed to a row that does not exist, and
  Raw mouse input's controls never moved at all and collided with Invert
  mouse Y. Every label now pairs with its control; both grids verified row by
  row.

  A XAML anchor has to be unique the way a C++ one does. `replace(..., 1)`
  silently takes the first match, and "first match" is only the intended one
  when the string could not have matched anything else.

---

## 0.9.33 — 2026-08-04

**Fixes**
- **Weapon switching died after a voluntary holster, and it was a default
  argument.** `SetHolstered(DBOOL bHolstered, DBOOL bForced = DTRUE)` — and
  every caller that unholsters does so as `SetHolstered(DFALSE)` with no
  second argument. So putting your weapon away and taking it back out left the
  **forced** flag standing: the client received `holstered=0, forced=1`, and
  `ChangeWeapon` refused every switch, silently, for the rest of the level.

  Found by the 0.9.31 tracing in one run — `refused, holster is forced`,
  nineteen times — after I had guessed wrong at it twice.

  Fixed as an **invariant** inside `SetHolstered` rather than at each call
  site: not holstered means not forcibly holstered. There are three callers
  today and the next one would not have known either.

**Additions**
- **Message of the day.** A server drops a `motd.md` beside the game or in
  `%APPDATA%\ShogoFRESH\` and every joining client is shown it once, over the
  world, under the same header the intermission carries.

  **The player spawns immediately** and dismisses it with any key. Holding the
  spawn until the client acknowledged would mean a client that never does — an
  old build, a dropped packet — is stuck unspawned, and the timeout guarding
  that is more machinery than the feature is worth. The key is swallowed, so
  the one that clears the notice does not also fire a weapon you were not
  aiming.

  Markdown is a **subset**: `#`/`##` headings, `**bold**`, `- ` bullets, blank
  lines. Links, tables and images have nowhere to go in a bitmap font and are
  drawn as written rather than half-rendered into stray brackets. Capped at 40
  lines of 120 characters, and anything dropped is reported in the server log —
  an admin whose message is too long should hear it there, not from a player
  saying it stops mid-sentence.

  Re-read on every join, so editing the file does not need a restart.

  *(An example was claimed here and did not ship — see 0.9.35.)*

---

## 0.9.32 — 2026-08-04

**Additions**
- **The player cannot hurt his own side in the campaign.** Killing a scripted
  friendly is not a setback, it is an **unrecoverable softlock**: no AI script
  command sends a trigger message, so a scene ends when something walks into a
  trigger and a corpse walks nowhere. `StoryDebug` has printed `a SCRIPTED AI
  died mid-script` for exactly this, which was diagnosis rather than defence.

  Filtered on **who caused** the damage rather than on how, so it covers
  hitscan, splash and anything added later — splash being the usual culprit, a
  rocket at an enemy and a team-mate walking into it.

  Not invulnerable: only the player is disarmed, so enemy fire still hurts them
  and the firefight still means something. Single player, FRESH only, and
  `FriendlyFire 1` restores 1998.

  **The other half is written up, not pretended away** — a friendly killed by
  the *enemy* softlocks the same way, and that needs scenes that survive a dead
  actor. `Docs/BUGS.md` **Z1** carries the problem and three sketched
  approaches.

- **Custom levels read `Custom\` as well as the game root.** The root is where
  1998 put this, which means a downloaded map had to be dropped in among
  `Client.exe`, the DLLs and the `.rez` archives — a directory nobody should be
  told to open, and one where a map sharing a name with anything else is a
  collision. Both are read, so no existing install loses a map it already had.

---

## 0.9.31 — 2026-08-04

**Fixes**
- **A zoomed weapon could not fire.** Toggling zoom re-entered
  `ChangeWeapon` with the *same* weapon, and the `MID_WEAPON_CHANGE` at the
  bottom told the server to equip a weapon it was already holding. What came
  back was a fresh equip — the animation replayed and `m_fEquipEndTime` pushed
  out, so the weapon was permanently mid-draw. Invisible while only the sniper
  rifle zoomed (its equip is quick) and immediate once four mech weapons gained
  one. The message is no longer sent when the weapon has not changed.

**Additions**
- **The zooming mech weapons play the zoom in/out sound.** Every zoomable
  weapon except the assault rifle, which stays silent for the reason it always
  did — that one is a constant toggle because zooming changes its fire mode.
- **Campaign levels are numbered `3/17`.** The campaign is an *order*, and a
  list of names does not show one; the list is long enough to scroll, so
  "where am I and how much is left" was not answerable from the screen. Custom
  levels are not numbered — that list is a directory, not a sequence, and
  numbering it would invent an order that is really just whatever the
  filesystem returned.
- **"load level..." is now "custom level..."**, and sits below "campaign
  levels..." — the two above it are ways of continuing the game; that one
  leaves the campaign entirely.

**Diagnostics**
- **`WeaponDebug` names every silent weapon-change refusal.** "The key does
  nothing" has now been reported three times for three different reasons, and
  every path that declines is a bare `return`. Each now says which one, plus
  the transition it was attempting and the state it was in. The
  holster-then-switch failure is not fixed here — it is being measured instead
  of guessed at a third time.

---

## 0.9.30 — 2026-08-04

**Fixes**
- **Weapon switching died after holstering and unholstering.** `Deselect()`
  had a branch that did nothing: if the model was *already* on the deselect
  animation, it neither played anything nor raised `m_bWeaponDeselected`.

  Holstering borrows the deselect animation and leaves the model parked on it.
  If the weapon then has no **select** animation, `HolsterBack` never moves it
  off — so the next `Deselect` found the animation already set, took the dead
  branch, and never signalled completion. The weapon change parked in
  `m_nRequestedWeaponId` waiting for something that could no longer happen.
  **Fourteen of the twenty-one view models are missing animations**, so this
  was reachable across most of the arsenal.

  A function that means "put the weapon away" now always finishes putting the
  weapon away, whether that takes an animation or no time at all.

- **A weapon key while holstered now draws the weapon back out.** That request
  was sent only for a *forced* holster, which left the ordinary case worse than
  the restricted one: after putting the weapon away yourself, a number key
  switched the hidden weapon underneath without ever drawing it, so the key
  looked dead. Cycling and reload stay blocked only while forced.

**Launcher**
- **Zoom sensitivity sits directly under Mouse sensitivity**, where it belongs.
- **The slider starts at 100%, not 10%.** Match FOV is the default, so reaching
  the slider is a deliberate opt-out — and landing that person on 1998's tenth,
  the value this setting exists because it was wrong, is the least useful place
  to start. 1:1 is the neutral position to drag down from.

**Corrected**
- **There is no `COMMAND_ID_WEAPON_0`.** `autoexec.cfg` maps the *action*
  `Weapon_0` to command **79**, which is `COMMAND_ID_WEAPON_10` — so the melee
  slot on keyboard key 1 is the tenth command, not a zeroth one. I briefly
  "fixed" a missing case for it; the compiler caught that it does not exist.
  Fact 21's conclusion is unaffected.

---

## 0.9.29 — 2026-08-04

**Changed**
- **Match FOV is now the default.** Zoomed aim scales with the magnification
  unless you ask for a fixed percentage.

  Defaulting to 10 preserved 1998's bug for four weapons in order to avoid
  touching the one it was right for. Proportional gives the sniper rifle about
  **11** against that hardcoded **10** — so the weapon people actually zoom
  with barely moves — while the assault rifle goes from 10 to about **2.8**,
  which was the entire complaint.

  Changed in all four places that have to agree: the game's behaviour when the
  variable is unset, the launcher's tick, the launcher's two load paths, and
  the seeded `client-settings.cfg`. Preflight now asserts the seeded value, and
  the check was verified by breaking it — `ZoomSensitivity is 5, expected 0`.

---

## 0.9.28 — 2026-08-04

**Launcher**
- **Zoom sensitivity is a setting now**, in Settings → Input, under the mouse
  controls it belongs with: a **Match FOV** tick, and a 1–100% slider when it
  is off.

  Match FOV writes `ZoomSensitivity 0` — scale by how much the weapon actually
  magnifies, so every scope feels like the same mouse. Unticked, the slider is
  a straight percentage of your normal sensitivity, defaulting to 10 (the 1998
  behaviour).

  **The percentage is remembered while Match FOV is on**, so unticking returns
  you to your own number rather than to the default. And 0 is read back as the
  *mode*, never into the slider — a zero there would read as "no mouse at
  all".

---

## 0.9.27 — 2026-08-04

**Additions**
- **`ZoomSensitivity 0` scales zoomed aim by the magnification**, so every
  weapon's zoom feels like the same mouse.

  A fixed percentage still leaves the weapons feeling different from each
  other, because their zooms are not the same strength — one number cannot be
  right for both FOV 10 and FOV 40. The factor that makes them feel identical
  is how much each actually magnifies: the ratio of the half-angle tangents,
  which is the same relation governing how far a given mouse movement sweeps
  across the screen.

  Read from the **live** camera rather than the weapon's table, so sensitivity
  eases in with the zoom instead of snapping at the ends of the transition.

  **It checks out against 1998.** At the sniper's FOV 10 against a 90° hip
  view, proportional lands near 11 — and Monolith hardcoded 10. That constant
  was the sniper's correct value applied to every other weapon; the assault
  rifle at FOV 40 wants about 2.8 and was getting 10.

  `10` remains the default. `0` is proportional, `1`–`100` is a fixed
  percentage.

**Housekeeping**
- **The shogo-re session's DTX texture work is committed** — the format spec,
  the modding guide, the launcher's DTX validator, the writer updates and the
  preflight check that holds the two implementations to the spec. It had sat
  uncommitted in the shared tree for days, was inside every zip shipped today,
  and preflight's DTX check passes *because* those files exist, so it could
  never have simply been dropped. Not authored in this session; committed so
  it is a normal commit to edit rather than loose files nobody can safely
  touch.

---

## 0.9.26 — 2026-08-04

**Additions**
- **`ZoomSensitivity` — zoomed mouse speed as a percentage of normal.** 100
  means zoomed aim moves exactly as fast as hip aim; **10 is the 1998
  behaviour and stays the default**, so nobody's aim moves under them.

  1998 divided mouse movement by ten whenever the zoom was up — and by ten
  **regardless of how far you were zoomed**. It is one hardcoded constant, not
  per weapon and not per FOV, so the sniper rifle at FOV 10 and the assault
  rifle at FOV 40 pay exactly the same tax. The assault rifle magnifies about
  1.6x for a 10x sensitivity penalty, which is why its zoom feels stuck rather
  than steady while the sniper's feels about right.

  Clamped to 1–100 rather than trusted: zero would divide by zero and a
  negative would invert the mouse, and this is a value a player can type.

---

## 0.9.25 — 2026-08-04

**Diagnostics**
- **Cameras are named.** Every camera reported `#1`, so an `OFF` could not be
  matched to its `ON` — and a 15-second camera appearing to switch off in the
  same second it switched on turned out to be *two different cameras*, one
  timing out as another started. I came close to calling that a regression in
  the 0.9.17 camera guard on the strength of it.
- **Two flaws in my own dialogue timing, both mine, both corrected.** The start
  was stamped *before* `KillDlgSnd`, so the line being replaced was measured
  against its successor's clock; and the player never stamped it at all, which
  is why Sanjuro's lines reported 56s, 67s and 81s — measured from a clock
  nothing ever set.

**What the 0.9.24 measurements settled**
- **The server's timing is correct and the audio is not being cut by our
  code.** `3169.wav` is 20.49s and the engine allowed it 21.50s before calling
  it done; 3158 got 6.59s for 5.92s, 3175 got 8.03s for 7.15s. `PLAYSOUND_TIME`
  was my suspect in 0.9.24 and it is exonerated. No `CUT SHORT` fired anywhere.
- **It is not a size cap either.** 14 of the game's 2,091 sounds are larger
  than `3169.wav`, the biggest being 1.4 MB against its 452 KB.

  So the server runs the full length while the audio stops early — the client
  stops playing before the file ends, for a reason not yet visible from the
  server side. Reported as possibly not having happened before, which if right
  makes it a regression and a different search.

---

## 0.9.24 — 2026-08-04

**Diagnostics**
- **Akkaraju's line is 20.49 seconds long and the scene gives it 18.** Measured
  from `SOUND.REZ`: `3169.wav` is 451,870 bytes of 22050 Hz 8-bit mono audio —
  **three times longer than any other line in the briefing** (3158 is 5.92s,
  3175 is 7.15s, 3523 is 1.66s). The 2026-08-03 log has it starting at
  22:28:00 with the scene's next beat at 22:28:18.

- **`MajorCharacter` overrides `PlayDialogSound`,** so 0.9.23's tracing never
  saw a word he said — every `PlaySound` the briefing sent to `admiralh`
  produced no line at all, which read like the sound never starting when in
  fact it was a function I had not instrumented. Traced now.

- **Dialogue lines report how long they were allowed to run**, and whether the
  engine called them finished or something cut them off. "The engine believes
  this ended" and "the file ended" are different claims, and whether they agree
  is the entire question here.

  The suspect is `PLAYSOUND_TIME` — *"server must time sound"* — because
  `IsSoundDone` then answers from a server-side estimate rather than from the
  audio. If that estimate is short, `KillDlgSnd` fires early, which clears the
  dialogue-active flag, which drains the queue, which starts the next line.
  Unproven; the next run measures it.

**Noted**
- **Loading map 03 directly leaves Akkaraju absent.** He appears only when
  arriving from map 02, which sets story state on the way out (`Story:
  AngryAdm FALSE` and friends are sent by `02_QUARTERS`'s start trigger). Not
  a bug — a level warp skips the state the next level reads — but worth
  knowing before testing any briefing from a direct load.

---

## 0.9.23 — 2026-08-04

**Diagnostics**
- **`StoryDebug` reports every dialogue line that starts, and every one cut
  off before it finished.** For Akkaraju's briefing line, the last of the two
  cutscene faults.

  `PlayDialogSound` begins by calling `KillDlgSnd`, so **any new line stops
  the one in progress**. That is right when the previous line has ended and is
  the entire bug when it has not: a truncated line leaves the rest of a scene
  running on a schedule authored for its full length, which is exactly the
  reported symptom — the line cuts, and the animations no longer match.

  The dialogue queue exists to serialise these, so a cut means either
  something reached `PlayDialogSound` without going through the queue, or the
  next line was triggered too early. The log now names the speaker and the
  file on both sides of the transition, which separates those two without
  another round of guessing.

  `Dialogue: "<who>" starts <file>` on every line, and
  `Dialogue CUT SHORT on "<who>" - it was still talking` only when the sound
  being replaced had not finished. The second line is the fault; the one above
  it is the culprit.

---

## 0.9.22 — 2026-08-04

**Fixes**
- **The cutscene trigger guard was comparing against the wrong number, twice.**
  The 0.9.21 path tag ended the argument: `force1trigger` reports
  `ACTIVATE #1/#2/#3/#50/#100 via touch` — so it goes through the guarded
  path on every activation and the guard simply did not fire.

  Working back through it, one condition is left that can be false:
  `m_fTriggerDelay <= 0.0f`. Its `TriggerDelay` is a small **positive**
  number — and the config line printed it with `%.2f`, which renders anything
  below 0.005 as `0.00`. The value looked like the zero it is not, in two
  consecutive releases, and I read it as confirmation both times.

  The test is now `< TRIGGER_CONTINUOUS_GAP`: a delay shorter than a frame is
  not a delay, whatever the level stores. `TriggerDelay` is printed to four
  decimals so this cannot hide again.

- **The world name was still a level behind after completing a level.** 0.9.20
  cleared the cache except when switching worlds — but completing a level *is*
  switching, so the stale stamp was kept and `15_DEPOT` reported itself as
  `14_MARITROPA1`.

  The priority was backwards. `FreshWorldName` is re-read on every call and
  wins: the client sends it on every world entry, and the log shows its
  world-entry work landing **before** the server reaches `StartLevel`, so it
  is already this world's name by the time anything asks. The stamp goes stale
  because only one of the several ways into a world passes through
  `SwitchToWorld`. It stays as the fallback for the case that motivated it —
  a dedicated server with nobody connected has no client to hear from.

---

## 0.9.21 — 2026-08-04

**Additions**
- **Secrets, keys, upgrades and enhancements say so when you pick them up.**
  Until now exactly one pickup in the game produced any text — weapons. The
  gold SHOGO letters messaged *themselves* to disappear and told the player
  nothing at all; keys, upgrades and enhancements did the same, which is worse,
  because a key that quietly unlocks something two rooms later is not
  something a player ever connects to having walked over anything.

  The secret uses **`IDS_FOUND_A_SECRET`** — not a new string. It has been in
  the 1998 table at 1029 reading "You found a secret!" with no reference to it
  anywhere in the game. Monolith wrote it and never wired it up.

  Items report by **kind** rather than by their authored name: the wire shape
  is fixed at (byte, byte, float), and the name is level-editor data written
  for the designer. Only a genuine pickup speaks — a duplicate you walk back
  over is not a pickup, and saying so would be a line you could farm.

  `InventoryTypes.h` moved to `Shared/` so both sides read one copy of
  `IT_KEY` and friends, per the rule against a constant existing twice.

**Diagnostics**
- **Triggers report which of the four routes into `Activate()` they came in
  by** — touch, message, timed, or `Unlock()`. Public Nuisance's
  `force1trigger` fires 356 times in seven seconds while appearing to use none
  of them: never a message receiver, not a `TimedTrigger`, and never once
  tripping the touch guard, which prints on the first refusal of every episode
  so silence means it was never asked. All three cannot be true. Rather than
  guess a fifth time, the route is stamped at each entry and reported where
  they converge — the first three activations of every trigger, then every
  fiftieth, so a storm shows its shape without becoming the storm.

---

## 0.9.20 — 2026-08-04

**Fixes**
- **The server could be a whole level behind on which world it was in.**
  `GetCurrentWorldName()` caches, and only ever consulted the name the client
  reports when that cache was *empty*. `SwitchToWorld` is the one place that
  clears it — so every other way of entering a world inherited the previous
  world's name and kept it: a warp from the campaign levels menu, a loaded
  save, the first level of a new game.

  Seen 2026-08-03: level 04 loaded, then level 02 from the menu, and the
  server reported 02 as `04_SHUTTLEBAY`. **This is the explanation for the
  forced holster sometimes not clearing when entering a level** — the rule
  asks `FreshMapStartsHolstered(pWorld)`, and it was being asked about the
  level before. Anything else keyed on the world name was equally wrong.

  The cache is now dropped on entering a world unless `SwitchToWorld` just
  stamped it. Safe because the client sends `serv FreshWorldName <name>` on
  every world entry, so the fallback always has a current answer; the stamp is
  kept for the switching case because on a dedicated server with no players it
  is the only answer there is.

---

## 0.9.19 — 2026-08-04

**Diagnostics**
- **Triggers name whoever sends them a trigger message.** This is the last
  unlit entry point into a trigger and, by elimination, where the Public
  Nuisance loop lives.

  The 0.9.18 run narrowed it to one door. `force1trigger` activated **355
  times in a seven-second stretch** — a steady 55 a second that started when
  the cutscene camera went live and stopped the instant it timed out. Its
  touch path is provably silent: the occupancy guard never suppressed it once,
  and there is exactly one object by that name. `TimedTrigger` is off. So
  every one of those 355 activations arrived as a **message**, and the 0.9.18
  log showed nothing sending to it only because the trace covered
  `Trigger`-class senders and the sender is some other class.

  Now traced with the sender's name and class. The engine has no way to turn
  an object back into a class name — `server_de.h` will map a name to an
  `HCLASS` and answer `IsKindOf`, but not the reverse — so this probes the
  short list of classes that can actually send a trigger message and reports
  "other" if it is none of them, which is still an answer.

**Known, not yet fixed**
- Public Nuisance still replays a few seconds of the cutscene a second time.
  The 0.9.17 camera guard is what stops it hanging forever, and the 0.9.18
  occupancy guard is correct but sits on a path this trigger never uses.

---

## 0.9.18 — 2026-08-04

**Fixes**
- **The Public Nuisance loop, properly this time — the 0.9.17 guard asked the
  wrong question.** 0.9.17 got the camera half right: the scene now ends
  instead of hanging forever, which is why the loop "happens once and then
  breaks out". The trigger half did not work, and the log named the reason.

  0.9.17 asked *"is this the same object that was touching me last frame?"*.
  `force1trigger` is `AITriggerable` and one of the things it sends is
  `Script;FollowPath horace` — which walks horace **into the volume the player
  is standing in**. With two touchers they alternate, each looks new to the
  other's record, and the guard never fires. It held for exactly two frames —
  until horace arrived — and then the trigger went back to **56 fires a
  second**, re-sending his script every one of them, so he restarted his path
  forever and never left. A self-sustaining loop the guard was blind to.

  The question is about **occupancy, not identity**: a trigger fires when its
  volume goes from empty to occupied, and does not fire again until it has
  been empty. Who is standing in it does not matter.

**Diagnostics**
- **Triggers describe themselves once** — timed or touch, `TriggerDelay`,
  min/max interval, activation count, and who may trigger them. Whether a
  runaway trigger is touch-driven or on a timer decides which guard applies,
  and working that out from the outside cost a test run.
- **The trigger trace no longer prints eight blank lines per fire.** The
  unused message slots hold empty strings rather than null handles, so the
  stock null test lets them through. Check the contents, not the handle.

---

## 0.9.17 — 2026-08-04

**Fixes**
- **The Public Nuisance cutscene loop is fixed — a trigger that seals you
  inside itself.** The oldest open bug in the book, found by playing rather
  than by being asked to look, and now measured rather than guessed at.

  A trigger fires when something *enters* it. With `TriggerDelay` left at its
  default of zero, the stock test is `fTime >= fTime` — true on every touch
  notification — so the trigger fires once per frame for as long as the
  toucher stays inside. Harmless when you walk through a volume and out the
  far side. A softlock when you do not leave, and in Public Nuisance what the
  trigger *does* is close the doors around you: it seals you inside itself and
  then re-fires forever.

  Measured at **819 fires in fifteen seconds**, roughly one per frame. Each
  one reset the cutscene camera's seven-second timer seven seconds into the
  future, so the scene could never end, and the dialogue queue was being
  refilled at the same rate. Nothing in the level was wrong.

  Two guards, either of which would have been enough:

  - A trigger with **no** `TriggerDelay` no longer re-fires for a toucher that
    has never left. A trigger with a real delay asked to repeat on a timer and
    still does — the zero case never asked for once-per-frame, it just got it.
  - **A camera that is already on ignores a further `ON`** rather than
    restarting its clock. `ParticleSystem` has guarded itself this way since
    1998; the camera never did, so anything re-sending `ON` silently turned a
    seven-second scene into a permanent one.

  The earlier theory — that a dead scripted AI left the scene waiting on
  someone who could never arrive — is **wrong**, and the instrument said so
  before the fix went in: zero scripted AI deaths in the whole session, and
  the loop happens with the helpers alive.

**Diagnostics**
- **Triggers say what they fired and what they told to do it.** `StoryDebug`
  now names the trigger, its target and the message on every fire. A camera
  turning on is half a story without the name of the thing that turned it on,
  and working that out by hand cost a test session.
- **The dialogue-queue "blocked" line reports once per change, not once per
  frame.** The 0.9.16 log had hundreds of identical lines burying the three
  that mattered. A diagnostic that reproduces the storm it is diagnosing is
  not a diagnostic.

---

## 0.9.16 — 2026-08-03

**Diagnostics**
- **The game keeps a log file now.** Everything ShogoFRESH prints to the
  console is also written to
  `%APPDATA%\ShogoFRESH\Logs\freshgame-YYYY-MM-DD.log`, beside the crash
  reports and the dedicated server's own log.

  The dedicated server has had one for a while and the argument was always
  the same here: a diagnostic that lives only in a console scrollback is one
  you have to be looking at when the thing happens. `StoryDebug` names a
  cutscene fault in a single line, and capturing that line meant photographing
  the monitor with a phone, because the Windows screenshot shortcut minimises
  the game.

  **No console variable to arm it, deliberately.** The likeliest way a test
  session fails is that the fault finally happened and capture was not turned
  on — a rare bug you have now seen and not recorded is worse than one you
  have not seen yet, and a switch to remember reintroduces exactly that. It is
  free to leave on: only ShogoFRESH's own prints reach it, which is near
  silent in normal play and only becomes chatty when a debug channel is
  deliberately enabled.

  One file for both game DLLs, tagged `[S]` for the server half and `[C]` for
  the client shell — they are separate modules with separate static state, so
  each holds its own append-mode handle to the same path and every line is
  flushed as it is written. Lines interleave; they do not corrupt. Flushing
  per line matters more here than on a server: the sessions this exists for
  are the ones that end in a crash or an alt-F4 out of a level that would not
  stop looping.

**Documentation**
- **`##N` in a keybind file is a DirectInput scancode, correcting what 0.9.15
  said.** The conclusion there was right — `COMMAND_ID_WEAPON_N` is on
  keyboard key N+1 — but the reason given was "one-based keys against
  zero-based action names", which is wrong and gives nonsense outside the
  digit row. `DIK_1`=2 … `DIK_0`=11 is why the weapon slots come out one
  higher; it is a coincidence of that row, not a rule. The whole file decodes
  as scancodes and nothing else does: `##17`=W, `##19`=R, `##34`=G, `##42`=
  LShift, `##15`=Tab.

  Which also answers a question that had no answer: **F8 is the in-game
  screenshot key**, writing `ScreenshotN.bmp` beside `Client.exe` without
  minimising the game. F6 quicksave, F7 quickload.

---

## 0.9.15 — 2026-08-03

**Balance**
- **The sniper rifle no longer one-shots people.** It was not close to a
  one-shot, it was exactly one: 100 damage against a player's 100 hit points.
  That number was written for mechs, which have 1000, and it was correct right
  up until 0.9.0 gave the weapon to infantry.

  On foot it does **55**. With the +/-20% spread every weapon already gets
  that is 44-66, so it can never kill in one hit, and two clean hits land
  88-132 — usually a kill, occasionally not. Against the rest of the infantry
  scale (colt 20, AR 25, MAC-10 30, kato grenade 50, energy grenade 80, TOW
  90) that puts it where a sniper rifle belongs: behind the explosives, ahead
  of anything you can spray. Shogo has no headshot multiplier, so the one
  number carries the whole weapon.

  **In a mech it is untouched at 100.** Nothing was ever wrong with it there.
  The split is asked of the *shooter*, not the target — the same question
  `WeaponUsableOnFoot` asks — and it reads `IsMecha()` off the owner, because
  a weapon does not know which tier is carrying it. Classic is unaffected.

**Documentation**
- **Every weapon key number I have written down was off by one, and they are
  all corrected.** `Defaults/defkeybd.cfg` binds one-based keys to zero-based
  action names: `##2` (keyboard 1) runs `Weapon_0`. So `COMMAND_ID_WEAPON_N`
  lives on keyboard key **N+1**, and the on-foot sniper rifle — slot 9 — is on
  key **0**, not key 9. Classic's mech sniper is on **6**, not 5.

  This is now engine fact 21 in `CLAUDE.md`, with the rule stated once so it
  is not rediscovered: read the slot off the code, read the key off
  `defkeybd.cfg`, and never write one as if it were the other. It also settles
  an older disagreement in the user's favour — melee really is on key 1.

---

## 0.9.14 — 2026-08-03

**Diagnostics**
- **`StoryDebug` now traces cutscenes.** Cameras report going on and off, how
  many times they've been turned on, and whether they have an `ActiveTime` at
  all — a camera without one **never times out** and depends entirely on
  something else sending it an "OFF".

  That is the shape of a looping cutscene: turning ON more than once is a scene
  restarting, and the count says so in one line. A scripted AI dying mid-script
  is reported too, because **no AI script command sends a trigger message** —
  the end of a scene is a separate trigger that something has to walk into, and
  a corpse walks nowhere.

  Written ahead of a reproduction rather than after one. The looping cutscene
  on "Public Nuisance" was reported with both AI helpers dead, which is a
  plausible cause that can't be confirmed by reading the code alone: the level
  data decides which object turns that camera off.

---

## 0.9.13 — 2026-08-03

**Changed**
- **"TOW arena" is now "TOWs Out"**, and `tows_out` in the server browser.

  A mode now carries **two names, because it has two audiences.**
  `FreshGameModeName()` is for a person reading a scoreboard or a dropdown and
  is written the way it would be said out loud; `FreshGameModeSlug()` is for the
  browser's type field, which is machine-adjacent — lower case, no spaces, the
  kind of thing somebody will eventually filter or sort on. One string doing
  both jobs means either the scoreboard reads like a config file or the browser
  field has a space in it, and the first person to write a filter has to guess
  which.

  Both halves of the browser field are slugs, so a TOWs Out server advertises
  `deathmatch - tows_out` rather than mixing a slug with prose.

- **The scoreboard always names the mode**, including plain `Deathmatch`.
  Showing it only for the unusual modes looked tidier and was worse: a player
  who has never seen a mode named there doesn't know the line can carry one, so
  its absence tells them nothing. Naming it every time makes the line mean
  "this is what you're playing" rather than "this is unusual".

- **Normal is called Deathmatch, not "normal".** It's the mode Shogo has always
  had; naming it after the absence of a mode describes the code rather than the
  game.

---

## 0.9.12 — 2026-08-03

**Added**
- **The server list Type column shows the game mode.** A TOW arena server now
  advertises `deathmatch - TOW arena` instead of `deathmatch`.

  **Composed, not replaced.** "Deathmatch" describes the ruleset and the mode
  describes the weapons — a server running both should say both, and replacing
  the first would leave no way to tell an arena deathmatch from a future arena
  anything-else.

  It goes through `SpyGameType`, the console variable the dedicated server
  already hands to GameSpy as its `gametype`, which is free text. Deliberately
  **not** `NST_GAMETYPE` in the in-game session string — that one is `atoi`'d
  into a game-type number by every client including stock ones, so a mode name
  anywhere near it would read as a nonsense type on clients that can't be
  fixed.

  Written at every level load rather than once at startup, because the rotation
  can change the mode per map. The server application re-reads the variable on
  its own timer, so nothing needed telling.

- **The intermission scoreboard names the mode**, on the map line:
  `OF_NIGHT (TOW arena)`. Beside the map rather than on a line of its own — it
  is a property of the match the way the map is, and the board has four lines
  and no room for a fifth.

  The mode is decided entirely server-side, so it had to be sent: appended to
  `MID_SERVER_RULES` after the infinite-ammo byte. Appended rather than
  squeezed into the flags byte, because a mode is a number from a growing list
  rather than a switch, and there are three bits left in that byte which will
  not be enough for long. A stock server sends neither byte and an integer read
  past the end returns zero — which is "normal", the right answer for a server
  that has never heard of modes.

---

## 0.9.11 — 2026-08-03

**Added**
- **A map can ask for its own game mode.** A rotation entry may be written
  `world:mode`; a bare entry follows the server's `GameMode`. That one rule
  covers every case worth having — an all-arena server sets the default and
  annotates nothing, a mixed rotation annotates the entries that differ, and a
  one-off annotates one.

  The mode rides *inside* the level name rather than beside it, because the
  rotation lives in a fixed-size array in `NetGame` — a structure shared with
  the server application. A parallel array would be two things that can
  disagree about how many entries there are, and a config written by one
  version misread by another. A suffix costs a few characters of a 32-byte
  field and cannot fall out of step with what it describes.

  Anything unparseable is treated as a plain world name. A rotation is the last
  thing that should refuse to start because a mode was typed wrong.

- **A Game mode dropdown in the server app**, under Round Limits, setting the
  default. The map list shows per-map overrides as `world  [TOW arena]` — the
  suffix is configuration and the operator is reading a list of maps, but a
  rotation where one map plays differently and doesn't say so is a support
  call.

---

## 0.9.10 — 2026-08-03

**Added**
- **`GameMode 1` — TOW arena.** Every weapon pickup on the map becomes a rocket
  launcher, and everyone spawns holding one with 25 rounds. Multiplayer only;
  a campaign level's pickups are part of its authoring.

  It needed **no new hooks at all**. The blocklist machinery already tears down
  weapon pickups and stands new ones in their place — a mode just answers
  "which weapon" differently. That is why this is the first mode rather than
  gun game: it proves the pickup half of a mode costs nothing, which leaves gun
  game to prove the part that actually needs work (hearing about kills, and
  carrying per-player state).

  The arena weapon is **per tier**, because the two arsenals don't overlap: a
  mech can't hold a TOW, so an MCA map gets the juggernaut — the closest thing
  a mech has to a rocket launcher. "TOW arena" therefore means the same thing
  on both kinds of map without being the same weapon.

  Adding another arena mode is one case label in `Shared/FreshGameModes.h`.
  Anything that isn't "one weapon everywhere" doesn't belong in that file.

  You keep the sidearm, deliberately — it's what you have when the arena weapon
  runs dry.

---

## 0.9.9 — 2026-08-02

**Fixed**
- **The sniper rifle was broken in a mech, and 0.9.0 broke it.** Model hovering
  above the head, an astronomical ammo count, an animation and a sound with no
  bullet — all the same cause: the weapon id was **-1**.

  Giving the sniper rifle slot 9 on foot meant `GetCommandId` answered "slot
  9" for it. But that function is never told which mode you're in, and slot 9
  was filled *only* for on-foot — so a mech asked for slot 9, got nothing, and
  carried on with an invalid id. Its answer is fed straight back into
  `GetWeaponId` by every auto-switch and every next/previous cycle.

  Slot 9 answers in both modes now, so a mech reaches the sniper rifle on 6 or
  on 9. Two keys for one weapon is a small oddity; a mapping that doesn't
  survive a round trip is a broken weapon.

- **A forced holster could be beaten by pressing weapon keys quickly.** The
  server refused the change, but the client doesn't wait to be told — it swaps
  the player-view model the moment the key goes down. Both ends refuse now,
  because both ends can accept.

**Tools**
- **Preflight checks that a weapon's key leads back to it.** `GetCommandId` and
  `GetWeaponId` are two switch statements that have to agree, which is exactly
  the shape the project adds checks for.

  Worth recording that the first two versions of this check were useless. The
  first was condition-blind and passed happily on the re-broken tree; the
  second caught it but failed eight stock weapons too, because a one-tier
  weapon *should* have a mode-guarded key — the .45 owning key 1 only on foot
  is right, since a mech asks the same key for its pulse rifle. It only counts
  for weapons in both tiers, which it now reads from `WeaponUsableOnFoot`.
  Verified by breaking it on purpose, which is the only reason any of that was
  discovered.

---

## 0.9.8 — 2026-08-02

**Fixed**
- **The juggernaut fired for ever once its magazine ran out.** The client set
  "a shot happened" *outside* the check for whether the magazine had anything
  in it — so a weapon with an empty clip but reserve left reported a shot on
  every trigger pull without spending a round.

  Normally the auto-reload refills the magazine on the same frame and the
  window never opens. **The juggernaut has no reload animation at all** —
  `WeaponDebug` says so in as many words — so once its magazine emptied it
  never closed: the client claimed a shot every time, the server refused every
  one because its ammo really had run out, and you watched a weapon fire with
  nothing coming out of it.

- **The autosave slot showed `09_COMM1|1785695266`.** That row read the raw ini
  string while every other row goes through `FreshReadSaveEntry`, which splits
  the timestamp off. So the number was still stapled to the world name when it
  reached `GetNiceWorldName`, nothing matched, and the menu printed the lot.

  It now shows the mission name, and the date in the same column and format the
  named slots use — an autosave is the one save you didn't choose to make, so
  when it happened is the only thing that says what going back to it costs.

---

## 0.9.7 — 2026-08-02

**Fixed**
- **Mark clipping works.** The diagnostic in 0.9.6 answered it in one line: the
  polygon never arrived. The trace that looks for it didn't set
  `INTERSECT_HPOLY`, so the query reported a hit, filled in the point and the
  plane, and left the polygon handle alone. It looked exactly like
  `ClipSprite` being broken and was a missing flag. Fixed for both the impact
  marks and the ground blood.

- **A forced holster refuses the weapon change**, not just the draw. Refusing
  to unholster wasn't enough — the change went ahead, the client drew the new
  weapon's model because that's what a weapon change means to it, and you were
  left holding something you couldn't fire while being told you couldn't draw
  weapons here.

- **The client forgets its holster when a world loads.** The server asserts the
  real state at level start, but this side has to begin from a known one: the
  server's flags are rebuilt with the player object, so it had nothing to
  correct and said nothing, and a forced holster walked into a level that never
  asked for one.

**Changed**
- **The TOW's blast and its fireball are separate numbers now.** They were one:
  `GetWeaponDamageRadius` fed the damage sphere, the blast push, the scorch
  *and* the fireball — the last at **twice** the value, so every explosion drew
  a ball twice the size of the thing that could hurt you. Shrinking the danger
  zone shrank the spectacle by the same proportion, which is not how anyone
  wants to tune an explosion.

  TOW blast radius 300 → **200**, damage 120 → **90**, and the fireball holds
  the size it always had. Every other weapon keeps the old doubling, so nothing
  unexamined changes.

- **Weapon wind-up animations can be cut short.** The delay between clicking
  and the TOW's missile leaving is an animation — `"Start_fire"`, which firing
  waits on until it reports done — and not a number anywhere. There's no way to
  play it faster (engine fact 5), so it's ended early instead: the launcher
  still visibly rises, it just stops waiting for the last of it. `WeaponWindup`
  is the fraction to stand through, default 0.35; `1` is stock and `0` skips it.

---

## 0.9.6 — 2026-08-02

**Added**
- **A weapon that runs dry steps aside on the shot that emptied it.** Stock
  waited for the *next* trigger pull and answered it with a click — so the
  shot that emptied your weapon left you holding a prop, and you found out
  about it at the exact moment you needed it not to be one.

  It picks the best thing still loaded, and **melee is a last resort**:
  swapping a dry rifle for a knife while a shotgun with two shells sits in the
  inventory would be a worse answer than the empty rifle was. If genuinely
  nothing else is loaded, you keep the empty weapon and get the click, which
  at that point is the honest answer.

**Fixed**
- **A forced holster followed you into the next level.** The client is told
  the holster state only when the *server's* state changes — and on a level
  change the player object is rebuilt, so the server's flags are already false
  and the "unholster" does nothing and says nothing. The client isn't rebuilt,
  is still holstered from the previous level, and has been told nothing to the
  contrary: a holster with no server state behind it at all.

  The state is now asserted at every level start, changed or not. Somebody has
  to break that tie and it has to be the server.

**Diagnostics**
- **Mark clipping reports what it's doing.** Reported as ineffective twice, so
  rather than guess a third time it now prints, on the `WeaponDebug` channel,
  whether a polygon arrived at all and what `ClipSprite` returned. Those are
  opposite problems — a missing polygon is the caller's fault, a refused one
  is the engine's — and they need opposite fixes.

---

## 0.9.5 — 2026-08-02

**Changed**
- **The player lights are reverted to the 0.8.93 implementation.** That is the
  build they last looked right in, and seven versions of trying to improve on
  it have not produced one that does.

  They are server-side again, and everything the client-side move bought comes
  back off: other players' lights are visible again, and the pair costs two of
  the ten dynamic light slots per character rather than two in total. Those
  were real gains and they are being given up on purpose, because a design
  that looks wrong is not better than one that looks right.

  The 20% dimming from 0.8.94 is treated as the mistake it appears to be
  rather than repeated — the colours are 0.8.93's exactly.

  The client-side version is deleted rather than left behind a switch. It was
  brighter than the version that works by a factor of three and still read as
  a shadow, so no value would have rescued it, and leaving broken code around
  with a flag on it just means finding it again later.

**Fixed**
- **`MarkClip` now has something to clip.** 0.9.4 wired the polygon through for
  the blood decal only — the scorch, which is the mark the request was about,
  never passed one, so the setting did nothing for it.

  `CWeaponFX` has no polygon to pass: everything it knows about the impact
  arrived in a server message, which carries the surface *type* and a rotation
  but no handle, because a polygon is a client-side notion and the two
  machines don't share one. It's found locally now with a short trace across
  the surface, and both bullet holes and scorches hand it over.

---

## 0.9.4 — 2026-08-02

**Fixed**
- **Decals are lit by the room now.** Marks carried `FLAG_NOLIGHT`, which means
  "use the texture's own brightness and ignore the area and every dynamic
  light" — so a bullet hole was drawn at full brightness wherever it was. On
  the metal impact sprites, whose brightest pixels are near-white, that put a
  scatter of white specks on the wall of an unlit corridor: the decal was the
  brightest thing in the room.

  Dropping the flag means a bullet hole in the dark is dark, and a passing
  explosion lights the holes it made. `MarkBright` (default 0.78) takes a
  little more off the top, because in a *bright* room the impact sprites are
  still louder than the surface they're on.

- **Decals can be clipped to the surface they're on.** A scorch on a narrow
  step spilled past the tread and hung in the air either side. `ClipSprite` is
  the engine's own answer to this and **nothing in the codebase had ever
  called it**.

  It clips to *one* polygon, which is the catch: a step is usually one, but a
  large floor is often several, and a big scorch spanning a seam will be cut
  at that seam — trading an overhang for a straight edge through the mark.
  Which looks worse depends on the map, so `MarkClip 0` turns it off.

  Also fixed on the way: `MARKCREATESTRUCT` zeroes itself, and zero is a
  *valid* polygon handle — `INVALID_HPOLY` is `0xFFFFFFFF`. Left as-is, every
  mark whose caller didn't set a polygon would have been clipped against
  polygon 0 somewhere else entirely.

---

## 0.9.3 — 2026-08-02

**Added**
- **Blood stays on the floor.** Everything the gib effect did until now was
  *event* blood — sprays that fade, particles that vanish, splats stamped
  wherever a piece happened to hit a wall. All of it gone in a second or two,
  so a room that had seen something violent looked exactly like one that
  hadn't.

  A pool is now traced straight down from the body and painted as a **decal**,
  the same machinery the explosion scorch uses — out of a pool of 300 marks
  rather than the ten dynamic lights. Four overlapping splats at random sizes
  and angles rather than one, because a single disc of exactly the sprite's
  shape reads as a sticker where four read as a mess.

  No floor within reach — over a pit, in a lift shaft, gibbed off a ledge in
  mid-air — and nothing is drawn, rather than something drawn nowhere. Skipped
  on sky and water like the impact splats already are, and it respects the
  Gore setting.

**Changed**
- **More blood in the burst.** The fine spray goes from 60 particles to 100,
  and a third, wider, slower spray joins the two that bracket the body's
  middle.

---

## 0.9.2 — 2026-08-02

**Fixed**
- **The carried lights were being created at the world origin.** That is the
  dark patch, and it has nothing to do with colour, radius or flags — all of
  which were changed, repeatedly, across five versions that could never have
  worked.

  When these moved client-side in 0.8.95 the creation was hand-rolled, and the
  create struct was left empty except for type and flags. An empty struct
  means position (0,0,0) — inside solid geometry in most levels, or outside
  the world altogether. The light was then moved onto the player on the same
  frame, and carried a dark sphere of its own colour with it wherever it went.

  `CLightFX` — how this effect reached the client back when it looked right —
  copies a real position in before calling `CreateObject`. That was the only
  difference. A dynamic light evidently registers something with the surfaces
  around it at the moment it is made, and one made in the void registers it
  against nothing.

  Colours and flags are back to the values from 0.8.94, the last version that
  looked correct. `PlayerLightWorldOnly 0` still swaps the flag if anyone
  wants to compare; neither setting was ever the cause.

---

## 0.9.1 — 2026-08-02

**Fixed**
- **The carried lights were drawing a dark sphere instead of a lit one.** A
  screenshot settled what three rounds of "try it dimmer / try it brighter"
  could not: the patch on the wall was the light's own colour, subtracted
  rather than added. It was never too dim — it was working backwards.

  The cause was found by comparison rather than reasoning. The lights under
  the pickups are client-side `OT_LIGHT`s created the same way in the same
  renderer, and they look right. They differ in exactly two things:
  `FLAG_DONTLIGHTBACKFACING` where the player lights had
  `FLAG_ONLYLIGHTWORLD`, and a colour around 0.8 where the player lights had
  0.29. The player lights now match the configuration that demonstrably works.

  `FLAG_ONLYLIGHTWORLD` was only ever mitigation for the third-person crash,
  which turned out to be the attachment — so it was never earning its place.
  **Why** it darkens is still not understood, only that it does and that the
  configuration next door does not, so `PlayerLightWorldOnly 1` puts it back
  for anyone who wants to compare rather than take this on trust.

---

## 0.9.0 — 2026-08-02

**Added**
- **The sniper rifle is an infantry weapon.** On **key 0** — the slot 1998 left
  empty in both tiers, so it takes nothing away from anything else. It appears
  in on-foot pickup rotations, can be carried and fired on foot, and is handed
  out by the give-all-weapons cheat like any other.

  Its damage is 100, which sits between the assault rifle's 25 and the TOW's
  120 — a weapon already on foot — so nothing was rescaled. Whether that lands
  right in a match is a judgement no table can make.

  Classic keeps 1998's arrangement: a mech weapon, on key 6, nowhere near
  infantry.

**Changed**
- **Weapon tiers are asked, not inferred from the id.** Stock decided "is this
  an infantry weapon" by testing whether the id fell between
  `GUN_FIRSTONFOOT_ID` and `GUN_LASTONFOOT_ID`, written out longhand in six
  places across three files. That worked only because the two tiers were
  contiguous and nothing belonged to both.

  The sniper rifle now does — its id is 4, inside the mech range, and it
  **cannot be moved**: weapon ids are baked into every level's pickup
  entities, so renumbering would silently rearrange the contents of every map
  ever made for the game. So the range test became `WeaponUsableOnFoot()` with
  one exception in it, and the six sites now ask that instead. The next
  dual-tier weapon is one line in one file rather than six edits that have to
  agree.

---

## 0.8.99 — 2026-08-02

**Tools**
- **The DTX format is properly worked out now, and 0.8.98's converter was
  wrong.** The header is **44 bytes, not the 164 every LithTech description
  gives** — what is documented as a 128-byte command string is palette data.

  The wrong reading almost works, which is why it shipped. Image data starts
  at the same offset either way; the palette is just sampled 30 entries late.
  A shell casing came out banded with green fringing instead of smooth brass
  with a specular highlight, and a hull panel came out blue mush instead of
  brushed metal with rivets. All 111 skins have been reconverted.

  Also settled: **flags bit 1 marks a 4-bit alpha channel** after the mipmaps
  — a perfect predictor across 4,854 textures, 4,065 without it clear and 789
  with it set, no exceptions. Alpha is packed two pixels to a byte, low nibble
  first, which was determined by measuring: unpacked the other way a scope
  lens is high-frequency noise, this way it is smooth.

- **`Tools/png2dtx.py` writes textures back.** Round-trips **byte-exact** on
  everything tested, including a texture with the alpha block — which is the
  test that separates a correct writer from one wrong in the same way as its
  reader.

  Two things worth knowing before authoring with it. **Every DTX is 256
  colours** — that is the format, not a setting, so resolution is the cheap
  axis and colour depth the expensive one. And **nothing in Shogo exceeds
  256×256**, not one of ~6,000 textures, which is the Voodoo-era limit the
  engine was built against. Whether the renderer refuses larger is not
  answerable by reading, since the renderers are closed. `--size 512` writes
  it; loading it is the experiment.

---

## 0.8.98 — 2026-08-02

**Changed**
- **The player lights are brighter again**, roughly double, and
  `PlayerLightScale` now goes to 6 instead of 2.5. The ceiling matters as much
  as the default: "still looks dark" has two causes that are identical from a
  chair — a light too dim to register, and a light that isn't reaching the
  geometry you're looking at — and the only way to tell them apart is to turn
  it up until it's unmistakable. The old ceiling couldn't reach full
  brightness from these defaults, so it couldn't settle it.

**Tools**
- **`Tools/dtx2png.py` converts Shogo's textures to PNG.** The format isn't
  documented anywhere in the SDK; this was worked out from the files and
  checked by looking at the results.

  One thing is genuinely unresolved and the tool says so: the palette is
  sometimes 904 bytes and sometimes 1024, and no header field found so far
  distinguishes them. Both readings are defensible on paper — 904 is the exact
  difference between file size and mipmap data for every texture in the
  archive, 1024 is what an eight-bit index implies — and the files say each is
  right some of the time. So the length is picked per file by trying both and
  keeping the one where every index lands inside the palette. It is never a
  close call: the wrong length leaves a third of the image pointing past the
  end.

  Chroma-key green becomes transparent, so the output is usable directly;
  `--keep-green` leaves it if you intend to re-import.

---

## 0.8.97 — 2026-08-02

**Changed**
- **Zoom is per weapon now, and three mech weapons have one.** Stock had a
  single zoom level shared by everything that zoomed — 10 degrees, which is a
  sniper scope. Handing that to a rapid-fire rifle makes it a sniper rifle that
  happens to hold forty rounds.

  The assault rifle drops to **40 degrees** — closer to leaning in than to
  using an optic. The laser cannon (28), juggernaut (30) and spider (34) gain
  one, because a mech fights at ranges where its targets are a few pixels tall
  and none of them could see what they were shooting at. Their numbers are
  deliberately modest: a pilot leaning forward, not a marksman.

  Classic keeps 1998's behaviour — the two weapons that zoomed, at the one
  level they were given.

- **The player lights are brighter again**, up about 70%. Dimming them twice
  overshot: below a certain level they stopped reading as light at all.

**Fixed**
- **Shutdown no longer announces itself as "next level".** The button sent
  `NEXTLEVEL`, so the log carried two lines for one action and the first of
  them described a button nobody pressed. It has its own request now
  (`SHUTDOWNHOLD`) with the same behaviour and its own words.

- **An empty `StartHolstered` no longer turns forced holstering off.** An
  existing-but-empty console variable reads as `0.0` through
  `GetVarValueFloat` exactly as an absent one does, so anything that merely
  brought the variable into existence silently disabled the feature — which is
  what a regression looks like from the outside. The string is checked first
  now, so "nobody said anything" keeps meaning the default.

---

## 0.8.96 — 2026-08-02

**Added**
- **`EnemyHighlight` — enemies stay legible in unlit rooms.** Single player
  only, off by default.

  This is the rim light, arrived at sideways. A real rim light is a shader
  effect and this engine has none; faking it with a light behind the character
  fails because `FLAG_ONLYLIGHTWORLD` has no complement — any light placed to
  rim someone lights the wall behind them too, and the silhouette stops being
  one. What does work is `FLAG_NOLIGHT`: the model ignores area and dynamic
  lighting and renders at its own texture brightness, so a highlighted enemy
  stays readable while the room around it stays as dark as it was. Costs
  nothing from the ten-slot light budget.

  Only characters that *hate* you. A readable enemy is the point; a readable
  ally gives away who is where, and the campaign spends real effort on that.

  **Multiplayer is refused at the server**, not trusted to the client. There it
  wouldn't be readability, it would be seeing people who are standing in the
  dark — and there is no dimmed version to offer, because the flag is all or
  nothing and `FLAG_MODELTINT` only scales colour *down*.

**Fixed**
- **Dying no longer leaves a transmission talking at you.** A portrait
  explaining your next objective over a death screen reads as the game not
  having noticed what just happened. The transmission clock is stopped rather
  than merely hidden, so a long one isn't still running when the level
  reloads, and any in-game dialogue box goes with it — waiting to be dismissed
  by someone who can't dismiss it is the same bug.

- **The Kato core stops chattering as it settles.** A settling grenade bounces
  many times in its last half second, each weaker than the last, and every one
  was firing the same sound at the same volume. Below a threshold it's rolling
  rather than landing, and that should be quiet.

  This was a side effect of making the bounce springy in 0.8.89 — before that
  it stopped dead and there was only ever one impact to hear.

**Diagnostics**
- **The pulse rifle self-damage guard now reports what it sees.** It was fixed
  in 0.8.89 and is reported still happening, so rather than guess at it a
  second time, any blast that reaches whoever set it off prints the weapon id,
  the ruleset and the guard's answer on the `WeaponDebug` channel. If it prints
  the wrong id, the id is being lost between the projectile and the damage; if
  it never prints, the damage isn't coming through that function at all.

---

## 0.8.95 — 2026-08-02

**Changed**
- **The player lights are yours alone now — nobody else sees them.**

  They exist so that a player in an unlit room can find a doorway. That is a
  fact about *your screen*, not about the world, and the other twenty-three
  players don't need to be told about it. The previous version was server-side
  and lit every player for everybody, which is a different feature wearing the
  same clothes: an *enemy visibility* aid rather than a navigation one.

  Three things follow from the move, and the first is the one that matters:

  **Darkness is somewhere to hide again.** A light on every player was a
  permanent marker saying where they were — it quietly removed shadow as
  cover, which nobody decided to do. Now you can navigate a dark room *and*
  use it.

  **Two lights total, instead of two per player.** The ten-slot dynamic light
  budget stops being strained at all, and explosions keep their light in a busy
  match. The deliberate overspend flagged in 0.8.93 is simply gone.

  **They're a personal setting, not a server rule.** `PlayerLightRadius`,
  `PlayerFootLightRadius` and `PlayerLightScale` are read from your own
  console now — no `serv`, no host involved. Which is what a readability aid
  should have been from the start.

  The cost, stated plainly: bots no longer carry them, so there is no longer
  any way to look at the effect on somebody else. It is tuned by how the room
  around you looks, and that is all.

  Most of the machinery this needed has been deleted rather than moved — the
  light object classes, the server objects, the inter-object links, the
  death and respawn bookkeeping. All of it existed to get a light onto a
  server object and replicate it, and none of it is needed to put two lights
  on your own screen.

---

## 0.8.94 — 2026-08-02

**Fixed**
- **Holding Shutdown until it said "Shutdown Now" shut the server down by
  itself.** The hold completes on a timer while the mouse is still down — but
  the button doesn't know that, and sends its ordinary click the moment it's
  released. By then the control is armed, and an ordinary click means close.
  So the confirming press was being delivered by the same press that armed it,
  which is the opposite of what a two-stage control is for.

  That one click is now swallowed. If the pointer is dragged off the button
  instead of released on it, no click ever arrives — so the expectation is
  dropped once the button is seen to be up, rather than eating the operator's
  next genuine press.

- **The lights now go out when a player or bot dies.** Left burning, a light
  stayed where the body fell and marked the spot — a lit corpse, which reads
  as the game having lost track of somebody rather than as an effect. The body
  keeps whatever light the room gives it, which is the right answer: a corpse
  should be as hard to find as the room is dark.

  Players are relit on respawn. A player object is *reused* across a death
  where a bot is replaced by a new one, so a bot gets its lights from being
  spawned and a player has to be handed them back — otherwise you'd die once
  and spend the rest of the match dark.

**Changed**
- **Both lights dimmer again**, about another 20%.

---

## 0.8.93 — 2026-08-02

**Changed**
- **The floor pool is a light now, not a sprite.** `glow.spr` is a 1998 impact
  flash — a hard-edged blob meant to be seen for a tenth of a second at the end
  of a bullet — and a permanent one under a walking player read as a decal that
  had forgotten to expire.

  This is a deliberate overspend against the ten-slot dynamic light budget:
  two lights per character where the sprite cost none. It is here because it
  looks right and the sprite did not. Both lights have their own off switch,
  and if explosions start losing their light in a busy match,
  `PlayerFootLightRadius 0` is the first thing to try.

- **The overhead light is dimmer again**, down about 40%. The intent is that
  you should not notice it in a lit room and should be glad of it in a dark
  one.

- **Bots carry both lights too.** Partly because a bot you can't see coming
  isn't the practice a bot is for, and partly because they're the only way to
  look at the effect on somebody else — you can't see your own from inside your
  own head.

  The light code moved to `CBaseCharacter` to make that possible, but stayed
  **opt-in**: players and multiplayer bots ask for it, campaign NPCs don't.
  Forty lit shock troopers would be forty demands on ten light slots.

**Fixed**
- **The light handles were never cleared.** `CreateInterObjectLink` doesn't null
  a handle on its own — `MID_LINKBROKEN` does, by hand, and it only knew about
  the dialogue sprite and the held weapon. A light removed for any reason left
  a dangling handle behind it. Present since the light was added in 0.8.90.

---

## 0.8.92 — 2026-08-02

**Added**
- **A pool of light on the floor under each player.** The carried light gives
  someone presence in a room but says nothing about where their feet are, and
  the floor is what the eye actually reads at distance.

  It is a **sprite**, not a second light, and that is not a shortcut. The
  engine's dynamic light list is ten entries for the entire world — one light
  per player already spends most of it in a full game, and explosions and
  muzzle flashes compete for the same ten. That limit is why bullet impacts
  became glow sprites in the first place; sprites come from a pool of 150. So
  the light does the room and the sprite does the ground, and the pair costs
  one light per player rather than two.

  It sits on the **floor**, found by tracing down, not at the player's feet —
  a glowing disc travelling upwards with a jumping player is the kind of thing
  nobody can name but everybody notices. Over a pit or a lift shaft, with no
  floor within reach, it hides rather than drawing somewhere arbitrary.

  `PlayerGlowScale` sizes it; `0` turns it off, because "off" has to mean *not
  drawn* for something whose only knob is a size.

**Changed**
- **The carried light moved from chest height to overhead**, and its height now
  comes from the player's own dimensions rather than a fixed number. In a mech
  the same object is several times taller, so an offset that reads as overhead
  on foot was somewhere around the ankle in an MCA.

  This is as close as the engine comes to lighting a player from above: there
  are **no spotlights in LithTech 1.0** — lights are point lights with a
  radius and a colour, no cone and no direction — so height and radius are the
  entire vocabulary available.

---

## 0.8.91 — 2026-08-02

**Fixed**
- **The player light no longer rides on the player model.** It was an
  attachment, which the engine draws along with its parent — and every other
  attachment in this codebase has something to draw: the dialogue sprite is a
  sprite with a filename, the bounding box is a model with one. A light has
  none, and the moment that would first show is the moment your own model
  starts being drawn, which is third person.

  A client died going into third person in multiplayer. That is not proof —
  the fault address was zero and named nobody — but a geometry-less
  attachment on a drawn model was a new case here, and moving the light with
  the player each update is the same effect without it. It now follows rather
  than attaches.

  If the crash survives this, the light can be ruled out in one go: set
  `PlayerLightRadius 0` and it is never created.

**Changed**
- **The player light is smaller, dimmer, and lights the world rather than
  models.** Radius down from 150 to 105, brightness down about a third. It
  also carries `FLAG_ONLYLIGHTWORLD` now — a light sitting *inside* the model
  it is lighting is a degenerate case at zero distance, and the pool on the
  floor is what makes a player visible from across a room anyway. Lighting the
  model itself mostly washed out its own texture.

- **Servers can set both.** `PlayerLightRadius` (default 105, max 400) and
  `PlayerLightScale` (default 1.0, max 2.5). An explicit `0` radius creates no
  light at all rather than a light of size zero — an off switch that leaves
  the thing running is no use to anyone trying to find out whether it is the
  thing at fault. Read once per light, so a change applies from the next level.

---

## 0.8.90 — 2026-08-02

**Added**
- **Every player carries a dim light.** Shogo's interiors are dark and its
  characters are dark models standing in them, so in multiplayer the first
  thing you usually see of an opponent is their muzzle flash. A small pool of
  light around each player gives them a presence in a corridor.

  Deliberately dim, deliberately small, and deliberately cool-toned: it should
  make a player register in a dark room without lighting the room for them.
  It is attached rather than moved, so it follows through jumps, lifts and
  teleports on its own.

**Changed**
- **Gibs leave the body in every direction instead of one.** The launch code
  built a minimum and a maximum velocity *vector* along the blast axis and
  then picked each component between them — and with the shipped numbers the
  forward term dominated the two lateral ones, so the "random" spread was
  about 27 degrees wide and every piece went the same way.

  Each piece now gets its own direction, leaned towards the blast and upwards
  rather than dictated by either. The angle of the explosion still shows; it
  has just stopped deciding everything.

- **Gibbing a body actually produces blood.** Two splatter sprays now bracket
  the body's middle — one climbing out of the chest, one running down out of
  it — and a burst of fine, fast particles leaves the centre in every
  direction. The first of those was written in 1998 and left commented out,
  so the one moment that most wants blood in it had none at all.

  All of it respects the Gore setting, and the fine spray is skipped at the
  lowest detail level.

**Fixed**
- **The mod rules file was never found.** Both copies of the path builder
  wrote `"%s\%s"` with a single backslash, which is not an escape sequence —
  so the compiler dropped it, the format became `"%s%s"`, and the game looked
  for the file at a path with no separator in it. Missing rules are the normal
  case and fail silently by design, so nothing ever said so.

---

## 0.8.89 — 2026-08-02

**Fixed**
- **The pulse rifle stops shooting its own pilot.** Its rounds are implemented
  as small explosions, so the splash that gives it weight was also chipping
  away at the person holding it — several times a second, every time they
  fired at anything close. Nothing about that reads as a design decision from
  the player's chair.

  A very short list, and self-damage stays on for everything else: a TOW at
  your feet **should** hurt, and removing that would take a real decision out
  of close-quarters fighting. Classic keeps 1998's behaviour either way.

- **The Kato grenade is back to its own launch speed.** The tuned 1400 was
  arrived at by throwing the *energy* grenade, and applying it to the Kato as
  well took that one from 750 to nearly double without anyone asking for it.

**Changed**
- **The Kato grenade bounces instead of sliding.** Stock added the stopped
  velocity back on, which removes the component that ran into the surface and
  keeps the rest — so it slid along the floor and read as a ball rolling
  rather than a grenade bouncing. The commented-out alternative beside it had
  the same flaw: both take energy out of a collision and never put any back.

  It reflects properly now and keeps about half its speed through each
  bounce, so the first is lively and the fourth has clearly given up.

## 0.8.88 — 2026-08-02

**Fixed**
- **"sec" is no longer clipped to "se" in the server's Options dialog.** The
  gravity label added last release started at x=113 while the "sec" label
  still ran to 127, so the two overlapped. The Round Limits fields are
  narrower now and the two columns are properly separated — the gravity field
  brought the row to a width the old spacing could not carry.

**Changed**
- **The exit confirmation is titled "FreshServ".** Its own string rather than
  a rename of `IDS_APPNAME`, because that one is also the host name advertised
  in the session — renaming it would have changed what every server browser
  sees in order to retitle one message box.
- The gravity hint beside the field reads `0=off` rather than `0=def`, which
  says what zero does instead of where the value comes from.

**Unchanged, and confirmed:** the window's close box and Escape still ask
before quitting. Only the Shutdown button skips the question, and only after
it has been held for four seconds and then clicked.

## 0.8.87 — 2026-08-02

**Fixed**
- **The energy grenade ignored the launch angle in multiplayer while the Kato
  grenade obeyed it.** One cause, and it explains why it looked so arbitrary.

  For a projectile you fired yourself, the server's copy is suppressed and the
  client's own predicted copy is what you see — *except* for the Kato grenade
  and the spider mine, which the effect code exempts **by name**. So the Kato
  was showing the server's object, elevated correctly, and the energy grenade
  was showing a local one built from the raw aim direction that had never
  heard of `GrenadeAngle`. The client applies the angle to its own copy now.

  The same thing was making `GrenadeDrop` look inert.

- **Pickups can be picked up again.** The floor correction was done on the
  client, which moved the model and left the thing you actually walk into
  where the server put it — so an item could be seen at one height and
  collected at another. It is done on the server now, and sits higher: 24
  units rather than 10, because the model's origin is at its middle and a
  smaller number sinks half of it into the floor.

- **Shutdown shuts the server down.** It sent `NEXTLEVEL` and stopped there,
  so the server did what that means — ran the intermission and loaded the next
  map. It now closes when the intermission has run, timed from the same
  variable the shell clamps.

- **"Shutdown Now" no longer asks "are you sure".** A shutdown held for four
  seconds and then clicked has been confirmed twice; a third question is not a
  safeguard. The close box and Escape still ask, because those are the
  accidental ways out.

**Changed**
- The armed message reads `shutdown invoked - running intermission`.

## 0.8.86 — 2026-08-02

**Added**
- **Gravity in the server's Options dialog**, in Round Limits directly under
  Time limit. 0 means "leave the engine's value alone" and is shown and stored
  as 0 rather than clamped up to the minimum — it is the off switch, not a low
  setting. Clamped the same way the game clamps it otherwise.
- **Shutdown is a hold, and then a click.** Hold it for four seconds and the
  server goes to the intermission: the match ends, the scoreboard comes up,
  and the people on the server find out while they can still read it. Press it
  again and the application closes at once, as it always did on the first
  press.

  Four seconds rather than Next Level's two, because the worst a mis-pressed
  Next Level costs is a map and the worst a mis-pressed Shutdown costs is
  everyone on the server.

  **The button is deliberately not disabled between the two steps.** Greying
  it out is what the Next Level guard does and it would be wrong here: an
  operator who has just started a shutdown is exactly the person who might
  need to finish it immediately, and a dead button in that moment reads as the
  server having hung.

  Escape and the window's close box are untouched and still quit at once. The
  guard is for the button that sits under a mouse all day, which is why the
  button stopped being `IDCANCEL` rather than the cancel handler being
  overridden.

## 0.8.85 — 2026-08-02

**Added**
- **Gravity is a Host tab setting.** A checkbox and a number beside the
  intermission. Unticked leaves the engine's own value alone, which is what
  every Shogo server has always used; ticked writes it to `ShogoSrv.cfg` and
  the server picks it up without a map change. Clamped to the range the game
  clamps to, so the launcher cannot write a number the server will refuse.

**Fixed**
- **Pickups stop spinning — caused by the change that made them spin.**
  Turning on the stock `m_bBouncing` flag did two unrelated things: it raised
  the bounce user flag, which is all the client needs, and it put the object
  on a **0.001-second server update**. The bounce is drawn client-side, so
  that update bought nothing — and an object the server refreshes a thousand
  times a second is an object whose position and rotation it keeps sending,
  landing on top of the rotation the client just applied.

  Two things moving one object, and the visible result is a spin that stalls.
  The flag is raised directly now and the server-side update is left off
  unless a level actually asked for a server-side bouncer.

## 0.8.84 — 2026-08-02

**Fixed**
- **The black behind the server window's group boxes.** `WS_CLIPCHILDREN`,
  added beside the double-buffering in 0.8.60, had to come back out. A group
  box paints its frame and its caption and nothing else — the inside of the
  box is the *dialog's* background showing through. Clip the children and the
  dialog stops painting there, so every box filled in black.

  `WS_EX_COMPOSITED` alone is what actually cures the flicker, and it stays.

- **Pickups no longer rest in mid-air.** The bounce took the item's rest
  position once, on the first frame it saw it — and a *dropped* pickup is
  still falling at that moment, so the base was captured mid-fall and then
  held there for good. Level-placed items were fine and dropped ones were not,
  which is why it looked intermittent. The base is re-taken whenever the
  server moves the item.

**Added**
- **Pickups settle a fixed distance above the floor.** They used to rest
  wherever they stopped — on a step, on a corpse, part way down a slope — so a
  row of them sat at a row of different heights. A trace finds the floor; if
  nothing is within reach the item keeps its height, which is the right answer
  over a pit.
- **`Gravity` takes effect mid-match.** Change it at the server console or
  over rcon and the next update carries it in — no map change, no reconnect.
  Written only when it has actually moved.

## 0.8.83 — 2026-08-02

**Fixed**
- **Glass is detected from the polygon as well as the object.** The pass-
  through only ever read the surface off the object's user flags, which
  carry one when the glass is a breakable *entity* and not when it is part of
  the world. So panes that were entities worked and panes that were brushes
  stopped the shot — reported precisely as "goes through sometimes, stopped
  sometimes". The sky check a few lines above already reads the polygon, for
  the same reason.

**Added**
- **`GrenadeDrop` lowers the launch point.** A grenade leaves from the centre
  of the screen because that is where the aim ray starts and nothing ever
  said otherwise, while the model is held low and off to one side. Straight
  down in world space rather than down the view — a throw from the hip leaves
  below the eye whatever the head is doing, and tying it to the aim would make
  looking up raise the hand. Dropping the point costs range at a given angle,
  so it and `GrenadeAngle` are worth tuning together.
- **`Gravity` sets the world's gravity.** The engine's global force is what
  makes things fall, defaults to 2000, and is settable — so a flatter grenade
  arc can come from more speed, more angle, or less gravity, and only the last
  also changes how the player moves. Applied per world, because starting a
  world resets it.

**Changed**
- Pickup lights are smaller, and a **weapon** now lights nearly white while an
  item keeps the warmer tone. White carries further against Shogo's blue-grey
  interiors, and it means the colour says which kind of pickup it is before
  the model is legible.

## 0.8.82 — 2026-08-02

**Added**
- **The game says so when a tuning value was typed into the wrong console.**
  Every weapon tuning variable is a *server* variable, because both sides have
  to agree on it — so they are set with `serv Name Value`, and typing
  `Name Value` on its own sets a **client** variable that nothing anywhere
  reads.

  That produces no error and no effect, which is the worst possible
  combination: the value looks accepted and the game behaves as though it were
  never typed. On entering a world, any tuning name found set locally now
  prints the command that would actually work.

  Added because I gave exactly that wrong instruction for `ReloadScale` in the
  previous release — if the person who wrote the variable gets it wrong, the
  documentation is not the fix.

## 0.8.81 — 2026-08-02

**Changed**
- **The fire-mode gaps are settled: burst 0.55s, semi-auto 0.70s.** Dialled in
  play rather than reasoned about, which is the third time that loop has
  produced a number none of my guesses had reached — 0.70 is nearly four times
  the 0.18 this started at.

**Added**
- **`ReloadScale` multiplies every reload time at once.** The per-weapon table
  stays, because a TOW and a pistol should not take the same time and that
  relationship is worth keeping. What was not worth defending is the absolute
  size of the numbers, several of which read as sluggish in play.

  `ReloadScale 0.8` takes the assault rifle from 1.50s to 1.20s and moves
  everything else in proportion. `HandgunReload` still sets the .45 and the
  MAC-10 outright and wins where it applies — an absolute number is easier to
  talk about when only one weapon is wrong.

## 0.8.80 — 2026-08-02

**Fixed**
- **The wheel fix now reaches installs that already exist.** 0.8.79 lowered
  the threshold in `defkeybd.cfg`, which was the right number in a file that
  only ever seeds a **new** `autoexec.cfg`. Anyone already playing kept the
  old binding and saw no change at all.

  It is applied once at start-up now, guarded by a variable so a deliberate
  rebind is never stamped on — the same mechanism the Reload and Holster keys
  already use for exactly this reason, which was sitting three lines away and
  should have been used the first time.

**Changed**
- **The fire-mode gaps are console variables.** `SemiFireGap` and
  `BurstFireGap` override the table while the game runs; 0 or absent keeps
  the built-in value.

  Two builds have now shipped a guess at this number, and the grenade tuning
  already taught the lesson: a value that is pure feel is settled by playing
  with it, not by reasoning about it. Whatever feels right becomes the
  constant. The trace also reports the gap each time an allowance is granted,
  so what is actually in force is visible rather than inferred.

## 0.8.79 — 2026-08-02

**Fixed**
- **One wheel notch changes one weapon.** The trace answered a question none
  of the three theories had: a single notch produced **no cycle command at
  all**, and two notches produced one. So the input was never firing twice
  and never being refused — it simply was not arriving.

  The binding ignored anything under **0.1**, and one notch does not reach
  that in the units the engine reports the wheel in. Two notches accumulate
  past it, which is exactly the reported symptom. The threshold is 0.01 now.

  Worth recording as a shape: three plausible causes were ruled out by
  reading, all three were wrong about *where* the problem was, and the fourth
  possibility — that the command never arrived — was not visible from the
  code at all.

**Changed**
- **The zoomed assault rifle's semi-auto gap is 0.30s, up from 0.18s.** The
  trace shows the mechanism working exactly as intended: one round per
  trigger edge, every fire key in between refused, and a second edge blocked
  when it arrives too soon. But 0.18s is five and a half shots a second, and
  a spammed mouse button clears that easily — so what reached the player was
  two rounds in quick succession and no sense of a rate limit.

  This one was a number being wrong, not logic. Worth stating plainly,
  because the two previous attempts at this bug both changed logic that
  turned out to be correct.

## 0.8.78 — 2026-08-02

**Changed**
- **Save and load menus: text left-aligned, the block centred.** Every slot
  name used to be right-aligned against a centre line, so the names *ended*
  in a straight edge and *started* wherever their length happened to put
  them — and reading a list of saves means scanning down their first
  characters, which were the ragged end. The widest name and widest date now
  set a block that is centred as a whole, with every name starting at its
  left edge. The rows without a date line up with the rest instead of being
  individually centred, which is what made them read as a separate menu.

**Added**
- **The weapon cycle reports itself on `WeaponDebug`** — what each notch
  stepped away from, the weapon state, what was pending, and where it landed.

  "It takes two notches" has been reasoned about twice and fixed once, and
  the remaining half is not visible from the code: the binding threshold is
  0.1 against a notch that reports about 120, the cycle origin already
  follows the pending weapon during a deselect, and a second notch does
  update the request. So the next step is a trace, not a third theory. The
  line separates "the input fires twice" from "the input fires once and is
  refused".

## 0.8.77 — 2026-08-02

**Added**
- **Pickups rotate, bounce, and light the floor beneath them.** Rotation
  existed and was left to a per-item level property, so almost nothing used
  it. The bounce shipped with a flag, a level property and a message — and an
  **empty function body** on the client; it has never moved in 27 years. Both
  are on for every pickup under FRESH rules now.

  The bounce phase is seeded from the item's resting position rather than a
  clock, so two items on the same shelf are out of step with each other and
  stay that way across a save and reload.

  The light is a real dynamic light rather than a sprite, so it lights the
  floor and the nearby walls instead of drawing a glow on top of them — which
  is what carries across a room, and Shogo's levels are mostly dark rooms. It
  sits under the item's *resting* height, because a light that bobs with the
  model makes the floor pulse and reads as a fault. Off at the lowest special
  effects setting, with everything else that costs fill rate.

  Classic keeps 1998 behaviour: whatever the level author asked for, and
  nothing more.

**Changed**
- **The grappling beam is opaque** — being tried, not decided. E3 is not a
  depth-test bug; it is translucent sort order, and the engine exposes no
  sort or draw-order control anywhere in its headers. Opacity is the only
  lever: a surface that writes depth sorts correctly against everything else.
  The cost is that the beam no longer shows what is behind it. If the solid
  beam reads worse than the artefact did, the old value goes back.

## 0.8.76 — 2026-08-01

**Changed**
- **Nothing is painted onto something that can be broken.** A mark is placed
  in the world and stays there; the crate it was painted on does not. Shoot a
  box a few times and the scorches hang in the air where the box used to be —
  worse than no mark at all, and the debris says what happened far more
  clearly. Destructible objects now advertise themselves and the client
  declines the mark, the blast scorch and the smoke.
- **Glass never takes a mark**, by surface type rather than by flag — a pane
  is about to be somewhere else, and a bullet hole is right for the instant
  before it shatters and wrong for every instant after.
- **Projectiles break glass and carry on through it.** A pane is a thing you
  shoot *through*; a rocket detonating against a window robs the room beyond
  of the shot entirely, and stock treated glass as any other solid so every
  projectile stopped dead at one. The pane still takes the damage that breaks
  it — what is refused is the detonation, so the shot keeps its velocity and
  its remaining lifetime. Each pane is passed through once, because otherwise
  the projectile is still inside its box on the next update and would damage
  it every frame.

**Added**
- `preflight` checks the new impact flag is appended after the existing one on
  both sides. It failed on its first run and was right to: the field was being
  written and never read. That is the class of bug engine fact 9 exists for —
  it does not crash, it silently mis-reads everything after it.

## 0.8.75 — 2026-08-01

**Fixed**
- **A forced holster no longer walks into the next level.** It is player
  state, and player state survives a level change — so a level that does not
  want one has to clear it, and nothing did. Levels 2 and 3 were correct and
  level 4 inherited theirs, which would have continued for the rest of the
  campaign. Only a *forced* holster is released; a player who chose to put the
  weapon away keeps that choice across the transition.

**Changed**
- **Grenades are tuned.** 1400 units/sec and 14 degrees of elevation are the
  defaults now, replacing a table value of 2000 — identical to a TOW missile,
  and most of why a thrown grenade read as fired — and no elevation at all.
  The console variables still override, so the next round of tuning starts
  from here rather than from scratch.
- **A weapon number key draws from a forced holster.** Naming a weapon is a
  deliberate enough act to count as drawing it. The forced holster exists so a
  briefing does not open with a rifle pointed at your commanding officer, not
  to disarm a player who has decided otherwise. Cycling and reload still do
  nothing — cycling is a fidget rather than a choice.
- **Walking over a weapon no longer draws it while holstered.** The pickup
  still happens; only the drawing is refused. On a level staged to start you
  unarmed, auto-equip was the one thing the player had not asked for.
- **The version line and licence notice on the main menu use the scaled
  font.** At 10 design pixels rather than the menus' 12: this is fine print
  and should read as fine print, but the 8-pixel bitmap original was genuinely
  unreadable at 1440p and above — which rather defeats a licence notice that
  is required to be legible.

## 0.8.74 — 2026-08-01

**Fixed**
- **Hank speaks. Wrapped text stopped allocating a surface it was only going
  to measure.**

  To break a paragraph into lines, the text wrapper created a surface from the
  *whole remaining string*, read its width, threw it away, and tried a shorter
  one. So the first attempt at any paragraph is a surface as wide as that
  entire paragraph set on a single line — and at the scaled font sizes this
  build draws at, that is many thousands of pixels. The engine refuses to
  allocate it, and one refusal aborts the whole paragraph.

  That is why the longest string in the game was the one that failed, why it
  failed *identically* on a retry, and why it looked like a transient surface
  fault for three builds running. It was arithmetic, not luck.
  `GetStringDimensions` answers the same question and allocates nothing; the
  surface is built once, at a width already shown to fit.

  This is why raising the transmission wrap width in an earlier release helped
  the layout and not the failure: fewer, longer lines still start from the
  same single-line measurement.

  **Every wrapped string in the game goes through this**, so anything long —
  transmissions, the mission log, the briefing between levels — was one string
  length away from the same fault at high HUD scales.

## 0.8.73 — 2026-08-01

**Fixed**
- **The server knows which world it is in, however that world was reached.**
  Its own idea came from the level *list*, which only list-driven play fills
  in — so a campaign, a level picked from the menu and a loaded save all left
  it blank, and anything keyed on the world name had nothing to match. The
  0.8.71 fix covered only levels walked to; this covers the rest.

  The client always knows the name, and `serv` runs a line on the server —
  engine fact 18, the same route the hosting options have used since 1998. No
  new message and no protocol version. The server reads it lazily, when
  asked, rather than at a fixed point in start-up: pinning down whether the
  client's send or the level's start happens first across a new game, a warp
  and a loaded save is exactly the ordering assumption that breaks quietly
  later.

  **This is what the start-holstered list has been failing on**, so those two
  levels are worth testing properly for the first time.

- **The transmission text is retried when it fails to build.** The trace named
  it exactly — *"Transmission 3016 ABANDONED - text surface failed to build"* —
  and 3016 is Hank's briefing, the longest in the game.

  That is not a coincidence. The paragraph is built one surface per wrapped
  line, and a single failure among them discards the whole thing, so the
  longest transmission is the most exposed to a transient surface failure.
  Engine fact 7 again, and the same level entry logged `mid NULL` from the HUD
  for the same reason.

  0.8.72 stopped a failure costing the rest of the level. This tries to stop
  the failure.

## 0.8.72 — 2026-08-01

**Fixed**
- **Hank is silent, and it takes the rest of the level with him.** The 0.8.71
  fix was aimed at the wrong link in the chain; this is the one.

  Building a transmission needs two surfaces, a portrait and the subtitle. If
  either fails the client cleaned up and returned — **without telling the
  server**. The server sets "a dialogue is playing" when it sends a
  transmission and clears it on exactly one message, so a transmission that
  failed to build stopped not just its own line but every remaining line of
  the level.

  The portraits are all present in `SHOGO.REZ`, so this is engine fact 7:
  surface creation failing transiently and saying nothing. The same level
  entry that lost Hank also logged `mid NULL` from the HUD rebuild — the same
  failure hitting a different surface in the same moment, which is what
  identified it.

  A transmission that cannot be shown now costs its own line and nothing else.
  The portrait is also retried once before giving up, since the failure is the
  device being briefly unwilling rather than a missing file — which may well
  save the line outright.

**Changed**
- **The reload line moved behind `WeaponDebug`.** It printed on every reload
  and every refusal — which includes simply holding the key — so ordinary play
  filled the console with it, as the traces from the last session showed.

  The reason it was ever unconditional still holds: a bound key that reaches
  the client at all produces a line, which distinguishes "the key never
  arrived" from "the reload was refused", and those need opposite fixes while
  looking identical from the outside. That is a diagnostic, and diagnostics
  belong on a channel. `WeaponDebug 1` brings it back.

## 0.8.71 — 2026-08-01

Everything here was found by a `StoryDebug` / `ProjDebug` trace rather than
by reading, which is the point of having them.

**Fixed**
- **A voice line longer than five seconds silenced the rest of the level.**
  This is the long-standing "Hank's dialogue doesn't fire" bug, and it was
  never about Hank.

  The transmission display runs for five seconds. The check that tells the
  server the transmission has *finished speaking* lived inside that display's
  own update — and the recovery that holds the display open for a long voice
  line lives in the drawing code, which is only called while the display is
  still up. So the frame the timer went negative, drawing stopped, the
  recovery stopped with it, and the timer could never come back. The server
  was never told, its "a dialogue is playing" flag stayed set for the rest of
  the level, and every queued line waited behind a transmission that had
  already finished.

  The trace said it once per frame: *"DialogQueue: 2 waiting, blocked
  (dialogue already active)"*. Display timing and a sound's lifetime are two
  different things, and conflating them was the whole bug.

- **The start-holstered level list matched nothing in the campaign.** The
  server's idea of "which world am I in" came from the level *list*, which
  only list-driven play fills in — a campaign walks world to world and never
  touches it. So the check ran against an empty string every time, which the
  trace showed as `StartLevel: world "" starts holstered: no`. It is also why
  crash reports said `server started world` with a blank after it. The name is
  now recorded where the server is actually told one.

- **A projectile that leaves the world is removed rather than detonated.** A
  grenade found a gap and was still accelerating through 9710 units/sec at
  y = −24386 when its five seconds expired — twenty thousand units below
  anything. The blast could hit nothing and cost a trace looking for a floor
  to scorch that was not there. The engine's own world box decides, so no map
  needs tuning.

**Added**
- The "a dialogue is playing" flag has a deadline. It is cleared by a message
  from the client, and nothing guaranteed that message ever arrives — a client
  can leave, a sound can fail to start, and the bug above stopped it being
  sent at all. The cost of it going missing is every remaining line of the
  level, silently. The timeout is deliberately generous: it is insurance
  against a lost message, not a limit on how long a character may speak. It
  reports when it fires, because a watchdog that trips unseen is a bug nobody
  fixed.

## 0.8.70 — 2026-08-01

**Changed**
- **Crash reports carry a stack scan as well as a frame walk.** The
  frame-pointer walk answers "how did we get here" only while the chain is
  intact — and crash 30384 faulted at an address in **no loaded module at
  all**, which is a call through a corrupted pointer, which is precisely when
  there is no chain left to follow. It produced four frames, every one of them
  unknown, and said nothing.

  The scan makes no assumption about frames: it reads the stack and reports
  every value landing in loaded code. It over-reports, because a stack is full
  of addresses left by earlier calls, and it says so rather than pretending to
  be a call order. Against a wild jump it is the only thing that still works.

**Added**
- `Tools/dmpmods.py` — resolves raw addresses against a minidump's own module
  list, and with `--stack` scans the faulting thread. This is what established
  that 30384's fault address was in no module, which the report alone could not
  say: "module unknown" and "not in any module" look identical on paper and
  mean very different things.
- `Tools/mapsym.py` — turns a module offset into the nearest function from the
  build's `.map`. Pairs with the above; the two together take a crash report
  from an address to a function name without a debugger.

## 0.8.69 — 2026-08-01

**Fixed**
- **Two crashes on the music system, from one lie told at start-up.**
  `CMusic::Init` assigned its engine pointer on its first line and only then
  tried to load the music driver. `IsInitialized()` is that pointer being
  non-null, and every other guard in the music code and the shell is built on
  it — so when the driver failed to load, the object reported itself
  initialised for the rest of the session and every later call went through
  to a subsystem that had nothing behind it. All three callers discarded the
  return value, so nothing anywhere noticed.

  The pointer is assigned last now, which makes `IsInitialized()` true only
  when it is, and repairs every guard downstream at once rather than one call
  site at a time.

- **The one music call that reached the engine unguarded.** Every route into
  the engine's music goes through `TransitionToLevel`, which refuses when the
  play lists never loaded — except the silence-to-silence case, which asked
  for a list that may never have been built. It is also the most frequently
  reached, because silence is the default state and the server asks for it
  whenever a fight ends. That is why one crash happened while **idling** in a
  campaign level with nothing else going on.

  Both crash reports died on a sound-driver worker thread — one reading from
  a near-null address, the other calling address zero — which is what driving
  an uninitialised music system looks like from the outside.

- Level exit, pause and resume no longer reach for the engine's music without
  asking whether there is any.

**Changed**
- A machine where the music driver will not load now **says so once**, at the
  console, instead of leaving it to be discovered as silence. The game is
  entirely playable without music and nothing else depends on it.

## 0.8.68 — 2026-08-01

**Changed**
- **The retail campaign levels get their own menu instead of being poured
  into the custom one.** `EnableRetailLevels` used to add all thirty-odd
  retail levels to *load level…*, sorted alphabetically among whatever custom
  levels were installed — so a handful of rows became thirty-odd, with no way
  to tell whose level was whose.

  The campaign menu now grows a **campaign levels…** entry when the cheat is
  on, and *load level…* goes back to meaning the custom levels. One class
  serves both lists, because they differ only in which directories they read;
  a second copy of the scrolling, the surfaces and the nice-name lookup would
  have been 450 lines free to drift.

  The row appears only when there is something behind it, which is the whole
  difference between a cheat and a feature.

- **The mission briefing between levels is drawn at the size it is shown.**
  It was blitted 1:1 from a 1998 bitmap font inside a fixed 600-pixel column,
  so at 1080p and above it was a paragraph of postage-stamp writing floating
  in the middle of an otherwise empty screen — the same fault the save and
  load menus had, in the place where it matters most, because reading that
  text *is* the screen.

  White, with the drop shadow that keeps it legible over the bumper art, and
  it honours `HudTextShadow` like every other shadowed string. The column now
  grows with the screen rather than stopping dead at 600, but is held to about
  two thirds of the width — a line of prose spanning a wide monitor is
  physically hard to track back to the start of.

  Wrapping is measured with the real font rather than guessed from a character
  count, because at an arbitrary scale a proportional font has no useful
  relationship between the two. That is the mistake that put chat off the edge
  of the screen, so it is not repeated here.

**Fixed**
- The launcher's tooltip for that cheat described a menu path that did not
  exist and a rule the game does not have — "not just the ones you have
  reached". **Nothing anywhere tracks which levels you have reached**, so
  there was never a partial version of this list to be the other half of the
  promise. It now says what the option does.
- A level list with nothing in it no longer allocates a zero-length array and
  then indexes it. Previously unreachable, because the one list always had the
  custom folder to read; reachable now that a menu can legitimately be empty.

## 0.8.67 — 2026-08-01

**Changed**
- **`Prediction 1` is now seeded, so it stops depending on a 1998 file we do
  not own.** Despite the name it is not input prediction — it is the engine
  interpolating remote objects toward their reported position, and it is the
  mechanism that hides the fact that stock clients report at 7 Hz. Without it
  other players visibly jump seven times a second.

  **The engine does not default it on.** It is on today purely because
  Monolith's `defaults.cfg` sets it and the engine writes its console state
  back out. Nothing of ours asserted it. That is a load-bearing setting
  arriving from a file we did not write, with a silent failure mode — stutter
  on remote players, and nothing anywhere pointing at the cause.

  No behaviour change: this is already the effective value everywhere. It is
  insurance, and it is deliberately **not** offered as an option, because off
  is strictly worse and there is nothing to choose.

  `preflight` asserts it alongside the other documented seeds, so it cannot
  quietly disappear the way it quietly arrived.

- Several engine console variables documented, including which ones do
  nothing. `FarZ` (default 10000) is the far clip plane and works.
  `LightTableRes` (350) sets how finely the engine lights things that move, so
  a lower number improves lighting on characters. `LODScale` is settable and
  has no effect whatsoever — worth writing down precisely because it looks
  like it should. The renderers also carry their own variables that the engine
  does not — `MipmapDist`, `ShadowZRange`, `ShadowLODOffset`, `WarbleScale` —
  so the engine's list is not the whole story.

## 0.8.66 — 2026-08-01

**Fixed**
- **A custom level could crash the server, and the crash went to every player
  on it.** The engine has *two* double-formatting console functions, not one.
  Engine fact 19 documented `CPrint`; `BPrint` has the identical flaw and is
  the worse of the pair, because its second pass is **broadcast to every
  connected client** rather than written to a local console.

  The live door was a keyframer message. A level can attach text to a keyframe
  and have the server print it, and custom levels are a supported feature — the
  campaign menu loads straight out of `Custom\`. That call had already been
  hardened once to `BPrint("%s", pText)`, which is textbook-correct usage and
  was **not enough**: a `%s` substitution does not re-scan what it inserts, so
  `%s%s%s%s` in a keyframe survived pass one intact and was read as conversions
  against arguments nobody pushed in pass two. Exactly the 0.8.15 bug, which
  was also correct at the call site.

  All 61 `BPrint` calls now go through `FreshPrint`/`FreshPrintText` like
  everything else, and the level's text is stripped of percent signs rather
  than escaped — which needs no assumption about how many passes the engine
  makes.

**Changed**
- `preflight` covers `BPrint` in both of its print checks. Verified by breaking
  each arm on purpose — and that is how the 26 call sites outside the AI debug
  dump were found in the first place, having been missed by a survey that only
  looked where the problem was expected.
- The stock AI debug dump (`DebugAI`) stops broadcasting to every player and
  prints locally. It had no business broadcasting struct sizes in 1998 either.
- **Animation name lookup is case-insensitive, and that is now settled rather
  than assumed.** It was recorded the other way round and flagged unconfirmed,
  which left six spellings in `ResolveWeaponAni` that could never have been
  reached. They are gone; `select1` and `reload1` stay, being genuinely
  different names rather than different casing. No behaviour change — the
  fallbacks were already unreachable — but a piece of the weapon code now says
  what is true.

## 0.8.65 — 2026-08-01

**Added**
- **Three console variables for weapon feel, tunable while the game runs.**
  `GrenadeVelocity` (both thrown grenades, 100–4000 units/sec),
  `GrenadeAngle` (degrees to pitch the throw up, relative to the aim, −45 to
  60) and `HandgunReload` (the .45 and the MAC-10, 0.10–5.00 seconds). All
  three default to 0, meaning "use the table", so nothing changes until
  somebody types at one.

  They exist because the assault rifle proved the point expensively: a
  confident number was wrong, four speculative fixes could not settle it, and
  one trace settled it in minutes. Feel is not something to be reasoned about
  in a text editor. When a pair is right it becomes the default in
  `WeaponDefs.h` and the variable goes back to doing nothing.

  Two things the numbers make visible: there is **no launch elevation
  anywhere** in the projectile code — velocity is the aim direction times a
  speed, then gravity — which is why a thrown grenade travels like something
  dropped. And the energy grenade leaves at 2000 units/sec, **identical to a
  TOW missile**.

  These are SERVER variables, so in single player or a hosted game they are
  set with `serv GrenadeVelocity 1200`; on a dedicated server, from the
  console or over rcon without the `serv`. `ProjDebug 1` reports the speed
  and angle each launch actually used, so a value that never arrived is
  visible immediately rather than after a session of "feels the same".

**Changed**
- **Engine fact 17 was half the picture, and the correction is worth more
  than the feature.** The server still cannot READ a variable the player
  typed. But `serv <name> <value>` runs the rest of the line on the server —
  the engine's own command, and stock Shogo pushes every hosting option
  across that way — and `GetSConValueFloat` reads it back through the server
  console mirror. So any number both sides must agree on has a route that
  needs no new message, no protocol version, and works from the dedicated
  console and rcon for free. `MissileSpeed` has used exactly this loop since
  1998. Recorded as fact 18.

## 0.8.64 — 2026-08-01

**Fixed**
- **Disable fog now actually disables fog.** The launcher wrote `+DisableFox`
  and the game reads `DisableFog`, so the option had never done anything. The
  misspelling was not ours — it came from one of Monolith's own launchers —
  but Shogo.exe and the SDK source both spell it `DisableFog`. Monolith
  shipped their two launchers disagreeing by one letter and we copied the
  broken half.

**Removed**
- **Mipmap Sharpening.** `EnableMipSharp` appears in both of Monolith's
  launchers and nothing anywhere consumes it — not the game, not either
  renderer, not the SDK source. It was a dead control in 1998 and copying it
  faithfully made it a dead control here. A control that does nothing is worse
  than a missing one, because ticking it makes you believe something changed.

  The general lesson, since both of these flags came from the same place: a
  setting appearing in a launcher proves something WRITES it, and says nothing
  about whether anything reads it.

## 0.8.63 — 2026-08-01

**Fixed**
- **LAN discovery stops being the first casualty of a long server list.** The
  in-game multiplayer wizard rolls every remembered address into one
  2040-byte query string and appended the LAN broadcast `;*` only "if there's
  room". At about 22 characters an entry that is roughly ninety addresses — so
  a player who had joined enough servers filled the buffer, the test failed,
  and the game silently stopped finding servers on the local network. Nothing
  reported it, and the cause sat at the far end of the list from the symptom.

  Room for the broadcast is reserved now rather than hoped for. The launcher
  was the thing filling the buffer: it kept 256 addresses and now keeps 48.
  A history that does not fit is not history.

**Changed**
- Three engine behaviours corrected in our own notes. `TimeScale` is a real
  registered variable that has no effect — it will look plausible to anyone
  who finds it in a console listing and it does nothing. The frame delta is
  clamped to [0.01, 0.2] seconds and the server tick defaults to 30 Hz,
  neither of which the SDK mentions. And text reaching the engine's console
  gets formatted twice, with a 500-byte ceiling and no bounds checking, which
  is why our own correct-looking `printf` usage was not enough in 0.8.15.
- Console variable names are **case-insensitive**, which settles a suspected
  launcher/game mismatch as not a bug.
- Documents the engine's own console variables: `IPDebug`, `DropRate`,
  `LatencySim` and the rest. Free diagnostics we had no equivalent of.

## 0.8.62 — 2026-08-01

**Fixed**
- **Save and load menus draw text at the size they are shown.** The menu
  background was already stretched to fill the screen while the text was
  blitted 1:1 from 1998 bitmap fonts. At 1080p and above that is a
  full-screen picture with postage-stamp writing on it, which is why it read
  as broken rather than merely small.

  Menu text now goes through the same scaled-font helper the chat, the corner
  feeds and the HUD numbers already use. Bold with a drop shadow, because a
  scaled font at menu size is thin against a busy background picture.

  The colours are the **original** ones, sampled out of the shipped art
  rather than guessed: `Font08n.pcx` is RGB(255,107,0) and `Font08s.pcx` is
  white, so unselected rows are orange and the selected row is bright —
  exactly the convention the 1998 menus used, since those two fonts differed
  only in colour.

## 0.8.61 — 2026-08-01

**Fixed**
- **Chat no longer runs off the screen.** The message limit counts
  characters, which says nothing about how wide they are. The drawn width is
  clamped and the view scrolls along with the cursor.
- The `SAY:` prompt is drawn in the grey the text kill feed already uses, from
  one shared constant so the two cannot drift into different house styles.

**Added**
- **Quick load must be held; quick save is still one press.** The two keys sit
  next to each other and do opposite things, and only one can lose work — a
  mis-hit save costs a quicksave slot, a mis-hit load costs everything since
  the last one. A guard on the safe action would be friction with nothing
  bought.
- The launcher's setup cards show the installed version, or `v0.8.60 →
  v0.8.61` when an update waits, so "Update available" says which. Only the
  two ShogoFRESH cards show one; the rest are upstream projects whose
  versions are not ours to claim.
- The cheat tooltip names where the level list lives.

**Known gap**
- Player names in the chat box are not coloured yet. They arrive as finished
  lines, so colouring the name means parsing it back out or extending the
  protocol — and team colours need a team system that does not exist yet.

## 0.8.60 — 2026-08-01

**Fixed**
- **The server window stops flickering.** Two causes, matching the report of
  "the elements, not the background". Several fields were rewritten on a
  timer whether or not the value had moved, and every write is a repaint —
  all twenty text updates now compare first. Then the overdraw:
  `WS_CLIPCHILDREN` and `WS_EX_COMPOSITED`, since the earlier background fix
  covered the dialog's own erase and nothing else.

**Investigated, not fixed**
- The grapple beam drawing over scorch marks is **not** a depth-test bug —
  the mark is an ordinary Z-tested sprite and nothing draws on top
  deliberately. It is translucent sort order, and the engine exposes no sort,
  draw-order or render-priority call anywhere in its headers. The only lever
  is making the beam opaque so it writes depth, which trades the bug for the
  beam's translucency — a look decision rather than a fix.

## 0.8.59 — 2026-08-01

**Fixed**
- **The default MCA is the Ordog.** Several levels put the player in "the mech
  you picked", and skipping past the level that asks left the stock default of
  a UCA Enforcer. The Ordog is the machine the game hands you first and the
  one the story treats as yours, so that is what an unanswered question
  should produce.

## 0.8.58 — 2026-08-01

**Fixed**
- **Friendly AI stop being walls.** A squad-mate standing in a doorway is a
  wall, and the campaign puts them in corridors constantly, so a fight becomes
  shuffling around the people you are fighting alongside. They are now solid
  on the server and non-solid to the player on the client — projectiles,
  other characters and the AI's own physics are untouched, and clearing
  solidity outright would have made teammates immune to rockets.
  `TeammateCollision 1` puts the walls back.
- **Bots in MCA maps stop dropping weapons nobody can pick up.** The 0.8.57
  drop rule was single-player only, on the reasoning that multiplayer has no
  "the player" to ask — but an MCA deathmatch map spawns everyone in a mech
  and fills with on-foot bots, so every bot death left a MAC-10 on the floor.
  The question was never "which mode is this", it was "is anyone on foot".

## 0.8.57 — 2026-08-01

**Fixed**
- **Drops nobody can use stop being created.** An infantryman killed while the
  player is piloting an MCA left a hand weapon and sometimes a medkit, neither
  of which a mech can bend down for — and by the time the player is back on
  foot the fight has moved on.
- **Endless-ammo weapons stop spawning as pickups.** Under `InfiniteAmmo 2`
  the pistols went on dropping and spawning exactly as if ammunition
  mattered. The pedestal rebuild now swaps them for something worth crossing
  the map for, and turning infinite ammo on is now itself a reason to run
  that pass — previously it only ran for a blocklist or a shuffle.
- **Death shouts get rarer and get their variety back.** Stock shouted on
  every death, and a firefight is a lot of deaths, so the scream became the
  noise a room makes; it is 45 in a hundred now, and the rest die quietly.
  The genuine bug underneath: the choice between the three death sounds was
  drawn from the global random stream that `Weapon.cpp` re-seeds on every
  shot to keep bullet spread in step across the network, so the roll was
  pinned to a handful of states and three sounds very often played as one.
  The variety was in the files the whole time and nothing could reach it.
  Classic keeps the 1998 behaviour.

## 0.8.56 — 2026-07-31

**Fixed**
- **Explosion shrapnel stays near its explosion.** The emitters are the
  effect — each flies ballistically and trails particles — and one component
  of their velocity rode the surface normal outward at both ends. A one-way
  push of 150 to 400 over the better part of two seconds carries them several
  hundred units, which is why the effect read as sliding away from the blast
  rather than belonging to it. Most obvious on an exploding car, where nothing
  else is moving to distract from it. Cut to about a third; the sideways
  spread is untouched, because that is what makes it a burst rather than a
  puff.

## 0.8.55 — 2026-07-31

**Fixed**
- **Nobody walks during the intermission.** The earlier fix was not wrong —
  movement really was frozen. Footsteps were never caused by movement: a
  footstep is a keyframe in the animation, so a run cycle caught part way
  through when the match ended went on hitting its keys against a character
  standing perfectly still. Refused now at the one place the sound is made,
  which covers players, bots, AI, every animation and anything added later.

## 0.8.54 — 2026-07-31

**Fixed**
- **A blast that goes off in mid-air scorches the floor under it.** Exploding
  props fire their explosion with a zero lifetime, so the projectile detonates
  where it stands and never touches anything — the surface comes back as air,
  and air takes no mark. Right for a bullet, wrong for a blast. An air-burst
  now looks down for a floor, as far as its own damage radius, and marks it
  with that floor's own normal. Exploding cars were the report; grenades that
  time out in the air get their scorch back for free.

  Down and only down, deliberately: searching every direction would paint
  marks on ceilings above explosions that plainly never reached them.

## 0.8.53 — 2026-07-31

**Fixed**
- **The grapple stops catching on floating pickups.** The tractor beam
  accepted anything that was a model, and pickups are models, so the beam
  stopped at a rocket box instead of the wall behind it. In a room with
  pickups in it the grapple could not reach anything useful.
- **A spider mine no longer rides a powerup through its own respawn.** The
  item vanishes and comes back, and the charge attached to it went along for
  the trip.

  Stated plainly: a pickup the level author marked solid will still deflect a
  mine. It will not stick and it will not ride the respawn.

## 0.8.52 — 2026-07-31

**Added**
- **Next Level says what it did.** From the operator's chair this was a click
  and then silence: nothing distinguished a working button from a dead one
  until the map changed, and nothing said how long the scoreboard wait would
  be. The seconds come from the same accessor the intermission uses, so the
  line an operator is told and the wait the players get cannot drift apart.
- **Next Level is hold-to-confirm, two seconds, counting down in its own
  caption.** One stray click on a window an operator keeps open all day used
  to end the match for everybody on the server. A click too short to count
  says so, because a button that does nothing when pressed is
  indistinguishable from a broken one.

**Known consequence**
- Greying the button out for the intermission removes the press-again-to-skip
  the handler still supports.

## 0.8.51 — 2026-07-31

**Fixed**
- **Blast marks and explosion rings sit flush to walls in multiplayer.** The
  report that settled it was one observation: correct in single player, wrong
  in multiplayer, except the Shredder. For a projectile you fired yourself the
  *client* draws the impact locally and the server's arriving copy is
  suppressed — so in multiplayer the client's own copy of the decision is what
  you see, and it carried a worse one.

  The fault was the order of its tests. A collision against a wall reports the
  world as an object as well as a polygon, so the first branch matched, the
  plane normal was never read, and every wall hit kept the starting guess of
  straight up. It now reads the plane whenever there is one. The Shredder was
  exempt because it is hitscan and never went through this path at all.

  Two implementations of one fact, which disagreed for years. They agree now.
- **A fast finger cannot outrun the fire mode.** Limiting the rounds reloaded
  the allowance the instant it emptied, so clicking fast simply started the
  next burst. A new trigger pull now waits out the mode's own gap as well.

## 0.8.50 — 2026-07-31

**Changed**
- **The holster-start levels are truly forced.** The weapon keys and reload
  are refused and the holster toggle declines to take it back out — what the
  forced flag has always meant for a no-weapons volume. And it explains
  itself: refusing silently is how a deliberate rule turns into a bug report.

  Worth knowing before adding a level: nothing releases a forced holster
  except walking out of a no-weapons volume, so a level-start one lasts the
  whole level. Right for a briefing, wrong for anywhere that turns hostile.

## 0.8.49 — 2026-07-31

**Changed**
- The holster-start list is `02_QUARTERS` and `03_MCA_DOCK`. `33_QUARTERS` is
  dropped — it was a guess from the name rather than a judgement about the
  level, and an unasked-for holster in the middle of a campaign is worse than
  none. The header now carries the procedure for changing it, because the
  failure mode is silence: a level name with a typo simply never matches.

## 0.8.48 — 2026-07-31

**Changed**
- **A spider mine fired into a person kills them where it hits.** Stock
  attached the mine to whatever it touched and waited for the fuse, so one
  taken in the chest stuck to the target's collision box and rode them around
  while they went on fighting. A lump of metal arriving that fast is lethal by
  itself, and the charge that follows should be a second event rather than the
  only one. The explosion still happens — it just is not glued to a body that
  is already falling.

  Machines keep the stock behaviour, because sticking a charge to a mech and
  letting it work is the entire point of the weapon. FRESH only.

## 0.8.47 — 2026-07-31

**Fixed**
- **Explosions against something other than the world stop scorching the
  floor.** Every impact effect is built from one normal, and getting it wrong
  does not fail loudly — it lays the whole explosion out flat on the ground
  wherever the blast happened to be. Stock started that normal at straight up
  and only replaced it for world geometry, so a blast against a door, a crate,
  a mech or a person kept "up". Those now face the blast back down the
  direction of travel. A fuse that merely expired still uses up, deliberately:
  by then the projectile is usually lying on the ground.

**Added**
- `ProjDebug` reports the normal each explosion used and which branch produced
  it — because the above does not explain the wall case in the report, and
  guessing a fourth time is not a plan. (It settled it: see 0.8.51.)

## 0.8.46 — 2026-07-31

**Fixed**
- **The ammo icon rebuilds itself when the surface failed.** Surface creation
  returns null while the game is in the background and says nothing. The icon
  is built per weapon, in the one place that runs on a weapon change, so a
  failed creation left the counter with no icon until the player switched
  weapons and back — which is exactly how it was reported.

**Added**
- **Levels can open with the weapon put away.** Shogo opens several levels in
  a room where nobody is fighting and the player stands there aiming a rifle
  at their commanding officer. A list rather than a map edit, because the
  retail worlds ship as compiled data and nothing in this project builds
  worlds. `StartHolstered` 0 off, 1 the list, 2 every level.

## 0.8.45 — 2026-07-31

**Fixed**
- **The assault rifle fires what the mode says.** The trace refuted the
  suspect and named the cause in one session: zoom and mode were correct
  throughout, and three rounds still left per click while the count read zero.

  Firing is triggered by a string key in the fire *animation*, and the assault
  rifle's animation carries three of them. So the gate was counting trigger
  pulls while the animation decided how many rounds actually left: one allowed
  start, three rounds out. Burst mode was the same arithmetic with three
  allowed starts, which is where the fourth round came from. A limited fire
  mode now refuses the fire key once the allowance is gone — the animation
  still finishes, it just stops producing shots.
- Unholstering a melee weapon no longer shows the pistol's ammo count
  (regression from 0.8.44).

## 0.8.44 — 2026-07-31

**Fixed**
- **Firing during cutscenes.** The player flags gate on editing, intermission
  and focus — never on the cinematic lock. The mouse offsets were already
  dropped for it, so a locked cutscene could not be *turned*, but the trigger
  is polled separately and fired happily through every cutscene in the
  campaign. The movement manager had the identical hole, so the firing intent
  was reaching the server too.
- **A holster survives a trip to the chase camera and back.** The pending flag
  only lasts until the putting-away animation ends, so the model had no
  durable idea it was holstered: returning from the chase camera showed the
  weapon again while every fire path still refused it. There is one now, and
  the visibility call refuses to *show* while it is set — one choke point, so
  the chase camera, vehicle mode and spectator all inherit it.
- The ammo readout follows the weapon while holstered.
- **`FirstPersonOnly` no longer applies in single player.** Single player runs
  a local server, so the multiplayer rules message arrives in the campaign and
  set the flag; three consumers guarded themselves and a fourth forgot, which
  is what overrode the vehicle-mode third-person preference. Fixed where the
  flag is set, so the flag now means "the rule, as it applies to us".

## 0.8.43 — 2026-07-31

**Fixed**
- **Player-spoken dialogue no longer crashes the campaign.** Two crash reports
  months apart, both dying in the game DLL reading address `0x6E75707B` — four
  bytes of a path string used as a pointer. Symbolicated against a rebuilt
  map: the dialogue message handler.

  When that message was converted from carrying a freed struct *pointer* to
  carrying the contents, two of the three receivers were converted and this
  one was missed. It kept casting the first four bytes of the message — now
  the head of a path string — to a pointer. So every line of dialogue spoken
  by the *player* crashed the campaign at world entry, deterministically,
  while NPC lines played fine. That is why `02_QUARTERS` and `09_COMM1` never
  worked and most other levels did, and why "disable the overlays" never
  helped.

  Because this was found by crash report rather than review — twice —
  preflight now bans the pattern outright: no message read may be cast to a
  pointer. A payload is data, never an address.

## 0.8.42 — 2026-07-31

**Changed**
- Engine start-up moved out of `RiotClientShell.cpp`. One 550-line function
  doing seven jobs inside a 12,400-line file becomes a named orchestrator with
  the straight-line blocks named. A move, not a rewrite: the initialisation
  order is preserved line for line, and every step that can abort start-up
  stays inline so the early-return control flow reads in one place. No
  behaviour change intended.

## 0.8.41 — 2026-07-31

**Changed**
- The launcher's 1,747-line view model becomes a core plus a partial per tab.
  Every move verbatim; no logic changed. No behaviour change intended.
- Peer exchange is documented as shipped rather than planned — it has been
  implemented since the browser gained peer crawling.

## 0.8.40 — 2026-07-31

**Changed**
- The options dialog is out of the dedicated server's `NetStart.cpp`, and the
  build has one source list per project instead of two that could disagree. No
  behaviour change intended anywhere in this release; it exists so the
  refactored server ships and gets exercised rather than sitting unreleased.

## 0.8.39 — 2026-07-31

**Changed**
- **Master volume is a multiplier, not a third hand on the same dial.** It no
  longer drags the sound and music sliders; they stay where you put them and
  the product is what reaches the game. Sound 50% and music 80% under a master
  of 50% arrive as 25% and 40%, and the sliders still read 50 and 80.

  Scaling the other two was lossy — anything that clamped on the way down did
  not come back — and it meant the sliders showed what master last did to them
  rather than what you chose.

  Master and both slider positions are kept in the launcher's own prefs,
  because `autoexec.cfg` holds one number per channel and it is the product:
  master 50 with sound at 100 writes exactly what master 100 with sound at 50
  writes. A dim `→ 25%` beside each slider shows what the game will actually
  be given.

  An install that predates this opens unchanged.

## 0.8.38 — 2026-07-31

**Changed**
- **The .rez format has one spec.** `Docs/public/REZFORMAT.md` is now the authority;
  the two readers that implement it — the launcher's `RezArchive.cs` and the
  server's `Shared/FreshRez.cpp` — point at it instead of each carrying their
  own copy of the explanation. The document also records the three bugs that
  shaped it, so the reasoning outlives whoever remembers it.
- `preflight` checks the two readers agree on the structural constants and
  both cite the spec, and specifically that neither has reintroduced an
  entry-size cap. Both arms verified by breaking them on purpose.

## 0.8.37 — 2026-07-31

**Changed**
- **Server window reorganised.** The console moves to the top, full width —
  it is the thing an operator reads continuously and everything else is
  reference. Players moves up beside Server Info and Game Info; Commands
  drops down level with the level list.

**Added**
- `Docs/TESTRUN-v0.8.37.md` — a fresh runbook for everything added since
  0.8.6 that has never been run by a person: the moderation stack, spectator,
  the mod layer, the rebuilt server window, and the regressions worth
  re-checking.

## 0.8.36 — 2026-07-31

**Fixed**
- Round Limits rows overlapped — two rows 10 units apart with 12-high fields,
  so Intermission sat on top of the frag limit. Rows are 17 apart now and
  every numeric field in the dialog is the same width.
- **Frag and time limit showed 0** on a server running to 25 frags. Truthful
  about the *override* and useless as an answer to "what is the limit". They
  fall back to the session's own numbers now, so the boxes agree with the
  Level Goal line; typing over one is what makes it an override.

**Changed**
- "Tractor Beam" is **Grappling beam**, "Ramming Damage" is **Ramming
  damage** — in both the options dialog and the setup wizard.
- The main window's level list says whether it is the play order:
  *Levels in Rotation (in order)* or *(random order)*. Reading the list top to
  bottom under a random rotation tells you nothing about what is coming, which
  is the same misreading that made the old "Next Level" wrong.

**Added**
- **`Mods`** in the server console lists the manifests this server loaded,
  with author and source archive.

## 0.8.35 — 2026-07-31

**Added**
- **Mod gameplay rules now work when you host from the game** — stage 4, the
  last gap in the mod layer. Until now a manifest's rules only applied on a
  dedicated server, because that is the only process that could read them.

  Not over the wire. Sending them would mean a *server* accepting console
  variables from a *client*, which is not a thing to put in a protocol.
  Instead the client — which can read inside an archive — writes the gameplay
  half to a file, and the server in the same process reads it. A file cannot
  be sent by a remote player, so there is no trust decision to get wrong. It
  is skipped when a dedicated server is hosting, and the allow-list is
  re-checked on read.

## 0.8.34 — 2026-07-31

**Added**
- **Frag and time limits can be changed while the server runs.** They could
  not before, and not by oversight: the limits live in the NetGame struct the
  engine hands back from `GetGameInfo`, and there is no `SetGameInfo` -
  `server_de.h` has the getter and nothing else. `FragLimit` and `TimeLimit`
  now override them when set above zero, so the new **Round Limits** box is a
  real control rather than a decoration. Zero means "whatever the session was
  started with", so nothing changes for a server that never touches them.
- **First person view only** and **Quick turn allowed** in Options, below
  Critical hits.

**Changed**
- **Options reorganised**: Round Limits across the top (frag, time,
  intermission), then Toggles and Rules side by side, then Speeds and Scales,
  then World. The dialog was overcrowded once the ShogoFRESH settings were
  added to it.
- **"Next Level" stops lying under a random rotation.** It read "the entry
  after this one", which is right for a sequential order and false for a
  random one - the next map is chosen at the moment of the change. It now
  names the map when one is actually decided (during an intermission,
  `BeginIntermission` picks it so the scoreboard can announce it) and says
  `(random - chosen at the change)` otherwise, rather than inventing a name.
## 0.8.33 — 2026-07-31

**Fixed**
- **Players kept walking on the spot during the intermission.** The freeze
  was only ever half a freeze: the *client* stops sending input when the
  scoreboard comes up, so your own character stops, but the **server** was
  never told. It kept whatever movement state each player last sent, so
  anyone who was running when the match ended went on running for everyone
  else - the animation looping and its footsteps firing, on a character
  standing perfectly still. Visible only on *other* players, which is what
  made it confusing.

  `UpdateControlFlags` now treats an intermission as "no input", which is
  what it means. The flags were already being cleared for the existing
  no-input case, so the character falls into its idle animation on its own
  rather than needing a freeze of its own invention.

## 0.8.32 — 2026-07-31

**Changed**
- **The Options dialog is themed.** It is a plain `DialogBox` with its own
  procedure rather than an MFC dialog, so the dark theme had never reached it
  and it opened in 1998 grey on top of a dark window. `FreshTheme` gained
  HWND entry points for exactly this case, sharing the main window's brushes
  and fonts so the two cannot drift apart.
- **"Next Level" holds the scoreboard first.** It used to cut straight to the
  next map, which from a player's side is the world vanishing mid-game.
  It now runs the same intermission the end of a match does. No special case
  was needed for `Intermission 0` — that already means "straight to the next
  map". Pressed *during* an intermission it skips the rest of the wait.

**Added**
- **A ShogoFRESH Rules group in Options**: Ruleset (Classic/FRESH), Infinite
  ammo, Random pickups, Critical hits and Intermission seconds — the settings
  added since 1998, in the window that already existed for rules. They read
  and write the game console directly rather than going through the shared
  `ServerOptions` struct, because that struct is also the client's and these
  are settings only a dedicated server can use.

## 0.8.31 — 2026-07-31

**Fixed**
- **The console corrupted itself when scrolled.** A *read-only* multiline edit
  sends `WM_CTLCOLORSTATIC`, not `WM_CTLCOLOREDIT`, so the console was landing
  in the label branch of the theme and being told `TRANSPARENT` — right for a
  caption on the dialog, wrong for a scrolling text box, which then never
  erased what it scrolled away from.
- The same bug is why the console did not match the Levels list beside it: it
  was taking the dialog colour instead of the field colour. One cause, both
  symptoms.

**Changed**
- **Server window rearranged.** Server Info and Commands across the top, Game
  Info with the Players list beside it, Levels below, and the console **full
  width** along the bottom. The console is what an operator actually reads and
  it had a quarter of the window.
- **"Boot" is now "Kick", and there is a "Ban" beside it.** Ban goes through
  the game's existing `Ban <id>` command, so it records the install token
  rather than just a name, and it asks first — the two buttons are one slip
  apart.
- Server title reads `ShogoFRESH Server v0.8.31`, matching the launcher.

**Removed**
- **Double Jump**, from both the options dialog and the wizard. The checkbox
  has been there since 1998 and nothing has ever read the variable — no
  `DoubleJump` appears anywhere in the game code. A control that does nothing
  is worse than a missing one, because ticking it implies something changed.
  Everything else in Options was verified to be read and stays.

## 0.8.30 — 2026-07-31

**Added**
- **Manifests can set gameplay rules** — stage 3. `FreshSrv.exe` reads the
  manifests in the rez files it is loading and applies the server half:
  `Ruleset`, `RandomPickups`, `InfiniteAmmo`, `CriticalHits`, `BlockWeapons`,
  `BlockItems`, `TractorBeam`, `RammingDamage`, `RunSpeed`, `MapOrder`. So a
  mod can now describe a *game mode*, not just a look — and it still runs on
  ShogoFRESH's game code, which means bots, moderation, spectator and match
  records all keep working.

  A mod may describe the game. It may **not** touch the server's identity,
  network settings or moderation — no `RconPassword`, no `MaxPlayers`, no
  `ServerName`, nothing from the ban stack. That boundary is the point.

- `ModRules 0` in `ShogoSrv.cfg` refuses manifests entirely. Every setting a
  manifest changes is written to the session log **with its previous value**,
  so an operator can see exactly what a mod did.

**Note on precedence**
- Manifests are applied after `ShogoSrv.cfg`, so a mod's rules win. The
  alternative sounds better and is worse: a stock Shogo install already ships
  a `ShogoSrv.cfg` with values for several of these keys, which would silently
  neuter most manifests on most servers. Hence the log line and the off
  switch.

**Fixed**
- The new C++ rez reader had a 4 MB entry cap — chosen because a manifest is a
  text file — which silently dropped a 5 MB map from a pack. Caught by
  validating against `lithrez` before shipping it. The cap now lives with the
  caller that actually knows what size to expect.

## 0.8.29 — 2026-07-31

**Added**
- **The launcher reads mod manifests** — stage 2. The Mods tab gains a
  "Describes itself as" column showing *name by author*, and a row tooltip
  with the description, the settings the mod applies, and anything the game
  will refuse. A mod author sees a mistake without launching the game.
- The archive viewer leads with the manifest line when a `.rez` has one.

  This is the first time the launcher can say anything about a mod beyond its
  filename and size. The RezMgr format has no author field, no description and
  no version — a fact established the hard way when the question was first
  asked and the answer came back empty.

**Changed**
- `preflight` checks that the manifest allow-list in the game
  (`Shared/FreshManifest.cpp`) and in the launcher (`ModManifest.cs`) agree,
  and that every name on it is a console variable something actually reads.
  Two copies of a list is what drifts, and the symptom would be the launcher
  telling an author a setting works while the game refuses it. Both arms of
  the check were verified by breaking them on purpose.

## 0.8.28 — 2026-07-31

**Added**
- **Mod manifests — stage 1 of the modernisation layer.** A mod can now
  describe itself as data instead of shipping a game DLL. Put a text file at
  `FreshMod\<name>.txt` inside the rez:

  ```
  FreshMod    1
  Name        "Squishie 2.2"
  Author      "Wraith"
  Description "Human-sized players against 60ft MCAs."

  Set FovX             110
  Set Gore             2
  Set MuzzleFlashScale 2.0
  ```

  The client reads every manifest it finds and applies it, so the mod and all
  of ShogoFRESH are true at the same time — which layering game DLLs can never
  do, because the engine loads exactly one `CShell.dll` and one `Object.lto`.
  See [Docs/MODLAYER.md](Docs/MODLAYER.md).

  A **folder** rather than a well-known filename, because `-rez` is last-wins
  per file: if every mod shipped `freshmod.txt` at the root, installing two
  would silently hide one.

  Only ten client presentation variables are accepted, each verified to be
  read by something today. Anything else is refused and reported. A manifest
  is data and cannot execute, so its worst case is a setting you can type back
  — an improvement on a mod DLL, which can do anything.

- `ModDebug 1` narrates what was found, parsed, applied and refused.
  `FreshMods 0` ignores manifests entirely.

**Note**
- This is presentation only, and it does not *run* existing mods — it lets one
  be re-expressed. Scale, loadout and weapon tables are server-side and come
  later; the server has no way to read a file at all, so its half arrives over
  the wire or through `FreshSrv.exe`.

## 0.8.27 — 2026-07-31

**Added**
- **Partial-overlap warning.** "Contains game code" was one verdict covering
  two very different situations, because `-rez` resolves last-wins *per file*
  rather than per archive. A mod carrying all four of ShogoFRESH's game files
  is a clean swap — its game instead of ours. A mod carrying only some of them
  produces a mixture neither project has ever run, and until now the launcher
  said the same thing about both.

  The Mods tab and the archive viewer now name which files a mod replaces and
  which ours would still supply. Real cases on hand: Squishie 2.2 carries
  three of the four and would pair its server game code with **our** server
  strings, and `WidescreenPatch.rez` from the ShogoFix package carries only
  `CShell.dll`. Strings resolve by number, so neither crashes — the text just
  goes quietly wrong, which is the failure worth naming.

## 0.8.26 — 2026-07-31

**Added**
- **Extraction from the archive viewer.** *Extract selected* writes the
  highlighted rows, *Extract all* writes the whole archive, both to a folder
  you pick, keeping the archive's folder structure. A filter plus Ctrl+A
  extracts just the matches. Retail `SHOGO.REZ` comes out as 6,135 files in
  about two seconds. The archive is never modified, and extracted bytes are
  verified byte-identical to their originals.
- Extraction reports what it did rather than only what worked: files renamed
  because the name repeated or held characters Windows rejects, and files
  **refused** because the entry tried to write outside the folder you chose.

**Security**
- Entry names are treated as hostile input. A `.rez` is a file somebody else
  made, and a name like `..\..\Windows\System32\x` would otherwise write
  wherever it liked — the zip-slip bug. Every output path is resolved and
  checked to be inside the destination before anything is opened. Verified
  against a purpose-built archive carrying both slash styles, folder-based
  traversal, drive-qualified paths, reserved device names and duplicates:
  everything hostile refused, everything legitimate written.

## 0.8.25 — 2026-07-31

**Changed**
- Archive viewer columns reordered to Name, Type, Folder, Size — the name is
  what you are looking for, the folder is where it happens to live. *Copy
  list* follows the same order so a paste lines up with the screen. Rows
  still arrive grouped by folder; click any header to sort another way.

## 0.8.24 — 2026-07-31

**Added**
- **Archive viewer.** Select a `.rez` on the Mods tab and click *View
  Contents* (or double-click the row) to see what is inside it: every file,
  its folder, type and size, with a filter box and a *Copy list* button. It
  opens with a one-line summary — how many files, how many multiplayer
  levels, and whether the archive carries game code — so the usual question
  ("what did I just install?") is answered before you read a single row.
  Read-only; nothing is extracted or modified.

**Fixed**
- Level listing and the game-code warning are read from the archive's own
  directory now instead of being inferred from a byte scan. Both previous
  versions shipped bugs — a warning that fired on every map pack, then one
  missing map per archive — and the second was only visible because the first
  fix made the numbers comparable. The parser is checked entry-for-entry
  against Monolith's `lithrez` across eight archives and 8,383 resources.
- Archives written by other tools are read correctly. WinRez LT 3.0 signs its
  own banner where Monolith's writes "RezMgr", and `ShogoP.rez` leaves the
  second banner line blank — so the banner is free text and cannot be used to
  identify the format. `WidescreenPatch.rez` was affected.

## 0.8.23 — 2026-07-31

**Added**
- Fira Code ships with the dedicated server. The font loads from beside
  `FreshSrv.exe` with `AddFontResourceEx(FR_PRIVATE)` — this process only,
  nothing installed on the machine and nothing left behind — so the console
  gets the monospace face 0.8.20 asked for instead of falling back.
- `FiraCode-OFL.txt` alongside it. SIL OFL 1.1 permits the bundling on the
  condition that the licence travels with the font, which is why the two are
  shipped as a pair or not at all.

## 0.8.22 — 2026-07-31

**Fixed**
- Map listing inside a `.rez` dropped one map per archive. The type code
  *precedes* the entry name; the scanner looked forward for it and kept
  finding the **next** entry's, which works for every map except the last one
  in the directory. 21 of 22 in both `SHOGO.REZ` and a custom pack, with a
  different map missing from each. It now reads the type at its fixed offset.

**Changed**
- A fix may declare optional files. `FiraCode-Regular.ttf` and its licence are
  the first: `prepare-redist.ps1` ships the pair or neither, and a payload
  without them is complete rather than "missing".

## 0.8.21 — 2026-07-31

**Added**
- `MCA_SPIRES` and `MCA_ZERO` to the retail rotation list. Both are in
  `SHOGO.REZ`, both are ordinary deathmatch maps, and neither has been
  selectable since the launcher was written. Found by counting a map pack's
  levels against ours.

**Fixed**
- Levels inside a `.rez` were never found. A RezMgr entry does not store
  `NAME.dat` — the name is one field and the extension a separate four-char
  type code held as a DWORD, so on disk it reads backwards (`TAD`). Scanning
  for `.dat` found zero entries in a file holding 22.
- The game-code warning fired on every map pack ever made. It scanned for the
  substring `OBJECT`, which appears 4,674 times in one 28 MB pack because
  level files are full of object class names. It now asks whether any entry is
  *typed* `dll` or `lto`, which is the actual question.

**Changed**
- Mod sizes read as KB/MB. `29302011` tells you nothing at a glance; `27.9 MB`
  tells you it is a map pack and not a skin.

## 0.8.20 — 2026-07-31

**Added**
- Levels inside `Custom\*.rez` appear in the rotation list. A map pack shipped
  the normal way had every level hidden, and the only way in was hand-editing
  `ShogoSrv.cfg`.
- Master volume slider, and percentages on all three. 100% is the engine
  ceiling of 90 — `RiotSettings` clamps there with the stock comment "hack to
  keep sound volume reasonable". The clamp stays; the slider stops offering
  numbers that do nothing.
- Refresh button on the Mods tab. The list was built once at startup, and
  trying a mod out is exactly when you add one.
- Intermission checkbox, derived from the value rather than stored separately
  so the two cannot disagree.

**Changed**
- The server console uses a monospace face — Fira Code, then Cascadia Mono,
  Consolas, Courier New. A console is a grid of characters; the proportional
  dialog font left no column lining up.
- Respawn and heal tooltips carry the actual numbers.
- "Auto 3rd person" onto its own line above "Filter profanity"; padding under
  the rotation list.
- The Classic tooltip no longer promises the sniper's 2-round clip. 1998 had
  magazines and no way to see or operate them (0.8.5).

## 0.8.19 — 2026-07-31

**Changed**
- V-sync moved beside the Mode dropdown. Both are display output settings, and
  someone choosing borderless windowed is the person about to wonder about
  tearing.

## 0.8.18 — 2026-07-31

**Added**
- `FreeMouse` console variable, written by the launcher and honoured once a
  frame. For running two clients side by side on one machine — how multiplayer
  gets tested without a second PC.
- V-sync checkbox (dgVoodoo `ForceVerticalSync`), which existed all along and
  was exposed nowhere.

**Fixed**
- Capture mouse released nothing, and could not have: the checkbox wrote a
  dgVoodoo setting governing dgVoodoo's own clipping, while the engine calls
  `ClipCursor` for itself whenever it believes it is active.
- FreshSrv flicker — the dialog erased itself with the system grey before our
  colour was painted over the top, once per repaint, and it repaints on a
  timer.
- The white window borders were `WS_EX_CLIENTEDGE`: a bevel that is white on
  the bottom and right, designed against light grey.
- The console font's colour fringing was ClearType, which is tuned for
  dark-on-light. The font is greyscale-antialiased now.

## 0.8.17 — 2026-07-31

**Changed**
- Nothing calls the engine's `CPrint` any more. `Shared/FreshPrint.h` wraps
  it — `FreshPrint(fmt, ...)` for literals, `FreshPrintText(data)` for
  anything else — and 50 call sites became 2 functions. `CPrint` is variadic,
  so passing it a typed-in line compiles exactly as well as passing it a
  format string, and the difference between those is a remote crash. There is
  now one function that touches the variadic call, and the percent-stripping
  lives in it.
- preflight gained the strong rule: nothing calls `->CPrint` outside
  `FreshPrint.cpp`. A grep with no false positives.

## 0.8.16 — 2026-07-31

**Fixed**
- The client had the same hole in a simpler form. `CSPrint` ended by handing
  the finished chat line back to a variadic function as its *format* string,
  so every client that could see the message crashed. Worse than the server
  version, which took down one process. Four more call sites had the shape
  without being player-reachable.

**Added**
- preflight fails any print call whose format argument is a variable. This
  class was fixed three times and found again twice, because every fix was to
  the site rather than to the class. Verified by reintroducing the bug.

## 0.8.15 — 2026-07-31

**Fixed**
- The format-string crash, properly. The 0.8.2 fix corrected our own `printf`
  usage and was not enough: the engine formats the result a *second* time
  of its own accord, so `%s%s%s%s` in chat still crashed the dedicated
  server. Untrusted text now bypasses `CPrint` entirely, and player names have
  `%` stripped at join.

**Changed**
- Runbook S1 marked for a full re-run, with a note that the earlier "never
  validly tested" claim was itself wrong.
- Engine fact 19 recorded in `CLAUDE.md`.

## 0.8.14 — 2026-07-30

**Added**
- Admin spectator: `Rcon "Spectate on"` / `"Spectate <id>"` / `"Spectate off"`.
  Invisible, off the scoreboard, follows another player. Does not count as a
  player in the human count, votekick quorum, idle check, frag limit or match
  record. Gated on the rcon password.
- ShogoSrv runbook session S7 covering it.

## 0.8.13 — 2026-07-30

**Added**
- Evidence snapshots. `actions.jsonl` gets one record every time the server
  acts on somebody: what fired, who, the thresholds in force, and the last
  twenty lines of context. Covers bans, ban enforcement, allowlist refusals,
  idle kicks and votekicks including failed ones.

## 0.8.12 — 2026-07-30

**Changed**
- `FireRateCheck` and `FirePosCheck` now measure and report even when off, so
  they can be calibrated before being armed. Off means measure-and-allow; on
  means measure-and-refuse, through the same code path.

**Removed**
- `FireRateExecute`, which killed the player. Replaced by a ladder: refuse the
  shot, warn at ten violations, and `FireRateBan <n>` for a short ban (0 by
  default). A config still setting it is told it does nothing.

## 0.8.11 — 2026-07-30

**Added**
- Idle handling: warned at 3 minutes, disconnected at 5, and **only when the
  server is at 75% of `MaxPlayers` or above**. `IdleKick 0` disables.
- Reconnect throttle: 5 seconds after leaving, 30 after a kick or votekick.
- `Docs/public/SERVER-GUIDE.md` — the operator guide.

## 0.8.10 — 2026-07-30

**Added**
- Allowlist. `AllowList 1` admits only known installs. `Allow <id>`,
  `AllowAdd <token>`, `AllowRemove <n>`, `Allowed 1`. Off by default; the
  local client is always admitted, and an empty list is announced loudly.

**Changed**
- Token hashing moved to its own module, now that two lists use it.

## 0.8.9 — 2026-07-30

**Added**
- Chat rate limiting: 6-line token bucket refilling at 0.5/s, 160-character
  cap on relay, same line three times in a row dropped. Excess is dropped
  silently.
- Server-side name sanitisation at join: control characters and non-ASCII
  stripped, spaces collapsed, length capped, wordlist hit renamed to
  `PilotNNN`. The player is told.

**Changed**
- The profanity wordlist moved to `Shared/` so both sides use one copy.

## 0.8.8 — 2026-07-30

**Added**
- Votekick, driven from chat so it works from a stock client: `!votekick <id>`,
  `!vk`, `!yes`, `!no`, `!who`. 30 seconds, 3-human quorum, 120s caller
  cooldown, 300s immunity for surviving one, 15-minute ban on success. Every
  vote logged with its tally. `VoteKick 0` disables.

## 0.8.7 — 2026-07-30

**Added**
- Per-install client token, so a ban can outlive a rename. Random, not derived
  from hardware; stored in `client-id.txt`; `FreshClientId 0` opts out. Servers
  store only a salted hash, so ban files cannot be compared across servers.

**Fixed**
- The prefs handler read the ruleset byte only in single player, so in
  multiplayer every field after it was read from the wrong offset.

## 0.8.6 — 2026-07-30

**Added**
- Ban list. `Ban <id> [minutes] [reason]`, `Unban <n>`, `Bans 1`. Keys on the
  player name and, from 0.8.7, the install token. Persisted and hand-editable.
- Match records: one JSON line per finished match in `matches.jsonl` — map,
  duration, ruleset, scoreboard with bots tagged, and each player's client
  version.

**Fixed**
- `SyncCampaignRuleset` ran every update as well as at world start, so the
  ruleset the client sent was overwritten on the next frame. 0.8.3's Classic
  fix had never once worked.

## 0.8.5 — 2026-07-30

**Fixed**
- Classic magazines restored. 0.8.3 removed them on the reasoning that 1998's
  clip table was inert; it was not, and every weapon fired forever. Magazines
  exist under both rulesets; the reload key, deadline, sound and clip readout
  are what FRESH adds on top.

**Added**
- Dark theme for the dedicated server window, matching the launcher.
- Console timestamps and command history (up/down).
- File logging: one log per day in `%APPDATA%\ShogoFRESH\Logs`.

**Changed**
- The server's version resource is derived from `FreshVersion.h` rather than
  hand-typed; it had claimed 1.0.0.1 since 1998.
- Patch numbers may exceed 9 — 0.8.10 is a legal version.

## 0.8.4 — 2026-07-30

**Changed**
- The dedicated server is now `FreshSrv.exe` and installs *beside* the stock
  `ShogoSrv.exe` rather than over it. Still reads `ShogoSrv.cfg`. Upgrading
  restores your original.
- Window title reads "ShogoFRESH Server <version>".

**Added**
- Launcher and server icons.
- `prepare-redist` sweeps stale files from the payload directory.

## 0.8.3 — 2026-07-30

**Fixed**
- Realistic gore never reached the gib system — shock troopers still bled and
  landed wetly.
- The in-game gore menu toggled a three-state setting as a boolean, so `Full`
  became `Off` the first time it was touched.
- Assault rifle burst rewritten as a committed burst: exactly three rounds per
  click, no spam-through, and zoomed semi-auto works.
- In-game rcon was written and never called from anywhere.
- Classic ruleset and debug channels now reach the server, which could not see
  the player's console variables at all.
- Campaign menu leads with New game when there is nothing saved.
- Save menu opens on the first numbered slot, not quick save.

**Added**
- Crash breadcrumbs for the dedicated server.
- Clean v0.8.3 runbooks.

## 0.8.2 — 2026-07-30

**Fixed**
- Remote denial of service: player chat used as a `printf` format string, in
  both the dedicated server and the client. *(Incomplete — see 0.8.15.)*
- Dialogue queue use-after-free — a freed heap pointer was sent in a message.
- `g_bMultiplayerRules` was never cleared, poisoning the campaign after any
  multiplayer session.
- Frag limit ended matches one short; bots were invisible to it.

## 0.8.1 — 2026-07-30

**Added**
- Gore modes: off / realistic / full.
- Infinite ammo modes: off / sidearms / all weapons.

## 0.8.0 — 2026-07-30

**Added**
- Crash reports with a minidump and a call stack, written to
  `%APPDATA%\ShogoFRESH\Logs`.
- Smart spawn selection — no repeat spawns, and not into someone's line of
  sight when a better point exists.

---

## Earlier releases

0.1.0 through 0.7.5 are listed in
[Docs/public/CHANGES-IN-DETAIL.md](Docs/public/CHANGES-IN-DETAIL.md), which covers the whole
history with the reasoning attached.
