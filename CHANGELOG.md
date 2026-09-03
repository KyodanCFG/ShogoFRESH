# ShogoFRESH — changelog

What shipped in each release, one entry per version. Entries from 0.9.40
onward are the release commits' own messages — this project's commit
messages carry the reasoning, so the changelog records not just what
changed but how each change was found, which was usually by playing.
Recent releases also carry short notes on the
[Releases](../../releases) page.

ShogoFRESH is built from Monolith's official Shogo v2.2 source release (March
1999). `Client.exe` is closed and unmodifiable, so everything here is the game
DLLs (`Object.lto`, `CShell.dll`, `CRes.dll`, `SRes.dll`), the dedicated
server (`FreshSrv.exe`), and the launcher.

---

## 0.10.96 — 2026-09-03

**FreshSrv learns to check in with shogoservers.com**

The community master site's registration protocol is spoken by its own
server package: /api/Sync/ServerCheckIn, PlayerCheckIn and
UnregisterServer, JSON with every value string-quoted. NOT the 1998
srv_send GET this project assumed earlier; reading the real thing
corrected the guess. Confirmed live with one labelled loopback test row:
appeared on the site, unregistered cleanly, HTTP 204 both ways.

ShogoServersSync.cpp is the sender: rides the existing WebRegUpdate
timer beside the legacy GET (srv_send.txt hosts untouched), stable GUID
identity persisted in %APPDATA%\ShogoFRESH\Logs\sync-id.txt so a
restart updates our row rather than littering new ones, name stripped of
quotes rather than escaped. OFF BY DEFAULT (SyncShogoServers 1 enables)
while making the master the default home is discussed with the site's
maintainer. PlayerCheckIn is known but not yet sent; server listing
first.

Matrix row 23 tests OUR sender; the protocol itself is already proven.

---

## 0.10.95 — 2026-08-30

**the blocklist binds drops and late arrivals, not just the level's furniture**

A banned red riot stood on a swept MCA map (owner, in play). Two holes,
both real, both closed:

THE DEATH DROP. SpawnWeapon spawns whatever the corpse carried, and an
NPC bot's loadout is authored in its AI attachments - not drawn from the
FreshBot pool, which is the only armory the blocklist filtered. So any
NPC carrying a banned weapon minted a banned pickup on death.
FreshWeaponIsBlocked (WeaponBlocklist.cpp, reads the live var the way the
bot pool does) now gates the drop.

THE LATE PUSH. The sweep runs once, in the first server update - and the
ServerName saga proved a listen host's serv pushes can land AFTER that.
First world after launch: sweep against an empty list, blocklist
silently void until the next map. Update() now remembers what the sweep
ran against and re-runs the blocklist passes when the value differs -
blocklists only, never the shuffle, so a mid-match change cannot
reshuffle the map. Free consequence: rcon 'BlockWeapons ...' now takes
effect mid-match. Unblocking mid-level stays next-map - nothing
remembers a swept spot's original.

Matrix row 22 carries the three-part play test.

---

## 0.10.94 — 2026-08-30

**the pickup lights agree among themselves**

The inconsistency the owner kept seeing was never a fault in any one
light: every pickup created its own OT_LIGHT unconditionally, against the
engine's TEN dynamic lights for the entire world, so which pickups glowed
depended on what else happened to be lit. The budget note in CLAUDE.md
even warned about one-light-per-thing designs; this one predated the
warning.

An arbiter in PickupItemFX now ranks every live pickup FX twice a second
- intrusive list, nearest-N insertion, distance to the player's object -
and the nearest FOUR visible pickups hold a light while everyone else
lets go. Four so the pickups never take more than half the world's
budget. A taken pickup waiting to respawn is invisible and now holds
neither a slot nor a glow, which the old code also got wrong (the light
outlived the take). Deadline reset guard for the engine clock restarting
on world change.

Matrix rows 20-21; the ten-lights paragraph in CLAUDE.md carries the
worked example.

---

## 0.10.93 — 2026-08-30

**the bullgut gets its fourth missile back**

Stock fires a barrage of FOUR per magazine (the 1998 clip table says so)
and the FRESH table's 3 was a transcription mistake that survived because
nobody counted. Owner counted, comparing MCA classic against FRESH in
play. Pickup 8 and carry 16 follow the table's own doctrine - two
magazines per pickup, four carried - so the preflight ammo invariant
holds untouched. The single-player carry floor stays 60 (it was set by
the 40-round box, not the magazine) and the SP box clamp derives from the
clip, so both follow automatically.

---

## 0.10.92 — 2026-08-30

**classic grenades throw like 1998, and the sticky mine gives its name back**

The projectile CLASS already reverted under classic (RiotWeapons.cpp,
0.10.6x) but the FLIGHT did not follow it: FreshTunedVelocity,
FreshTunedLaunchAngle and FreshTunedLaunchDrop handed out the FRESH throw -
1400 units/sec, the 14-degree lob, the hip drop - regardless of ruleset,
so the classic energy grenade flew a FRESH arc out of a 1998 class. Owner
spotted the lob after the 0.10.91 classic pass. All four tuned-launch
helpers (fuse included) now return the table under classic, dials ignored:
classic is 1998, the artwork and the stock tables decide. The kato's
DoBounce gets the same fork - the 1998 slide-and-settle (stock's
add-stop-velocity, sound per touch) under classic, the 0.32 reflect under
FRESH.

The name follows the weapon: under classic the energy grenade is 'Energy
Grenades' again - the 1998 resource text, plural and all - via the same
UseFreshRules fork the DMR/Sniper Rifle name already uses
(WeaponStringDefs.h). One new string id, loc JSON regenerated.

Also answers a question from the same session: yes, the 1998 assault
rifle really carried a 50-round magazine (root-commit table).

---

## 0.10.91 — 2026-08-30

**classic weapons get 1998 back: the artwork decides, and fact 20 stood on its head**

Found on a stock server (owner, 2026-08-30): no magazine weapon ever
reloaded, and the shotgun and kato fired at the raw speed of their
truncated animations. The classic doctrine said 1998 had no reload
mechanic and made classic clipless; the stock source at the root commit
says otherwise - CWeaponModel decremented the clip per shot, went
W_RELOADING at empty, and held the state for the length of the reload
ANIMATION. The clip-1 weapons' per-shot pause IS their 1998 cadence: the
pump and the re-arm are reload animations. The owner's memory of 1998
outranked a confident doctrine note, and the root commit settled it.

Classic now exits W_RELOADING and W_SELECT when their animations end,
never on a window, and neither the Fire/Equip dials nor the FRESH cadence
tables apply - four UseFreshRules() forks in WeaponModel.cpp, client side
only. The server was already permissive under classic (GetReloadTime 0
means IsReloading never holds; IsEquipping has no consumers), which is
itself faithful: the stock server never refereed the player's weapon
state either (engine fact 1).

Also: the OVERLAY WORKS smoke file deleted from the live install at the
owner's ask (matrix row 1 closed out), fact 20 rewritten in CLAUDE.md
with the corrected story, and matrix row 18 added - if the classic feel
still reads wrong in play, it is the doctrine that reopens, not just the
code.

---

## 0.10.90 — 2026-08-30

**the master list vouches for servers the probe cannot see**

The dead-join gate from 0.10.86 had a false premise: joinable implies
query-answering. The flagship community servers disprove it permanently -
they forward only the game port, so the join (TCP) works while every
GameSpy query (UDP) times out, and shogoservers.com knows they are alive
because the server PUSHES heartbeats, not because anyone queries it. All of
this was measured and written into Launcher/README.md on 2026-07-26, with
the fix prescribed ("master-site data as primary, UDP query as
enrichment"); the gate was then built a month later doing the opposite,
and blocked a server its owner had joined three times the same evening.

So the gate learns two grades of evidence. A row with Source == Master was
on the site during THIS refresh - a heartbeat minutes old, better liveness
evidence than our probe can collect against a firewalled server - and joins
straight through a dead probe. Everything unvouched (manual, favorite,
seed, stale cache) gets a soft gate: first press warns, a second press on
the same server inside ten seconds overrides, and the ten-second engine
freeze it risks is the informed player's to spend. Same doctrine as the
kill feed's vouch byte: absence of an answer is never treated as an answer.

The README's July question is marked settled with the in-play evidence.

---

## 0.10.89 — 2026-08-30

**the clear button loses its chrome, and the kit is ShogoMAKE everywhere**

THE SEARCH FIELD'S X was rendering as a grey slab the height of the row: the
app theme restyles Button with its own template, and Background=Transparent
on the button element lost to it. The button now carries a minimal inline
template - a transparent grid and the glyph - so there is no chrome left to
win. Reported with a screenshot; the slab was unmissable once seen.

THE KIT'S NAME settled as ShogoMAKE, and every reference here follows: the
public README links github.com/KyodanCFG/ShogoMAKE, and the docs index, the
sync script and preflight stop saying Creative Kit. As with FRESH, the
capitals stand for something, and what they stand for is written down
nowhere - which is the point.

The kit itself moved further today, over in its own lanes: the manuals now
live under docs/ in the zip AND browsable in the repository, the ModDB Shogo
Tools package is linked beside the Processor.exe requirement, and the v1.0
release was re-cut with the new layout.

Also severed today, recorded here because the lesson is a release-mechanics
one: the FIRST v0.10.85 GitHub release was created before the public repo's
history squash, so its TAG kept the entire pre-squash history reachable -
the Claude co-author trailers (which is how a "claude" contributor appeared
in the sidebar), the needle-bearing leakcheck, all of it. A force-push
replaces branches; it does not touch tags. The release was deleted, the tag
deleted, and the release re-created against the clean history from the
archived clean zip. THE RULE: after any history rewrite, audit every TAG,
because each one is a root that keeps the old world alive.

---

## 0.10.88 — 2026-08-30

**classic until the server says otherwise, and the search box finds its row**

CLASSIC-ON-STOCK, the owner's call closing the question 0.10.87 left open. A
ShogoFRESH client on a stock server used to play with FRESH client-side
weapon behaviour - magazine sizes, reload pauses, per-weapon feel - house
rules the server neither knows nor enforces, so the player reloaded against
opponents who never had to.

The mechanism is the one already in the building. A FRESH server sends
MID_SERVER_RULES at world entry; a stock server cannot. So multiplayer now
DEFAULTS the client ruleset to CLASSIC, and the rules handler - whose arrival
is itself the proof of a FRESH server - latches a seen-flag and sets the real
ruleset. The default only ever stands on servers that never spoke, which is
exactly the set running 1998 rules. Same shape as the kill feed's vouch byte
one release ago: absence of a claim is never treated as a claim.

The seen-flag resets on every world entry, so a rotation moving between a
FRESH and a stock host proves each server afresh, and the ruleset default is
guarded on the flag so a late call from the update loop cannot clobber a
verdict already delivered.

ClassicCampaign is untouched: single player keeps its own switch, and this
is deliberately its multiplayer twin rather than a new idea.

THE MAP SEARCH moved under the Available list, on the same margin as the
Order row opposite so the two land level across the group, with the
Refresh/Open-folder row dropping below it. It gains a clear button INSIDE
the field's right edge - the text box pads right so typing never runs under
the X.

---

## 0.10.87 — 2026-08-30

**a stock server's kills stop claiming the pulse rifle**

Reported from play on a stock community server: the kill feed named the pulse
rifle for every kill, whatever anyone fired.

It is the C8 collision arriving over the wire. FRESH appends the killer's
weapon to MID_PLAYER_FRAGGED; a stock server appends nothing; the engine's
past-the-end promise returns 0 for an integer read; and 0 is
GUN_PULSERIFLE_ID. "Absent" and "pulse rifle" were the same byte - the exact
shape that made every map-placed pickup a pulse rifle in C8, this time in a
message instead of a property.

THE FIX IS A VOUCH BYTE, not a re-encoding. The server appends 1 after the
weapon; the client believes the weapon only when that byte reads 1, and
otherwise renders "<killer> killed <victim>" with no icon - a new
GUN_UNKNOWN sentinel that cannot fall through to the environment shape,
which has no killer slot and would have shown every stock-server kill as a
quiet death. Attribution survives every server; only the icon needs a FRESH
one. Re-encoding the weapon as id+1 was considered and rejected: an old
FRESH server against a new client would then misattribute every kill by one
weapon, and degrading to WRONG is the one direction a mixed fleet cannot
afford. With the vouch byte an old FRESH server degrades exactly like a
stock one - icons lost, nothing invented.

THE FACT-9 COROLLARY, now written down where appended fields are designed:
the zero-read promise is only safe when zero is not a meaning. If zero is a
valid value of the field, append a validity byte after it. This is the
second zero-as-id-versus-zero-as-absent collision; there should not be a
third.

decisions.md records the larger finding the same session produced: a FRESH
client on a stock server WORKS - HUD, client-side weapon feel, all of it -
and the owner has set mixed-fleet compatibility as a requirement in both
directions. The doctrine (client keeps feel, server keeps authority,
degradation is always to less information rather than wrong information)
and the three risks that now need stock-server validation are written there.

---

## 0.10.86 — 2026-08-30

**the browser finds your own machine, refuses a dead join, and the server window grows**

Four owner requests from one testing session, all about the seams between
the launcher, the server window and the game.

THE FROZEN BLACK SCREEN ON A DEAD JOIN, diagnosed before fixed. Double-click
a server that is not up and the game launched into a black window that hung.
The mechanism is stock and documented: the engine's JoinSession BLOCKS the
whole client for up to ten seconds when nothing answers (300ms resends, 10s
give-up), and one-click join fires it on the FIRST menu frame - before
anything has drawn. The fix is where the knowledge is: the launcher already
speaks the query protocol the browser is built from, so JoinSelected now
probes the target first and refuses with a sentence when nothing answers. A
dead server is a status-bar line, not a spent game boot. The residual case -
a server dying between probe and connect - keeps the stock behaviour, which
CShell cannot reach.

THIS MACHINE IS ALWAYS PROBED. A hosted FreshSrv (27888) or listen server
(27889) on the same box never appears on any master or peer list from in
here, so the most common test setup - launcher and server side by side -
showed an empty browser. Both loopback ports are now probed on every
refresh and shown ONLY when something answers: a permanently dead 127.0.0.1
row would read as the browser being broken, while a real remote server's
dead row stays because a known server being down is information. The
synthetic pair is not a known server; it is a question asked each refresh.

THE AVAILABLE MAP LIST GAINS A FILTER - a view filter, not a rebuild, so
Refresh keeps its +N arithmetic against the full set and clearing the box
costs nothing. The rotation list deliberately does not get one: it is a
curated dozen the operator just arranged; the available list is every map on
disk plus every level in every rez, and it is the one that outgrows finding
by eye.

FRESHSRV RESIZES VERTICALLY, and the console log is the element that grows.
WS_THICKFRAME replaces DS_MODALFRAME; OnGetMinMaxInfo pins the width (the
layout has no horizontal story) and floors the height at the designed size;
OnSize lays out from baseline rects captured once at init - never from the
previous size, so repeated resizes cannot accumulate drift. The
classification is geometric rather than a control list: whatever sits below
the log slides, whatever contains it stretches, everything above stays. A
control added later lands in the right bucket without anyone remembering
the function exists.

AND FOCUS STARTS IN THE COMMAND LINE, not the log. The log is first in tab
order, so the dialog handed a read-only edit the caret - inviting typing
that went nowhere while the box built for typing sat one stop away.

---

## 0.10.85 — 2026-08-30

**the release stops carrying its author's machine and mailbox**

A pre-publish sweep before the first GitHub push found identity leaks at
every layer, including two inside compiled binaries that no repository scan
could see:

- FRESH_CREDIT_LINE compiled a personal email address into all four game
  DLLs and the rez. The credit now names KyodanCFG and points at the public
  repository. The attribution header's own comment records the trade: the
  SDK EULA asks for creators AND email addresses, and a repo link reaches
  the author the same way while giving the reader the project - if the
  letter of that clause ever matters more than its intent, a public email
  goes back in.
- FreshCrash's footer asked crash reporters to EMAIL the dump to a
  personal-domain address. It now points at the repository's issues page,
  which is where a crash report is useful anyway.
- The launcher DLL and its .pdb embedded the full build path -
  C:\Users\<name>\... - via the debug-path record, and the pdb itself
  shipped in every zip. DebugType=None on the publish: no symbols, no path,
  and nothing game-side ever used the managed pdb (FreshCrash is C++).
- PackageReadme carried the PRIVATE home git server's URL twice and the
  personal email once. Now the GitHub URL - and the file joins
  sync-public.ps1's list, because it was seeded into the public repo once
  and never synced, so a private fix could never have reached it.

The public repo's leakcheck learned the four exact strings as BLOCKING
patterns, plus the note that commit authorship cannot be caught by any file
scan - four of five public commits carried a personal gmail as author, found
only by reading git log by hand. All six commits there are rewritten to the
KyodanCFG noreply identity and the branch is main.

The zip this builds is the one to attach as the first GitHub release.

---

## 0.10.84 — 2026-08-30

**the listen server's name arrives through the road that exists**

Ships the ServerName CVarTrack fix (48e01f3, reviewed): the 0.10.83 serv push
was writing into a console space where raw GetGameConVar had never seen a
ServerName, because nothing had created one. CVarTrack::Init is the pattern
every working serv tunable already uses - create the variable, then let serv
write it - and the review confirmed the empty-string default takes the string
path in Init and the "Shogo Server" fallback survives for a genuinely empty
name. Dedicated is unchanged; its LoadConfigFile value is found by the same
track.

Owner retest is consolidated-list item 14: host a listen game from the
launcher, the match info should show the launcher's name within five seconds.

Also carries the accumulated docs backlog that was riding unpushed: the
consolidated validation list itself, the Squad Deathmatch / Shrinkage /
Headcount spec work, and - updated in this commit - item 3 of the list closed
as FAILED: the owner's close look at OF_MeccaF found sharp seams at the lamp
shadow's edges, so the relight recipe is not canonical and the anchor sweep is
scheduled in the RE lane with seamcheck as the judge. Struck from the walk so
tomorrow does not redo a comparison whose verdict is already recorded.

RELEASE MECHANICS NOTE: this build is the one the stale-Dist trap was waiting
for. Dist\ still held 0.10.82 binaries while the 0.10.83 zip was correct, so
a package run without a full build first would have shipped 0.10.82 code
under a 0.10.84 label - and readback would have passed, because it compares
the zip against Dist and they would have agreed. All five projects were
rebuilt before packaging, which is the only defence that works.

---

## 0.10.82 — 2026-08-30

**the string overlay merges every file, and a listen server says its name**

Ships four commits from other threads that had been sitting unpushed, plus
today's B8 update. Nothing of mine in the behaviour.

EVERY Strings\*.txt MERGES (984e121). A single shared filename was a
landmine: two mods, or a mod and a map, both shipping override.txt meant
whichever mounted last silently won and the other's strings vanished with no
error. Merging every file in the directory makes shipping strings a thing two
authors can do independently, which is the difference between a feature and a
trap.

A LISTEN SERVER SAYS ITS OWN NAME in match info (cbe1799). SendMatchInfo
reads ServerName out of the server console, which a listen host never
populated - so the browser showed the chosen name and the game showed "Shogo
Server". Two places disagreeing about what a server is called, one of them
wrong.

Plus the string-id namespacing decision, parked with its revisit trigger
stated (6779ad5), and the holstered-weapon-after-the-menu report logged in
BUGS (27a9cca).

B8 UPDATED, and the wording is the point. The mapping thread reported every
TOW on ZZ_TunnelTest detonating and recommended retiring the rig. Verified
before accepting: three days of logs carry ZERO GONE, left-the-world or
OUTSIDE-the-world lines. Recorded as NOT REPRODUCED rather than FIXED and
deliberately left open - the original was rare but persistent, so silence is
weak evidence by itself.

What makes it safe to stop hunting is not the quiet. It is that 0.10.57
changed what a recurrence LOOKS like: a projectile leaving the world now
prints its touch count and traces back to the muzzle to say whether it came
through solid geometry or a gap. The next one names the mechanism the first
one refused to. The rig can go; the entry stays; and its phase-pad protocol is
preserved in BUGS.md because the stochastic reasoning behind it outlives the
rig and the next thickness question will want it.

LOCALISATION.md's "Since 0.10.79" line was checked and is correct - the
overlay itself shipped in 0.10.79 and only its merge behaviour lands here.

---

## 0.10.83 — 2026-08-30

**the reconciled debris release: two 0.10.82s cannot share a name**

---

## 0.10.81 — 2026-08-29

**the newer-build guard was blanket; a licence file got a veto over the game code**

0.10.79's re-staging guard misfired on its first release, in two ways, and
both were reported within the hour.

RECOMMENDED DEFAULTS could not be applied at all. The game directory's
autoexec.cfg is rewritten by the ENGINE on exit and by the launcher on save,
so it is permanently newer than the shipped copy and permanently different -
the guard fired every single time and Apply threw.

THE SHOGOFRESH CARD REFUSED TO OFFER AN UPDATE, reported as "0.10.77
installed and it is not detecting 0.10.80 as a version, let alone a newer
one". Measured against the owner's actual install: the rez and all five DLLs
were older than the payload as expected, and ONE file was newer and different
- FiraCode-OFL.txt, a licence text. That was enough to flip the whole card to
NewerInstalled, which hides the version arrow and removes the Apply button. A
licence file had a veto over the game code.

ONE CAUSE. The guard assumed every payload file is one WE own and nothing
else touches. True of the rez and the DLLs; false of anything merged,
generated or incidental. And because a single tripped file disables the entire
card, the least important file in a payload outranked the most important.

So the guard is opt-in per file now. Six files carry it - the rez, the four
DLLs and FreshSrv.exe - which is exactly the set a hand-delivered test build
replaces. Nothing else can block anything.

The general lesson, which is why this is written at length rather than
labelled a typo: A GUARD THAT BLOCKS SHOULD BE EXPLICIT ABOUT WHAT IT BLOCKS.
Applying it to "everything in the payload" was the lazy generalisation, it
read as safer, and it was strictly worse - it broke two working features to
protect six files, and it did so on the release that shipped it.

Also worth recording: 0.10.79 was verified before shipping, against three
synthetic cases, and all three passed. What the synthetic cases could not see
is that real payloads contain files nobody has thought about. Verified this
time against the OWNER'S ACTUAL INSTALL - the same file list, the same
timestamps, the same comparison the launcher makes - plus a case proving a
genuinely hand-swapped rez still trips.

---

## 0.10.80 — 2026-08-29

**colour and mech are rolled on a fresh install, so a server is not eight blue Ordogs**

A stock install puts every player in a blue Ordog. An unconfigured server is
therefore eight identical machines in eight identical liveries, and nobody can
tell who they are shooting at.

THE COLOUR IS THE ONE THAT MATTERS, and the reason is that nothing had to be
built. Multiplayer spawns already set FLAG_MODELTINT (PlayerObj), so the
engine has been tinting player models by NetPlayerColor since 1998 - the
uniform has been there the whole time and only the DEFAULT never varied.
Rolling it turns a feature that already shipped into one you can see.

Same shape as the pilot name generator directly above it, which has been
rolling names for the same reason.

ABSENT MEANS FRESH, and the default VALUE is deliberately not treated as a
choice. Blue is 5 and Ordog is 1, so once written, "unset" and "deliberately
picked the default" are indistinguishable - which is the trap the name
generator sidesteps by testing for absent OR "Sanjuro", a luxury a number does
not offer. So only an absent key rolls: somebody who picked blue keeps blue,
somebody who never opened the launcher gets a roll.

The cost is stated rather than hidden: a player who chose the defaults before
ever saving is re-rolled once. The alternative is re-rolling everyone who
genuinely likes blue on every launch, which is worse.

Written back immediately rather than waiting for Save, for the same reason the
name is - the fields have to read correctly the moment the launcher opens, or
nobody thinks to press Save and the game reads a value that is not there.

Lengths come from the arrays rather than the literals 8 and 4 that were there
before, so adding a colour or a mech does not silently leave the new entry
unreachable by the roll.

ON-FOOT MODEL IS NOT ROLLED. OnFootModels currently has one entry, so a roll
would be a no-op dressed up as a feature. It joins the moment there is a
second body - the code is the same three lines.

---

## 0.10.79 — 2026-08-29

**the launcher refuses to revert a build newer than its own**

A hand-delivered test build looks EXACTLY like an out-of-date install. The
game-dir file differs from the payload, the Setup card says "Update
available", and applying it reverts the build under test. The tester never
finds out - they gather a round of results from code that is not the code they
think they are testing, and the result has to be thrown away later, if anyone
ever works out which rounds were affected.

It has cost THREE playtest rounds: two on 2026-08-26 during the dims-trim
work, recorded in SCALE.md where it silently undid a build between rounds two
and three, and at least one this week. The current workaround is a
prerequisite line at the top of every validation list telling the owner to
extract the zip first. That should not have to exist.

MODIFICATION TIME IS THE DISCRIMINATOR, and choosing it over version is the
part worth reading. A version check is cleaner and cannot see the case that
actually happens: the surgical rez swap - extract, replace the DLLs from
Dist, repack - leaves the manifest's version untouched, so the install still
claims to be the release it started as. Its mtime does not lie. File.Copy
preserves mtime, so a payload file carries the timestamp of the release build
while a hand-placed file carries the moment it was placed. That asymmetry is
already recorded as the tell for spotting a reversion after the fact; this
just uses it to prevent one.

REFUSAL, NOT A WARNING, and the UI refuses BEFORE the click rather than the
service refusing after it. GetStatus now distinguishes "different because
older" (UpdateAvailable, as before) from "different because newer"
(NewerInstalled), and NewerInstalled is deliberately absent from CanApply - so
the button is not there to be pressed. A dialog is something you dismiss
mid-playtest; that is the SCALE.md history, and it is why shogo-0c asked for
refusal over a loud line when I offered both. Apply throws as well, because a
refusal that lives only in the UI is one code path away from not existing.

NewerInstalled is also NOT setup-needed. Someone running a test build has a
MORE current install than the launcher, not a broken one, and nagging them to
finish setup for the length of a playtest is how a warning becomes something
you learn to ignore.

One second of grace on the comparison: a file copied from the payload can land
a tick after its source, and two files from one build step can straddle a
second boundary. Anything genuinely newer clears it by minutes.

Verified by exercising the three cases the detector has to separate rather
than by reasoning about them - identical copy (same bytes, not newer),
hand-placed test build (differs AND newer), and a genuinely old install
(differs, older). Only the middle one trips.

ALSO IN THIS RELEASE, from shogo-e4 via shogo-0c: the string overlay
(d387520). Eight TextHelper id entry points routed through FreshFormatString,
% stripped at load per fact 19, 480-char cap, reload on world entry, ids
50000+, and a preflight check that was break-verified. LOCALISATION.md already
named 0.10.79 as the release carrying it, which is why this is that number.

---

## 0.10.78 — 2026-08-29

**the box bottom is an invariant, not a favour two of three callers do**

B9, and the entry's own diagnosis was one caller short in a way worth
recording: the one-shot ground adjustment DID exist at the animation-change
transition - but a third path sets dims every frame (AreDimsCorrect ->
ResetDims with no offset, symmetric by construction), and the merge's
visual-scale read races exactly on the transition frame, so crouch dims
routinely settled through the symmetric path. Boots lifted on crouch, sank
on stand, and moving hid it because movement re-grounds every frame -
symptom 3 named the layer precisely.

The fix makes the invariant structural rather than per-caller: ResetDims
itself preserves the box bottom from the LIVE dims, both directions,
whichever caller reaches it - the deliberate transition and the racing
convergence now do the same thing because they are the same code. The
animation-change caller's own offset block is deleted, not moved. Server
side, CBaseCharacter::SetDims mirrors the GROW direction stock never
compensated (it only moved down on shrink and let SETDIMS_PUSHOBJECTS
shove the grown box out of the floor - symptom 2 was stock's own
asymmetry, observable for the first time now that a box rests on its
boots). MoveObject, not SetObjectPos, so growing under a low ceiling
stops instead of jamming through.

This release also carries the two validation-pass changes that had only
been deployed in place: Prop's Animation property (4e7e8dc) and the
multiplayer-music announce line (9b49311).

Unverified in play; unblocks the crawl-height row B9 was blocking
(SCALE.md 7 gauge, crouched).

---

## 0.10.77 — 2026-08-26

**two sessions solved the squishie in parallel; the merge composes them**

Reconciliation merge of main (0.10.73-0.10.76, the SquishHull thread)
into the calibration branch (SCALE.md, the measured constants, ShowDims
repaired, the dims trim). Each side had half the truth: main stopped the
walls outside the near plane (hull dims at 1.5x the visual, dialable)
and scaled the crouch camera and viewmodel, but at hull scale the
untrimmed box floats the model ~7 units; this branch put the box bottom
at the boots and measured why, but had priced the wall fix as too costly
- not knowing main had made it a dial.

Composed axis by axis: x/z dims carry the HULL (players only - bots
have no camera, so no hull, stated hitbox asymmetry); y dims carry the
feet-depth trim at the VISUAL scale, via a new m_fSquishVisualScale on
the server (m_fDimsScale now holds the hull) and the replicated object
render scale on the client (the wire dims scale is the hull). Both
crouch fixes stack, and the smaller hull-scaled duck drop moves the
crouched eye UP - the safe direction against the measured 8-unit near
plane. The bot constructor arms the trim beside its size, so the first
box is right.

Version: this branch 0.10.72 collided with main's already-shipped
0.10.72; that zip is discarded and the merge ships as 0.10.77. All five
projects build, preflight clean including check_squish_trim_gap. One
playtest wanted on the combined squishie - the list is in decisions.md.

---

## 0.10.72 — 2026-08-26

**the squishie measured, and everything the measuring found**

The calibration release. One day took the squishie from "calibrated by
feel against 1998 maps that disagree with each other by 3.5x" to a body
whose every number is measured on purpose-built instruments:

- Docs/SCALE.md, the canonical scale document: 40 units/m on foot, 8 on
  MCA maps, the /5 proven three ways from Monolith's own data, and a
  measured-constants table where open questions used to be.
- ZZ_ScaleBlock and MP_SquishTemplate (shogo-re): the calibrated test
  block and the dual-scale mapper starter, iterated through five
  playtest rounds that caught two instrument bugs via their own
  orientation indicators.
- ShowDims repaired after 28 years dead: its only caller was the base
  Update both player and AI replace - engine fact 1 shape. Now called,
  announcing, and tracking live dims.
- Constants: near plane 8, viewmodel reach 12, walk-up step 2, door
  clearance width box+2 / height box+5..8, jump and run speed unscaled.
- The dims trim (Kyodan's call): squishie dims.y = scale x feet depth,
  42 standing / 30 crouched, both sides, preflight-guarded invariant.
  Confirmed in play: boots down in both poses, eye 15, crouch box ~12 -
  crouch shrinks more than stock ever managed.

HANDOFF.md carries the state; decisions.md the calls; memory the
launcher-restages-the-rez delivery trap that cost two rounds.

---

## 0.10.76 — 2026-08-26

**bots are born small, and the rules are chosen once per tick**

Two of the banked cosmetics:

- The shrink pop. The squishie sizing lived in the multiplayer kit, which
  waits for the bot's first Update - so every squishie bot stood at full
  size for the opening seconds and then visibly shrank, four playtests
  running. m_eModelSize is now set in the constructor (the spawn hints are
  consumed there, so the side is known), which lets CBaseCharacter's own
  creation path scale the model before it ever replicates. The kit's
  scaling stays as an idempotent re-apply.

- The doubled degrade line. The engine reaches PostStartWorld twice on
  some map-change paths, and every line of SelectLevelRules printed twice
  in the same second. Same tick = same convar and same start points = the
  second pass chooses identically, so it now returns instead.

MOTD-on-reconnect stays as observed behaviour for now (draws on first
connect and server open) - display-side timing, recorded, not chased.

---

## 0.10.75 — 2026-08-26

**the first-person weapon wears the player's size too**

The crouch fix left the gun behind: the camera now stays a squishie
height above the floor, but the viewmodel hung a full-size arm's length
below and ahead of it - so every squishie crouch put the weapon through
the floor. Same class as the duck constant and the camera offset before
it: a 1998 number that assumed a full-size body.

The whole hand now wears the client's own dims scale - offsets, bob,
reload dip, muzzle-flash position, and the model itself via
SetObjectScale. Scaling the distances AND the model together keeps the
picture identical on screen (a fifth-size gun a fifth as close renders
the same pixels); what changes is that the weapon stays inside the
squishie's own space instead of reaching a full-size arm through the
world. Full-size players multiply by 1.0.

The wall HOM stays on the SquishHull ladder - better at 0.35, not gone;
the clearing threshold is the near-plane measurement that decides
whether the hull default moves or the RE dig into d3d.ren begins.

---

## 0.10.74 — 2026-08-26

**the crouch wears the player's own size**

'Crouching especially' was the tell: the crouch camera drop is a 1998
constant - 20 units at 75 units/sec - which is a third of a full-size eye
height and THREE TIMES a squishie's whole eye height. Crouching drove the
small eye 13 units below its own body and through the floor, which is most
of what the hull experiment could only lessen: the walls were the marginal
case, the crouch was the guaranteed one.

Client-side, in the code we own: UpdateDuck scales both the duck distance
and the duck speed by the client's own dims scale (CMoveMgr knows it from
the physics update; new GetOwnDimsScale accessor), so a crouch feels
identical at any size and the eye stays inside the body that owns it.
Full-size players see arithmetic multiplied by 1.0.

The wall half of the HOM stays on the SquishHull dial - lessened at the
default 0.30 hull; the threshold that clears it measures the renderer's
near plane, which is the number the RE route would need anyway.

---

## 0.10.73 — 2026-08-26

**the squishie's hull is wider than its body, because the wall must stop outside the eye**

The hall-of-mirrors experiment, reshaped by geometry before it was built:
the banked idea said pull the camera back from the hull, but the on-foot
camera offset is purely vertical - the eye sits at hull CENTRE, so there
is no direction to pull it. The near plane is a full-size assumption and
a fifth-scale hull is narrow enough for a wall to stand inside it; the
only lever is stopping the wall further out, which means the HULL, not
the camera.

So the collision scale decouples from the visual: the model (and eye)
draw at SquishScale, the dims arrays - client movement dims, the wire,
everything size-keyed - carry SquishHull, default 1.5x the visual (0.30
for the 0.20 squishie), never smaller than the body, never above 1.0.
'serv SquishHull 0.2' restores the coupled behaviour, which is the A/B.
The cost is stated in FreshTuning.h: shots can clip a hull slightly wider
than the body they see, and a squishie needs a slightly wider gap than it
looks like it needs. WeaponDebug now prints both numbers per spawn.

Whether 1.5x clears the near plane is exactly what the experiment
measures - the dial exists so the answer comes from play, not from
guessing the renderer's constant.

---

## 0.10.71 — 2026-08-25

**the eye height was mailed before it was measured**

Five playtests of 'I'm not as small as the other squishies' - and after
0.10.70 fixed the model scale, Kyodan's clarification landed the real
report: the MODEL was right and the FIRST-PERSON EYE was not. The bug is
pure ordering, visible in twenty lines: CPlayerMode::SetMode sets the
full-size on-foot camera offset and sends MID_PLAYER_MODECHANGE in the
same breath - and the squish path's ScaleCameraOffset runs AFTER SetMode
returns, scaling a number the client had already received and would never
be sent again. A squishie has looked out of full-size Sanjuro's eyes over
the head of his own correctly-scaled model since 0.9.68 - mode 2 shipped
with this and 'the camera offset scales with it' in CLAUDE.md described
the server's variable, not the player's experience.

The send is extracted to CPlayerMode::NotifyClient (with a guard the old
inline send lacked: a null client handle would have BROADCAST one
player's mode to the room); SetMode still calls it, the squish path calls
it again after scaling, and the one-second belt resend covers the same
enter-world race the physics state rides.

---

## 0.10.70 — 2026-08-25

**every acquisition route asks one predicate, and squishies get one number**

The 0.10.69 targeting filter was correct and irrelevant: it filtered
CCharacterMgr's list walk, and the path that actually hunts players is
BaseAI's SEPARATE players-only spotting loop (UpdateSenses ->
FindVisiblePlayer -> CheckAlignment) - which also explains in one stroke
why bots never engaged the bot mech: that path cannot see non-players at
all, so the only mech it ever found was the human's. Playtest four
reported all three symptoms of that one miss.

The filter is now ONE predicate - FreshTargetingBlocked, asked by both
CharacterMgr finders and by CheckAlignment itself, so the spotting path,
the list walk, and damage retaliation all get the same answer, and a
future acquisition route has to go out of its way to miss the rule.
Same-side war combatants read as LIKE regardless of character class;
deathmatch and the campaign are untouched for the usual reasons.

And the size complaint was real, measured at last: foot mode carries a
1.1 dims scale (PlayerMode.cpp), so a squishie player was 1.1 x 0.2 =
0.22 against the bots' flat 0.20 - taller than his own teammates by a
head, two playtests running. Squish now assigns the flat scale rather
than multiplying, which makes player and bot the same number by
construction - the contract the bot code always claimed ('a fifth scale,
exactly like an opted-in player').

---

## 0.10.69 — 2026-08-25

**version corrected around the bot-targeting fix**

fd5e18c calls itself 0.10.63 and is actually the first commit after the
0.10.68 balance run - this session's version bump was a no-op against
files other threads had already carried to 0.10.68, and its package
therefore OVERWROTE the v0.10.68 zip with different bytes, which is the
same-version-different-contents failure the packaging notes warn about.
This bump makes the targeting fix a release of its own; the v0.10.68 zip
should be considered replaced by v0.10.69.

---

## 0.10.63 — 2026-08-25

**an AI must not target what the mode will not let it damage**

The war's bots spent whole matches emptying magazines into teammates the
damage gate was refusing on every hit - the match log made it plain
(scorelines of 2, 0 and -1 over three and a half minutes while the human
scored 30). The fix is one filter in CCharacterMgr::FindAITargetInList,
asked with the SAME AllowsDamage hook the damage gate asks, so targeting
and damage can never disagree about who is on whose side. Deathmatch
answers true for every pair and is untouched; the campaign never enters
(multiplayer rules only); anonymous NPCs stay their own faction on both
sides of the question, exactly as at the gate; and the WarFriendlyFire
dial re-opens targeting for the same reason it re-opens damage.

This should also fix the bots' empty scorelines for free: a squishie bot
that stops wasting its magazine on teammates is a bot with time to hunt
the mech.

---

## 0.10.68 — 2026-08-25

**Open folder goes to the folder, even when Explorer is already open**

Reported: the button worked, but only when no Explorer window was already
open. If one was, it just activated that window - on whatever tab it happened
to be showing - and never navigated anywhere near maps\mp.

Windows 11's Explorer is tabbed, and asking the SHELL to "open" a directory
lets it satisfy the request by activating an existing window. That is the
worst kind of intermittent: it looks like the button is flaky rather than like
the shell being clever, and it works perfectly on the machine of anyone who
happens to close their file windows.

Now launches explorer.exe with "/n," and the path, which forces a new
single-pane window AT that path rather than asking the shell to interpret an
intent.

The cost is real and stated rather than hidden: press it twice and you get two
windows. That is a better failure than pressing it once and getting nothing.
If reuse turns out to be wanted, the only route is COM automation over
Shell.Application's window list, which is a lot of machinery for a button.

TRAILING SEPARATOR STRIPPED before the path goes in the argument. A path
ending in a backslash puts \" at the end, where the backslash escapes the
quote and Explorer receives something mangled. Path.Combine does not produce
one here, so this is a guard against a future caller rather than a fix - but
it is one line and the failure would be baffling.

---

## 0.10.67 — 2026-08-25

**Open folder beside Refresh, and the retail legend stops taking a row**

Two small things asked for while testing, plus one consequence worth catching.

OPEN FOLDER opens Custom\maps\mp in Explorer. Beside Refresh rather than in
the centre column, because that column is for MOVE operations - Add, Remove,
Top, Up, Down, Bottom - and this belongs with the list it describes.

It CREATES the folder if absent, which is a side effect worth defending: it is
a folder the launcher already scans and already documents, and a button that
opens nothing teaches a mapper that maps\mp is not a real place. Making it is
the answer they wanted anyway.

maps\mp specifically, not Custom\. The folder a map lands in decides which
LISTS it appears in - maps\mp feeds the rotation and is deliberately kept out
of the single-player menu - so opening the parent would point at the wrong
answer for the list this button sits under.

THE SCAN SUMMARY MOVED under the buttons instead of beside them. Its wording
varies ("no change (35 total)" against "+2 new (37 total)"), and a row that
grows sideways pushed the buttons out of line with the Order row opposite -
which was the whole point of putting them where they are.

"* RETAIL MAP" moved from a footer row into the heading's tooltip, where it
explains itself properly rather than as three words under a group: a star
means every player already has that map, and an unmarked one means a player
without the file cannot join while it is running. That is the thing a host
actually needs to know and there was never room for it on one line.

AND IT WENT INTO THE ROTATION HEADING'S TOOLTIP TOO, which was not asked for
and is the point of this note. The footer sat under the whole group and served
BOTH lists; moving it to one heading would have quietly left the other list
with a marker and no legend. Moving a shared thing into one of its consumers
is a deletion for the others.

---

## 0.10.66 — 2026-08-25

**a dry weapon said so once a frame, because nothing could follow it**

Reported from play: firing an empty TOW in TOWs Out fills the pickup feed with
"TOW out of ammo", several lines per click.

CWeaponModel::Update calls AutoSelectWeapon EVERY FRAME while the state is
W_FIRING_NOAMMO, on the assumption that a dry weapon is a transient condition
somebody is about to be moved off. With ONE weapon there is nowhere to move:
the search comes back round to the gun already in your hands,
CanChangeToWeapon finds it empty, reports out of ammo, and the state never
clears. Sixty lines a second.

The assumption held for twenty-eight years because you always had a sidearm.
Removing it for arena modes in 0.10.64 made a one-weapon loadout reachable for
the first time and turned a dormant assumption into a wall of text - the
previous release caused this one, which is worth the note: "everyone always
has a fallback weapon" was load-bearing in a place nobody had looked.

Fixed at source: nothing to switch to is not a switch. AutoSelectWeapon bails
when the candidate is the weapon already held. The state stays put and it runs
again next frame, which is correct and now silent - there is genuinely nothing
to do until ammo arrives, and the empty magazine on the HUD already says so.

AND GUARDED AT THE DISPLAY END, which is the part that matters beyond this
bug. "Out of ammo" is a STATE, and every route that notices a state reports it
for as long as the state lasts. AutoSelectWeapon was the route that filled the
feed; it will not be the last, because anything that polls a condition and
prints is one loadout change away from doing this again. So the handler
refuses a repeat for the same weapon inside two seconds. A DIFFERENT weapon
reports immediately - stepping through two empty guns should say both - and
the window is short enough that firing dry, reloading and firing dry again
still tells you twice.

Same shape as the reload interrupt from 0.10.54: the message is an EDGE, and a
caller that hands you a level has to be turned back into one. That was written
down in decisions.md at the time as a pattern to watch for. It took three
weeks to recur.

---

## 0.10.65 — 2026-08-25

**Refresh the available maps without losing the rotation you just built**

Asked for while testing: build a map, alt-tab, and the launcher has no way to
see it. The only route was restarting the launcher, because the scan ran once
inside LoadHostState.

Re-running THAT would have been the easy fix and the wrong one. It rebuilds
MapRotation from the saved cfg, so a refresh would silently discard whatever
had been arranged since the last save - a refresh that costs you your work is
worse than no refresh. So the scan is extracted to ScanAvailableMaps and the
button calls only that. Nothing else on the tab is touched.

Placed to match: same DockPanel.Dock="Bottom" and the same 0,12,0,0 margin as
the Order row opposite, so the two land on one line across the group.

It SAYS WHAT CHANGED rather than just that it ran - "+2 new (37 total)", "no
change (35 total)". The no-change case is the one that matters: for a mapper
it means the map did not land where the launcher looks, not that the button
failed, and those need different next steps. The selection is preserved
across the re-scan too, so picking several and refreshing does not undo the
picking.

Two things preflight caught, both worth recording.

The extraction moved a local (customDir) that code AFTER it still used, for
the rez list. Compiler error, fixed by re-declaring it where it is now used -
noted because "extract a block" and "extract a block that declares things
used later" look identical until the build runs.

check_dirty_tracking_covers_tabs then failed on MapScanSummary: bound on a
dirty-gated tab, not in HostProperties. Correct of it - that check was merged
two days ago precisely to catch a control whose edits get dropped. This one is
a READOUT rather than a setting, so it goes on the exemption list with its
reason rather than into the tracked set: nothing writes it to a config, and
marking the tab dirty would offer to save a sentence. First real use of that
list since it was written, and it did its job by making the exception explicit
instead of silent.

---

## 0.10.64 — 2026-08-25

**an arena mode is ONE weapon, and it binds bots as well as players**

Two reports from play on 0.10.63, both about the loadout half of TOWs Out
rather than the pickup half that was fixed last release.

BOTS IGNORED THE MODE ENTIRELY. PickLoadout chose from a fixed pool and asked
the rules nothing, so TOWs Out armed its bots with shotguns and mac10s while
every pickup on the map was a rocket launcher. The same omission twice over:
the pool already respected BlockWeapons - a server setting - while ignoring
the mode, which is the stronger statement of the two. The arena weapon is
now taken before the pool is consulted, because in an arena mode there is no
pool.

THE SIDEARM IS GONE, and this one was a deliberate decision being reversed
rather than an oversight. The .45 and pulse rifle were granted on purpose,
commented as "the thing you have when the arena weapon runs dry". That sounds
like mercy and plays like dilution:

- The mode's identity is that everyone holds the same weapon and every fight
  is the same fight. A sidearm makes it "rockets, mostly", and the fallback
  is what people reach for when the interesting weapon is inconvenient.
- Running dry is an AMMO problem and deserves an ammo answer. Every pickup on
  the map is already the arena weapon, so supply IS the mode. Rocket Arena's
  answer was infinite rockets, never a backup gun.
- It inverts on a server running InfiniteAmmo 1, which is where this was
  reported from: sidearms never run out while the mode's own weapon does. The
  scarce weapon was the one the mode is named after.

MELEE IS WHAT MAKES IT SAFE, and it is the reason this is a small change
rather than a risky one. Melee is auto-acquired when the weapon objects are
created (CWeapons, the bMelee branch) rather than obtained, so every player
has it whatever else happens. Empty the launcher and you still have a knife -
gauntlet and rocket launcher, which is the shape the genre settled on.

Structured as a hook, GrantsSidearm, defaulting TRUE. Squishie and Squishie
War keep their sidearms; only an arena mode declines one, and a future mode
can decide for itself. Gated on there actually BEING an arena weapon, so a
mode that declines the sidearm and hands out nothing leaves a player armed
rather than empty-handed.

Bots read the same hook, so bots and humans cannot end up on different
loadout rules - which is precisely the bug being fixed, one layer up.

---

## 0.10.60 — 2026-08-24

**TOWs Out did nothing at all, and the guard that stopped it predates the mode**

Reported from play: TOWs Out left every weapon pickup where it was. Not "some
wrong weapons" - the mode had no effect whatsoever unless the server also had
a blocklist or randomized pickups.

The path has THREE gates and the arena test only reached two of them.

  1. CRiotServerShell decides whether to run the weapon pass. Knows about
     arena - the comment even says "a third reason to run the weapon pass".
  2. ApplyWeaponRules' early return decides whether there is work to do.
     Did NOT know about arena.
  3. The collect loop's bTakeAll decides whether to take every pickup or only
     the blocked ones. Knows about arena.

So the caller correctly called and the callee immediately refused. Gate 2 is a
second, incomplete copy of a decision gate 1 had already made, weighing two of
its four reasons.

NOT A REGRESSION FROM THE RULES EXTRACTION, which is what it looked like and
what I assumed for the first few minutes. git log -L on those lines shows the
guard was written for the blocklist in 6200913, long before any mode existed;
every mode added since has had to sneak past it. The extraction moved the
arena test to a hook and correctly updated both places that ASK the question -
nobody had reason to look at a line that never mentioned modes.

INFINITE AMMO ESCAPED BY ACCIDENT, and that is the part worth remembering. It
marks endless weapons as blocked a few lines above the guard, which bumps
nBlocked and carries it through as a side effect. Its caller-side fix has a
comment explaining that without it "the pass is never CALLED" - true, and it
needed a second piece of luck at gate 2 that nobody noticed it was relying on.
Arena had no such luck and was the one reason that reached the guard and died.

check_arena_pass_reaches_pickups asserts all three gates consult IsArena().
The failure mode is silent - no error, no log line, the map simply unchanged -
so nothing else would have caught it. Verified by breaking gates 2 and 3
separately.

The guard stays rather than being deleted, though deleting it would also be
correct: ApplyWeaponBlocklist is dead code, so the live caller is the only
gate that matters. It stays because a function that walks every object in the
world should be able to say no on its own - it now says no for the same four
reasons the caller says yes.

Also confirmed this session, from the server log rather than from reasoning:
the dedicated-server crash on disconnect is GONE. Two clean disconnects at
20:26 and 20:29 with the server running on afterward, and no FreshSrv crash
report. That fix landed in 0.10.43 and had never once been watched working.

---

## 0.10.62 — 2026-08-24

**the bot forgot it was a mech, and the client deserves a second copy of its size**

Third war playtest, three findings, one certain fix and one theory now
instrumented well enough to be settled next round:

- CFreshBot never set the base-class m_bIsMecha, which every stock AI sets
  in its own constructor. IsMecha() answered DFALSE for a bot piloting an
  MCA, so the war's side gate filed the bot mech WITH the squishies:
  nobody could hurt it, and it could hurt nobody. Reported as 'can't
  damage the mech (unless my weapons are just too weak)' - the weapons
  were fine, the bot was on your team as far as the gate knew.

- The full-size-squishie report survived the spawn-tier fix, which changes
  the suspect: the server scales, but the OWNING client's CMoveMgr - whose
  physics-update message is the only way a client ever learns its own eye
  height and movement dims - never hears about a level's FIRST spawn,
  flagged while that client is still entering the world. One deferred
  resend of the state, a second after any squish spawn, covers the race;
  WeaponDebug now prints on both halves (server: 'scaled to 0.20, resend
  queued'; client: 'own dims scale 0.20'), so if the theory is wrong the
  next report will say so in two lines instead of a feeling of being tall.

- The join announce was never seen in three playtests because it lands
  during the world fade with the MOTD window on top of it. The mode line
  now rides the MOTD itself - appended to the default notice and to an
  admin's own file (their words outrank ours: dropped only if all 40
  lines are spent) - which reaches exactly the FRESH-only audience the
  moded servers have.

Mech-tier-only pickups on war maps are BY DESIGN for now (squishies live
off the spawn grant; the mech lives off the map) - flagged in the spec as
a balance dial to revisit, not a bug.

---

## 0.10.61 — 2026-08-24

**in a war, the side decides the body; the start point only decides position**

Second Squishie War playtest read as three bugs: spawned full-size with the
on-foot arsenal, still on foot after !mech and a respawn, and dealing no
damage to the bots. One cause: the spawn gate trusted the start point's
tier, and on real MCA maps mixed tiers are the NORM - which is documented
in MapSpawnsAreMech itself, 'any spawn is mech' exists BECAUSE MCA_SPIRES
is eighteen mode-7s, one 6 and one 0. Land on the stray foot spawn and you
came up full-size on foot (no squish flag, so no scale), the arsenal grant
followed your body, and the friendly-fire gate then correctly refused to
let you shoot your own side - the 'no damage' was the mode working.

War modes now normalise the body from the SIDE: squishie-wanted spawns as
a scaled squishie from any start point, mech-side spawns as the player's
chosen mech from any start point (including the mode-6 PM_CURRENT_MCA
starts some retail maps carry). Mode 2's gate is untouched - its contract
is 'on foot where a mech was offered', and the start-point trust is correct
there.

BACKLOG carries what round two leaves open: the bots' visible shrink-pop
(client message timing, cosmetic), mechs inheriting foot-spawn POSITIONS
(watch for pinched spawns), and the join announce not yet re-confirmed.

---

## 0.10.59 — 2026-08-24

**Squishie War, and the matrix takes back what the handoffs dropped**

Two unrelated things, released together because they were both sitting
unpushed and neither justified a release of its own.

SQUISHIE WAR (mode 3), built yesterday afternoon by the build thread from
Docs/SPEC-SQUISHIE-WAR.md. Notable for HOW it was made rather than what it
does: the first mode in this project specced before it was written instead of
documented afterwards, and it landed on the rules interface from 0.10.58
without needing a new hook. That is the strongest evidence available that the
extraction got the surface right - the acceptance test for CFreshRules was
whether it could express modes nobody had written yet, and it could.

It also forces the FRESH gate on (3eb2f0c), which is the doctrine settled the
same day: new modes are FRESH-first, rules stay server-side always, and stock
presentation parity is no longer paid for.

UNTESTED, entirely. Eight validation rows live in the spec, written before the
code. The three judged most likely to be wrong are named in BACKLOG.md, and
they are all the same shape - whether a squishie-spawned player is faithfully
not-a-mech everywhere it matters, since the damage gate and the bounty both
ride on that one answer.

THE MATRIX REPAIR is the other half, and it is the more important of the two
for anyone reading this in a month.

HANDOFF.md is overwritten every session by design. Twice now that silently
dropped work carried nowhere else: the dedicated-server crash verification -
fix confirmed against the binary since 0.10.43, a character never released
from CCharacterMgr, NEVER ONCE watched not dying - and the TOW radius verdict.
Both were called out on consecutive days and both were gone by this morning,
from HANDOFF.md and BACKLOG.md alike.

The failure is not carelessness, it is a category error: a disposable file was
holding non-disposable work, and the rewrite looks exactly like completion.
Both are now rows in BACKLOG.md Part 2 with the reason recorded beside them,
and HANDOFF.md carries the RULE instead of the items - anything that must
outlive one session goes in the validation matrix, not in next steps.

Also in: Docs/SPEC-VOICE.md, scoping Mumble positional voice. No code. It
carries a withdrawn claim on purpose - Mumble's context field does NOT give
team channels, it governs positional audio only - because that claim was made
confidently in conversation and would otherwise have been built on.

---

## 0.10.58 — 2026-08-24

**the mode queue is picked, and stock parity stops being a design tax**

Design session, no game code of its own - the code in this release is the
extraction thread's (rules interface, free-fly, reverse-LOS). What this
commit adds is the design record that work now serves:

Three mode specs and a design doc. Squishie War (mode 3, builds first: sides
make mode 2's ability structural, cooperative in spirit, zero open
questions), Headcount (mode 5, the Attrition shape renamed into the corpo
ethos - the score IS a weighted count of heads; gated on measuring the NPC
ceiling and the mid-life body swap), Duel (mode 4, drops to third). The
conductor (DESIGN-CONDUCTOR.md) is a scheduler above the rules layer, whose
contract the extraction thread verified same-day: rules hold no state, so a
mid-match swap is a pointer assignment.

Decisions that change doctrine: new modes are FRESH-first. Rules stay
server-side always - that discipline is engineering, not stock charity - but
stock presentation parity is no longer paid for. Duel ENFORCES RequireFresh
(7 Hz stock reporting makes a 1v1 a duel against a slideshow); Squishie War
and Headcount likely follow. Phase changes force-switch now-illegal weapons
(Kyodan's call, until playtest; the grandfather reasoning is preserved as
the fallback). Minions are a dial, not a mode.

The FragValue correction is why specs are walked before APIs freeze: the
candidate signature drawn from Squishie War alone was one argument short
the moment the Minions dial exists under any mode. Caught on paper, fixed
in 20f7aec.

Version to 0.10.58 in all three places; preflight caught the two this
commit initially missed, which is that check earning its keep.

---

## 0.10.57 — 2026-08-23

**the music driver stays out of multiplayer, and a rocket says where it was born**

E7, mitigated at last, in the launcher. "+DisableMusic 1" is passed when the
launcher goes STRAIGHT into a multiplayer game: a join from the Servers tab, or
a listen host from the Host tab. Not for a plain launch into the menus, because
that is not a multiplayer session until the player makes it one and guessing
there would take campaign music away from everybody.

Why the launcher rather than the game. The driver loads ONCE at startup, before
any world exists, so "multiplayer only" can only be a launch-time decision
unless music init is re-sequenced next to E10. Option (a) of the two shapes on
file: no game code, no risk.

Why the existing mitigation was not enough, since it looks like it should have
been. "MusicInMultiplayer 0" has been the default since 0.9.63 and skips
InitPlayLists for a multiplayer world - and the fifth occurrence proved from a
minidump that ima.dll loads and runs its worker thread anyway. It reduces what
the middleware is asked to DO without changing whether it is RUNNING, and the
fault is on the thread that keeps running. Every crash on file happened under
the mitigation meant to prevent it.

MusicInMultiplayer 1 OPTS OUT, and wiring that was the substantive part rather
than a courtesy. Somebody who has asked for multiplayer music must not have the
driver killed underneath them: they would get silence, with nothing anywhere
explaining why, and would reasonably conclude the variable was broken. Read
from autoexec.cfg and client-settings.cfg, either one asking being enough. One
variable, one meaning - it is the answer to "do I want music in multiplayer",
and this reads it instead of growing a second switch beside it.

THE COST IS STATED, not hidden: start in the campaign, then join through the
in-game menus, and the driver is already loaded. That path is still exposed and
option (b) is what would close it.

---

ProjDebug can now tell "spawned outside the room" from "tunnelled out of it",
and it could not before - which would have made the B8 experiment ambiguous
without anything saying so.

B8 lists three mechanisms for a projectile that flies its whole life without a
collision, and they have different fixes. The out-of-world back-trace shipped
in 0.10.56 reports where the escape crossed solid geometry - and that is
IDENTICAL for tunnelling and for spawn-outside, because a segment from the void
back to the muzzle crosses the wall either way. The world box cannot settle it
either: a point just past the wall of a box-shaped room is still inside the
level's bounding box, so "outside the world" is a much later symptom than
"outside the room".

A trace from the FIRER to the MUZZLE does settle it. The muzzle sits forward of
the shooter, so standing close to a wall can put the spawn point beyond it, and
a hit on that short segment prints SPAWNED THROUGH SOLID at fire time with the
offset and the surface position. Silence on that line while a rocket still
escapes is the positive evidence for tunnelling that the wall-thickness ladder
needs - so the rig built by the mapping thread can now reach a conclusion
rather than a shortlist.

Worth one deliberate point-blank shot to prove the line FIRES before trusting
its silence as evidence. A diagnostic whose negative result carries the
argument has to be shown to have a positive result at all.

Verified 0.10.56 before building on it: the pickup fix guards on non-zero
rather than deleting the read, which is the better call and retail data is why -
32_MUSEUM places a bare WeaponPowerup and makes it an assault rifle purely
through WeaponType=15, so deleting would have broken shipped content. Version
agrees across FreshVersion.h, the csproj and CLAUDE.md; preflight clean at 30
checks.

---

## 0.10.56 — 2026-08-23

**the pickup that was always a pulse rifle, and a sphere that moves**

Every map-placed weapon pickup in single player gave the pulse rifle, and had
since 1998. Three threads found it between them: the TrenchBroom session
noticed it while mapping, a second session root-caused it and corrected the
first diagnosis, and this one wrote the fix. The corrected version is BUGS.md
C8; the fix is the guard below.

WeaponPowerup::ReadProp overwrote each subclass constructor's weapon id with
the map's stored WeaponType, and a stored 0 plus GUN_PULSERIFLE_ID being 0
collapsed everything to the pulse rifle. Multiplayer hid it because respawn
re-creates pickups through CreateObject, which skips the property path
entirely (engine fact 8).

DELETING the read - the first proposal, and the obvious one - would have been
wrong, and RETAIL DATA is what says so. 32_MUSEUM places a BARE WeaponPowerup
and makes it an assault rifle purely with WeaponType=15. Guarding on non-zero
keeps that pickup, keeps every correct SP campaign id, and gives up only "a
subclass overridden to pulse rifle through a hidden property" - which no map
does and PulseRiflePowerup expresses better. Worth recording that the
measurement is what changed the fix: the claim "no map carries a meaningful
value" was true of the MULTI maps it was measured on and false of all 39
retail campaign worlds.

Ammo is clobbered the same way, one line up, and MULTI maps store Ammo=0 too.
NOT given the same guard - a stored zero may legitimately mean an empty
pickup, so it needs its own answer rather than this one applied twice. Left in
BUGS.md and in the handoff's open list.

THE BLAST SWEEP, which is the other half of this release and is documented at
length in b495362. An explosion's damaging radius is animated: a quarter of
the weapon's radius, growing to the full radius halfway through, shrinking
back, re-damaging every 0.1s while the damage decays to zero. Found from five
DamageDebug lines in the owner's own log reading "of 135/141/117/136/139"
against a stated radius of 150. ExplosionDebug now tracks it; FreshBlastSweep
is the one implementation and the server damages from the same function.

That finding is why this release matters for the TOW question. "Radius 150" is
a peak touched for one instant, at which point the damage has already halved -
so the verdict on 200 vs 150 was being formed against a number that does not
mean what it looks like. The handoff carries the table.

Fall damage's two dials reach the Host tab, tooltips cut to a sentence each,
and ProjDebug stopped claiming to detonate projectiles it was about to delete.

THREE NEW PREFLIGHT CHECKS, each verified by breaking it, and each guarding a
fact this session found written twice:
- check_weapon_powerup_ids: the non-zero guard, plus all 16 subclass ids
  matching their property defaults. The guard reads like a style choice and is
  the entire fix, so it needed something asserting it stays.
- check_sfx_id_table: a new FX id without a ceiling row gets a maximum of ZERO
  objects and silently never draws. This nearly shipped that way.
- check_fall_tuning_exposed: the launcher's clamps and defaults against
  FreshTuning.h.

Built all five, preflight clean, handoff rewritten rather than appended to.

---

## 0.10.55 — 2026-08-23

**two tools for measuring a blast instead of guessing at it**

DamageDebug, server side. Two prints that answer different halves.

CDestructable::HandleDamage is where every route into damage converges -
hitscan, blast, melee, crush, drowning, script - and it is AFTER armour has
taken its cut, so it is the only place that knows what the victim actually
lost. It reports who hit what, with which weapon, for how much, how much
health is left, and marks the killing blow.

DamageObjectsInRadius reports the WORKING, which is the half a damage number
cannot show you: "dist 92 of 150, falloff 0.63, 90 -> 57". The distance and
the scale were already computed there for the damage itself, so this reports
them rather than recomputing - and that pair is what turns a blast radius from
a feel judgement into a measurement.

Names, not handles: FreshDamageName resolves players by net name and bots by
bot name, because two object ids do not answer "who hit what". Safe to pass as
a %s argument because FreshNameSanitise strips % at join - engine fact 19, the
same reason the existing CPrint("%s", GetNetName()) sites are safe.

NO MASK BIT for the channel, and that is deliberate rather than a shortcut.
All eight bits are spoken for and widening the mask is a protocol change. A
channel without a bit is simply one a multiplayer client cannot switch on by
telling the server - and engine fact 18 is the other door: "serv DamageDebug
1" writes into the server's own console space, which works in single player,
in a hosted game, at a dedicated server console and over rcon. The preflight
channel table has room for sixteen and holds ten.

ExplosionDebug, client side, and it draws the number you have never been able
to see. The visible fireball is GetWeaponVisualRadius, which DEFAULTS TO
DOUBLE the damage radius and has since 1998 - so the ball engulfing you is
twice the size of the thing that can hurt you. That mismatch is deliberate and
runs in the safe direction (inside the fireball and unhurt, never the
reverse), so this does not change it. It draws the other number beside it.

Same mesh as the explosion, so the eye reads it as a blast; wireframe, so it
can be seen from inside and does not hide what it measures; static rather than
expanding, because the explosion grows because it is an event and this is a
measurement. Entirely local - GetWeaponDamageRadius is shared code, so nothing
is asked of the server and nothing is sent anywhere.

ExplosionDebug 2 adds a calibration cube, and this is the part worth reading.
Whether the sphere mesh's scale means RADIUS or DIAMETER cannot be read from
source - it depends on the model's own size. But the cube's mapping IS known:
UpdateBoundingBox scales 1x1_square.abc by VEC_DIVSCALAR(vScale, vDims, 0.5f),
and dims are half-extents, so scale = twice the half-extent. Setting the cube
to twice the radius puts its FACES exactly on the radius with no assumption at
all. Show both once, see whether the sphere matches, and the sphere is
trustworthy afterwards. It is scaffolding, not a display.

The cube is exact where it touches and generous where it does not - faces on
the radius, corners at 1.73x - which is stated in the comment rather than left
for someone to discover by standing in a corner and not dying.

Hooked at CreateWeaponSpecificFX, the single dispatcher, so it fires for every
weapon at every detail level rather than at the five per-weapon explosion
functions. Weapons with no blast report radius 0 and draw nothing.

Untested. Both are diagnostics, so the first real use IS the test: the TOW at
150 needs a verdict and now has numbers behind it.

---

## 0.10.54 — 2026-08-23

**a held trigger no longer interrupts a shell reload**

Two reported faults, one cause, and the cause was mine.

"Hold fire, press reload, and it fires without the pump." "Fire dry while
holding and the reserve drains a shell at a time with the magazine stuck at
zero." Both are the interrupt firing on a HELD trigger rather than a pulled
one, and UpdateFiring runs every frame the trigger is down.

The first: reload starts, next frame the break-out sees a non-empty magazine,
clears the reload window and calls PlayFireAnimation - which restarts the
animation, whose fire key then fires. A shot per reload press, outside any
cadence, with the pump cut off because the animation went back to frame zero.

The second is the same loop closed on itself. Firing dry starts the auto shell
reload; the first shell lands; the break-out takes it, clears the reload and
fires it; the magazine is empty again; the auto-reload starts again. The
magazine never climbs, the reserve drains one shell per shot, and the pump
never plays because the fire animation is restarted every time.

Both stop with one rule: a trigger held from BEFORE the reload is not a
decision to interrupt it. m_bReloadHeldFrom latches when a shell reload begins
with the trigger already down, clears when the trigger is released, and the
break-out requires it clear. Release and pull again and the interrupt happens
immediately, which is what the feature was for.

Holding through a reload now does what it should: the reload runs to
completion and firing resumes when it finishes.

Worth naming as a pattern rather than a slip. The original comment said "no
trigger test needed: UpdateFiring is called only when the trigger is down" -
which is true, and was exactly the wrong thing to conclude from. Being called
while the trigger is down says nothing about whether it was JUST pressed, and
an interrupt is an edge, not a level. The comment reasoned correctly from a
fact to a false conclusion, which is harder to catch on review than a plain
mistake.

Server side is unchanged and can still cancel a shell reload on a fire
message. That is the same client-authoritative caveat the fire modes already
carry (see GetWeaponFireMode) - the server sees discrete fire messages and has
no trigger edge to test. A modified client could fire during its own reload,
which it could already do before any of this.

Also this session, from the owner: the shipped reload sound is audible and
working.

---

## 0.10.53 — 2026-08-23

**a shotgun reload sound, and the first asset in our rez**

Kyodan supplied the sound. It ships as Sounds\Weapons\Shotgun\reload.wav
inside ShogoFRESH.rez, which makes it THE FIRST NON-DLL THING EVER TO GO IN
THERE, and that is worth saying out loud rather than discovering later.

Why it has to ship with the game rather than as a mod: Shogo never recorded a
shotgun reload sound. Only five weapons have one - assault rifle, .45,
juggernaut, pulse rifle, shredder - so GetWeaponReloadSound has always built a
path to a file that does not exist and the shotgun has always reloaded in
silence. The shell-by-shell reload plays one sound per shell, so without this
the feature is eight silent steps.

THE COST IS STATED RATHER THAN HIDDEN. Our rez loads after Custom\ by default,
so a sound mod replacing THIS ONE PATH loses while FreshTakesPriority is on.
Every other asset is unaffected, because every other asset is still absent
from our rez - which is the property engine fact 2 describes, now carrying one
documented exception instead of an absolute.

Shipped assets live in Assets/rez/, laid out as the tree they land on;
prepare-redist.ps1 copies them into the rez source before mkrez.py runs, and
mkrez mirrors the directory. Verified by extracting the built archive:
/SOUNDS/WEAPONS/SHOTGUN/RELOAD.WAV, and the readback still matches all four
DLLs against Dist.

The stand-in is gone with it. GetWeaponShellSound and PlayShellSound existed
only because there was no reload.wav to play; with a real one the shell sound
IS the reload sound, so both are deleted and PlayReloadSound does the job. One
fewer concept for the next reader to hold.

The "reload begins" sound is now suppressed for weapons that load one at a
time. Each shell announces itself as it lands, and playing the start cue as
well would double it at t=0 and give nine sounds for eight shells.

The file is what the engine wants, and that was measured rather than assumed:
every one of the 2,091 wavs in SOUND.REZ is uncompressed PCM, mono, and
22,050 Hz - there is not a single stereo file in the archive, which is Miles
telling you that a 3D-positioned sound cannot be panned. Weapons are 16-bit.
This one is PCM mono 22050 16-bit, 0.319s, which also keeps it clear of the
0.5s between shells.

Confirmed in play this session by the owner: the shell reload works, and
firing dry no longer refills the magazine.

---

## 0.10.52 — 2026-08-23

**the shell reload's three reported faults, and why each happened**

All three came from the same place: 0.10.51 taught the MANUAL reload to load
one shell at a time and left everything else believing a reload fills the
magazine at once.

"GOING TO 0 REFILLS TO 8." Both sides auto-reload when the magazine empties -
CWeapon::Fire and CWeaponModel::UpdateFiring - and both filled the clip
outright. Reload() had been taught the new trick and Fire() had not, so firing
dry was the one case where none of the shell machinery ran at all. Both now go
through one shared CWeapon::StartShellReload, which returns DFALSE for a
weapon that does not load that way and lets the ordinary path continue.

While fixing it I wrote an early "return eRet" out of the auto-reload block
and caught it before it shipped: the function still has to clear m_bFire on
its way out, and returning there would have latched the trigger into the next
frame. It is an if/else now.

"NO SOUND PER SHELL." Not the detection - the SHOTGUN HAS NO RELOAD SOUND. Only
five weapons ship one (assault rifle, .45, juggernaut, pulse rifle, shredder)
and the shotgun is not among them, so GetWeaponReloadSound has always built a
path to a file that does not exist and the shotgun has always reloaded in
silence. 1998's, not ours; it only became noticeable when the reload grew
eight steps that each wanted announcing.

PlayShellSound borrows the shotgun's own select.wav - the weapon being handled,
the closest thing the game owns to a round being worked into a tube, and
already cached. A stand-in for art that does not exist, and the first thing to
replace if a shell sound is ever recorded.

The refill bug was also hiding this one: the client had already snapped its
magazine to 8, so the server's per-shell reports never arrived as "+1" and the
sound could not have fired even with a file behind it.

"RELOADING FROM 6 TAKES THE FULL TIME." The owner is right and this was my
error. The server sized its window by the shells it actually needed; the
client used the whole reload flat. So topping up from six finished loading in
one second and then sat in the reload pose for three more - which is exactly
why the raising animation appeared not to play until the end.

Two sides computing the same quantity differently is the precise failure the
reload comments in this file already warn about, and the fix is the one that
file already prescribes: FreshShellReloadWindow in WeaponLogic.h, called from
all four places - the server's manual and auto reloads and the client's two of
the same.

So no, "Reload14 3" does not mean eight seconds. It means the whole reload
takes three seconds when the magazine is empty, and topping up from six takes
two eighths of it.

Untested. The handoff carries the list, with interruption and firing dry at the
top.

---

## 0.10.51 — 2026-08-23

**the shotgun loads shell by shell**

The reload at 4.00 stops being a placeholder. It is now the WHOLE reload, and
FreshShellInterval divides it by the magazine - eight shells at half a second
each.

FreshReloadsByShell is a BOOLEAN and the interval is derived, deliberately. A
"seconds per shell" constant would be a second number that could disagree with
the reload time, and the first thing anybody would do is tune one and not the
other. Dividing means Reload14 3 still means exactly what it says - the whole
reload takes three seconds - and each shell follows on its own.

NO NEW MESSAGE, and this is the part worth reading. The server has always been
authoritative about the magazine and CWeaponModel::SyncAmmoInClip snaps to
whatever it reports; UpdateInterface already sends the count whenever it
changes. A shell landing IS that count changing - so the readout climbs by
itself, and the per-shell sound is inferred from "went up by exactly one,
while reloading, on a weapon that loads that way". Nothing on the wire, and
the sound cannot disagree with the number on the HUD because it IS that
number.

The tick is in CPlayerObj::Update because that is where a tick actually runs:
the weapon state machine does not advance for the player at all (engine fact
1), which is why every other timing in this file is a deadline rather than a
counter. Feeding shells genuinely needs a clock, so it goes where one exists.
It loops rather than feeding one per call, so a frame hitch pays what it owes
instead of stretching the reload, and the deadline advances by the interval
rather than being reset from now.

INTERRUPTION IS THE POINT, not a nicety. Loading one at a time only means
something if you can stop part way and shoot what you have; a shotgun that
commits you to all eight is the single-window reload with extra steps. The
trigger cancels on both sides - CWeapon::Fire decides, and the client stops
holding the reload pose so the gun does not go on reloading on screen while
firing perfectly well.

Cancelling clears BOTH deadlines. m_fReloadEndTime is what refuses a shot, so
leaving it would mean an interrupted reload still locked the trigger for the
rounds it never loaded, which is the opposite of what interrupting is for. And
it is guarded on having at least one round: cancelling into an empty magazine
would just be a slower way of loading nothing.

The client half went into UpdateFiring rather than UpdateNonFiring, which is
the distinction that makes it correct without a trigger test - UpdateFiring is
called only when the trigger is down. UpdateNonFiring has the same W_RELOADING
case and must NOT break out of it, because nothing there asked it to.

Classic is untouched: FreshReloadsByShell returns false without FRESH rules,
and Classic has no reload mechanic at all (fact 20).

Untested. The handoff carries a test list ordered by how likely each case is
to be wrong, with interruption at the top.

---

## 0.10.50 — 2026-08-23

**the shotgun's numbers land, TOW radius under test**

THE DIALS DID THEIR JOB. Tuned in play, retired into the tables, still
available as overrides.

Shotgun fire interval 1.10, into a NEW GetFireInterval table - there was none,
because the fire animation had always been the default and nothing could
override it before 0.10.49.

Shotgun equip 0.40 -> 1.10, AND THIS WENT THE OPPOSITE WAY TO THE REQUEST THAT
PRODUCED IT. The ask was "skip the equip animation, it pumps the shotgun".
Played, the answer was not to cut the pump but to let it finish: 0.40
truncated it mid-stroke, which reads as a glitch rather than as speed. Worth
recording as the argument for dials over reasoning - the request was
plausible, specific, and wrong, and only playing it showed that.

Shotgun reload 1.10 -> 4.00, explicitly TEMPORARY. It stands in for a
shell-by-shell reload that does not exist yet: eight shells fed one at a time
is roughly four seconds, so the cost is right while the presentation is a
single wait. It goes away when the real thing lands.

Shotgun view model +1, -1, -0.5 baked into BOTH offset tables, because the
live nudge added to whichever the PVWeapons setting had active - baking it
into one only would have moved the gun on one setting and not the other.

TOW blast radius 200 -> 150, under test. The question two rounds raised is
whether a near miss should still kill. Radius and not the 90 damage on
purpose: damage is already low enough that cutting it would make the direct
hit non-lethal and force two rockets every time, which is more spam rather
than less.

ViewModelOff 1 ignores every nudge, global and per weapon. The owner asked for
a way back to stock for comparison and found that setting six variables to 0
by hand does not give you one - you end up comparing against "stock plus
whichever I missed", which is not a comparison.

CORRECTION, and it nearly shipped. I claimed the mech weapons had no authored
view-model offsets, that they fell through to a default with a positive y, and
that this was why they had always looked oddly placed. I acted on it. The
compiler rejected it: both tables already name all twenty-one weapons. The
claim came from reading the tables through a fixed-size window that stopped
before the mech entries and reporting the absence inside the window as an
absence in the file.

That is the third claim this month from reading PART of something and
describing the whole - this, the shotgun's damage (20 read as the per-shot
figure when it is per pellet, ten of them), and the F-rebind diagnosis (engine
fact 3 asserted before the file was read). All three confidently stated, all
three wrong in the same direction. Written into decisions.md as a pattern
rather than three separate corrections.

---

## 0.10.49 — 2026-08-23

**Fire, Equip and ViewModel dials, every one of them inert by default**

Nothing in this release changes behaviour until a number is typed. That is
deliberate: it is a lot of new surface at once, none of it is a balance
decision yet, and the owner is about to dial it in play.

FIRE<ID> EXISTS BECAUSE THERE WAS NO LEVER ON SHOT CADENCE AT ALL. The owner
asked to shorten the shotgun's time between shots and reached for Reload14,
which is a different thing entirely - the gap between pressing reload and
being ready. The rate is the FIRE ANIMATION: PlayFireAnimation only restarts
it once it reports MS_PLAYDONE, and for a pump-action shotgun the pump is
inside it. Nothing anywhere could shorten that.

It now can, the only way engine fact 5 allows: a set interval restarts the
fire animation early, which re-runs the fire key and produces the next round.
The visible cost is a truncated pump. Cutting short is the only lever there
is - there is no playback-rate control and never was - which is why
GetEquipTime has always worked exactly this way on the draw.

EQUIP<ID> outranks the GetEquipTime table (the shotgun sits at 0.40s there).
That is the "skip the equip pump" request; the window already cut the select
animation short, there was just no way to tune it per weapon.

VIEWMODEL, IN TWO LAYERS. ViewModelX/Y/Z nudges every weapon; ViewModelX14
nudges the shotgun on top of that, id on the end like Fire14 and Reload14.
Added rather than overridden, because the two answer different questions:
"everything sits too far out" is one number typed once, "the TOW sits wrong
next to the .45" is a correction to one weapon. If the per-weapon value
replaced the global, every one of them would need re-deriving the moment the
global moved, and the global would be useless the first time anybody touched
it.

Both layers add to the per-weapon table underneath, so Monolith's relative
placement survives and what moves is the whole hand, then one weapon within
it. The MUZZLE offset is deliberately untouched: it is relative to the model
offset, so it follows for free, and applying the nudge twice would walk the
flash off the barrel.

WHY THE TIMINGS ARE MIRRORED AND THE VIEW MODEL IS NOT. A weapon's cadence and
draw are things the server and client must agree on or the animation and the
ammunition end apart - the reason the reload dial reads through the server
console mirror (engine fact 18). Where a model sits on your screen is not
something the server has any business knowing and nobody else can see it,
which is the HudScale rule.

THESE ARE MEANT TO BE RETIRED. The mine and grenade dials set the precedent:
dial in play, then the number goes into the table and the variable stays as an
override. Fire<id> has no table behind it yet - the animation is the current
default - so the first values that settle will create one.

LEFT OPEN, and written up in the handoff rather than guessed at: whether the
TOW is too strong now it carries two. One rocket was already lethal (90 direct
plus a 200-unit blast against 100 on-foot health), so the second only changes
what happens when you MISS, which is what the change was for. If it does need
cutting, cut the radius and not the damage - 90 is already low enough that
lowering it just forces two rockets every time.

---

## 0.10.48 — 2026-08-22

**GetCommandId derives from GetWeaponId instead of duplicating it**

The task was "make GetCommandId tier-aware", and the obvious reading is: add a
mode parameter and split each case into an on-foot arm and a mech arm. That
would have worked, and it would have left TWO tables that have to agree -
which is the defect, not the missing parameter.

So GetCommandId(nWeaponId, dwPlayerMode) now SEARCHES GetWeaponId over slots
1..10 and returns the first key that answers. The second switch is gone. The
round trip is true by construction: there is one table, the inverse cannot
disagree with it, and a weapon can sit on DIFFERENT KEYS IN THE TWO TIERS -
which is what the on-foot laser cannon needs and what the old shape could not
express at all.

This is the bug the sniper rifle already shipped once: it answered "key 9",
key 9 answered "nothing" in a mech, and the result was a model hovering above
the head, ammunition read off the end of a table, and a fire that played its
animation and its sound and produced no bullet. A preflight check was written
to catch a repeat; making the repeat impossible is better than catching it.

Cost is ten switch evaluations per weapon pickup and weapon change - not a
per-frame path. A lookup table would buy nothing and would be the second copy
all over again.

A REAL BEHAVIOUR CHANGE, named because it is easy to miss: the search returns
the LOWEST matching key, so under FRESH a mech now reports the sniper rifle as
slot 5 - its mech slot - where it used to say 9 whatever the mode. CWeapons
compares command ids to decide whether an auto-switch is an upgrade, so the
sniper's auto-switch priority in a mech drops from 9 to 5. More correct, but a
gameplay difference rather than a pure refactor.

All fourteen call sites had the player mode within reach, including
WeaponModel.cpp which already asked g_pRiotClientShell->GetPlayerMode()
elsewhere in the same file. CPlayerObj gained GetPlayerModeId() for the two
Weapons.cpp sites that hold a player pointer rather than a mode.

THE PREFLIGHT CHECK CHANGED SHAPE WITH IT. It cannot compare two tables any
more, so it asserts the invariant the fix bought: no "case GUN_*" inside
GetCommandId, the body still calls GetWeaponId, and the signature still takes
a mode. Verified by breaking each on purpose.

Worth recording that the old check had already gone silent when the second
switch disappeared - it reported "every weapon's key leads back to it (0
weapons)" and passed. A check that passes vacuously is worse than no check,
and this one only got noticed because the ok line was read rather than the
FAIL count.

CONFIRMED IN PLAY this session, from the owner: quick melee rebinding (the
0.10.47 malformed-line sweep), the custom-SP map restore degrade WITH its
on-screen notice - the oldest unconfirmed correctness fix in the project, from
0.10.41 - the vehicle-mode holster, and the juggernaut at 450.

---

## 0.10.47 — 2026-08-22

**a bind line that does not parse is dropped, not preserved forever**

THIS IS WHAT "QUICK MELEE WILL NOT REBIND" ACTUALLY WAS. The owner was using
the launcher, and their live autoexec.cfg held both:

    rangebind "##keyboard" "##33" 0.000000 0.000000 "QuickMelee"
    rangebind "##keyboard" "##16"

The old F binding intact, and the new Q one written with no ranges and no
action. The engine cannot parse the second, so Q did nothing and F kept
working - which from the outside is exactly "the rebind silently failed".

Neither launcher writer can produce such a line: FormatBind and
KeybindFile.Rebind both always emit the action. And the surrounding block is
in the ENGINE's serialization style rather than ours - the digit row comes
back as "0", "9", "8" device-object names instead of ## scancodes, every line
carrying a trailing space the launcher never writes. So the game rewrites
autoexec.cfg on exit and something in that path dropped the action name. Who
wrote it is NOT settled and I am not going to pretend otherwise.

The fix does not depend on knowing, because the durable defect is on our side:
a malformed bind is invisible to the parser, so it never reaches _binds, never
appears in the UI, and Save()'s else branch copied it through verbatim on
every write - permanently. Nothing could ever clean it up. BindingStore now
DROPS a line that opens as a bind, fails the grammar, and carries no quoted
action.

Narrow on purpose, and "I do not recognise this" still means keep: it fires
only when all three hold, and a bind with no action cannot be doing anything
because the action is the entire point of one.

It composes with the F -> Q migration that shipped in 0.10.46: the broken line
is not in _binds, so nothing holds ##16, so the migration will not refuse to
move quick melee there - and the same Save() that writes the move sweeps the
debris. One launcher run should both repair the file and land the new default.

If the engine mangles it again on the next game exit, that is the remaining
half, and the way in is to diff autoexec.cfg immediately before and after a
session rather than reason about it further.

---

## 0.10.46 — 2026-08-22

**quick melee on Q, vehicle mode holsters, Nade* tuning names**

QUICK MELEE MOVES F -> Q, and the interesting part is that changing a default
moves nobody. defkeybd.cfg only seeds a NEW autoexec; an autoexec that already
exists keeps what it was written with, and FreshQuickMeleeBound is already 1
on every install that saw 0.10.44, so the runtime branch never runs for them
either. The engine has no unbind command (fact 3), so nothing in the GAME can
take F away. The launcher owns autoexec.cfg as text and is the only thing that
can.

So this ships with KeybindMigration.ApplyQuickMelee - and with a pref flag of
its OWN, because KeybindsMigrated0108 is already true on every install that
has ever run the launcher. Anything added to that pass from now on is dead
code. A migration needs a flag of its own or it is not a migration; first time
that has come up here, so it is written down.

Same refusals as the 0.10.8 pass: untouched-only (still bound exactly once, on
F), and it will not steal Q from anything that already holds it.

ON THE REBIND REPORT ITSELF - the owner reported quick melee would not rebind
off F, and I could not reproduce a QuickMelee-specific fault by reading. The
live autoexec registers the action before it binds it, the command table is 33
rows against NUM_COMMANDS 33 with UNASSIGNED still last, BindingStore's regex
matches the line so Save() rewrites rather than appends, and
EnsureDefaultBindings skips an action already mentioned. The most likely cause
is engine fact 3 and it is not specific to this action: rebinding IN-GAME adds
the new key and nothing can remove the old one. If it was the LAUNCHER that
failed, that is a real bug and none of the above explains it - the handoff
says to read the written file rather than guess a second time.

VEHICLE MODE NOW HOLSTERS. The model was already hidden on transform, but only
cosmetically: the weapon stayed selected, loaded and firable, so dropping out
of vehicle mode was a free instant shot with no tell of any kind. A mech in
vehicle mode has no hands to hold a gun with.

Routed through the holster system rather than given a rule of its own, which
buys three things already built: the refusal is enforced SERVER-side
(HandleWeaponFire returns early while holstered, so a client that ignores the
message still cannot shoot), the third-person model and HUD already follow the
holster state, and coming out plays the select animation - which IS the draw
delay being asked for, rather than a new timer that would have to be kept in
step with the animation.

Forced going in, because it is not a suggestion and a number key must not talk
its way out of it. Coming out is an ordinary unholster, which SetHolstered
downgrades to not-forced by itself. It does override a VOLUNTARY holster
across a transform, which is the right answer for re-entering combat form and
the only place the two systems disagree.

GRENADE* TUNING RENAMED TO NADE*. NadeVelocity, NadeAngle, NadeDrop, NadeFuse,
NadeBounce - they exist to be typed at a console mid-match and the long form
was six characters of nothing. The FRESHTUNE_* macro names moved with them, so
a reader who types what the macro is called is not wrong.

Safe because every one of these defaults to 0 meaning "use the table": a cfg
with the old spellings now tunes NOTHING rather than quietly tuning something
else. Worth naming, because the other half has already happened here - the
Mine* rename of 2026-08-10 REUSED the old Grenade* names for a different
weapon, and a stale cfg silently started tuning the kato. Renaming into a free
namespace and into an occupied one are different operations.

Nothing here is confirmed in play.

---

## 0.10.45 — 2026-08-22

**weapon tuning, mine counter-play, and two sounds**

THE SHOTGUN was never weak - ten pellets at 20 is 200 against a 100-point
person, the biggest burst on foot by a wide margin. It was UNPREDICTABLE, in
both directions: a 60 cone made close range a dice roll, and with no falloff a
lucky convergence across a hall was worth exactly as much as a point-blank
hit. Position did not matter and luck did, which is backwards for the weapon
whose whole job is winning a doorway.

Spread 60 -> 40 makes the close burst reliable. FreshVectorFalloff - the first
distance falloff any hitscan weapon has ever had here - is what stops that
reliability reaching across a room: full damage inside 400 units, a quarter at
its 2000 range, floored rather than zeroed so the far edge stings instead of
becoming a line you can stand behind. Reload 1.60 -> 1.10 because it was the
longest on foot, on the weapon least able to afford losing the second
exchange.

Scoped to the shotgun alone on purpose. Falloff as a blanket hitscan rule
would turn the DMR and the assault rifle into different weapons, and "bullets
weaken with distance" is a simulation nobody asked for. This exists to make
ONE weapon short-ranged, which is what it always claimed to be.

Raising its damage - the obvious reading of "the shotgun feels weak" - was
rejected: it already one-shots people, and that would only have let it do so
from further away.

THE JUGGERNAUT 300 -> 450. Stock had the shredder at 400 against the
juggernaut's 300 - the rapid flak gun hit harder per shot than the heavy
cannon, on top of firing faster and carrying twice the magazine. Cutting the
shredder to 300 in 0.9.60 stopped it out-shooting everything and left the two
EQUAL, which is still wrong: if the slow single-shot and the fast repeater
cost the same per hit, nobody has a reason to carry the slow one. The mech
tier is now a ladder - about five shredder bursts, four and a half juggernaut
rounds, or two red riots.

THE TOW takes 2 in the magazine and 8 carried. One round between reloads is
what stopped it being a movement weapon: you cannot build the habit of
rocket-jumping on something that reloads after every shot.

THE ASSAULT RIFLE bursts when zoomed as well. The old zoomed-SEMI was sound
when the rifle was the only scoped weapon on foot, and stopped being true when
the DMR arrived - a zoomed rifle and a hip-fired DMR were doing the same
thing, and precision is the DMR's job. The scope now only magnifies.

MELEE SCRAPES MINES OFF. A mine on your own hull was the one thing in the game
you could not see, could not shoot and could not walk away from - the only
weapon with no answer at all. All of them come off in one swing, because
three mines and three swings under fire is not a counter, it is a slower
death; the cost is already right, since a swing is seconds of not shooting
back. Removed rather than detonated, for the same reason RemoveMinesFor
removes: scraping a charge off is an act of clearing it, and making that a
suicide would be worse than the problem.

Hooked before the ammo test because it is a property of SWINGING, not of
hitting - you cannot be asked to aim at something you cannot see. Effectively
MCA-only, since a spider mine kills a person on foot rather than sticking.

MINE REMOVAL ON DEATH NOW COVERS BOTS, by moving the call from
CPlayerObj::HandleDead to CBaseCharacter::HandleDead. CFreshBot derives from
BaseAI, so it never took the player path and a bot's mines simply never
cleared. Same shape as the CCharacterMgr fix: the rule was right and only ever
applied to half the people it was written for.

TWO SOUNDS. A small-metal impact when a swing actually takes mines off, silent
when it does not - otherwise the sound stops meaning "you cleared a mine" and
starts meaning "you swung", which the animation already says. And the
launcher's dry-fire click when a detonator press actually fires something,
deliberately NOT the mine's own beep: Timer.wav is a CLUE for whoever is about
to walk into a charge and this is a CONFIRMATION for the person holding the
trigger, and one sound for both would make the warning and the kill
indistinguishable.

NOT DONE, and the handoff says why at length: the laser cannon as an on-foot
railgun. GetWeaponId is tier-aware but GetCommandId is not, so putting a
both-tiers weapon on a different slot per tier breaks the round trip - the
exact failure the sniper rifle already shipped once, documented in the
COMMAND_ID_WEAPON_9 comment. The fix is 14 call sites and not a wrap-up
change.

Nothing here is confirmed in play.

---

## 0.10.44 — 2026-08-22

**quick melee, the .45 carries twenty, HandgunReload removed**

QUICK MELEE. A held key (QuickMelee, command 86, F by default): switch to
melee on press, swing while held, put the previous weapon back on release.

The swing goes through the ORDINARY firing flag rather than a path of its own
- ORed into CS_MFLG_FIRING in the per-frame poll - so the attack, its
animation, its damage and its trip to the server are all the ones a trigger
pull already uses, and there is no second thing to keep in step with the
first.

The release is POLLED as well as handled in OnCommandOff, because OnCommandOff
does nothing at all while a chat line is being edited: opening chat mid-swing
would have swallowed the key-up and left the player swinging at air with their
weapon gone. Same reasoning as rebuild-if-missing over rebuild-on-event
(engine fact 7) - a state that must END has to be checked, not merely
notified. That also covers focus loss, menus, and a bind changed underneath us.

Its own memory slot rather than the melee KEY's: the two can be used in the
same breath, quick melee out of a weapon reached with the melee key, and one
slot would mean whichever finished last decided what you were holding. And the
restore is declined if the player chose something else mid-swing - pressing a
number key is a decision, and quietly undoing it on key-up would be the
surprise this feature must not have.

Registered at runtime as well as in both shipped layouts, per engine fact 3,
so an install whose autoexec predates this gets the key. Preflight caught the
one wiring point I missed (keybind-layout.json), which is what that check is
for.

THE .45 CARRIES TWENTY. Magazine and pickup 14 -> 20, carry 42 -> 60. Fourteen
is the real weapon's magazine, which made it a stock number rather than a
decision; against a 24-round assault rifle and a 32-round MAC-10 the sidearm
spent most of a firefight reloading. Pickup and carry move with it so the table
keeps its doctrine - pickup is one magazine, carry is three.

Note this also changes what a SPAWN hands you: CPlayerObj::Respawn asks for 50
rounds and SetAmmo clamps to carry, so it has been quietly giving 42.

HANDGUNRELOAD REMOVED, with its predicate FreshIsTunedHandgun and the parameter
it occupied in FreshTunedReloadTime. The 2026-08-08 decision already found it
"strictly worse than what replaced it" and deferred only the removal: it
reached exactly two weapons through a special case that Reload13 and Reload18
do without, so it was a second spelling of an answer the per-weapon variables
already gave.

ReloadScale is NOT removed and is not redundant in the same way - it moves all
twenty-three together while preserving their relative shape, which no
per-weapon variable can do.

Nothing here is confirmed in play, and neither is 0.10.43. The handoff carries
both releases' test lists.

---

## 0.10.43 — 2026-08-22

**the dedicated server survives a disconnect; spawning on someone kills them, and gibs**

THE CRASH. A dedicated server with bots died about a second after the last
client left, reproducibly, on 0.10.15 and 0.10.42 alike. Confirmed against the
binary rather than reasoned about:

CBaseCharacter::InitialUpdate adds every character to CCharacterMgr, and the
only Remove in the game sits in CBaseCharacter::RemoveObject() - a path a
PLAYER never takes, because the object is reused across respawns and a
disconnecting client's object is destroyed by the engine. So a player who left
stayed in m_playerList as a pointer to a destroyed object for the rest of the
map. The next AI sense tick called FindVisiblePlayer, got that handle, and
handed it to CreateInterObjectLink, which dereferences the engine record the
engine had already torn down.

Ghidra: server.dll+0x111B7 is inside CreateInterObjectLink, and the
disassembly shows the fault is the SECOND of its two list insertions - the
object from [EBP+0x10], the third parameter, which is hObj2, the AI's new
target. The wrapper at 0x1002451a null-checks both handles, so this was never
a null handle; +0x124 is zeroed in exactly one place in the image, the
teardown path. The one-second gap is the next AI update, not the disconnect.

Fixed with CCharacterMgr::RemoveFromAllLists(), called first thing in
~CBaseCharacter - the one path every character actually takes. It removes by
pointer from all eleven lists and asks the character nothing, because
Remove() picks a list via IsPlayer(m_hObject) and GetCharacterClass() and a
character being destroyed can answer neither. That also closes a quieter hole:
Add() files by class, Remove() looks up by the class the character has NOW,
and CPlayerObj::Respawn resets m_cc. Now engine fact 24, and preflight asserts
both the call and the sweep's completeness (verified by breaking it).

SPAWNING ON SOMEONE. The rule was always meant to be "the one already standing
there dies" - the arriving player cannot see where they are about to be put,
so they cannot be asked to avoid anything. Three defects stopped it working.
The overlap test doubled the OCCUPANT's dims, which is the real test only
while both bodies are the same size, and Shogo multiplayer is mecha and people
in one room; it is now the two half-extents summed. DT_KATO reads as
CD_VAPORIZE, parking a fading body on the spawn point for sixty seconds; the
telefrag now gibs. And FLAG_SOLID is cleared by SetDeathAnimation(), which
runs from the VICTIM's own UpdateAnimation() - so the arriving player
teleported into a still-solid body and got pushed out of the world by the
engine's overlap resolution, which is the camera-through-the-floor. Cleared
immediately now, guarded on the victim actually being dead.

Mecha are killed with DT_MELEE rather than DT_EXPLODE: both gib, but
BodyProp::CreateGibs detonates a gibbed mech with a full-strength bullgut
round at its own feet, and its feet are where the new arrival is standing.

KILL FEED. A telefrag states GUN_TELEFRAG through the existing damage weapon
byte - it is sent by our own code, so nothing downstream has to infer. Ramming
stays inferred, because MID_CRUSH and MID_TOUCHNOTIFY are engine callbacks
with no way to describe themselves. Both render as their own line: "Telefrag"
and "Roadkill".

ROADKILL. A ram that shoves you into geometry was scoring as a suicide and
reading as "died", because the blow that KILLS is the wall's and HandleTouch
names the world as responsible for it. Only a ram lethal on first contact was
ever attributed. CDestructable now remembers who last hit it with DT_IMPACT,
and a DT_IMPACT death with no scoreable killer inside two seconds is credited
to them. The remembered handle is never dereferenced on trust - it is matched
against the live object list first, which matters because this runs on a
death, exactly when things are being removed.

The other half of the same bug: m_nLastDamageWeapon was assigned only when a
message stated a weapon, which made it "the last weapon anyone ever mentioned"
rather than the "most recent damage" its own comment promised. Shoot someone,
then run them down, and the feed credited the rifle. Assigned on every message
now; nothing that travels loses, because a mine's damage names the mine and an
explosion's names its weapon on every tick.

PROJECTILE DIAGNOSTICS. The "weapon N fired" line printed constructor defaults
on every shot ever logged - it lived in InitialUpdate, which the engine runs
at CreateProjectile, and Setup() assigns the id, velocity, radius and lifetime
the line after. Every log said "weapon 0 fired: speed 0, life 5.0s, radius 0"
whatever was fired. It does not look broken, it looks like a finding: "radius
0" is exactly what a rocket doing no splash damage would report. Moved to the
end of Setup().

Added a report for the one silent no-damage path: all splash damage comes from
the Explosion object, gated on RadiusDamageType && eType != ST_SKY, and
Detonate's own damage call only fires when m_fRadius <= 0, so a rocket has no
fallback. If that gate refuses, the rocket goes off, logs detonated=1, and
hurts nobody at all. It now says so, and which half refused.

Also documented in CLAUDE.md: a server-side debug channel cannot be switched
on from a multiplayer client at all - MID_FRESH_DEBUG is accepted only when
GetGameType() == SINGLE - so ProjDebug typed at the game console against a
dedicated server records nothing, in any permutation. That has now cost two
investigations. Use rcon or the server's own console.

Nothing here is confirmed in play.

---

## 0.10.42 — 2026-08-22

**map lists show each custom map's real location; silence the routine custom-map fallback log**

Two related quality-of-life fixes for anyone hosting off custom maps.

MAP LISTS (launcher). The Host tab's Available/Rotation lists showed every
custom map by its bare name, so an operator could not tell an maps\mp map from
a loose Custom\ one from a level packed inside a .rez - all three flatten to
the same bare world on every client (fact 22) and the list threw that
distinction away. MapDisplayConverter now prefixes the display with the real
source: "Custom\maps\mp\MP_ArenaOF", "Custom\MP_ArenaMCA", "Custom\pack.rez\X".
DISPLAY ONLY - the lists still store and the cfg still writes the canonical
bare name the server loads, so nothing about what ships changes. The folder is
not recoverable from the name, so the Host-tab scan (the one place that still
sees which directory each map came from) fills the lookup and clears it each
rescan.

This does NOT extend to FreshSrv's own rotation list: the dedicated server
never scans Custom\, it only has the bare names the cfg hands it, so it has no
folder to show. Giving it one would mean adding a disk scan the server has no
other reason to do - not worth it for an operator's already-configured list.

SERVER LOG (FreshSrv). Every bare custom entry made the server print
'"Custom\X" would not start - started as "X" instead' on start, because the
server probes a synthetic "Custom\<name>" candidate first (some clients
resolve it) and that always fails - bare is canonical. The warning was correct
but fired for every custom map, burying the genuine case. Now silent when the
failed candidate is exactly the "Custom\<bare>" we synthesised; an unexpected
fixup (a retail Worlds\ path that fell back, say) still reports.

---

## 0.10.41 — 2026-08-22

**the custom-map restore degrade now actually fires (menu detection)**

0.10.40 shipped the degrade but it never triggered: the custom detection used
GetWorldInfoString, which answers nothing from the load-saved-game menu where
the restore is triggered, so every custom map read as campaign and the engine
crashed identically. ResolveWorldLoadPath now detects via GetFileList (the
mounted tree, which works from the menu, fact 22), so both the load-path
resolution and the LOAD_RESTORE_GAME -> LOAD_NEW_GAME degrade fire from the
save menu as intended. Built clean, readback verified.

---

## 0.10.40 — 2026-08-22

**custom-SP restore degrades gracefully, and the mine launcher draws empty**

Two cshell fixes since 0.10.39, built clean, preflight green, readback verified:

- Custom-SP save restore no longer crashes. LOAD_RESTORE_GAME of a root-mounted
  custom map faults the closed engine on the first post-restore tick (crash-34132,
  reproduced on SP_Example, so it is the path not the map). It now degrades to a
  fresh LOAD_NEW_GAME - the level restarts from the beginning, with a one-time
  notice - rather than crashing to desktop. Campaign saves untouched.

- The sticky-mine launcher stays drawable at 0 ammo, so its secondary-fire
  detonator is reachable after you have thrown your last mine (ff973ee).

---

## 0.10.39 — 2026-08-22

**SP-map save-restore crash fix (server side) and weapon-roll logging**

Two object-side commits since 0.10.38, reviewed here, built clean, preflight
green, readback verified:

- d0705d9 (shogo-ed): the authoritative half of the custom-SP load fix. The
  client resolver in 8122838 missed the server, so a saved-game restore handed
  HandleLoadGameMsg the campaign-style "worlds\<name>", LoadWorld refused it
  for a root-mounted custom map, and the failed restore left torn state the
  next load walked into as the Client.exe+0x4B8A3 null deref. The server now
  retries with the worlds\ prefix toggled, mirroring the client - and this
  path also covers the dedicated server and rcon. Confirmed from the owner's
  crash-49448 that this was the chain.

- 5ef0db0 (this thread): WeaponDebug logs what each random weapon spot rolls -
  map tier, and per spot what was there and what it became - so "only DMRs and
  TOWs" gets measured rather than guessed from a five-pickup sample.

This packaging run supersedes the hand-deployed ShogoFRESH.rez.pre-srvload
that shogo-ed put on the live install for playtesting.

---

## 0.10.38 — 2026-08-22

**batch: SP-map quickload crash, MP arena tiering, MaxFPS, screenshots button**

The accumulated work since 0.10.37, reviewed here as packaging gatekeeper,
built clean, preflight green, readback verified. Four substantive commits
plus a decisions record, from three threads:

- 8122838 (cshell): custom single-player maps no longer crash on
  quickload/reload/level-change. LoadGame hardcoded a "worlds\" prefix that
  does not resolve for a root-mounted custom map.

- 8e30d8f (object): MP arenas read their mech-vs-foot tier from the SPAWNS
  rather than an "MCA_*" filename prefix and a weapon-id range, both of which
  were unreliable - MP_ArenaMCA (MCA at the END) spawned foot bots and rolled
  the wrong weapon tier, and the id-range test misread the FRESH sniper.
  MapSpawnsAreMech reads any mech spawn mode (MCA_AP..SA, 6, 7); checked
  against all 68 shipped worlds, with the Squishie and TOWs-Out exceptions
  handled. Directly fixes the new demo arenas.

- 4bfbdf5 (launcher): the frame-cap control now writes MaxFPS, which the
  engine's limiter actually reads. It had been writing NetLimitFps/NetFpsLimit,
  which only fill the 1998 host dialog - the launcher's number reached nothing.

- b606aba (launcher): an "Open screenshots folder" button beside the logs one.

Game code changed in cshell and object and the launcher changed, so all five
projects were rebuilt; the readback gate confirms the shipped DLLs match Dist.
This packaging run supersedes the hand-deployed load-fix rez that shogo-ed put
on the live install for playtesting.

---

## 0.10.37 — 2026-08-21

**a visible hint under the mouse-smoothness slider**

The slider had a thorough tooltip but nothing visible, and the label
"smoothness" invites exactly the wrong instinct from a raw-input player: read
it as the thing you disable, reach for the bottom. The bottom is right here -
10 is the floor and the most responsive sane setting - but nothing on screen
said so without a hover.

Adds a dim one-line caption under the slider: "Lower = more responsive.
Floored so a high-polling-rate mouse can't jitter." That makes the direction
legible and confirms the minimum is the responsive end, which is the whole
point - inputrate 0 was never raw input, it was UNFLOORED, letting a 1000 Hz
mouse divide the turn rate by ~1ms. The full mechanism stays in the tooltip.

Keeps the game's own label ("smoothness" = IDS_MOUSE_INPUTRATE) rather than
renaming, so the launcher and the retail menu still agree; the fix for "the
name means something else" is making the direction visible, not a new noun.
The smoothness row goes Auto-height to fit the caption; the floor of 10 is
untouched, so check_mouse_smoothness still passes.

Launcher-only; DLLs rebuilt only to report the version. Readback verified.

---

## 0.10.36 — 2026-08-21

**package another thread's mouse-smoothness slider**

Version bump and release for one launcher-only commit reviewed here as the
packaging gatekeeper. No game DLL logic changed; the DLLs are rebuilt only so
they report the new version. The launcher changed under 0.10.35 without a
bump, so this gives the new launcher its own release rather than a second zip
claiming to be 0.10.35.

The change (b887ff3, that thread's work, sound and confirmed by them): the
"Raw mouse input" checkbox is replaced by a single "smoothness" slider,
10-40ms, floored at 10. inputrate 0 was never raw input - it is UNFLOORED
input, letting a high-Hz mouse divide the turn rate by ~1ms, which the owner
could feel. The floor is clamped in the setter, the save and the first read of
an existing autoexec, and check_mouse_smoothness asserts it agrees across the
seed, the C# constants and the XAML slider - verified by breaking it both
ways. It also uses the game's own label (IDS_MOUSE_INPUTRATE = "smoothness")
rather than the launcher's wrong rename.

Reviewed, built clean, preflight green (the new check passes), readback
verified.

---

## 0.10.35 — 2026-08-21

**package another thread's LOD/inputrate, SP-spawn and decal work**

Version bump and release for six commits from a parallel thread, reviewed
here as the packaging gatekeeper. The game DLLs changed under 0.10.34 without
a version bump, so the shipped 0.10.34 zip no longer matches the tree - this
gives the new DLLs their own release.

The substantive game-code changes, both confirmed in play by that thread:

- Custom SP maps spawn on foot. LoadWorld/DoLoadWorld does not establish an
  NGT_SINGLE game, so FindStartPoint's game-type filter matched nothing and
  the player defaulted to mech mode. FindStartPoint now falls back to every
  GameStartPoint when the filter is empty, honouring the map's own PlayerMode.
  Server-side; a real game-type match still wins, so the campaign is untouched.

- No decals on destructibles or movables from your own shots. AddLocalImpactFX
  set bBreakable now (it already declined the bullet mark on movables but let
  the blast scorch through), and CreateBlastMark is gated on WFX_MARK so the
  movable suppression reaches the scorch. No wire-format change.

Plus the LOD/inputrate investigation the owner has been testing: inputrate
seeded at 30 (Monolith's value, previously overwritten), the LodScale claim
withdrawn after the zoom was found to change two things. Docs NETRATES and
RENDERVARS updated with those findings.

Reviewed, built clean, preflight green, readback verified.

---

## 0.10.34 — 2026-08-19

**the energy-grenade mine stops flashing on landing and eating its own explosion**

M1/M2 fixed, root cause exactly where the owner's observation pointed.

Three client sites route a projectile as SERVER-DRIVEN instead of
client-local: WeaponModel::DoProjectile skips the local copy,
CProjectileFX::CreateObject shows the server's object for the shooter, and
CWeaponFX::CreateObject declines to suppress the server's arriving detonation
FX. All three listed GUN_SPIDER_ID and GUN_KATOGRENADE_ID by hand.

The energy grenade fires the spider's own class but carries
GUN_ENERGYGRENADE_ID - CProjectile::Setup sets m_nId = pWeapon->GetId() - and
it was added to FreshIsThrownGrenade without being added to any of the three.
So for the shooter it got a local copy that flew and detonated on IMPACT (the
flash on landing, M1) while the server's real detonation FX was suppressed as
a duplicate that had already played (damage with no explosion, M2). Two
symptoms, one omission.

Fixed with one shared predicate - FreshIsServerDrivenProjectile (SPIDER,
KATO, ENERGYGRENADE) - at all three sites, so the set lives in one place and
the three cannot drift from each other again. That drift is exactly what
caused this: the concept was implemented four times (FreshIsThrownGrenade
plus three hand-coded pairs) and the rework updated one.

check_server_driven_projectile fails on any future hand-coded SPIDER/KATO
pair, verified by reintroducing one and watching it fire. The energy-grenade
mine now behaves identically to the mech spider, which was always correct.

Symptom 3 (0.10.33) already clears a dead player's mines; with these two the
weapon is whole.

---

## 0.10.33 — 2026-08-19

**a dead player's mines are cleared, and the mine visual bug is pinned**

Symptom 3 built (M3): CStickyGrenadeProjectile::RemoveMinesFor walks the mine
list on player death and REMOVES - not detonates - every undetonated thrown
mine whose owner is the dead player. Called from CPlayerObj::HandleDead,
which is gated != PS_DEAD so it fires once. Removed rather than set off
because dying is not a trigger the player chose, and a corpse detonating its
own charges reads as a bug. Same list-walk EnforceMineLimit and
DetonateForPlayer use, for the same reason: a registry leaks a slot on any
path it does not cover; the walk cannot go stale.

M1/M2 diagnosed but NOT fixed - the owner's observation settled the shape.
"I take damage but the visual explosion does not play, presumably since it
already played when it landed." Decisive: the detonation VISUAL draws at
LANDING, not at detonation. On impact the client detonates locally (flash)
while the server attaches its authoritative mine (stays) - M1. On trigger the
server really detonates, radius damage fires and the object is removed, but
the firer's client suppresses the server's arriving FX because it already
"detonated" on landing - damage, no visual - M2. One multiplayer FX-routing
bug: the spider mine is handled as a LOCAL projectile when it must be
server-driven. The fix location is the CreateObject-by-name exemption that was
meant to make kato and spider server-driven and does not cover the energy
grenade's local-detonate-on-impact path. Left for the next pass.

Recorded a diagnostic trap that cost real time: ProjDebug is server-side and
its switch travels by MID_FRESH_DEBUG built from the CLIENT mask. Enable it
with plain "ProjDebug 1" on the client - it reaches a remote dedicated
server. "serv ProjDebug 1" does NOT: serv cannot reach a separate-process
dedicated server, and the server reads the traveling mask rather than its own
convar. An empty server log after serv is that trap, not a quiet weapon.

---

## 0.10.32 — 2026-08-18

**competitive host defaults, added maps highlight, and E7 confirmed from a dump**

Two shippable changes plus documentation of a crash the owner captured.

HOST DEFAULTS. A new host now starts on the intended flagship ruleset:
infinite ammo on sidearms only, Red Riot (8) and Squeaky Toy (21) blocked,
frag limit 45 and a 15-minute time limit both on, FirstPersonOnly on. Set in
the shipped server-settings.cfg seed and the launcher's absent-key fallbacks,
so it reaches new configs and configs missing a key - never an existing
launcher-written config, which carries explicit values for all of them.
Reasoning in decisions.md.

ADDED MAPS ARE SELECTED in the rotation list, scrolled into view, so adding
to a long rotation shows what happened and where. Skipped duplicates are not
selected, so the highlight always means "new".

E7 FIFTH OCCURRENCE, and the first parsed from the minidump rather than a
text report. On a CLEAN machine in multiplayer, v0.10.21. The faulting
thread's stack is the music middleware - IMUSIC25 +0x3721, MSynth25 +0x20CE,
IMRT25 +0x30F8/+0x1831/+0x1782 - calling through a garbage pointer into
unmapped memory. The dump's module list shows ima.dll, AM18, IMUSIC25,
MSynth25, IMRT25 and mss32 ALL loaded with MusicInMultiplayer at its OFF
default, which proves what the earlier entries could only infer: the gate
skips InitPlayLists but never stops CMusic::Init loading the middleware and
starting its thread. DisableMusic 1 is under test by the owner. See BUGS.md.

Two launcher keybind findings recorded, NOT fixed, because each needs input:
A13, a FRESH keybind row absent until the game has registered the action in
autoexec (needs a repro data point); A14, the shipped keybind-layout.json and
the C# Default() have drifted on which actions are hidden (needs the owner's
intent on whether those four are meant to be hidden). Neither is the reported
SecondaryHold row directly - the diagnosis is in the entries.

---

## 0.10.31 — 2026-08-18

**the yaw a remote player turns in stops being 1.41 degrees**

A player's facing has always crossed the wire as ONE BYTE.
CompressRotationByte puts the whole circle in a signed char, so the finest turn
anyone else can see you make is 2*PI/255 = 1.41 degrees. Raising CSendRate from
7 to 30 made those steps arrive more often; it could not make them smaller, and
a step is what reads as choppy. On a 21:9 display it is worse for a reason with
nothing to do with the network: ~32 pixels per degree against 16:9's ~21, so the
same 1.41 degrees moves the image half again as far.

The client now appends a 16-bit yaw - 0.0055 degrees per step - guarded by a new
CLIENTUPDATE_PLAYERROT16 bit. Appended PAST the position block, not written
beside the byte, because these fields are positional: put it next to the byte
and every later field moves, which an older server would read as a weapon
rotation made of position bytes. The bit, not a length check, is what makes it
safe to read - a stock client never sets it, and reading past the end is not a
harmless zero for every type.

The byte still goes out. This refines it rather than replacing it, so a server
that predates the bit sees exactly what it always did.

THE SECOND HALF IS UNVERIFIED and the comment in PlayerObj says so. The player
object carries FLAG_YROTATION, which is 1998 asking the engine to send this
object's rotation to other clients in one byte - the same 1.41 degrees again, on
the hop we do not own. FLAG_FULLPOSITIONRES is now set alongside it, which the
SDK header describes as "some things must be exact". Whether the two combine or
YROTATION's byte wins is not something the headers answer. Watch a remote player
turn slowly: if the body still steps, it wins and that flag is doing nothing,
and the fix is only half a fix.

ValidateQuickTurn deliberately still reads the byte. It is calibrated in byte
steps and feeding it a finer unit would quietly change what it calls a turn.

---

## 0.10.30 — 2026-08-17

**+hostport reaches the launcher's host path, and the box shows it**

Two reports, and the second explains why the first was possible.

A12: "+hostport" WAS BEING THROWN AWAY on the launcher's path.
s_nFreshHostPort was set only inside NetStart_FreshHostWorld, which runs only
when "+hostworld" is given - the editor's path. The launcher passes
"+FreshHost 1" with no world and lands in NetStart_FreshHostAuto, so the
override was read off the command line into a local and discarded. The game
bound the config's port while the launcher's status line named a different
one.

It hid behind a change made two builds earlier. The "a server is already
running" dialog had just been suppressed for listen hosts BECAUSE this
override was supposed to make that collision impossible. Two halves of one
feature, each assuming the other worked - and the warning that would have
caught the failure was removed by the same reasoning that broke it. That is
the whole lesson: suppressing a warning on the strength of a fix means the
fix had better be reachable from the path the warning covered.

Fixed by setting the port where it is READ - NetStart_FreshHostPort, called
unconditionally before either branch - rather than inside one of two callers.

+HOSTBOTS HAS THE IDENTICAL SHAPE and is fixed at the same time. Same
static, same single writer, same reader in the Auto path. Latent rather than
live, because the launcher does not send +hostbots today, and that is
exactly the reason to close it now instead of after somebody spends an
evening on it. The port version cost one.

THE PORT BOX NOW FOLLOWS THE MODE. Selecting Listen moves 27888 to 27889 and
selecting Dedicated moves it back, so the number on screen is the number
that will be used and the number you would give somebody to connect to. The
launcher no longer applies a silent +1 at launch and explains it afterwards.

Only the two defaults flip, and symmetrically, so a port somebody actually
chose is never touched. FreeListenPort stays as the collision backstop and
is now the only thing that can move a port without being asked - which is
also the only case the status line still reports.

I argued for the old behaviour a few hours ago on the grounds that the field
is the SETTING. That was true and it was the wrong call: the field's whole
job is to tell you where the server is.

---

## 0.10.29 — 2026-08-17

**the reload window decides, not the artwork; three more values retire**

Three dialled numbers into the table, and the sidenote turned out to be a
real bug sitting next to the one fixed yesterday.

RETIRED: pulse rifle 1.40 -> 0.80 and bullgut 1.80 -> 1.55, both dialled in
a mech. Assault rifle 0.60 -> 0.75, which is a straight re-tune rather than
a first measurement - it has no reload animation, so unlike the animated
weapons its number always ran. Laser cannon is now the ONLY one of A9's four
still unmeasured.

A10, AND IT PREDATES A9. "Juggernaut and shredder seem to fire their fire
animations while going through the reloading animation" - they did.
PlayReloadAnimation returns DFALSE the instant m_nReloadAni is INVALID_ANI,
so a weapon with no reload artwork left W_RELOADING on its FIRST FRAME and
UpdateFiring went straight to PlayFireAnimation. The fire animation then ran
for the whole reload while the deadline quietly refused every shot.
Juggernaut at 2.20s and shredder at 1.60s are just the two where it lasts
long enough to see.

The old code exited on "no reload animation" exactly as eagerly, so this is
not fallout from A9 - A9 only made the deadline long enough to notice. Which
is the usual pattern here: a fix does not create the next bug, it makes it
visible.

One rule now covers both faults. THE RELOAD WINDOW DECIDES, NOT THE ARTWORK:
stay in W_RELOADING until IsReloadWindowOver, and hold the idle pose when
there is no reload art to show. That subsumes yesterday's "whichever comes
first" and is easier to state, which is usually the sign it was the right
rule to begin with.

HoldReloadPose rather than PlayIdleAnimation, deliberately: the latter sits
behind a random idle timer and will not reliably displace a fire animation
on the frame you need it to.

A11 RECORDED, NOT CHASED. The assault rifle possibly firing its first bursts
too fast after a reload wants re-testing on this build first, because A10's
fix plausibly changes it - that weapon has no reload animation, so it used
to leave W_RELOADING immediately and now stays for its full 0.75s. The burst
machinery is running in a different state than it was when this was seen.
Diagnosing it against the old behaviour would be answering a question that
no longer exists.

---

## 0.10.28 — 2026-08-17

**MAC-10 reload retires at 0.80, so neither number needs typing again**

Both dialled values go into the table, which is where they stop resetting.
"serv Reload18 0.8" sets a RUNTIME server console variable and nothing
persists it - that is the whole design of the tuning dials, and the last
step has always been retiring the answer here.

MAC-10 0.50 -> 0.80. Same story as the .45's 1.10: set in 0.10.3, when it
could only ever move the moment the weapon fires again and never the
animation, so it was chosen against behaviour that did not exist. A9 made it
real and 0.80 is what survived play.

It stays the weapon that made A9 findable, and the comment now says so: at
0.50 its animation and its table value were close enough that nothing
visible was being cut, so it behaved correctly beside a .45 that did not,
and that contrast read as a per-weapon quirk rather than a missing branch.

The .45 was already retired at 1.70 in 0.10.27, so a fresh install now has
both without typing anything.

---

## 0.10.27 — 2026-08-17

**the .45 reload retires at 1.70, and three siblings are still unmeasured**

A9 is confirmed working in play: the .45's reload delay adjusts. The first
thing that adjustment revealed is that the number it was adjusting from had
never run.

Until A9, the four weapons with an authored reload animation ignored the
table entirely - the animation held W_RELOADING and outlasted whatever the
table said. So those four values were INERT. Fixing A9 made all four live at
once, and 1.10 turned out to be cutting roughly six tenths off the .45's
animation. Nobody chose that; it is the first time the number has been in
force.

1.70, dialled in with Reload13 and retired here, which is what the tuning
variables are for. It sits a hair under the animation: enough to trim the
dead tail - the stretch where the gun is visibly finished and the weapon
still will not fire - without cutting anything the eye reads as part of the
reload. That tail is the whole reason to cut, and it is invisible from the
code. Only playing it finds the number.

THE OTHER THREE ARE UNKNOWN AND NOW MATTER. Pulse rifle (1.40) and laser
cannon (1.50) are in exactly the position the .45 was: numbers that never
ran, against animations of unmeasured length. Rows added to Session 1B for
both, plus a control on the assault rifle - no animation, so lengthening it
worked before A9 and must still work; if that broke, the new escape is
firing too eagerly.

The MAC-10 is the one A9 did not change, confirmed in play. Its animation is
about 0.50 long, so nothing was being cut before and nothing is now - and
that coincidence is exactly what made A9 findable, since a correct MAC-10
beside a wrong .45 read as a per-weapon quirk rather than a missing branch.

Also corrects a comment that was wrong rather than merely stale. The MAC-10
entry claimed the animation "still runs at its authored length - what moves
is when the weapon will fire again". That was the intent and it was false:
the fire key lives inside the FIRE animation, so while the reload animation
held the state, nothing could fire either. Both halves move now.

Recorded: Reload13 0 returns to the TABLE value, not the uncut animation. 0
has always meant "use the table" and there is no spelling that means "play
the whole animation" - flagged in the runbook in case that is wanted as a
distinct setting.

---

## 0.10.26 — 2026-08-17

**the reload tail gets cut, the way the weapon draw already did**

Session 1B did exactly what it was written to do: it cleared the number and
found the thing nobody had listed.

The tuning was never at fault, and one pass proved it. serv Reload13 0.30 on
the .45 reported "table 1.10, Reload13=0.30 -> 0.30" on BOTH sides -
arrived, unclamped, client and server in agreement - and the weapon still
felt exactly as slow. Which is precisely the case the two-sided print was
added for a build earlier: with only the server's line, "the number is
right" and "the number is right on one side" look identical.

UpdateModelState left W_RELOADING only when PlayReloadAnimation() ran out.
The animation, not the deadline. Beside it:

    case W_SELECT:     if (!PlaySelectAnimation() || IsEquipWindowOver())
    case W_RELOADING:  if (!PlayReloadAnimation())

THE EQUIP PATH HAD ALREADY SOLVED THIS. Its comment states the principle in
full - there is no playback-rate control (engine fact 5), so the tail gets
cut rather than sped up - and the reload path was simply never given the
same escape. On any weapon whose reload animation outlasts its reload time,
the state stayed, the animation stayed, and the FIRE string key that lives
inside the FIRE animation never came round, whatever the deadline said.

IsReloadWindowOver() mirrors IsEquipWindowOver(). Both W_RELOADING cases
take it.

Why the MAC-10 looked fine and the .45 did not is the detail that made this
findable at all: the MAC-10's table value of 0.50 happens to match its
animation, so nothing was ever being cut there. The .45 at 1.10 did not, and
asking for 0.30 asked for a cut that could not happen. One weapon with a
coincidence next to one without made the bug look like a per-weapon quirk
rather than a missing branch.

The general shape is worth keeping. Both fire gates were deadline-backed and
CORRECT - CWeapon::Fire and CWeaponModel::Fire each check m_fReloadEndTime,
and each has a comment explaining why a deadline rather than a state. A
third gate nobody had enumerated, the animation state machine, outranked
them both. Two right answers and one forgotten one reads from the outside as
"the variable does not work".

---

## 0.10.25 — 2026-08-17

**the reload diagnostic reports both sides, because only one was**

Session 1B asks the player to read the reload line and not form a view
first. Checking the line existed before sending them to look for it turned
up that half of it did not.

ObjectDLL/Weapon.cpp has printed the SERVER's answer since the tuning went
in - table, per-weapon variable, scale, and which won. That settles "did the
variable arrive". It cannot settle the question 1B is actually about.

The server owns the CLIP and the client owns the ANIMATION, and they have to
end together. Both read through GetSConValueFloat, the server console
mirror, and a mirror read that finds nothing leaves the value at 0 - which
FreshTunedReloadTime correctly treats as "not set" and answers with the
table. So a client that cannot see the variable does not error. It quietly
uses a different duration from the server, and the reload looks finished
before the weapon will fire.

That is a real failure mode, it is silent on both sides, and one number
could never show it. The client now prints the same line tagged "client"; in
single player, which is how 1B is run, both land in the same console.

The runbook now says to compare the two "-> " figures FIRST: if they differ
that is the bug and nothing else in the section matters, and if they agree
the number is settled and what remains is the animation. Which is the
distinction the section was written to make and could not previously make.

---

## 0.10.24 — 2026-08-17

**a listen server defaults one port up instead of recovering from a clash**

Session 1A and 1A-bis both pass; 1A's first two rows are retired because the
in-game controls menu is gone, not merely unmaintained. MainMenu.cpp builds
Campaign / Credits / Quit and Options is not on it, so there is no second
place to rebind and nothing to reconcile with the launcher's layout. (The
keyboard, mouse and joystick sub-menus are still initialised behind that
removed door - harmless, building surfaces nobody can reach, worth a tidy
some day.)

THE PORT CHANGE. 0.10.19 taught the launcher to detect a busy port and move;
this stops the clash happening. A listen server now defaults to 27889, one
above the dedicated default, so the two kinds of host on a machine are not
competing for the same socket in the first place.

Avoiding a collision beats recovering from one, and the recovery stays as
the backstop - occupy 27889 too and the search still walks up and still
reports where it landed. What changes is that the ordinary case, a mapper
with a dedicated server already running, no longer produces a port move to
explain.

It also makes the launcher agree with the editor rather than inventing a
third convention: TrenchBroom's build-and-host already picks 27889 for
exactly this reason.

"Configured" is unambiguous here, which is what makes the default safe to
change. WriteConfig stores 27888 as 0 ("Port 0 means default"), so a config
never holds 27888 explicitly - HostPort == 27888 can only mean the player
left it alone. Any other value is a deliberate choice and is used as given.

The status line now distinguishes the two cases rather than reporting a
collision that did not happen: it says a listen server uses 27889 to stay
clear of a dedicated one, and separately reports a real move if 27889 was
taken as well.

---

## 0.10.23 — 2026-08-17

**a weapon cycle stops being read as a deliberate re-press**

Session 0 passed all seven rows on 0.10.22 and turned up one bug on row 5:
"while switching weapons, the zoom randomly activates". The mouse wheel
detail, given afterwards, is what made it findable - and changed the fix.

Stock CWeaponModel::ChangeWeapon has always carried a re-select toggle,
"some weapons have toggles if selected again", gated on CanWeaponZoom. Two
things had quietly widened what that one branch catches.

THE REPORTED ROUTE IS THE WHEEL. ChangeToNextWeapon/ChangeToPrevWeapon
resolve a target and then come through the same ChangeWeapon as a number
key. So whenever the cycle had nowhere else to go and handed back the weapon
already held, the branch read it as a deliberate re-press and zoomed.
Nothing about a cycle carries that intent - it is the cycle finding nothing
to switch to, and the right answer is to do nothing. The three cycle entry
points now pass bAllowZoomToggle = DFALSE.

THE SECOND ROUTE IS THE WIDENED SET, and I would not have looked without the
first. In 1998 exactly two weapons could zoom, both marksman tools nobody
re-presses by accident. FRESH grew that to five, three of them mech weapons
cycled constantly in a fight, and the gate followed silently.
CanWeaponZoomOnReselect keeps the number-key toggle on the 1998 pair. The
dedicated secondary key still zooms everything that can zoom - that is the
route designed for it, and this was only the number key doing it as a side
effect.

That second half is a feel change nobody reported, so it is easy to revert
on its own if a mech pilot wants the double-tap scope back.

ENGINE FACT 20, IN A DIFFERENT SYSTEM. A stock MECHANISM is not
automatically stock BEHAVIOUR once a new table feeds it. The magazine sizes
went exactly this way - real 1998 numbers that invented behaviour the moment
a reload system read them. Worth a sweep for anywhere else FRESH has widened
a set that an older gate reads.

ALSO: the on-foot body picker is hidden, not deleted. Sanjuro is the only
entry since the Trooper row came out, and one entry is not a picker - it
offers a choice that does not exist. Restoring it is a content job (retarget
a body onto Sanjuro's 64 animations, then actually ship it in
Launcher/Redist/shogofresh); the binding, the prefs round-trip and the
server-side index all still work, so it is one Visibility change the day a
second body exists. Session 2G stays marked BLOCKED to match.

Session 0 is recorded as passed in the runbook, with row 5 annotated to be
re-tested on this build.

---

## 0.10.22 — 2026-08-17

**cutscenes stop being the only thing in the game running Vert-minus**

Ships the ultrawide pair reported from play on 32:9, plus another thread's
Trooper removal committed above.

The cutscene FOV was pinned at 90 degrees horizontal with the vertical
derived from it, so the wider the display the less of the shot you saw:

    screen      old h/v          new h/v          gameplay h
    4:3          90.00 / 53.13    90.00 / 53.13    90.00
    16:9         90.00 / 48.46   106.26 / 61.93   106.26
    21:9         90.00 / 37.03   121.66 / 61.93   121.66
    32:9         90.00 / 25.36   138.89 / 61.93   138.89

25 degrees of vertical at 32:9, against 53 at 4:3. Gameplay has always been
Hor+ and says so in a comment, so a cutscene was framed tighter than the
game it interrupts. They now agree exactly at every aspect, from one
reference rather than two constants. 4:3 is arithmetically unchanged.

16:9 moves too. Same bug, less visible, and worth saying out loud: every
widescreen player has been watching tighter cutscenes than they were
playing, not just ultrawide ones.

The intro video is exposed rather than fixed - playback is inside the closed
engine - but a failure now retries unscaled and reports on FocusDebug
instead of being indistinguishable from a missing file.

---

## 0.10.21 — 2026-08-13

**the chase crosshair marks the ray the bullet actually takes**

Packages another thread's crosshair fix, which is the good kind: it replaces
an assumption about where the shot starts with the line the server already
had in it.

The client sends m_vFlashPos, offset from the CAMERA, and in third person
PlayerObj.cpp throws it away and substitutes HandHeldWeaponFirePos() - the
GUN_HAND node of the animated model. Stock code, since 1998. So the
projection added in 0.10.13 was marking a point the shot never passed
through: right about the parallax, wrong about which ray had it. That is
both reported symptoms at once - crosshair disagreeing with impacts, and the
two views disagreeing with each other.

Verified the mirror rather than taking it on trust, since that is the whole
claim. GetHandFirePos matches HandHeldWeaponFirePos operation for operation:
node transform, then the flash offset along the NODE's F/R/U axes in the
same order. m_pHandName really is "GUN_HAND" on CBaseCharacter, and the
"turret_node" case is Vehicle, which has its own override - so the client's
fallback to the first-person muzzle is right there rather than merely safe.
And the offset genuinely is one table: PVWeaponModel.cpp:240 calls the same
Shared/WeaponDefs.h inline the client does.

Expect the crosshair to MOVE with the firing animation. The origin is an
animated node, so that is faithful rather than jitter, and it is the likely
reason stock's world-space crosshair was remembered as woefully inaccurate
rather than merely offset - no constant could have stood in for it.

ONE DEFECT FOUND IN REVIEW, recorded as BUGS.md I1 rather than fixed here,
because it belongs with the rest of that feature while it is fresh. The two
sides read one table with DIFFERENT arguments - the server passes
m_pParent->GetSize(), the client takes the MS_NORMAL default - and the
offset is scaled by size, 0.2x for MS_SMALL. Squishie is the live case; mech
mode has the 5x version and is already excluded by the IsVehicleMode guard.
Strictly better than before regardless, since the old behaviour was wrong at
every scale.

The general shape is worth more than the bug: one shared table is not one
shared answer. A defaulted parameter lets two callers read the same function
and still disagree, and a duplication check cannot see it - both sides
honestly do call the same code.

Also notes that d41ab17's message describes LightTest as if it landed there;
it shipped in b61a956. The code is not duplicated - only GetHandFirePos was
added - and the message carries real new research either way: the binaries
cannot answer the light-cap question, with no limit-shaped message, no
counting variable and no fixed light array anywhere in the three.

---

## 0.10.20 — 2026-08-13

**the pickup light stops sinking into the floor it is lighting**

Reported from play. The item rests PICKUP_HOVER_HEIGHT (24) above the floor
and the light hung 18 below it, which put the light 6 units up - close
enough to read as sunk into the ground, and close enough that most of a
point light's lower hemisphere was spent lighting the inside of the floor.
Drop is now 10, so it sits 14 up: still under the item, so the pool lands
on the floor where it belongs, but clear of the surface.

The two numbers live in different files and only their DIFFERENCE matters,
which is the usual hazard here, so the constant now says so.

Worth recording what this investigation ruled OUT, because the suspicion
was reasonable and the answer is in neither file's obvious place.

Pickups have no physical collision and never did. Their flags are
FLAG_VISIBLE | FLAG_TOUCH_NOTIFY - TOUCH_NOTIFY is the pickup trigger, not
solidity, and FLAG_SOLID is never set. So a pickup cannot be caught on a
wall or a floor, and collision is not what makes one stop spinning. They
also already float deliberately: a downward IntersectSegment puts each item
a fixed height above whatever it finds, done server-side because the object
the player walks into has to be the one that moved.

The stalling spin has a different cause and it is already fixed in this
build: m_bBouncing was being set alongside m_bRotate, which put the object
on a 0.001s server update. The bounce is drawn client-side so that update
bought nothing, but a server updating a thousand times a second is a server
continuously re-sending position and rotation on top of the rotation the
client just applied. Two things moving one object, and the visible result
is a spin that stalls.

Two cases can still stall and are worth knowing before the next report: a
level that genuinely sets Bouncing still takes the 0.001s update, and under
Classic rules pickups spin only where the author ticked Rotate, which
defaults off. A Classic server showing still pickups is behaving correctly.

Also carries two threads' DTX work, reviewed here and both good. 68fb5bd
deletes a verification section that could never have passed - DTXFORMAT.md
promised 4,921 byte-identical round trips, the real number is 0, and the
claim survived because the test it prescribed was too expensive for anyone
to run. Replacing it with a table of which test proves which property is
the right correction; a check that cannot pass reads as coverage. 8f0cf9e
then makes --from carry mip DEPTH, so a replacement texture comes back the
same shape as the one it replaces.

---

## 0.10.19 — 2026-08-13

**the launcher stops sending listen games at a port that is taken**

The game gained "+hostport" this session because the editor's build-and-host
collided with a dedicated server on the same machine. The launcher has the
identical bug and it is the half more people will hit: Play + Host and
FreshSrv.exe read the SAME ShogoSrv.cfg, so hosting while a server is
running asks for a bound port, the game dies with "Unable to bind to the
requested port", and the launcher's status line cheerfully names the port it
just failed on. Two things wrong, and the second is worse - it is the one
that makes the first hard to diagnose.

FreeListenPort asks the network stack which UDP ports are bound and walks
up from the configured one. Then "+hostport" carries the answer, and the
status line says the port moved and why.

Asking the stack rather than looking for a server PROCESS is the point, and
the difference is the trap. The running server read its port when IT
started; the launcher may have rewritten the config since, so the config no
longer says where that server is - a process check would find the server
and still get the port wrong. The stack knows. It also catches whatever
else happens to be on the port, which no amount of reasoning about our own
processes would.

Candidates must clear the query port too (gamePort + 149), or the server
comes up unable to answer the browser and nothing says why.

When the wanted port is free it returns it unchanged and no override is
passed at all, so the normal case is byte-identical to before. On any
failure it also returns the wanted port: better to let the game report a
bind error than to silently host somewhere the player was never told about.

Verified the load-bearing assumption rather than trusting it - bound a UDP
socket on 27888 and confirmed GetActiveUdpListeners reports it, then closed
it and confirmed it stops. That API seeing the bind is the whole mechanism.

Also documents LightTest in the console table and, beside the ten-light
claim itself, says outright that it is asserted in several places and
sourced in none - with the command that settles it.

---

## 0.10.18 — 2026-08-11

**the chase camera becomes adjustable in all four axes**

Follow-ups to the camera fix, from a play session that confirmed 0.10.17
worked.

ThirdPersonPitch joins the family: how far up and down third person may
look, default 70 where stock hardcoded 45. That 45 was written for a camera
29 units back, which swings into the floor within a few degrees of pitch;
the camera is a variable now, so the number protecting it is one too.
Capped at 89, because past that it flips.

ThirdPersonShoulder is not new but this is effectively its first working
build - it existed and did nothing, because the solver's cache ignored it
exactly as it ignored ThirdPersonDist until 0.10.17. It is the lever for the
over-the-shoulder view the owner is aiming at.

The vehicle-mode crosshair no longer pins itself to the scenery. It had no
weapon to trace from and traced from a stale muzzle position instead,
hitting the same surface every frame.

The melee key toggles back to the weapon it put away, and the squeaky toy
shows neither an ammo panel nor a weapon name until it is given a job worth
counting.

Nothing here is confirmed in play. The one judgement call wanted back is
where ThirdPersonPitch stops being usable before the camera starts finding
floors and ceilings.

---

## 0.10.17 — 2026-08-11

**the camera arrives, and the weapon name clears the figures**

The chase camera finally goes where ThirdPersonDist sends it. Two faults
stacked: the solver was answering from a cache keyed only on the target, so
a console variable changed while standing still never recomputed; and
AttachToObject re-seeded the camera position every frame, so the smoothing
started from zero each time and the camera reached about a fifth of whatever
was asked for. The residue moving with frame time was the jitter. Both were
found by reading CamDebug's output rather than the source, which is the
third time this week that has been the faster route.

A jump over 500 units is now a cut rather than a slide, so a respawn or a
level start does not send the camera across the map - a consequence of the
position persisting, which it did not before.

Also: the HUD weapon name lifts clear of the ammo figures, 13 to 24. The gap
is measured from the figures' centre so HudNumberY carries the name with
them, which meant half the digits' height was spent before the name began.

Nothing here is confirmed in play.

---

## 0.10.16 — 2026-08-11

**+hostworld actually hosts, and the weapon name actually draws**

Two threads' work plus one correction of my own.

+hostworld could never have worked. The first version filled in NetGame and
NetHost by hand and called StartGame(STARTGAME_HOST) without selecting a
network SERVICE, which hosting requires. It now routes through
NetStart_FreshHostAuto, which also brings back player identity, the session
string and the rules pushed over with "serv" - all quietly dropped by the
hand-rolled path and each of which would otherwise have been found one at a
time.

Worth recording why it cost an afternoon: RiotStartup answers a missing
service and a bad world name with the SAME IDS_NOLOADLEVEL, so the symptom
pointed at spelling. Driving the game directly, one spelling per launch,
showed the name had been bare and correct the whole time - engine fact 22
was right and a stale comment on dead code in NetStart.cpp was what argued
otherwise.

Bots move to "+hostbots <n>", because BotAddNpc is a SERVER console variable
and "+BotAddNpc 4" on a command line set the client's copy where nothing
reads it. Engine fact 17, third appearance in a week.

The HUD weapon name never drew: the guard was "> GUN_NONE" and GUN_NONE is
50, above every real weapon id rather than below. False for everything.
IsRealWeapon now, the helper the kill feed already trusts.

E5 reopens with a switch rather than a third guess - FreshSrv.exe
-nocomposite - and G1 loses its +hostworld half, because the rework removed
the hand-rolled GetConsoleString calls and closed that door by accident.

---

## 0.10.15 — 2026-08-11

**the weapon name, the ruleset, and the host tab tidied**

Client:
  - the equipped weapon's name above the ammo figures, italic and at the
    kill feed's size, on foot and in a mech. Positioned from the figures'
    centre so HudNumberY carries it, and absent for melee where the ammo
    readout is already hidden.
  - the scoreboard map line names the ruleset beside the mode:
    OF_IKARI (FRESH Deathmatch). No protocol work - g_nRuleset already
    arrives via MID_SERVER_RULES, so it is the SERVER's ruleset rather than
    the joining player's.
  - CamDebug now reports raw versus solved distance and which clamp moved
    it, so "ThirdPersonDist does almost nothing" has three distinguishable
    answers instead of one.

Launcher:
  - infinite ammo sits directly above randomize pickups, shaped the same,
    so the two read as a pair.
  - the gravity checkbox is gone - it was pure derivation of the number
    beside it - and the field defaults to 2000, the engine's own value.
  - archive lists hide the .rez extension. DisplayName only: Name still
    builds the -rez command line and still gates the contents viewer.
  - the ruleset tooltip leads with FRESH and calls it FRESH.
  - keybind order moved. This one needs the AppData layout deleted before
    it can show, which is why two previous attempts appeared to do nothing.

Nothing here is confirmed in play. Runbook rows added for the weapon name
(1C-bis) and the ruleset line (3C-bis).

---

## 0.10.14 — 2026-08-11

**+hostworld, the camera diagnostic, and a table that was one short**

Three things from the parallel thread, plus one bug found reviewing them.

+hostworld starts a listen server on the map being edited, so the editor's
build-and-run can finally preview a deathmatch map as it will be played -
+runworld builds a single player session, and multiplayer spawns do not
apply there. It reads the same console variables the host dialog does, so a
hand-edited config and the dialog agree without either knowing about the
other. Builds; NOT YET RUN, and its own commit says which questions are open.

CamDebug reports the chase camera once a second: state, mode, raw offsets,
and asked-versus-got distance. That last pair is the diagnosis - they
disagree exactly when collision has pulled the camera in, which is what
distinguishes "the variable is not arriving" from "it arrives and the room
takes it back off you". Written because reading the code settled the camera
question wrongly twice.

AND THE DIAGNOSTIC WOULD NOT HAVE WORKED. Shared/FreshDebug.h declares nine
channels; both FreshDebug.cpp copies registered them lazily into a table of
eight and returned null once full, silently. CamDebug was the ninth and
would have raced the others for a slot - working on one machine, in one
level, and not the next. Sixteen now, in both copies, with a preflight check
asserting the table stays ahead of the count and the two copies agree. Both
arms broken on purpose.

Also recorded, not fixed: G1 in BUGS.md - GetConsoleString writes unbounded
and +hostworld is a second caller. Stock 1998 code, self-inflicted rather
than remote, six lines to fix properly, and not worth touching the host path
while the owner is mid-test.

All five projects rebuilt. Nothing in this release is confirmed in play.

---

## 0.10.13 — 2026-08-10

**the third-person camera, and the crosshair that finally agrees with it**

One change over 0.10.12, and it is the oldest open question in the handoff
finally answered: "what is wrong with the third-person camera?" It was two
things, and the second is why fixing the first alone would not have been
enough.

The camera sits back 70 and up 28, was 29 and 10. Those were MLOOK's
hardcoded numbers, kept as defaults when the variables were first made to
work so that the fix changed nobody's view - right then, wrong as a resting
place. ThirdPersonHeight is a dial now too, because pushing distance out
without height flattens the angle into a view of the player's back.

And the chase crosshair no longer lies. The shot leaves the muzzle PARALLEL
to the camera's ray but displaced by the whole back/up/shoulder offset, so
the middle of the screen was never where the bullet went - a fixed
displacement subtending a big angle up close and a small one far away, which
is why it read as miscalibration rather than as geometry. Pulling the camera
back makes it worse, so the two had to ship together.

First person is untouched to the pixel: there the muzzle ray and the camera
ray are the same line, and both offsets compute to zero.

All five projects rebuilt. Nothing here is confirmed in play, and this
release changes how third person LOOKS for everyone - if the new distance is
wrong, ThirdPersonDist and ThirdPersonHeight are the dials to say so with.

---

## 0.10.12 — 2026-08-10

**the kato is visible, and the chosen body actually gets worn**

Two fixes over 0.10.11, both of them the same class: an ordering assumption
that held everywhere except one path.

The kato grenade had no model in flight - only its light. CProjectile
constructs as OT_NORMAL, which basetypes_de.h defines as "Invisible
object", and 0.10.11 set a model filename while skipping SetType. Nearly
every projectile survives that because the client draws a local copy and
the server object is meant to be invisible; the kato and the spider mine
are the two the client exempts by name. Bounce default also drops 0.5 ->
0.32, played and judged - at 0.5 it still read as the 1998 bouncing ball.

The on-foot model choice never applied. MID_FRESH_PREFS arrives on world
entry, after SetPlayerMode has already run, and a foot-to-foot respawn is
not a mode change - so the index was stored one beat too late and nothing
read it afterwards.

All five projects rebuilt from a clean tree; both fixes were in play
before the bump and neither is confirmed yet - the kato rows are Session
2F, and the model row wants a respawn as well as a fresh join.

---

## 0.10.11 — 2026-08-10

**the kato grenade launcher, and the weapon corrections behind it**

Rolls up four commits since 0.10.10, three of them weapon-layer:

  - The kato is a real grenade launcher: 3D tumbling grenade in place of
    the 1998 sprite, GrenadeFuse and GrenadeBounce dials, 0.75x
    self-damage once it has bounced, and the oriented explosion ring that
    effects pass 2 deferred.
  - The launch tuning split in two. Mine* is the sticky launcher's throw,
    Grenade* is the kato's. Anyone with GrenadeVelocity in a cfg is now
    tuning the kato.
  - The mine detonator narrowed to the energy grenade. 0.10.6 put it on
    both launchers believing both laid mines; only the energy one does,
    and the kato has fired the bouncing grenade since 1998.
  - On-foot player model selection, from the parallel thread.

All five projects rebuilt from a clean tree after every thread had
committed - the previous build's Dist\ held other threads' uncommitted
work, which is exactly the hazard package.ps1 warns about.

---

## 0.10.10 — 2026-08-10

**the release that actually loads**

The only change over 0.10.9 is the mkrez type-code alignment already on
main (d7d805a), but 0.10.9's ZIP shipped before that fix, so every copy of
it carries an archive the engine cannot find files in - installed, the mod
silently gives way to stock game code. Confirmed loading in game this
morning: version line and licence text back on the main menu.

0.10.9 must not be distributed. This is its replacement, and the first
release packaged under the gate-5 rule: the zip's own archive is read back
and its type DWORDs checked before the package is called done.

---

## 0.10.9 — 2026-08-09

**first release built without Monolith's packaging tool**

No behaviour change over 0.10.8. What makes this one worth a number is what
is NOT in the build: ShogoFRESH.rez is written by Tools/mkrez.py, so a clean
checkout of this repository can produce a release with nothing but the
compiler and Python. Every release before this needed lithrez.exe staged by
hand from the Shogo SDK, a file we are not allowed to redistribute.

Also corrects the build section of CLAUDE.md, which still told the next
person prepare-redist.ps1 shells out to lithrez.

Version is 0.10.9, not 0.10.09: FreshVersion.h is explicit that the patch is
a NUMBER rather than a digit, and UpdateService.ParseVersion int.TryParses
each component into a System.Version.

---

## 0.10.8 — 2026-08-09

**the launcher explains a missing Shogo instead of showing one card**

Reported from a clean machine with no Shogo installed: the setup window
opened carrying the DirectPlay card and nothing else. No heading, no
explanation, no way forward. It reads as a broken launcher rather than a
missing game, which is the worst possible first impression for a mod whose
entire premise is that you already own the 1998 original.

TWO BUGS, and the second is worse than the reported one. RefreshSetup does
"Fixes.Clear(); if (!GameFound) return;", so with no game the list is empty -
and SetupNeeded was "!DirectPlayEnabled || Fixes.Any(...)", which reads an
empty list as "nothing needs attention". On a machine that already has
DirectPlay enabled, BOTH halves were false and the window never opened at
all. The reporter only saw the lesser failure because their DirectPlay was
off. GameFound now leads that test: the absence of the game is the most
important thing this window can say.

The panel says what ShogoFRESH is - a modification of the 1998 game, not a
copy - because "not detected" on its own invites the reasonable but wrong
conclusion that the download was incomplete. Everything below it is hidden
rather than greyed: a disabled Enable All is a promise that something could
happen if you found the right click.

WRONG FOLDER, decided rather than left to fail. GameLocator.ResolvePickedDir
forgives the two near misses people actually make - picking the PARENT
(steamapps\common) and picking a CHILD (Custom\, which the modding guides
send people into). Both are one hop from right and answering "not a Shogo
installation" to either is technically true and useless. One level in each
direction only; past that this stops being a correction and becomes a disk
scan. When it genuinely fails it names the missing FILE - a folder with
Client.exe and no SHOGO.REZ is a broken install and the person needs to know
which half is absent. The window stays open with the button still under the
message, because making them find this window again is a punishment for a
typo.

NO FOLDER: Close launcher, confirmed, saying nothing was changed on the
machine. Every other door here leads somewhere that needs a game folder, so
there is nothing to fall back to and pretending otherwise wastes their time.
Confirmed rather than immediate because it sits beside two buttons that are
trying to help, and it is the only one of the three that clicking again
cannot undo.

The located folder is SAVED, explicitly. Prefs.Save() is otherwise only
called from the Settings tab - CLAUDE.md fact 10 - so without this the
launcher would forget by the next start and ask someone who already went
looking to go looking again. It also parks the answer in GameDirOverride so
detection is skipped next time: a folder a human named beats anything a
search would guess.

Session 1A00 added to the runbook, including the two near-miss rows and the
one most likely to fail (that the choice survives a restart).

NOT PACKAGED. FreshShot.cpp/.h are uncommitted in the tree and a zip built
now could not be rebuilt from its own commit, which is the exact failure the
commit-before-package rule exists to prevent. Rebuild the DLLs and package
once that work lands.

---

## 0.10.7 — 2026-08-09

**the archive viewer gets a door to any .rez**

RezArchive could read and extract any LithTech archive since it was written.
It had no way in: ShowRezContents took its path from ModsGrid.SelectedItem,
and that grid scans Custom\ - so SHOGO.REZ and SOUND.REZ, which live in the
game folder, were unreachable by the one tool in the project that could open
them. The capability predated the feature by months; what was missing was a
file picker.

That gap had already leaked into the documentation. TEXTURE-MODDING opened
by telling modders to fetch lithrez.exe from Monolith's SDK to extract the
game's art - a tool ShogoFRESH cannot redistribute - for a job the launcher
was already capable of. Yesterday's engine-fact addendum said lithrez "is not
something a texture, model or sound modder ever needs", which was true for
packing and false for extracting. Both halves are now true.

Mods tab -> Open Archive..., and an Open... button inside the viewer as well.
The second one is not redundant: pulling art out of SHOGO.REZ and sounds out
of SOUND.REZ in one sitting is the normal case, and bouncing back to the Mods
tab between them is not.

THE READ MOVED OFF THE UI THREAD. The directory is scattered through the file
rather than sitting in one region, so TryRead loads the whole archive to parse
it - nothing for a mod, 259 MB for SHOGO.REZ. Measured at 68 ms warm, so this
is insurance against a cold read and a slow disk rather than a fix for a
freeze you would see every time; the window says what it is doing instead of
going white. The verified parser is untouched - it is checked against 7,965
resources and this was not the change to risk it on.

Open... deliberately stays enabled when an archive fails to read. The answer
to picking the wrong file is to pick another one, and a dead window means
closing and starting again.

Docs follow the capability: step 1 of the texture guide is now the launcher,
and lithrez survives only where it is genuinely required - creating an
archive, which nothing of ours can do. MODDING gains the same correction it
was missing, that an asset mod needs no archive at all and packing is for
handing the thing to somebody else.

Session 1A0 added to the runbook, ahead of the game sessions because none of
it needs the game running. The rows that matter are the two that would show
this was built carelessly: the window staying responsive while it reads 259
MB, and Open... surviving a file that is not an archive.

---

## 0.10.6 — 2026-08-08

**the sticky mines get a detonator on the secondary key**

The first real secondary fire, plugged into the seam 0.10.2 built for it:
holding either thrown grenade, the secondary key sets off placed mines.
Single press detonates the one under the crosshair, double press detonates
all of that player's. A press that finds nothing does nothing - no fallback
to "the oldest", because that would let a mistimed double press blow a
charge the player did not choose and the two presses would stop meaning
different things.

MID_FRESH_MINEDET (232) carries ONE BYTE: which set, never which mine. The
client does not name a target and could not be believed if it did. The
server already holds the eye position and the aim rotation - the tractor
beam has taken them the same way since 1998 - so the trace, the sight test
and the ownership check all run there.

Nothing is deferred. The textbook way to tell one press from two is to hold
the first for the length of the double-press window; that puts 0.30s of lag
on the COMMON action, on a weapon whose whole point is choosing the moment.
The first press sends "aimed" immediately and the second sends "all" behind
it - the outcome of a double press is unchanged, every mine goes off, so the
lag would have bought nothing but tidiness. The window is compared forward
in time only: engine time restarts with each world, so a press in the last
world sits in this one's future and an unguarded subtraction would read as
"no double press for the rest of the level".

Line of sight is level architecture only. Walls, doors and lifts hide a
mine; PEOPLE DO NOT. The obvious reading says a body in the way is a body in
the way, and it is wrong here - the moment worth detonating a mine is
precisely the moment somebody is standing on it, and a filter that let them
shield it would refuse the weapon exactly when it was wanted. Ignoring the
player and the mines themselves falls out of the same rule rather than
needing a filter of its own.

Picked by ANGLE, not distance: a fixed tangent (0.13, ~7.4 degrees) with a
40-unit floor, tightest angle wins. A constant world-space tolerance would
make a mine across the room unselectable and one at your feet unmissable; a
constant angle is what the crosshair describes, and the floor keeps
point-blank from demanding pixel accuracy. Sight is tested only for a
candidate that would win, and the best is replaced only if that test passes
- without the second half a hidden mine with a better angle would displace a
visible one already accepted and the press would find nothing.

An unarmed mine is not a candidate. MineArm is what stops a mine at your own
feet from killing you while it settles, and a manual trigger is still a
trigger. A holster does NOT block the key: the charges are already in the
world and their fuse and proximity trigger keep running whatever the player
is holding, so refusing it would not make them safe, only unreachable.

Walked rather than registered, for the reason spelled out over
EnforceMineLimit - a registry has to be maintained on every route a mine
leaves the world by, and one missed path leaks a slot forever.

Gated on FreshIsThrownGrenade rather than the energy grenade alone: both
thrown grenades become CStickyGrenadeProjectile under FRESH rules, so both
leave mines and both must be able to set them off.

---

## 0.10.5 — 2026-08-08

**menu corners follow the HUD band, and the mine's light blinks with its beep**

MAIN MENU CORNERS. The version line was right-aligned to the edge of a
centred 4:3 box - where the menu ART ends - which on a wide display sits a
long way inside everything else the player has been looking at. Both corners
now use GetHudInset, the same answer the HUD uses for "how far in should
edge-anchored things sit", so the menu stops having a private rule.

With HudAspect unset the inset is 0 and both lines go to the real screen
edges. That is also why the left-hand attribution already looked right: it
was never inside the 4:3 box in the first place, so only the version line
was actually wrong.

THE MINE'S LIGHT is back to full brightness and now BLINKS, lit only for the
third of a second its beep lasts.

Dimming it (0.10.3) was the wrong fix for the right complaint. A constant
light, at any brightness, reads as a glow the mine simply has, and quietly
gives its position away for the whole sixty seconds between beeps. A bright
flash every five to ten seconds is a device announcing itself - and it is
findable exactly when it is audible, so both clues arrive together.

The wire is USRFLG_MINE_BEEPING, a user flag rather than a message: user
flags already replicate on the object the client effect is following, so this
costs no protocol and nothing when nobody is looking. Object COLOUR was the
other candidate and was rejected - it replicates too, but it also tints the
model, so the mine would change appearance in order to say something about
its light.

The light starts DARK rather than lit. A mine that flashes the instant it is
thrown announces the placement that is the entire point of the weapon.

Still open from the same request: the sticky mine's secondary fire - single
press to detonate the mine under the crosshair, double press to detonate all
of them. That is a real feature rather than a tweak (a trace, an owner test,
a double-press timer and a new client-to-server message) and wants its own
release rather than being folded in behind two visual changes.

---

## 0.10.4 — 2026-08-08

**the launcher's keybind list learns about the new actions**

"I do not see the new keybind ordering" - because there are TWO lists and
0.10.3 only reordered one.

g_CommandArray in ClientUtilities.cpp drives the IN-GAME controls menu.
Defaults/keybind-layout.json drives the LAUNCHER's Keybinds tab, with its own
Order and its own Labels, and nothing connected them. Anything not in Order
goes to the END, so two controls added under Fire arrived at the bottom of
the launcher's list - no error, no missing row, just the wrong order in one
of the two places.

Both now agree: secondary pair under Fire, Holster under Previous Weapon, and
the labels "Primary Fire" / "Secondary Fire/Zoom (hold)" / "(toggle)" in the
shipped JSON and in the C# fallback that stands in when it is absent.

AND THAT WOULD STILL NOT HAVE FIXED IT, which is the more useful half. The
layout file is written to AppData on first run and then owned by the player,
so shipping a new default does nothing for an install that already has one -
exactly the shape of engine fact 3, where registering an action at runtime is
needed because existing autoexec files predate it. KeybindLayout.Load now
merges: any action the saved file has never heard of is inserted after the
same neighbour it follows in the shipped layout, so a control added "under
Fire" lands under Fire rather than at the bottom. Player edits are untouched;
only genuinely absent entries are added.

PREFLIGHT gains check_keybind_rows, because this is the project's own rule -
one fact in two places gets a check. It asserts presence only, not position:
the two lists differ on purpose (the launcher hides legacy actions), and what
is never intentional is an action the launcher has never heard of. Verified
by removing SecondaryToggle from the JSON on purpose.

---

## 0.10.3 — 2026-08-08

**reload defaults settled, Sticky Mines, and the controls menu reads in order**

RELOAD DEFAULTS, retired from the tuning variables into the table exactly as
intended: assault rifle 1.50 -> 0.60, MAC-10 1.25 -> 0.50. Both settled by
playing with Reload15 and Reload18, which is what those variables exist for.

RELOAD13 REPORTED RATHER THAN GUESSED. "Reload13 does not adjust" has three
causes that look identical from the outside - the variable never reached the
server, it was read and clamped away, or it IS applying and what is still
being seen is the authored reload ANIMATION. That third one is specific to
the .45 and the MAC-10, the only two weapons with an authored reload, and
engine fact 5 says there is no playback-rate control: shortening the reload
lets the weapon fire while the animation is still running, it does not make
the animation shorter. So under WeaponDebug the server now prints the table
value, the per-weapon variable, the scale and the number that won.

Checked and ruled out first: the precedence is right (per-weapon outranks
HandgunReload and ReloadScale), and CVarTrack resolves its handle at Init and
reads through that handle rather than through the name - which matters
because FreshReloadVarName returns a shared static buffer and CVarTrack
stores the POINTER, not a copy. Reading is safe; SetFloat on one of these
tracks would not be. Noted rather than fixed, since nothing calls it.

STICKY MINES. The energy grenade is renamed everywhere the player sees it -
weapon name and ammo name - now that it throws a spider mine rather than a
grenade. The internal id stays GUN_ENERGYGRENADE_ID: renaming that would
touch the ammo tables, the FX switches and every saved game, to no visible
benefit.

CONTROLS MENU, owner's ordering: the two secondary-action rows move under
Fire, Holster moves under Previous Weapon, and "fire" becomes "primary fire".
The secondary rows are labelled "secondary fire/zoom" rather than "zoom",
which is what they will do once a weapon has a real secondary - the action
NAMES were already neutral for exactly this reason, so the relabel costs
nobody their keybind file.

---

## 0.10.2 — 2026-08-08

**a secondary-action key, the grapple moves to mouse 4, and the halo is gone**

HUDTEXTSMOOTH REMOVED, owner's call after seeing it. Taken out rather than
defaulted off: a console variable nobody wants is worse than no code, and the
comment explaining why real anti-aliasing is impossible now lives in this
message instead of in a function. The 0.10.1 shadow tightening STAYS - that
was the half that worked.

For the record, since the finding outlives the code: the engine cannot
anti-alias text. CreateFont takes name, width, height, italic, underline and
bold, with no quality parameter to ask GDI for smoothing, and
ScaleSurfaceToSurface has no filter flag, so rendering large and shrinking
drops pixels rather than blending them.

A SECONDARY-ACTION SLOT, two new bindable actions under Fire in the controls
menu: "zoom (hold)" and "zoom (toggle)".

Named for the SLOT, not for today's behaviour, at the owner's request -
weapons that zoom will zoom, and weapons given a real secondary fire later
use the same key. So the command ids are COMMAND_ID_SECONDARY_HOLD/TOGGLE,
the engine action names are SecondaryHold/SecondaryToggle, and
CRiotClientShell::HandleSecondaryAction is the single dispatch both keys go
through, with the future branch marked. Only the MENU LABELS say "zoom",
because a label can be changed later without invalidating a keybind file -
the action name cannot.

This is also the first time zoom has been bindable at all. It was reachable
only by pressing the weapon's own number key a second time, which is why
nothing in the controls menu ever mentioned it and why it could not go on a
mouse button.

DEFAULT BINDS, in defkeybd.cfg and kyokeybd.cfg both: zoom toggle on mouse 2,
and the grapple moved to mouse 4 to make room - it has held mouse 2 since
1998. Registered at runtime as well (engine fact 3), guarded by
FreshSecondaryBound so an install that predates this gets both, once, and a
player who has since rebound either keeps their choice. The grapple move
rides in the SAME guard rather than a separate one, or an existing install
would end up with two things on mouse 2.

THE KEYBIND LAYOUT DROPDOWN showed "BindLayout { Title = ..." after a
selection - a C# record's generated ToString leaking into the combo box's
selection area, which DisplayMemberPath does not cover. Overridden on the
record so it is right wherever the value is displayed, not only in the one
template that revealed it.

NUM_COMMANDS 30 -> 32. That constant sizes both g_CommandArray and the
keyboard menu, and forgetting it is a compile error rather than a silent
overrun - which is the one good thing about it.

---

## 0.10.1 — 2026-08-08

**one shadow gap everywhere, and softened HUD text**

THE SHADOW. nShadowDiv sets offset = fontHeight / nShadowDiv, so bigger means
tighter, and it had drifted: 3 on the bumper screen, 5 on the MOTD and the
menus, 7 on the info rows and the feeds, 10 on the HUD figures and the
transmissions. Eleven call sites, four different answers to one question.

10 everywhere now, because the HUD figures and the transmissions were already
at 10 and were the two the owner judged right. The default moves 5 -> 10 as
well, so a caller that says nothing gets the same shadow as everything else
rather than the loosest one on offer.

At 3 and 5 the shadow was reading as a second copy of the line rather than as
depth under it - which is exactly what the header comment had warned about
since it was written, against a default that did not follow its own advice.

THE SOFTENING, and it is worth being exact about what it is not.

The engine cannot anti-alias text. CreateFont takes name, width, height,
italic, underline and bold - there is no quality parameter to ask GDI for
smoothing - and ScaleSurfaceToSurface has no filter flag, so rendering large
and shrinking would drop pixels rather than blend them. Real AA is not
reachable from the DLL, and no amount of arranging our own code changes that.

What IS reachable is a halo: the same glyphs in a mid tone one pixel out in
four directions, under the crisp text and over the shadow. Hard stair steps
get a dim neighbour, which reads as a softened edge. It is a cheat and at HUD
sizes it is a convincing one. HudTextSmooth turns it off.

Four extra blits per string, on surfaces that are cached and rebuilt only when
the text changes - paid once per value, not per frame. The output surface
gains a pixel of margin so the halo above and left of the glyphs has somewhere
to land; without it that side clips and the softening is lopsided.

One coupling recorded rather than engineered away: the halo rides below the
HudTextShadow check, so turning the shadow off turns the softening off too.
The shadow is on by default and nothing turns it off on the player's behalf,
so a second composite path would be code with no caller - but it is the first
place to look if anyone reports HudTextSmooth doing nothing.

---

## 0.10.0 — 2026-08-08

**roll the minor rather than run into three digits**

0.9.95 -> 0.10.0. Nothing technical forced this: System.Version compares
numerically per component, so 0.9.100 would have sorted correctly against
0.9.99 in the update check, preflight's regexes are digit-count agnostic, and
the Windows VERSIONINFO fields are 16-bit. 0.9.100 was safe end to end and
was checked before choosing.

Rolled because a version number is partly a claim about what changed, and
0.9.x has absorbed rather a lot: the localisation file, the limit
deduplication, Rcon NextLevel, the weapon-wheel fix, per-weapon reload
tuning, and a new on-foot weapon in the thrown spider mine, which arrived as
a projectile swap and ended up with its own arming delay, proximity fuse,
beep and per-player budget. That is not a patch series.

All five projects rebuilt rather than the two that changed - FreshVersion.h
reaches the resource DLLs and CoolServ.rc as well, so a partial build would
have shipped a mixed set of file properties.

Still 0.x. 1.0.0 is for when the validation backlog is empty, not for when
the version number gets awkward.

---

## 0.9.95 — 2026-08-08

**three mines to a player, one beep at a time, and the owner is not exempt**

THE LIMIT. Three live mines per player (MineLimit, 1-16); placing a fourth
detonates that player's oldest.

A BUDGET, not a refusal. The weapon always does something when you fire it,
and what it costs is a charge you placed earlier and may well have
forgotten. Refusing the throw once three are out would read as a weapon that
silently stopped working, with nothing on screen to say why.

Counted by walking the object list rather than from a registry. A registry
has to be maintained on every route a mine can leave the world -
detonation, level change, the owner disconnecting - and one missed path
leaks a slot that is never returned, which presents as the weapon quietly
running out. A walk cannot go stale: an object that is gone is not in the
list.

Ordered by a serial from a counter, not by time. Times are ambiguous here
because the fuse restarts on landing, so a mine still in flight carries an
EARLIER timestamp than one that has already settled, and "oldest by time"
picks the wrong one. The serial is assigned at the landing, so the sequence
is placement order rather than throw order - not the same thing once one is
lobbed across a room and another dropped underfoot.

ONE BEEP, NOT FIVE. Timer.wav is a run of beeps - right for a mech spider
counting down to its own detonation, wrong as a periodic tick, where five
beeps every seven seconds is a smoke alarm. The engine cannot play part of a
sound, so the handle is kept and the sound killed after roughly the first
beep. No new audio file needed, which was the alternative. Killed on removal
too, or a positional beep outlives the object it was following.

THE OWNER TRIGGERS IT NOW, reversing 0.9.94's exemption at the owner's
request - and it is the better rule. Exempting them made the mine safe to
stand on, which turns a hazard you have to remember into free area denial:
you could hold a doorway from inside your own minefield and never pay for
it. Three seconds of arming is already enough to walk away from one you
meant to place.

THE LIGHT is dimmer: 0.40 red at radius 30, down from 0.85 at 35. It is a
status LED on a device, not a lamp - findable if you are looking, easy to
miss if you are not.

THE EXPLOSION SOUND is the TOW's, matching the TOW's blast effect from
0.9.93. That is the THIRD switch holding whole sound paths for one weapon -
GetWeaponSoundDir, GetWeaponFlyingSound and GetImpactSound - which is why
moving "the" sound directory kept looking complete and kept leaving one
behind.

---

## 0.9.94 — 2026-08-08

**the thrown mine waits, arms, ticks and triggers**

The mine becomes a placed weapon rather than a grenade that sticks.

  Lands, then waits MineArm seconds (3) doing nothing at all - no beep, no
  trigger. That is what stops the obvious accident of throwing one at your
  own feet and being killed by it before it settles.

  Arms with a single beep, so whoever placed it knows it is live.

  Then beeps every 5-10 seconds, randomised per beep rather than on a fixed
  period. A metronome is something you learn to ignore, and worse, it lets a
  listener time their approach between beeps. FreshRandom rather than rand(),
  because rand()'s stream is pinned by the weapon-spread seeding on every
  shot (engine fact 6) and mines placed in one burst would beep in chorus.

  Detonates when a live character other than its owner comes within
  MineRange (110 units) - shorter than the 200-unit blast on purpose, so
  walking into one is a mistake rather than a warning. It ignores the player
  who placed it: a charge that kills its owner for tending it is a weapon
  nobody uses twice.

  Otherwise goes off on MineFuse (60s), counted from the LANDING rather than
  the throw. A charge should last the same time whether it was dropped at
  your feet or lobbed across the room, and the projectile's own lifetime had
  been running since it left the hand. Not infinite: a long match would
  otherwise silt up with live ordnance nobody remembers placing, each one
  costing a projectile.

THE BEEP MOVED SIDES. The client's flying sound LOOPS for the life of the
projectile - right for something in flight, wrong for a charge that lands and
stays, which is why it ticked continuously in 0.9.93. It is now suppressed
for thrown mines and the server plays one-shot beeps instead. The mine's
audio belongs with the code that owns its lifecycle, because "armed" and
"still counting" are things only the server knows.

Detonate() moves from private to protected. A subclass with a fuse of its own
has to be able to set it off, and proximity is neither an impact nor the
lifetime expiring, so neither existing route reached it.

All of it gated on FreshIsThrownGrenade && UseFreshRules, so the mech spider
still flies, sticks and goes off on the ordinary projectile fuse, and Classic
still throws a grenade.

---

## 0.9.93 — 2026-08-08

**the mine ticks, stops shaking, and borrows the TOW's blast**

Four owner reports from playing 0.9.92, all on the thrown mine.

IT WAS LOOPING THE GRENADE'S FLIGHT SOUND while stuck to a wall.
GetWeaponFlyingSound holds whole PATHS rather than going through
GetWeaponSoundDir, so moving the sound directory in 0.9.92 carried every
other sound across and left exactly this one behind. It now plays the
spider's Timer.wav under FRESH - a mine should tick.

Worth noting the shape: one weapon's sounds were selected two different ways,
so a change to "the" sound source moved most of them and looked complete.

THE LIGHT is now dim red at radius 35, down from yellow at 100. That radius
was the single value every projectile light used, which is fine for something
that flies past and wrong for something that PARKS in a room - the mine was
lighting the corner it was hiding in. Red because the light stopped being a
glow on a projectile and became a warning on an armed charge.

THE JITTER IS MINE, from 0.9.91. An attached mine is held in place by
Update() writing its position every frame, and giving thrown grenades
FLAG_GRAVITY meant both ran at once: the engine pulled it down, Update
snapped it back, and it vibrated against the wall at the frame rate. Gravity
is now cleared when it attaches - the arc has done its job by then.

THE EXPLOSION is the TOW's rather than the spider's, owner's call after
seeing both in play. A charge you place and walk away from wants the heaviest
blast in the on-foot tier behind it; the spider's effect is sized for a
mech-scale weapon fired repeatedly, not for one deliberate placement.

Classic is untouched in all four.

---

## 0.9.92 — 2026-08-08

**the thrown mine looks, sounds and explodes like a mine**

Four changes, all FRESH-only, all so the energy grenade stops advertising
itself as an energy grenade now that it throws a spider mine.

  Sound: the whole "Spider" sound directory instead of "EnergyGrenade".
  The directory is the one switch that moves every sound together rather
  than leaving one behind to give it away.

  In-flight glow and blast light: the spider's yellow instead of cyan. Cyan
  is the energy-weapon family colour - the pulse rifle uses the same three
  numbers - and a charge that flashes cyan reads as a grenade going off
  however it behaved.

  Explosion: the spider's layered effect instead of the grenade's, at all
  three detail levels.

  Size: twice the mech spider's, applied in CProjectile::Setup where the
  weapon id is known, so the mech's own spider is untouched. The model is
  sized for a mech launching it across an arena; thrown by hand it was a
  speck, which is no good for a weapon whose point is placing it
  deliberately. The client's predicted projectile takes the same multiplier
  from the same function, or the thrower watches a different-sized mine from
  everyone else.

A CORRECTION TO THE 0.9.87 EXPLOSION AUDIT. It said the energy grenade, the
kato grenade and the TOW have no bespoke explosion FX and fall through to the
generic path. That is wrong: all three have full LOW/MED/HIGH cases in
CreateWeaponSpecificFX. The audit scanned a 140-line window of that switch
and reported the absence of what was simply outside it - the compiler caught
it here, on "case value already used", when the fall-through this commit
first tried to add collided with the case that was already there.

The shape of that mistake is worth keeping: a partial read reported as a
complete finding. The rest of that audit came from whole-table extractions
and stands; this one line came from an awk range and did not.

So the real gap was narrower than described - not "these weapons have no
effect" but "this weapon has the wrong one" - and the fix is the same either
way.

Two smaller things the compiler was right about: VEC_SET expands to three
statements, so an unbraced if/else around it does not compile and would have
been wrong if it had; and server_de.h has ScaleObject with no getter, so the
scale is multiplied from m_vScale, which is what PRECREATE handed the engine.

---

## 0.9.91 — 2026-08-08

**the wheel was dumping you into the knife, and the thrown mine gets its arc back**

THE WHEEL. Every "cycle next" in the owner's WeaponDebug log ended the same
way: "settled on slot 79 (weapon 12)". Slot 79 is COMMAND_ID_WEAPON_10 and
weapon 12 is the monoknife.

Slot 10 is the melee weapon in every mode - tanto on foot, baton/blade/
monoknife per mech - and melee uses no ammo, so CycleCandidateOK can never
skip it. It was the guaranteed last stop of every forward scroll: with
nothing loaded in between, the wheel walked the whole range and put the
player in melee.

So "the wheel is less sensitive" was never about input at all. Nothing was
being dropped; it was arriving somewhere useless, and it did it more often
the emptier the loadout. The range now stops at 9, matching the bottom of the
range which already excludes COMMAND_ID_WEAPON_0 for exactly the same reason.
Melee keeps its own key, so nothing loses access.

Worth noting what the diagnostic was worth. Two guesses were on the table - a
magazine-versus-reserve ammo bug, and the undrawable-weapon early return - and
the actual cause was neither, and was invisible from reading the loop. The
juggernaut question is still open and now has a clean test: it logs have=1
ammo=0, so if the HUD shows rounds in the magazine at the same moment, that
IS the ammo bug; if the HUD says empty, the wheel was right all along and the
melee problem above was the whole complaint.

THE ARC, a regression from 0.9.89 reported as "shoots straight forward". The
arc is GRAVITY, and gravity is a flag the projectile class sets in its
constructor - CGrenadeProjectile has FLAG_GRAVITY, CStickyGrenadeProjectile
does not, because the mech spider is launched and flies flat. Handing the
energy grenade the sticky class took the arc with it. The elevation and
velocity tuning applied exactly as before and had nothing to arc against,
which is why "keeps the arc" was wrong in that commit message.

Fixed in CProjectile::Setup and asked of the WEAPON, not the class: a thrown
weapon arcs whatever is carrying it. FreshIsThrownGrenade already answers
"is this a throw" and is the same test the velocity and elevation tuning
uses, so the three cannot come to disagree about which weapons are throws.
The mech spider is not a thrown grenade and still flies flat.

Owner also confirms: spider scorch now lands on the wall, kato grenade at 6,
and the foot light no longer disappears in dark rooms. Smoke still renders
under scorch marks, so the 0.9.88 hypothesis was WRONG about that half - the
decal placement was a real defect and fixing it did not fix the sort order.

---

## 0.9.90 — 2026-08-07

**the foot light was inside the floor**

Reported as two things - "some surfaces don't receive the lights" and "the one
at the feet disappears if I lean against a wall" - and they are one defect.

vPos.y - vDims.y IS the sole line: the origin is the centre and dims.y is the
half height. UpdateCharacterLight then subtracted a further 4 units, so the
light sat INSIDE the floor the player was standing on. A point light buried in
solid geometry lights the underside of the world and contributes nothing above
it, and pressing against a wall buries it further.

The drop was deliberate and its reasoning does not survive checking. The
comment said the character's own body would otherwise stand between the light
and the floor - but both carried lights are FLAG_ONLYLIGHTWORLD and neither
casts shadows, so the player model is not in the way and never was. The light
does not interact with the model at all; that is what the flag means.

Now lifted 6 units clear of the soles instead. Radius 62 -> 48 to compensate:
out of the floor, the same radius reaches noticeably further across the ground,
and the intent is to say where the feet are rather than to light the room.
PlayerFootLightRadius still overrides, and 0 still turns it off.

NO PICKUP LIGHTS EXIST, which is worth writing down because the third report
asked to raise them. Only two ClientLightFX subclasses are ever created -
FreshPlayerLightFX and FreshFootLightFX, both from CBaseCharacter, both
carried by a character. Nothing in ObjectDLL gives a pickup a light. Whatever
was seen sinking through the floor was almost certainly this same foot light,
which is consistent with all three reports being the one bug.

The muzzle-flash question is answered in the reply rather than changed:
CreateMuzzleLight has existed since stock and still runs, but only for the
eight weapons carrying WFX_LIGHT - the pulse rifle is NOT one of them - and at
75-100 units for 0.15s with no ramp, against per-vertex world lighting that
needs a radius big enough to reach several vertices before it shows at all.

---

## 0.9.89 — 2026-08-07

**the energy grenade throws a spider mine**

Owner's call: replace rather than add a fire mode, keep the arc, FRESH only.

CEnergyGrenade::CreateProjectile now builds a CStickyGrenadeProjectile under
FRESH rules and the stock CGrenadeProjectile under Classic. The throw is
untouched - velocity, elevation and drop all come from FreshIsThrownGrenade
and are the same numbers as before - so only what arrives at the end of the
arc has changed. A charge that sticks where it lands is a weapon you place; a
grenade that bounces is one you aim at the floor and hope, and the two
grenades were otherwise the same gesture with different bounce.

Reusing the mech spider's own class means everything already learned about it
comes along: it will not stick to a pickup and ride it through a respawn, it
kills a person outright under FRESH instead of gluing itself to a body that
is still fighting, and since 0.9.87 it remembers the surface it attached to
so the scorch lands on the wall.

THE CLIENT NEEDED THE SAME SWITCH, which reading the server change alone
would have missed. ProjectileFX builds the LOCAL shooter's own predicted
projectile, and the energy grenade's entry there is a sprite
(Sprites\grenade1.spr) rather than a model. Left alone, the thrower would
have watched a grenade sail away while every other player watched a mine -
same throw, two objects. It now uses the spider model under FRESH and the
sprite under Classic, matching whatever the server built.

Classic is genuinely untouched in both places, which is the doctrine: this is
new behaviour, not restored 1998 tuning, so it belongs to the ruleset.

Untested in play. The interesting questions are all feel: whether a thrown
mine wants the current 1400/14-degree arc (the owner has the dials and said
they would adjust), and whether three per magazine is right for a weapon that
now places charges rather than lobbing them.

---

## 0.9.88 — 2026-08-07

**blast scorches land ON the surface, per-weapon reload tuning, ammo changes**

THE SCORCH OVER SMOKE. FindMarkPoly traces along the surface normal to find
the polygon a mark should be clipped to, and threw away iInfo.m_Point - the
place it just found. So a blast mark was drawn at the DETONATION POINT rather
than on the wall, and a projectile stops against a surface with its own dims
between it and the plane. The spider is 5x5x3, so its scorch parked several
units proud of the wall it was stuck to.

A decal floating in mid-air sorts against everything else in the translucent
pass on its own terms, which is what "the scorch draws above the smoke" looks
like from the player's side: the smoke hugs the surface, the scorch does not.
The mark now snaps to the point the trace already returned.

Worth being straight about the confidence here. That a decal belongs ON its
surface is not in doubt and the fix stands on that alone. That it is the whole
of the draw-order report is a HYPOTHESIS - sprite-versus-particle ordering is
the closed renderer's business and the RE notes have nothing on its sort, only
the DrawSprites/DrawParticles toggles. If scorches still sort oddly against
smoke after this, the remaining cause is not something the DLL decides.

AMMO, owner's calls from the 0.9.87 audit:
  pulse rifle carry 60 -> 90 (three magazines; it was the shallowest reserve
  in either tier while the rest of the table sits at four)
  kato grenade magazine and pickup 1 -> 6, carry unchanged at 12, so it is
  two magazines rather than twelve single grenades.

RELOAD, PER WEAPON. "Reload<id>" - Reload0 is the pulse rifle, Reload15 the
assault rifle - seconds, clamped 0.10-5.00, 0 means the table. It outranks
HandgunReload and ReloadScale because it is the most specific thing anyone
can say: ReloadScale moves all twenty-three together and HandgunReload only
ever reached the .45 and the MAC-10, so neither could answer "the pulse rifle
and the assault rifle specifically feel wrong".

Read on BOTH sides - CVarTrack on the server, GetSConValueFloat through the
server console mirror on the client - because the server owns the clip and
the client owns the animation. A value read on one side only would end them
apart, which IS the frozen gap being complained about: the animation finishes
and the weapon still refuses to fire.

Named by weapon ID rather than by weapon name on purpose. The ids are what
both sides already speak; twenty-three names would be twenty-three chances to
disagree about spelling, silently, in one DLL.

---

## 0.9.87 — 2026-08-07

**a spider stuck to a wall scorches the wall**

The scorch appeared on the FLOOR beneath the mine instead of the surface it
was attached to, and the whole chain is in the code rather than in the art.

A sticky charge attaches on impact and detonates on its FUSE, seconds later.
Detonate() is then called with hObj null, and the only branch for that case
says "lifetime was up, so we blew up in the air" and reports ST_AIR with the
default straight-up normal. The client's ST_AIR path is
CreateAirBlastMark(), which searches DOWNWARDS for a floor - correct for a
grenade that timed out mid-air, and exactly wrong for a charge welded to a
wall. Everything did what it said; nothing knew the mine had landed.

The gap is that the surface is only knowable at the moment of impact -
GetLastCollision() by detonation time belongs to somebody else entirely. So
CProjectile gains m_bHaveAttachSurface / m_vAttachNormal / m_eAttachSurface,
and Detonate uses them ahead of the air branch when they are set. A
projectile that never attaches never sets them and takes exactly the path it
did before.

CStickyGrenadeProjectile already READ the collision plane to align its model
into the wall; it simply threw it away afterwards. It now keeps it - the
OUTWARD normal, not the negated one used for the model, because that one is
flipped to face into the surface and handing it to the scorch would bury the
mark inside the wall it is meant to sit on.

ProjDebug reports the new branch as "stored attach plane", so the log
distinguishes it from a genuine air blast rather than quietly looking the
same.

AMMO AUDIT, requested alongside. The finding worth recording is that SINGLE
PLAYER AND MULTIPLAYER DO NOT AGREE, and not in the way the names suggest:

  - Magazine size follows UseFreshRules(), which is on in a FRESH campaign.
  - Pickup amounts and carry limits follow UseFreshEconomy(), which is
    UseFreshRules() AND multiplayer - so single player always draws them
    from the CLASSIC table.

So a FRESH campaign runs FRESH magazines against 1998 boxes and 1998 carry
limits. That is a real mismatch, it is already noted at the GetWeaponPickupAmount
comment ("this mixes two columns and that is the whole problem"), and it is
not a defect of this commit - but anyone reading the tables should know the
columns are not selected together. Full figures in the reply.

---

## 0.9.86 — 2026-08-07

**the weapon wheel gets a diagnostic, and its test stops being written twice**

"The mouse wheel is less sensitive, and sometimes it will not switch to the
juggernaut." Instrumented rather than fixed, because that sentence has at
least two shapes and they want opposite changes.

NextWeapon and PrevWeapon each carried their own copy of the same test:

    !CanDrawGun(id) || (UsesAmmo(id) && GetAmmoCount(id) <= 0)

Two copies of one rule, and a forward cycle that skips a weapon the backward
cycle stops on is EXACTLY what gets reported as "the wheel is unreliable".
Now one CycleCandidateOK(), used by both, with the reporting inside it - so
the answer comes from the code that decides rather than from a second opinion
written next to it.

Under WeaponDebug 1 each notch now prints the origin slot, every candidate it
considered with have/usesammo/ammo and whether it was taken or skipped, and
what it settled on.

The juggernaut is slot 6 in mech mode and its ammo row is { 3, 3, 3, 1, 12 } -
three per magazine. So the interesting possibility is the ammo test: with
magazines a weapon can hold three loaded rounds and no reserve, and if
GetAmmoCount reports the reserve then the cycle steps over a weapon the
player can fire right now. That would present exactly as "sometimes it will
not switch to the juggernaut", and it would be a FRESH-only fault, since 1998
had no magazines (engine fact 20). The other shape is simply that the
inventory does not think the weapon is held. The log says which; the code
cannot.

Also noted while reading, NOT changed: both functions open with

    if (!pInventory->CanDrawGun(m_nWeaponId)) return -1;

so cycling is refused outright while the CURRENT weapon is undrawable, and
the caller gets -1 with nothing said. That is a candidate for the
"sensitivity" half of the report - an input consumed and visibly ignored -
but it is a behaviour change and the same log will show whether it fires.

The remaining two reports are queued behind this: the spider's scorch drawing
below the mine instead of on the surface it stuck to, and scorch sprites
drawing over smoke.

---

## 0.9.85 — 2026-08-07

**the two match limits stop living under two names**

The launcher said 15 minutes, the server said 1, and saving in the launcher
changed nothing. Audited in 0.9.83: of every setting the launcher writes and
every console variable the server registers, exactly TWO exist under two
names - EndFrags/FragLimit and EndTime/TimeLimit - and GetFragLimit and
GetTimeLimit read "console variable if non-zero, else NetGame".

That split is not itself the bug and cannot be removed. server_de.h has
GetGameInfo and no SetGameInfo, so a console variable is the only way to
change a limit without restarting the server; it is what makes the Round
Limits box a control rather than a decoration.

The bug is that the value was invisible in both directions. The server SAVES
its console variables on exit, so one "Rcon TimeLimit 1" outlived its session
and silently outranked the launcher every session after, while the launcher
went on displaying and rewriting a number nothing read. Neither side was
wrong about its own variable; they were reading different ones.

Fixed as agreed with the owner: one value, last writer wins, every writer
persists.

  Launcher -> server. Writing EndFrags/EndTime now also writes FragLimit 0
  and TimeLimit 0, retiring the override. The launcher was the most recent
  writer, so it stops leaving a stale one behind to beat it.

  Server -> launcher. The standard update carries the EFFECTIVE limits
  (NST_ENDFRAGS/NST_ENDTIME), and the app writes them back into NetGame -
  which is what SaveConfigFile persists to EndFrags/EndTime on shutdown, the
  names the launcher reads. So an rcon change survives under a name the
  launcher can see. Win Condition redraws, and Time in Level re-renders
  against the new denominator.

PREFLIGHT gains an NST token check, because two more defines just got
duplicated across the NetDefs.h copies and that is the project rule. A
drifted token is the quietest possible failure: Sparam_Get returns false, the
field keeps its old value, and the display reads stale rather than wrong.

Writing it turned up something first: the server app spreads its token copies
over TWO headers, NetDefs.h and NetStart.h, and NetStart.h redefines most of
them. The first version of the check compared file to file and failed on four
tokens that were fine. It now compares against the union, and deliberately
does not require symmetry - Shared/ carries tokens the app has no use for,
and demanding they match would mean adding dead defines to keep a check
quiet. Verified by breaking a token value on purpose, both before and after
that correction.

---

## 0.9.84 — 2026-08-07

**squishie size becomes a dial, and the footstep report gets a measurement**

SquishScale, a live tunable alongside GrenadeVelocity and HandgunReload.
"serv SquishScale 0.15" at the game console, applies on the next respawn, no
map change. Clamped 0.08-1.00; 0 means the FRESH_SQUISH_SCALE constant, so a
server nobody has typed at behaves exactly as it did before.

The floor is collision, not taste: the movement dims shrink with this number,
and past a point a player is smaller than the step heights and floor gaps the
level was built with, and starts catching on architecture nobody meant to be
an obstacle. Eye height needs no dial of its own - the camera offset is scaled
by the same value.

This exists because 0.2 was chosen by reasoning about a number and the first
person to play it wanted it smaller, which is the exact argument that put the
grenade throw behind a variable. Retire the settled value into the constant
when there is one, the way 1400 and 14 degrees were.

Checked SQUISHIE22.REZ first, since matching the 1999 mod would have beaten
guessing. It ships three compiled DLLs and some sounds - no config, no readme
- so its scale is a float inside somebody else's 1999 Object.lto, where
nothing distinguishes it from every other float. Not worth a fishing trip
when the answer is one session of play.

FOOTSTEPS: a measurement, not a fix. The report is that a mech sounds human
to a squishie. Reading the path says that cannot happen - GetFootStepSound
(Shared/SurfaceTypes.h) picks the filename prefix from the WALKING
character's own mode, "f" on foot and "m" for a mech, and PlayFootStepSound
emits it from that character's object. Nothing consults who is listening, and
the squish code does not touch any of it.

So either a mech is not registering as one server-side, or what was heard was
the squishie's OWN steps, which at a fifth scale arrive far more often and
much closer to the ground than they ever have. Those want opposite fixes,
which is exactly when this project has historically guessed wrong four times
in a row. One AnimDebug line now names the character, its mecha flag, its
model size and the file chosen - so the next Squishie session either shows
"mstone1" coming out of a mech or it does not.

Deliberately reports the FILE rather than a verdict. A line that printed
"correct" would be the same assumption the code already makes, restated.

---

## 0.9.83 — 2026-08-07

**game mode reaches the launcher, and the server window answers better questions**

Squishie is CONFIRMED WORKING in play - the mode itself, the scale, the
respawn. First result for the largest untested item on the list.

LAUNCHER. A Game mode dropdown above Ruleset (Deathmatch / TOWs Out /
Squishie), writing GameMode into ShogoSrv.cfg. The setting existed and was
reachable only by typing it, which is why the owner did not know it was
there. Order mirrors FreshGameModeName() because the INDEX is what ships;
a rotation entry written "world:mode" still overrides it per map.

Gravity moves off the Ruleset row into the numeric grid under Heal scale,
where the other world values are. It was the only value sitting among
dropdowns.

SERVER WINDOW. Time in Level now reads elapsed/limit (12:04/15:00) when the
match has a clock at all - NGE_FRAGS stays bare rather than inventing a
denominator. Elapsed alone answers "how long has this been going" and never
"how much longer", which is the question anybody watching a server has.

"Level Goal" -> "Win Condition". "Running Time" -> "Server Uptime". "Net
Service" -> "Server IP", and it now shows address:port always, instead of
"TCP/IP [1.2.3.4]" - the transport named twice (there has been no other
service since 1998) with the port shown only when NON-DEFAULT, which is
backwards: somebody reading it off the screen to give to a player needs the
whole address, and the one time they were told the port was the one time
they could have guessed it. g_sFullTcpIpAddress keeps the old
conditional-port form because registration and peer exchange imply the
default.

PARITY, AUDITED. The owner asked for parity across the board and it is much
smaller than it sounds. Comparing every cfg.Set() key the launcher writes
against every CVarTrack the server registers, exactly TWO settings exist
under two names:

    EndTime  (launcher)  vs  TimeLimit  (console)
    EndFrags (launcher)  vs  FragLimit  (console)

GetTimeLimit() and GetFragLimit() both read "console var if non-zero, else
m_GameInfo", so an rcon TimeLimit 1 silently and permanently outranks the
launcher's 15 for the life of the process. That is the reported bug, and
FragLimit has the identical latent one that nobody has hit yet. Everything
else either round-trips already or exists on only one side.

Not fixed here because the fix depends on a design decision that is the
owner's: see the reply for why "launcher always wins" is the wrong shape and
what to do instead. Recorded now so the audit is not repeated.

NOT DONE, needs the owner: the squishie scale (FRESH_SQUISH_SCALE, one
constant in Shared/FreshGameModes.h, currently 0.2) wants a target number
rather than a guess; and the footstep report is ambiguous about which way
round it was heard - the squishie's OWN footsteps are already correct,
gated on m_playerMode.IsOnFoot() so GetFootStepSound picks PM_MODE_FOOT.

---

## 0.9.82 — 2026-08-07

**Rcon "NextLevel", because winding TimeLimit down is a trap**

The only remote way to end a level was to set TimeLimit to roughly the
elapsed time. That works exactly once and then bites: the limit STAYS set, so
the NEXT level ends the moment it begins. The owner hit this - TimeLimit 1
moved the map on and left the following level ending under itself.

Rcon "NextLevel" now does what the server window's Next Level button does:
hold the scoreboard for Intermission seconds, then load the next map; sent
again during an intermission, skip the rest of the wait. ONE implementation,
CRiotServerShell::InvokeNextLevel, called by both the button and rcon - an
operator and an admin pressing the same lever should get the same match, and
two copies of that would drift.

It prints through AdminPrint rather than FreshPrint, so the lines reach
whoever asked: the console always, and the rcon reply when a capture is open.
The button path is unchanged by that (no capture is open) and keeps the
behaviour the comment already described.

DIAGNOSED, NOT FIXED - the dialogue flood in the same log. On entering
OF_IKARI with four bots, Sounds\Enemies\Spot\Rogue.wav restarts roughly once
a frame, each one cutting the last short after 0.02s, which is the stutter
the owner heard. The chain is entirely ours and none of it is a mistake on
its own:

  - CFreshBot sets m_cc = ROGUE deliberately (FreshBot.cpp:100), because
    ROGUE has no case in GetAlignement() and so hates everything including
    other ROGUEs. That IS deathmatch, and it is why bots fight each other.
  - PlayDialogSound opens by calling KillDlgSnd, so any new bark cuts the
    one in progress.
  - So N mutually hostile bots in a tight map spot each other continuously,
    and every spot restarts the bark before the previous one is audible.

MCA_12FLOZ with the same four bots produced one Rogue.wav in ninety seconds;
OF_IKARI produced hundreds in nine. The difference is sightlines and spawn
density, not the mode - which is why this reads as new when it is really the
bot alignment finally meeting a map tight enough to expose it.

The fix is a guard on CST_EXCLAMATION so a bark does not restart one already
playing, near the m_bCanPlayDialogSound check that already exists for that
type. NOT done here: it changes AI audio in the campaign too, and dropping
that into a release the owner is mid-way through verifying would muddy what
the verification is measuring. It wants its own pass.

---

## 0.9.81 — 2026-08-07

**the rcon throttle was silencing the alarm it was protecting**

Two fixes and one retraction.

THE RCON FAILURE COUNTER. HandleRconRequest returned on the retry throttle
BEFORE incrementing s_nRconFails and before logging anything:

    if (fTime < m_fRconNextTry[nSlot]) return;

So a burst of guesses faster than FRESH_RCON_RETRY_GAP was neither recorded
nor counted, and therefore could never reach the >= 3 threshold that fires
the escalation line. The throttle exists to stop one guesser filling the log;
it had been quietly disabling the alarm that guessing is the thing an rcon
log is read for. Somebody trying passwords as fast as they could type them
was the quietest thing on the server - the exact inversion of what the
feature is for.

Now every attempt is counted and only what is SAID about them is throttled.
The routine DENIED line is suppressed inside the gap as before; the
escalation line fires on the crossing of 3 and every tenth after that, and
reports the TRUE count rather than the number that happened to get past the
throttle. The denial reply stays throttled - an attacker learns nothing from
it and a flood of them is bandwidth spent on their behalf.

Found while checking a report that the audit trail was not logging at all.
IT WAS. The lines were in the log the whole time:

    ShogoFRESH: rcon from WiredDropshipPilot (id 1): StoryDebug 1

My diagnosis before reading the code was that the audit used FreshPrint while
AdminPrint went to the server app - wrong, AdminPrint calls FreshPrintText
itself, same route. Worth recording because the wrong theory was plausible,
specific, and would have produced a confident no-op commit touching four call
sites. Reading AdminPrint took a minute and cost nothing; shipping the theory
would have cost a release. What the report actually reflected is that rcon has
no login: it is stateless, every command carries the password, so there is a
record per COMMAND and no authentication event to watch for.

THE UNDERSCORE IN THE HOST TAB. Server archives showed MPRETAILMAPFIXES.REZ
for MP_RETAILMAPFIXES.REZ, because a ContentPresenter treats "_" as an
access-key marker and eats it. Display only - the bound Name is what reaches
ShogoSrv.cfg and was always correct, verified against the live config - but a
launcher that disagrees with the filename on disk is one more thing to
discount while debugging a load order, which is precisely when nobody has the
patience for it. The name now goes in a TextBlock.

RETRACTION, from the same session: I claimed a dedicated server never mounts
Custom\*.rez and built an E11 explanation on it. False. HostService writes
RezFile0..N into ShogoSrv.cfg and CCoolServApp::AddResources appends them
LAST, after "custom", the retail patches and everything else. Ticked archives
do reach the server in the right order, confirmed against the live config.
The E11 theory that rested on it is withdrawn rather than left standing.

Still open and NOT explained: OF_LOST_CAT showing an MCA powerup after the
repaired archive was installed. The repair itself is verified good - the
object type is now BodyArmor_100 and the ArmorRepair_100 string remaining in
the file is the designer's Name label, which affects nothing. The next thing
to establish is whether the server's AddResources is last-wins like the
client's -rez, which has been assumed here and never confirmed, and which
decides whether any map fix can ever reach a dedicated server.

---

## 0.9.80 — 2026-08-07

**23 transmission lines get their punctuation back**

Kura trailing off mid-sentence rendered as a mojibake blob, and it was not
just hers: 23 strings in ClientRes.rc carried U+FFFD, the REPLACEMENT
character. That is what a decoder writes when handed bytes it cannot turn
into a character - so this was never corruption in the usual sense. Three
typographic characters failed to survive an encoding conversion at some
point in this file history, and the decoder recorded its own failure where
each of them had been.

Context recovers which was which, and there is no ambiguity in any of the
23: between two letters an apostrophe (1), spaced between words an em dash
(5), at the end of a line an ellipsis (17) - which is most of them,
because Shogo dialogue trails off a lot.

REPLACED WITH ASCII rather than with the correct Unicode. The engine draws
these through a 1998 bitmap font whose glyphs are ASCII; handing it a real
U+2026 would have produced a second generation of the same bug, authored
by us this time. So ' and " - " and "..." - all of which the font has had
since 1998.

Four of the 23 were an ellipsis ALONE as the entire string (2263, 2317,
2449, 2748) - a character pausing, with nothing else said. Those would
have drawn as a single blob with no clue what was meant.

---

## 0.9.79 — 2026-08-06

**revert the random opening map: it broke choosing a map by hand**

0.9.75 rotated g_NetGame.m_sLevels before the first world so a random
rotation would not always open on entry zero. The owner found the cost
immediately: double-clicking a specific map in the server window played a
DIFFERENT one.

The reason is the thing the change was written to avoid, arriving from the
other side. Rotating the array moves every map to a new INDEX, and the
level list and its double-click handler address maps by index. Whatever
order the app displayed no longer matched what the index meant by the time
the game DLL resolved it - so the click was accurate and the target had
moved. Two processes disagreeing about the rotation is precisely the
failure I claimed rotating would prevent; it just showed up in the server
window instead of in the map cycle.

Reverted rather than patched. Choosing a map by hand is a core admin
action that worked for years; opening on entry zero is a cosmetic
annoyance. Trading the first for the second is the wrong way round, and
the right version of this feature has to keep index identity - which
probably means an explicit start index the DLL is told about, not a
reordered array.

---

## 0.9.78 — 2026-08-06

**the grapple lets go of the sky**

Stock already refused to tractor-beam the sky, but the test asks about the
POLYGON: GetSurfaceType(info.m_hPoly) == ST_SKY. That covers a map whose
sky is sky-flagged world brushes and misses one whose sky is a WORLD MODEL
OBJECT, because the object carries whatever surface type its texture has.
OF_LOST_CAT is built the second way, so the grapple attached to the sky and
hauled the player toward the horizon; anything built the same way did the
same thing.

The hit object is now also asked what CLASS it is, against the sky world
model classes. By class rather than by flag because these are engine
classes we do not define - and GetClass returns null for a name this build
does not know, so an unrecognised one skips the test rather than refusing
every grapple. A refusal reports on the WeaponDebug channel, so "why did
my grapple do nothing there" has an answer.

Not a fix for the SKYBOX itself, which is a different and larger thing:
17_LOST_CAT builds its sky from three SkyPointers over Clouds1/Clouds2/
Buildings world models, and OF_LOST_CAT has none of that geometry - so
matching it needs brushes added to a compiled world, not a property patch.
Recorded here so the next session does not re-derive it.

---

## 0.9.77 — 2026-08-06

**the console stops fighting the person reading it**

Every arriving line yanked the server console back to the bottom, because
appending is done by selecting the end and the selection scrolls there.
On a busy server that makes reading anything older impossible, which is
exactly when reading it matters - and a console that fights the person
scrolling it is worse than one that does not scroll at all.

It now follows the tail ONLY IF ALREADY AT IT. "At the tail" is measured
before the insert from the control's first visible line, its client
height and its own font metrics rather than an assumed line height; within
one line of the bottom counts, so a view parked a pixel off still follows.
When the operator has scrolled up the text is still appended - the caret
and the scroll position are simply put back where they were looking.

Launcher Game limits: two aligned rows instead of one long horizontal run.
Four labelled fields on one line ran past the tab and clipped the last of
them. The two ROUND limits (what ends a match) are on the top row and the
two DURATIONS (what happens around it) below, which is the more readable
grouping anyway, and fixed column widths put both rows boxes under each
other. The server app got the same treatment in 0.9.75; this is the half
that lives in the launcher.

---

## 0.9.76 — 2026-08-06

**the rcon log answers "was that the same person"**

An audit trail already existed - one line per accepted command, one per
denial - but it carried only the net NAME. A name is chosen by the player,
is not unique, and can change between one command and the next, so a log
of names cannot answer the question anybody opens an rcon log to answer.
Both lines now carry the CLIENT ID as well, which is the server own handle
on them and the thing Kick and Mute take.

Consecutive failures are counted per slot. One typo is noise; a run of
them is somebody trying passwords, and that is the only event in this path
an operator would want to be woken up for - so it is said separately and
only past three, rather than being one more DENIED line to skim.

The first ACCEPTED command after a run of failures says how many it took.
"They guessed it on the fourth try" and "they knew it" are different
events, and the two lines were otherwise identical.

Nothing here is new plumbing: FreshPrint on the server already reaches the
server console and freshsrv-<date>.log, which is where these belong.

---

## 0.9.75 — 2026-08-06

**random rotations start randomly, and Limits stops clipping**

A random map order was only random from the SECOND map. Every later pick
already honoured MapOrder, but the opening world was always entry zero, so
a server on random order opened on the same map every restart and only
began behaving once a round had been played.

Fixed by ROTATING the rotation before the first world starts, rather than
tracking a start offset: the game DLL initialises m_nCurLevel to 0 and
counts from there, and handing it a different starting index would put the
same agreement in two processes. Moving the chosen map to entry zero keeps
one truth - the same cycle, entered at a different point, which is what
random order wants. The server console names the map and which entry it
was, so a surprising opening map can be checked rather than doubted.

Limits: all four fields now share one column pair, labels 56 wide instead
of 50. "Bodies linger:" is the longest of the four and was clipping - the
width is set by the LONGEST label rather than per label, or they stop
lining up the moment one grows.

---

## 0.9.74 — 2026-08-06

**one rcon command should cost one message**

The Rcon variable is consumed by writing it back empty, and that write
does not always take. UpdateRconCommand runs EVERY FRAME, so when the
variable survived, the command was resent every frame: the game console
filled with "rcon: StoryDebug", the server log took one "rcon from <name>"
line per frame, and the client went down under it. The owner hit all three
at once by doing nothing more unusual than turning on a debug channel.

Guarded on what was last SENT rather than on the clear having worked -
the same shape as m_nSentDebugMask, and for the same reason. The clear is
still attempted; it is just no longer load-bearing. A repeat of the same
command now needs the variable to change first, which is what typing a
new one does.

Also: the Host tab rcon password is masked, with a Show/Hide button. It is
two controls over one view-model string because WPF leaves PasswordBox's
Password off the dependency-property system deliberately - a password is
not something to leave in a binding engine's caches - so it cannot be
bound. The masked box is refilled from the view model whenever the Host
tab is shown, because the saved password arrives from ShogoSrv.cfg long
after the window was built and a stale blank there would become a real
blank on the next Save.

---

## 0.9.73 — 2026-08-06

**the MOTD case the reorder could not reach, and a field-height revert**

MOTD over the intermission, second attempt, and the first one was aimed at
the wrong order of events. 0.9.71 made the intermission message arrive
before the MOTD, which covers a player JOINING into an intermission. It
cannot cover the opposite order - notice already on screen, match ends
underneath it - because that decision is made where the intermission
message is HANDLED, and nothing there looked at the MOTD. It does now: an
active notice is dismissed rather than held, because the player has
already had it in front of them and a second copy after the map change
would be worse than none.

Field heights back to 12. A single-line Win32 EDITTEXT top-aligns its
text; there is no vertical centring to reach, so growing the box to 14
moved the text not at all and only added dead space beneath it - the
opposite of the intent. The owner offered the revert and was right to.
Both the growth and the revert were scripted over the SERVER_OPTIONS
block with assertions that the OK/Cancel row, always 14 and correctly
placed, did not move.

---

## 0.9.72 — 2026-08-06

**server options: quick turn is a toggle, and the numbers read like numbers**

Quick turn allowed moves from Rules to Toggles, where it belongs - it is a
checkbox and sat among the dropdowns only because it arrived with them.
Five checkboxes at 15 apart fill the height the four took at 18, so
nothing else moved.

Float fields showed printf default precision: "1.100000" in a box that
fits about that much, for a multiplier a human types. One shared formatter
(SetDlgItemFloat) feeds every one of them, so two decimals is a one-line
change and the read-back is unaffected - the getter is atof, which does
not care how many digits it is handed. The launcher's matching fields got
StringFormat=F2 so the two windows agree about the same variables.

The World box now shares one left column: the Gravity field and the Night
Color field both start at x=67, under labels that both end at 63. They
were at 67 and 73 under labels of different widths, which is the stagger
that was visible. Its right column is the one Speeds and Scales already
use, so the lower half of the panel lines up as a whole rather than each
box lining up only with itself.

Every edit field grew from 12 units to 14, centre held (y-1), because 8pt
text in a 12-unit box sits against the top edge. Done with a script over
the SERVER_OPTIONS block with assertions that the OK and Cancel buttons -
already 14 and correctly placed - did not move.

---

## 0.9.71 — 2026-08-06

**the MOTD ordering bug, and eyes on the MessageTouch path**

MOTD over the intermission scoreboard: the deferral was right and never
got the chance to run. The client holds the MOTD when it is already
showing the final scoreboard - two full-screen claims at once - and
decides that from m_bIntermission, which MID_FRESH_INTERMISSION sets. The
join sequence sent SendMatchInfo (which carries the MOTD) BEFORE
SendIntermissionInfo, so the MOTD arrived, found the flag still false, and
drew anyway. Reordered; the two lines were always in the wrong order and
nothing about the deferral logic needed touching.

Trigger instrumentation, for BUGS E11 - a trigger somewhere in OF_IKARI
drops the client, and "a hallway near the bathrooms" is not a coordinate.
Two additions to the StoryDebug channel:

- ACTIVATE lines now carry WHERE: the trigger's own position and the
  toucher's. Both, because a delayed trigger fires after the player has
  walked on, so the two answer different questions.
- The MessageTouch path is no longer a blind spot. It is how a trigger
  talks to whoever walked INTO it rather than to a named object, and it
  logged nothing at all while the named-target sends beside it logged
  happily. OF_IKARI's three "music level N" triggers are all of this
  shape, so the most suspicious triggers on the map were the ones the
  channel could not see.

---

## 0.9.70 — 2026-08-06

**the white monolith in a pickup spot, on any map**

Five pickup base classes - ArmorBase, FirstAidBase, EnhancementItem,
UltraPowerupItem, UpgradeItem - exist only to be inherited from, but each
is registered with BEGIN_CLASS, so DEdit lists all five beside the real
pickups and a level author can place one by mistake. Their constructors
leave the model and skin string ids at ZERO, and zero is IDS_DUMMYSTRING,
the literal word "placeholder". So the object asks for a model file called
"placeholder", gets the engine white default, and logs "Couldn't find
model skin placeholder." once per load. Stock knew: the comment at all
five sites reads "this will force the dummy model".

Tolerable in single player, where a level is authored once and looked at.
Not in multiplayer, where converted campaign maps circulate as packs -
OF_IKARI in MP_EnhancedRetailMaps carries exactly one FirstAidBase, which
is the white monolith the owner reported and the one log line per load.
A pickup nobody can take is worse than no pickup, because a player walks
to it.

All five now refuse to exist and say so once on the MapDebug channel.
m_nModelName is still the zero its own constructor set - only a real child
assigns one - so the member IS the evidence and no extra state was needed.
Removal happens at MID_INITIALUPDATE, the pattern BaseAI and BodyProp
already use, rather than depending on what the engine does with a
MID_PRECREATE return value. Two of the five had no initial-update handler
at all and gained one.

The test lives in Shared/FreshAbstractPickup.h rather than being written
out five times: five hand-copied blocks are how the identical "dummy
string" comment ended up in all five to begin with.

This is the "placeholder" line from the 0.9.68 OF_IKARI logs, now
explained and fixed. It is NOT the cause of that map dropping clients -
the owner has since confirmed that one is trigger-based (BUGS E11).

---

## 0.9.69 — 2026-08-06

**a 28-year-old white Akuma, and the server options dialog un-piled**

The bots'"'"' random mech pool rolled an Akuma and it came out white with
"Couldn'"'"'t find model skin Skins\Enemies\Akuma_a.dtx" in the console. The
file has never existed: GetSkin'"'"'s default case for the AI Akuma names an
_a skin that only the Ordog ever shipped - the line was pattern-copied
in 1998 and stayed unreachable for 28 years because campaign mech AIs
always carry one of the five faction classes. The ROGUE bots (0.9.61,
chosen so bots hate everyone including each other) are the first callers
ever to hit the default. It now names Akuma_CMC.dtx, which is what every
other mech'"'"'s default already says. The shock-trooper equivalent was
checked and handled when ROGUE was chosen; the mech pool was not - the
comment even says so.

The SERVER_OPTIONS dialog had accreted overlaps as controls were added:
"Rules" started 12 units inside "Limits", "Speeds" started 12 units
inside "Toggles", and Toggles'"'"' first checkbox sat ON its own group border,
overdrawing the caption - which is why the group looked headerless in the
owner'"'"'s screenshot. Rebuilt as a strict grid (rows 17 apart, groups 6
apart, two fixed columns), with the arithmetic in a comment so the next
control added has a rule to follow. "Game mode" moved from Limits to
Rules where it belongs, and "Random pickups:" got the label width it
needed instead of truncating to "Random".

---

## 0.9.68 — 2026-08-06

**Squishie: GameMode 2, fight on foot at a fifth scale**

On MCA maps, !squish in chat spawns you ON FOOT at 0.2 scale at your next
respawn; !mech climbs back in. BotAddNpc bots go squishie too. The joke is
Squishie 2.2's, done the FRESH way: that mod shipped replacement DLLs and
so could not coexist with anything; this is one game mode inside ours.

Nothing new crosses the wire. The scale rides the dims-scale array the
player-state message has always carried (forced under
PSTATE_MODELFILENAMES, since that is the only flag it travels beneath),
the eye height rides the server-sent camera offset, and the model scale is
ScaleObject - so a STOCK client can be a squishie. m_eModelSize = MS_SMALL
buys the whole 0.9.65 pipeline unmodified: small blood, small gibs, quiet
screams, scaled hand weapon, no weapon drops from squishie deaths.

Two traps dodged and recorded in decisions.md: SetCameraOffset writes the
per-mode STATIC defaults (a new ScaleCameraOffset touches one player
only), and the dims-scale resend is not implied by a mode change, only by
the filename flag. Speed and jump stay UNSCALED on purpose - a fast gnat
that jumps five times its own height is the mode. Preflight caught the
!squish announcement using a ternary as a format string; it is two literal
prints now, which is the rule working.

OF_* maps in a rotation are untouched - the override is gated on the start
point offering a mech, not on the mode being set.

---

## 0.9.67 — 2026-08-05

**centre the full-bleed menu backdrop by distance, not by dims**

0.9.66 widened the polygrid to fill the wider FOV and the owner saw it
left-aligned. The mechanism: FitPolyGrid takes a WORLD-axis-aligned box
(pos +/- vDims), and the menu camera sits at whatever yaw the world left
it. Stock survived that because 10.6 x 10.6 is a SQUARE - the only
footprint that presents the same width at every yaw. Widening world-X
broke the square, so the extra width landed on an arbitrary diagonal and
the surface sat off-centre.

The fix stops fighting the box: the grid is byte-identical to stock again
and the CAMERA DISTANCE closes from 10 to whatever fills the widened FOV
horizontally (7.5 at 16:9). Projection scales symmetrically, so it cannot
misalign; the result is a centre-crop of the stock 4:3 picture to the real
aspect - cover, not stretch. Capped at 13.3 so narrower-than-4:3 windows
cannot pull the grid's top and bottom edges into view.

Also: Tools/makespr.py - .spr reader/writer. Format settled against all
71 stock sprites (70 parse to the exact byte; one is a legal zero-frame
header). Writer round-trips SHOGO.SPR byte-identically. Frame paths are
engine paths; the tool refuses disk paths and non-DTX frames outright.

---

## 0.9.66 — 2026-08-05

**the menu backdrop fills the screen**

The pillarbox in CreateMenuPolygrid read as "the background video is 4:3",
but there is no video - the backdrop is a live-rendered polygrid with
Sprites\Shogo.spr playing across it, and a rendered effect has no native
aspect. The camera now covers the whole screen: vertical FOV pinned to
exactly what the pillarbox produced (90 horizontal at 4:3), horizontal FOV
computed from the real aspect, and the grid widened by the same ratio so
the camera never sees past its edge. Same height of effect as before,
wider aspects simply see more of it. The menu UI keeps its 4:3 design
space - only the camera changed. RemoveMenuPolygrid already restored a
full-screen rect, so the teardown needed nothing.

---

## 0.9.65 — 2026-08-05

**blood, gibs and screams sized to the body they came from**

The size system was half-plumbed in 1998: gib MODELS scaled with
m_eModelSize, the shooter's weapon FX scaled (weapon-id high bits), weapon
drops knew about size - but the gore and the screams never asked. So a
0.2-scale human on an MCA map bled a full-size cloud, burst into tiny
pieces wrapped in full-size gore, and died as loudly as the ten-metre mech
standing on him.

The victim's ModelSize now rides SFX_WEAPON_ID as an appended byte
(MS_NORMAL is zero, so a server that never sends it reads back as stock -
the safe integer-read default, engine fact 9). CreateVectorBloodFX in all
three detail tiers scales cloud, trail, splat and velocities by the
0.2/1.0/5.0 ladder; CGibFX scales its blood sub-effects (bounce splats,
mini explosions, ground pool, burst, spray, lingering smoke) and its two
incidental sound radii by the size it already carried. Screams:
CBaseCharacter::PlaySound scales radius by the dims scale and drops volume
to 55 for MS_SMALL - normal characters take the stock path bytes included,
because the volume flag only engages under 100.

MS_LARGE victims (campaign mechs under full gore) now bleed at 5x by the
same arithmetic. Deliberate; if play reads it as absurd, cap the factor at
1.0 upward rather than reverting the small end.

Also in this commit: Tools/makeplate.py, the transmission-plate assembler
(hi-res portrait into upscaled stock frame; new plates get captions
composed from glyphs harvested out of the stock captions themselves).

---

## 0.9.64 — 2026-08-05

**transmission portraits can be hi-res, and stock is pixel-identical**

The transmission plate's on-screen size WAS its pixel size: the draw treated
native dimensions as 640x480 design units, so the stock 296x68 plates have
been upscaled ~2.25-3x on modern displays (soft), and a sharper replacement
would have drawn larger instead of sharper - resolution and footprint were
welded together.

The weld is the height: a plate's design footprint is now its size divided
by (height / 68). Both stock shapes are 68 design units tall (296x68 and
the 68x68 ENFORCER variant), so every shipped plate normalises to a factor
of exactly 1 and renders pixel-identically - and a 4x plate (1184x272)
occupies the stock footprint and gets mildly DOWNSCALED at 1440p, which is
the right side of the resample to be on.

The portrait also always scales into its design rect now, in both HudScale
branches: with footprint and pixel size decoupled, the old HudScale-1 blit
would have drawn a 4x plate four times too large.

Authoring notes, measured from KATHRYN.PCX: plates are 8-bit palettised PCX
(the loader takes nothing deeper), the large lower-right region is PURE
BLUE RGB(0,0,255) which is the TRANSPARENCY KEY, not background - so art
must avoid pure blue - layout is a bordered 68x68 portrait at left with the
name banner across the top right (scale all coordinates by the chosen
factor), and 4x (1184x272) is the recommended authoring size.

Untested in play: a stock transmission rendering identically, and a hi-res
plate rendering at stock footprint.

---

## 0.9.63 — 2026-08-04

**multiplayer music defaults off, because IMA crashed three times today**

Three crashes in one evening of testing, all the same shape: the 1998 IMA
music middleware (IMUSIC25.DLL+0x3721, MSYNTH25 on the stack) calling
through a garbage pointer on its own worker thread, seconds after a
MULTIPLAYER world entry - twice on join, once on a map change. That is
exactly when the level playlists spin up, and it is the only place the
crash has ever appeared. The full record is BUGS.md E7.

Multiplayer worlds now skip the playlists unless "MusicInMultiplayer 1"
asks for them. The campaign is untouched - single player keeps its
soundtrack exactly as before. The variable started this release defaulting
ON out of caution about taking music away over a fault not yet understood;
the third crash arrived while that build was still compiling and settled
which way the trade runs. A soundtrack is not worth the client dying on a
map change, and the person who disagrees has the variable.

When the skip is active, whatever the previous world left playing is
stopped rather than carried over - silence that leaks the last map's
combat theme is not silence.

This is mitigation, not a fix: the fault is inside closed 1998 DLLs and the
honest options remain isolating IMA or replacing music playback, both out
of scope for a point release. What this buys is an evening of multiplayer
testing that does not end in a crash report.

Untested in play: an evening of multiplayer testing that does not end in a
crash report.

---

## 0.9.62 — 2026-08-04

**a multiplayer MCA map loaded in single player spawns the mech**

The custom levels menu made a new case routine: a MULTIPLAYER map loaded in
single player. Its start points carry PM_MULTIPLAYER_MCA - "the mech the
player picked" - and only the multiplayer branch translated that token.
Single player passed it through raw, SetPlayerMode could not apply it, and
the player KEPT the previous mode: on foot from the menu, or a mech if a
retail MCA level had set one earlier in the session. Which is precisely the
reported symptom, including the part where visiting a retail MCA map first
"fixed" it.

One line: the same translation multiplayer does, to the launcher's chosen
mech. m_dwMultiplayerMechaMode always holds a valid mode (Predator by
default, the launcher's mech setting otherwise), so there is no new
failure case.

Untested in play: loading a custom MCA map in single player cold.

---

## 0.9.61 — 2026-08-04

**bots get player-grade vitals, mechs on mech maps, and a loadout**

The DMR and shredder "too strong" reports turned out to be about the
TARGETS: a bot is a campaign shock trooper, with a campaign AI's hundred-ish
hit points and NO armour. Against weapons tuned for players - who spawn
with full health and full armour - bots were tissue, and every weapon read
as overpowered.

Armour, answered while implementing it: not simply a second health bar. It
is a damage sponge - each hit has 25-87.5% of its damage diverted to the
armour pool (the fraction scales with how full the armour is), and the rest
reaches health. Full armour on a fresh spawn therefore roughly halves
incoming damage, which is exactly the margin bots were missing.

Bots now spawn with a player's full health AND full armour for their tier,
from the same CA_PLAYER_* constants CPlayerMode reads. Applied on the FIRST
update, deliberately: BaseAI's initial update writes the campaign numbers
over everything, so a value set at spawn quietly vanished.

MCA MAPS GET MECH BOTS - one of the four campaign mech AIs at random
(Ordog, Enforcer, Predator, Akuma; the art and animations already ship),
with mech vitals and mech weapons. The tier comes from the map name, the
same MCA_* convention the alternating map order already trusts.

LOADOUT: a sidearm (colt pair on foot, pulse rifle in a mech) plus one
random weapon that is not on the server's blocklist, swapped between every
8-20 seconds with ammo refilled at the swap. Explicitly a stand-in for real
item-seeking, the same spirit as the waypoint patrol - the owner's framing,
and the right scope until a nav system exists.

Also: FreshSrv strips the stale Custom\ prefix from rotation entries AT THE
READ, so the "would not start - started as" line stops appearing for
configs the launcher has not re-saved. (The owner's sighting of it
postdated the 0.9.60 fix being written but predated running it; this makes
the answer not depend on which binary is running.)

Untested in play: all of it - mech bots especially, since campaign mech AI
has never run on a deathmatch map.

---

## 0.9.60 — 2026-08-04

**the Enter that closed a menu stops opening the chat, and tuning lands**

THE LEAKED ENTER. The keypress that worked a menu item (Respawn) or
dismissed the disconnect notice was still down - or its action still
latched - when play resumed, and the same press arrived at
COMMAND_ID_MESSAGE and opened the chat. Swallowed now by a one-shot
0.75-second window armed at the three places the press gets spoken for:
menu close, message-box dismissal, world entry. The guard deliberately
FAILS OPEN: a world load resets the engine clock, a stale stamp then reads
as out of range, and chat works normally rather than eating a real press.

TUNING, all FRESH-gated, Classic untouched as always:

- DMR on foot 34 -> 28. Reported still too strong once the trigger had no
  cadence limit - the DPS of "34 x as fast as you can click" outran
  everything on foot. Four hits nominal now, a high-rolling three still
  lands, and per SHOT it sits under the SMG's 30: the SMG sprays, the DMR
  lands its shots at any range. Precision is the premium, not the number.
- Shredder 400 -> 300 in the mech tier, via a new FreshWeaponDamage - the
  first tier-independent FRESH override, because the problem was the 1998
  number itself: a three-hit kill from rapid flak out-shot every other mech
  weapon. Four hits now.
- Shredder magazine 12 -> 6, pickups one magazine, carry four - the
  doctrine of the FRESH ammo table.

RENAMES: "Pistols" -> "Handguns", "Machine Gun" -> "SMG". Unconditional,
unlike the DMR: that rename tracked a behavioural change and Classic keeps
the 1998 weapon, but these are just better names for the same guns.

Also finishes the stale-prefix quieting from earlier: the listen-server
fallback (SwitchToWorld) had silently missed the 0.9.56 reorder - the
python edit reported success without applying - and both fallbacks now try
bare FIRST for Custom\-prefixed entries, so the "would not start - started
as" line stops firing for configs written by 0.9.46-0.9.55. One Host-tab
save retires those entries entirely.

Untested in play: the Enter guard at all three sites, the new damage
numbers, the 6-round shredder.

---

## 0.9.59 — 2026-08-04

**the dedicated server mounts maps\mp, one line beside the stock mount**

The owner's dedicated-server test hit the documented caveat: maps\mp maps
listed in the launcher, and "Unable to start world: OF_575" from FreshSrv.
The fix turned out to be one guarded line, because the mechanism was already
there in stock code: CCoolServApp::AddResources has mounted "custom" since
1998 - sGame[i++] = "custom", sitting right next to the game rez.

That line also corrects yesterday's theory. Bare names never loaded on the
dedicated server through some loose-file search in server.dll; they loaded
through THIS mount, the same mechanism as everything else. The 18:31 log
that seemed to prove the search (bare name failing while the file sat in
maps\mp) was really proving the map was outside the mounted directory.

custom\maps\mp now mounts beside it, GUARDED on existence - unlike "custom",
because AddResources failing refuses to start the server, and most installs
will never have the folder. maps\sp stays out: a dedicated server has no
single player and its rotation lists never offer it.

Untested in play: the dedicated server starting OF_575 from maps\mp - the
exact command that failed.

---

## 0.9.58 — 2026-08-04

**the sp/mp map split, on the mechanism that actually exists**

The folder split returns, built on what was measured rather than what was
assumed - and the owner did the measuring: adding a nested-directory mount
by hand through Extra Args put maps\mp maps in the game, which settled the
one assumption 0.9.46 shipped without testing.

The design is now honest about what each part does. MOUNTS make a map
loadable: the launcher passes -rez for Custom\, Custom\maps\sp and
Custom\maps\mp, every mount flattens to the file-tree root, and every map
loads by its bare name wherever it sits. FOLDERS only decide which lists a
map appears in: maps\mp feeds the launcher's rotation list and is excluded
from the single-player custom menu; maps\sp and loose Custom\ feed the
single-player menu. The exclusion reads the DISK (Win32, the same
loose-file access Object.lto has always used for bans) because the engine's
tree cannot answer "which folder" - it flattened the folders away, which is
the exact property that makes loading work.

Same name in two folders is one world; last mount wins. The launcher's
rotation list dedupes case-insensitively for the same reason.

DEDICATED-SERVER CAVEAT, stated rather than assumed: FreshSrv's loose-file
search covers Custom\ and the root, not the subfolders, and
directory-as-RezFile through the ServerMgr is untested. A dedicated
server's rotation maps stay in Custom\ until that is measured. Listen
servers get the mounts from the launcher and are covered.

Untested in play: the single-player menu EXCLUDING a maps\mp map, and a
maps\sp map staying out of nothing (it was never listed for rotation).

---

## 0.9.57 — 2026-08-04

**the hunt's scaffolding comes down, and the dedupe lands**

CONFIRMED IN PLAY by the owner: with "-rez Custom" mounted, loose maps work
in the single-player list AND in multiplayer with a joining client. The
custom-map saga is closed, and this release removes what was built to chase
it:

- the unconditional "levels:" startup lines return to the MapDebug channel.
  They ran unconditionally for exactly one useful reason - the menu hosting
  the channel hid itself in the broken case - and the null they caught is
  what settled everything. With the mount in place they are startup spam;
  the channel stays for the next hunt.
- rotation entries from the 0.9.46-0.9.55 era ("Custom\X", "Custom\maps\mp\X")
  are normalised to bare names on load, so the next save writes a clean
  rotation and the server's "would not start - started as" fallback line
  retires itself. The fallback code stays - it is the safety net for configs
  this launcher never touches.

HudDebug was never on by default - "HudDebug" "1" was sitting in the owner's
own autoexec.cfg from an earlier debugging session, and the launcher never
writes the key. Set to 0 in place.

The resource-id dedupe lands from its worktree, with one adjustment that
proves its own point: the dedupe moved the game-mode combo to 1084, and
BodyLifetime took 1084 on main in the meantime - the exact collision class
the work exists to end. The combo lands at 1085, and the check_resource_ids
preflight check that came with it caught the stale _APS_NEXT_CONTROL_VALUE
on its first run against main. A check that fires before its first commit
is a check earning its keep.

Untested in play: nothing new - this is removal and merge.

---

## 0.9.56 — 2026-08-04

**"-rez Custom": the missing mount that was the whole custom-map saga**

The answer was in our own reverse-engineering notes since July 31.
shogo-re/notes/02-launch-dll.md, step 3: Monolith's launcher passes
"-rez custom" - the DIRECTORY. A directory mounts like an archive, its
contents joining the engine's file tree at the root, so Custom\OF_Vision.dat
is the world "OF_Vision", bare. That is why stock rotations hold bare names,
why the stock menus saw loose maps, and why our clients were blind: this
launcher mounted only .rez files, never the directory.

Proved from the outside by the owner running Monolith's launcher: a listen
server came up on MCA_BattleCube straight out of Custom\, which killed the
"engine cannot see loose files" fact within an hour of it being written.
The corrected fact 22 now records the mechanism, the measurement, and the
lesson - three releases of inferences about world-name SPELLING, when the
variable was the file TREE, settled by one GetFileList measurement and one
run of the original launcher.

The launcher passes -rez Custom before the Custom\*.rez archives, so loose
maps reach every client this launcher starts, listen hosts included.
Rotations return to BARE names - the canonical form the mount produces - and
the map list writes them. The in-game wizard's custom list reverts to the
stock root scan, which only ever looked empty because the mount was missing.
The 0.9.53 Custom\ prefix and the 0.9.46 subfolders are both gone; the
server-side load fallbacks stay (a 0.9.53-era config still resolves) but try
the entry as written first, which also retires the "OF_Pikachu would not
start - started as OF_Pikachu" log line.

Also noted from the owner's diff: the Net* keys the original launcher left
in autoexec_n.cfg are the 1998 wizard persisting its own dialogs
(NetStart's WriteConsole* calls) - benign, and nothing of ours reads them.

Untested in play: a loose Custom\ map appearing in the single-player list,
loading in a rotation, and surviving a client join. One run answers all
three, and for the first time this evening the mechanism underneath is
measured rather than assumed.

---

## 0.9.55 — 2026-08-04

**the client cannot see loose files at all, and that was the whole thing**

The unconditional scan report answered it on the first run:

  levels: "Custom" - the engine returned no list at all
  levels: "\"      - the engine returned no list at all

GetFileList returns NULL - not an empty list - for Custom\ and for the game
root. The client's file tree is the -rez archives and nothing else, so on
the client a loose .dat map does not exist. It cannot be enumerated and it
cannot be loaded.

That single fact explains three symptoms I had been treating as separate
problems for three releases:

- the single-player custom-levels list is always empty, and always has
  been. The stock "load level" menu that read loose .dat files quietly did
  nothing, and nothing in the code said so;
- a custom map in a rotation loads on the SERVER and then drops every
  client that joins - "could not load level";
- putting the file in the game root changes none of it, which is why the
  1998-layout experiment came back negative.

The server is the odd one out: server.dll under FreshSrv.exe DOES read the
game directory, so a loose map loads there from its bare name and the
server's own log looks perfectly healthy while every joiner is refused.
Every asymmetry chased between 0.9.46 and 0.9.54 was this one wearing a
different hat, and it stayed hidden because the only instrument that could
see it was on a channel that printed from a menu the failure prevented
opening.

So there is no spelling that satisfies both sides. A map has to be inside a
.rez to reach the client - which is why every custom map that has ever
worked here shipped as one, and why MP_EnhancedRetailMaps.rez works while
the same maps loose beside it do not.

Written up as engine fact 22, replacing the version of it I wrote yesterday
from inference. Also corrects a log line that read "OF_Pikachu would not
start - started as OF_Pikachu instead": it named the rotation entry rather
than the candidate that failed, and with a bare entry those are different
things.

No behaviour change beyond the message. The fix - packaging loose maps into
an archive so the client can see them - is a feature, and it is the owner's
call whether the launcher should do it automatically.

---

## 0.9.54 — 2026-08-04

**the level scan reports itself, and the game root is offered again**

Two corrections, both to my own guesses.

"Custom\<name>" was wrong. 0.9.53 reasoned it was the form both sides could
resolve; the server answered directly:

  18:43:10  "Custom\OF_Vision" would not start - started as "OF_Vision" instead

So the server takes ONLY the bare name, the client takes neither from
Custom\, and the world load on connect happens inside the engine where
nothing of ours can intervene. What is left is 1998's own arrangement: a
loose map in the GAME ROOT, loaded by its bare name, which is the one
layout both sides have always handled. The launcher lists root maps again.
Custom\ stays listed - hiding maps somebody already installed helps nobody -
but it is now the half-working option rather than the recommended one.

MapDebug reported nothing, and the reason is worth keeping. It printed from
the custom-levels menu, and the single-player menu HIDES that item when the
list is empty. The one case the diagnostic exists for is the one case you
cannot open it in. A channel you have to open the broken thing to read is
not a diagnostic - so the per-directory summary is unconditional now and
goes to the client log, where it can be read after the fact and sent by
somebody who has never heard of a console variable. The per-file detail
stays on the channel.

That summary is what the next run needs to settle the remaining question:
whether the client can ENUMERATE Custom\ at all, or only fails to load from
it. The two have different answers and I have been inferring between them
for three releases.

Untested: whether a map in the game root fixes both the join and the
single-player list. That is the experiment this release exists to enable.

---

## 0.9.53 — 2026-08-04

**custom maps are "Custom\<name>", because the two sides disagree**

The server and the client do not resolve a world name the same way, and the
gap is what a server that starts perfectly and disconnects every joiner
looks like from the outside:

  18:35:01  Loaded world: OF_Vision
  18:35:06  New client, id 1, for a total of 1.
  18:35:06  Removing client, id 1, leaving 0.

A loose map in Custom\ loads on the SERVER from its bare name. The client
will not take one - "could not load level", and the connection drops. The
server's own log says nothing is wrong, because from where it is standing
nothing is.

"Custom\<name>" is the form with evidence on BOTH sides: it is what the
single-player level menu has always handed the client. Rotations are written
that way now, and both load paths try it first even when a bare name would
have worked locally, since a world only the server can load is no use to a
server. Legacy rotations full of bare names are therefore fixed without
being re-saved.

The Custom\maps\sp and Custom\maps\mp folders are GONE. The engine refuses a
nested path outright, so the split could never have worked - listing maps a
menu cannot then load is worse than not sorting them. That is 0.9.46's
mistake in full: a folder layout invented on an assumption about how the
engine addresses a world, shipped without testing the assumption, and it
took two releases and a lot of the owner's evening to find the floor.

Written down as engine fact 22, at length, because "put the map packs in
tidy folders" is an obvious idea to have again and the next person deserves
to meet the answer before the bug.

Untested in play: whether a client can now join a custom map. That is the
one thing this release exists to fix, and the single-player custom levels
menu is worth the same look.

---

## 0.9.52 — 2026-08-04

**a rotation entry the engine cannot resolve no longer kills the match**

The log settled what Next Level Now was accused of. It worked:

  16:33:33  Custom\maps\mp\OF_Vision chosen from the rotation list
  16:33:36  next level invoked again - skipping the rest of the intermission
  16:33:36  World ended

and then nothing. The skip fired, the world ended, and the replacement never
arrived - because the ENGINE WILL NOT RESOLVE "Custom\maps\mp\X". The
explicit form of the same failure is the server refusing to start at all:
"Unable to start world: Custom\maps\mp\OF_MoreSkillz". A map that sat in
Custom\ loaded from its bare name, so the folder path is what it objects to,
not the file.

0.9.46 introduced those paths. It should not have done so on an assumption
about how the engine addresses a world, and the assumption is now the thing
being tested rather than trusted.

Both load sites try the alternatives and SAY WHICH ONE ANSWERED: the entry
as written, then the bare name, then "Custom\<name>". That is the fix and
the diagnostic in one - the next run names the form the engine accepts,
which is what the folder feature has to be rebuilt on. Failing all three now
prints that the match has no world instead of leaving an empty server and no
explanation.

The server-start path mattered more than the rotation one: there the failure
was fatal and took the whole server with it.

NOT YET SETTLED, and deliberately not guessed at: whether the engine can see
Custom\maps\* at all. If it cannot enumerate the folder either then the
split needs a different mechanism - the SP menu reported "custom worlds not
found" before 0.9.50 made that list rebuild on open, so that reading is
stale. MapDebug 1 answers it.

Untested in play: the fallbacks, though they cannot do worse than the
current outright failure.

---

## 0.9.51 — 2026-08-04

**Next Level Now actually skips, and a level can be chosen by hand**

NEXT LEVEL NOW DID NOTHING, and the reason is that 0.9.50 changed the label
without changing the control. The button still wanted a two second hold, so
a click on something captioned "Now" was read as a press too short to count
and discarded. The server side had always supported the skip; the button
never sent it.

It is a two-stage control now, the same shape Shutdown has had: hold to
commit to the change, then a PLAIN CLICK to stop waiting for it. The hold
logic stands aside entirely while armed - left in, it would have counted out
"Hold... 1.9s" over a button already waiting for a single press, which is
the same class of mistake as the label change on its own.

DOUBLE-CLICK A MAP IN THE ROTATION to go to it. Runs the same intermission
the frag limit would, with the chosen map announced on the scoreboard rather
than the rotation's own next one, so players get their few seconds of final
scores instead of the world vanishing. BeginIntermission takes the choice
and StartNextMultiplayerLevel already honoured whatever the intermission
announced, so the load path needed nothing. The list had LBS_NOSEL and could
not be clicked at all; it is LBS_NOTIFY now.

A double click rather than a single, for the reason Next Level has a hold:
changing the map for everyone should take more than brushing a list an
operator keeps open all day.

Interface, as asked: launcher swaps Gravity and Bodies linger (gravity sits
with the game variables it belongs to, bodies linger with the other
durations) and "Round limits" becomes "Game limits". The server options
dialog gains Bodies linger where Gravity was, moves Gravity into World ahead
of Time Speed with Night Color on its own line beneath, and "Round Limits"
becomes "Limits". Mute/Kick/Ban are centred under the player list.

Untested in play: all of it.

---

## 0.9.50 — 2026-08-04

**the level list is read when you open the menu, not when the game starts**

MapDebug printed nothing because the thing it instruments had already
happened. CLoadLevelMenu read its directories once, in Init, at shell
startup - before there is a console to type a variable into. The channel was
working; there was simply nothing left to report by the time it could be
switched on.

The same single read is why a map dropped into a Custom folder stayed
invisible until the next launch: the game had looked before the player had a
chance to put anything there. Both are one fix - BuildFileList now runs on
every menu open, so the list reflects the folder as it is now and MapDebug
reports a fresh read every time.

Extracted with a FreeFileList that drops the surface HANDLES too, not just
the arrays holding them. The arrays are reallocated at whatever size the new
read produces, and freeing them alone would have leaked one engine surface
per level per menu open.

THE NEXT LEVEL LABEL WAS BEING TOLD TWO DIFFERENT THINGS. Two reporters
computed it independently: the periodic update was honest about a random
rotation ("chosen at the change"), and the level-change message assumed
sequential and named current+1. The server window alternated between them,
which is exactly what was reported - sometimes the random line, sometimes a
concrete map that usually was not the one that came up. One implementation
now, BuildNextLevelLabel, and the comment on the honest version says why
"current + 1" is a lie under a random order.

The Next Level button stays LIVE during the intermission and reads "Next
Level Now" instead of greying out as "Intermission...". The game has always
taken a second NEXTLEVEL as "stop waiting and load it"; the disabled button
was the only thing between the operator and that. Shutdown's hold drops from
four seconds to two, and the row now reads Next Level, Options, Shutdown -
left to right in the order they get used.

Rotation label carries its count against the game's limit, "Rotation
(12/100)", because the limit used to be invisible until the server met it -
and for one release meeting it crashed the server.

Untested in play: all of it, but MapDebug 1 before opening the campaign
levels menu is now the thing that answers the custom-map question.

---

## 0.9.49 — 2026-08-04

**a hundred maps in a rotation, in the same number of bytes**

MAX_GAME_LEVELS 50 -> 100, and the struct did not grow: entries went from
128 bytes to 64, so 100 x 64 is exactly the 50 x 128 it was before.

That is the point of doing it this way rather than simply making the array
bigger. NetGame reaches the server as an OPAQUE BLOB - StartGameRequest
carries a pointer and a length, ServerDE::GetGameInfo hands back a copy -
so the engine never parses it and the layout is ours. What is NOT ours is
how large a blob the engine's copy will accept: the SDK documents no limit
and whether a fixed buffer sits behind it is not established. Ghidra was not
running to settle it. Keeping the size byte-identical means the question
never has to be answered, and a wrong guess cannot become a worse crash than
the one 0.9.48 just fixed.

63 characters is comfortable for what an entry holds: "Custom\maps\mp\"
leaves 48 for the name and any ":mode" suffix, against 27 for the longest
retail path. Every write into a rotation row is bounded now - two were
plain strcpy from 128-byte scratch buffers and would have overflowed the
shorter row - and both the game and the launcher SAY when an entry is too
long rather than silently losing its tail.

Going past 100 means growing the blob, and that needs the engine's copy path
read first. Written down in NetDefs.h so the next person starts there.

Preflight now checks the two NetDefs.h copies agree on NetGame's layout.
ShogoServ carries its own partial copy deliberately (it is MFC), so this is
the "one fact implemented twice" case CLAUDE.md says to pin - and it nearly
cost us already, since 0.9.48's overflow fix had to change both. Verified by
breaking it on purpose: it reports which constant differs and in which file.

Also adds the MapDebug channel. "Custom worlds not found" is the same
message whether a folder is empty, missing, or full of maps the engine
declined to enumerate, and those need different fixes - MapDebug 1 now names
every directory searched, every entry in it, and the .dat count, so the next
run distinguishes them instead of another guess.

Untested in play: the 78-map rotation loading, and MapDebug's output.

---

## 0.9.48 — 2026-08-04

**the dedicated server could not start with more than fifty maps**

A rotation of 78 maps overflowed NetGame::m_sLevels, which holds 50.
NetStart_GoNoDialogs walked the level collection and wrote one entry per map
with no bound, so 28 entries - 3,584 bytes - went over the globals behind the
array. Among them MFC's own state, which is why the crash surfaced at
AfxGetMainWnd inside DoModal, nowhere near the write, and why the server died
before it drew a window.

nCount was computed on the line above and never used. That is the shape of a
bound somebody meant to apply, and the dialog path a few hundred lines away
has always had one: "if (nCount > MAX_GAME_LEVELS) nCount = MAX_GAME_LEVELS".
Stock 1998 code; it needed a rotation longer than fifty maps to reach, and
0.9.46's Custom\maps\mp folder is what finally produced one.

THE CRASH REPORT DIAGNOSED ITSELF. Its single breadcrumb should have read
"crash handler installed" and instead read "Custom\maps\mp\OF_Pikachu" - the
overflow had walked over the breadcrumb buffer on its way through, so the
diagnostic was overwritten with the data that overwrote it. That named the
subsystem, the input and the mechanism in one line, which no stack trace here
could have done: every frame in it was MFC.

Fixed on both sides, because they fail differently. The server bounds the
copy and logs what it dropped, so an operator who put eighty maps in a
rotation learns that fifty are running. The launcher trims to fifty on save
and reports it through the same clamp channel that already explains bot fill
and gravity - writing a rotation it knows cannot be loaded was the launcher
promising something the game cannot keep.

Raising MAX_GAME_LEVELS is deliberately NOT part of this. NetGame is shared
with the server application and handed to the engine by size, so all three
modules have to agree - that is a protocol change, and it does not belong in
the same commit as the crash fix that makes the current limit safe.

Also confirmed for the owner's question: the weapon is still "Sniper Rifle"
under Classic in single player. GetWeaponString reads UseFreshRules(), which
follows the ClassicCampaign var through g_nRuleset, and the lookup happens
per use rather than being cached - so the name tracks the setting live.

Untested in play: starting the server with the 78-map rotation that produced
the crash.

---

## 0.9.47 — 2026-08-04

**the marksman rifle is called the DMR, under FRESH rules only**

Two names for one weapon id, because there are two weapons. Under FRESH it
is a semi-automatic marksman rifle with hip spread and 34 damage; under
Classic it is the 1998 full-auto scoped rifle, untouched. Calling both "DMR"
would describe the code rather than the game - the same reasoning that gave
the game modes a human name and a slug, and the same reasoning Classic
itself rests on.

GetWeaponString picks the id from UseFreshRules(), so the HUD, pickup lines
and kill feed all follow without further edits. The launcher's blocklist
names it DMR outright with a note that says what it now is - a server runs
FRESH unless it opts out, and the blocklist is a server tool.

Left alone deliberately: IDS_SNIPERRIFLE (a bitmap path, not a name), the
"Slugs" ammo string (still slugs), the SniperRiflePowerup class name (levels
reference it by name and renaming it would break every map that places one),
and the launcher's two Classic tooltips that describe 1998 behaviour, which
is still what they describe.

Untested in play: the name appearing on the HUD and in pickup text under
each ruleset.

---

## 0.9.46 — 2026-08-04

**the zoomed-fire bug was a missing animation; a marksman rifle; map folders**

THE ZOOM-FIRE BUG IS FIXED, and it was never in the state machine. Both
copies of PlayFireAnimation selected m_nFireZoomAni whenever the view was
zoomed without checking that it exists. "Fire_zoom" is only in models
Monolith shipped with a scope, so every weapon ShogoFRESH made zoomable
since resolves it to INVALID_ANI - and setting that plays no animation,
which produces no fire key, which fires no round.

Settled by the owner's report that it "doesn't fire unless I fire unzoomed
first" - that is the function read literally: firing unzoomed leaves a VALID
fire animation running, so the guard at the top is false next shot, the
block is skipped, and the animation already playing keeps producing fire
keys. A description of when the bug does NOT happen located it in minutes
where two attempts at when it does had failed. Both of those searched the
weapon state machine (fact 1); nothing there was wrong, because a missing
animation is not a state. Same root cause as the laser cannon emptying a
magazine in one shot after a reload.

THE SNIPER RIFLE BECOMES A MARKSMAN RIFLE. New FIREMODE_SINGLE - one round
per trigger edge, no cadence limit - at the hip, SEMI's deliberate gap when
scoped. Hip spread 12 where it was pinpoint in both states. On-foot damage
55 -> 34: three hip hits to kill, two scoped.

And a finding that came out of reading rather than playing: the sniper's
m_fZoomDamageMult is 7.0, a number sized against a mech's 1000 hit points.
On foot that was 55 x 7 = 385 against a 100-point player - a guaranteed kill
anywhere on the body, four times over, at any range. The comment above
FreshOnFootDamage claimed the weapon "can never one-shot"; that was true
only at the hip, and the scoped case had never been worked through. It has
survived since 0.9.0. On-foot zoom multiplier is now 2.0. MECH DAMAGE AND
ITS 7x ARE UNTOUCHED, the same call FreshOnFootDamage made.

Zoom-dependent spread needed care: client and server each compute the path
and agree only by walking the same table from the same seed (fact 6). Safe
here because the zoom flag travels WITH the shot - CPlayerObj hands it to
SetZoom out of the fire message - so both use the client's state at the
instant the round left.

CUSTOM MAP FOLDERS. Custom\maps\sp feeds the campaign-levels menu,
Custom\maps\mp feeds the rotation lists and the in-game wizard. Nothing in a
.dat says which game it is for, so the folder is the declaration; GetFileList
does not descend, so a map in one never leaks into the other list and no
exclusion logic is needed. Custom\ and the game root are still read by both
and always will be - that is where every map installed before today lives.
The wizard's custom list also reads more than the game root for the first
time, which is a gap it has had since 1998.

NOT RENAMED YET: the weapon is still "sniper rifle" in the string table and
on the HUD. The behaviour change is the reversible half; the rename touches
resources and pickup text and is worth doing deliberately.

Untested in play: all of it - the zoomed fire on a mech weapon, the DMR's
feel and the new damage numbers, and both map folders.

---

## 0.9.45 — 2026-08-04

**a kicked or banned player is told why, on screen, before the door**

NoticeAndKick: the disconnect now waits six seconds while the reason sits on
the player's screen - the MOTD panel, overriding whatever welcome it was
showing (it is the one full-screen notice the client already draws), plus
the same words as a chat line for stock clients that have never heard of
MID_FRESH_MOTD. The window is spent HOLSTERED, forced, so reading time
cannot be spent shooting - the owner's suggestion, and the machinery
existed. A per-player pending time swept every update executes the kick.

Corrected premise, recorded here: bans were never on a 20-second window.
EnforceBans sweeps every update and kicked the moment the player object
existed; the 20 seconds is RequireFresh's hello grace, which must wait
because the hello arrives after world load. This release ADDS a short
window where there was none.

What each subject now sees:
- banned on rejoin: reason and time remaining (or "does not expire"),
  from the ban list's own expiry - the two questions the engine's bare
  LT_REJECTED could not answer
- banned live: reason and duration as the admin typed them
- kicked: "Kick <id> [reason]" - the variable became a string, so kicks
  carry reasons now too; bare "Kick <id>" still works

The server app's Kick button goes through the Kick variable instead of the
engine's BootClient, which it had used since it was built - BootClient
bypassed both the notice and the kick-reconnect cooldown, so a
button-kicked player could return instantly where a console-kicked one
could not. Two doors, one rule now.

Known edge: a banned player who joins DURING an intermission gets the chat
line but not the full-screen notice - 0.9.44's MOTD deferral holds it past
the kick. Rare enough to ship; noted for the backlog.

Untested in play: the whole notice window - screen, holster, six-second
timing - on both fresh and stock clients.

---

## 0.9.44 — 2026-08-04

**a mute button, a patient MOTD, and bodies that stay two minutes**

The server app gains a Mute button, left of Kick as asked - the moderation
ladder now reads left to right in escalation order: mute, kick, ban. It goes
through a new "Mute <id> [minutes]" string game variable, the same door Ban
already uses (the state lives in Object.lto with the player), and the one
implementation - MuteById, extracted from the rcon handler so the two routes
cannot drift - keeps mute's deliberate manners: the target is told, and only
the target. No confirmation dialog, deliberately: mute is the rung you press
without deliberating, and it expires on its own. "Unmute <id>" works as a
variable too.

A MOTD that arrives while the final scoreboard is up is now HELD rather than
drawn over it, and shown when the next map starts. Held rather than dropped:
the MOTD is sent once per connect, so a player who joined during the
intermission would otherwise never see the server's rules.

BodyLifetime default goes 10 -> 120 on both sides - Monolith's own original
value, before the 2.2 patch cut it to 10 for 1998 hardware ("ack", says
their patch note, in the other direction).

MarkClip needed no change: missing-var already means clip-on (MarkSFX.cpp),
and nothing seeds it off - a leftover "MarkClip 0" in a tester's autoexec is
the only way to see it disabled.

Untested in play: the Mute button end to end, the held MOTD appearing after
a map change, and 120-second bodies on a busy server.

---

## 0.9.43 — 2026-08-04

**model brightening is declined in multiplayer, and a campaign setting in single**

ModelAdd and ModelDirAdd - engine console variables that add ambient and
directional light to every model, recommended at 50 50 50 by Monolith's own
2.2 readme "to increase visibility of other players in some dark multiplayer
levels" - are now zeroed by CShell for the duration of any multiplayer world,
re-asserted once a second (the renderer re-reads them every frame, so a
retype flashes for under a second and is caught), and restored on world exit
and at engine termination.

ALWAYS ON, no server var, no client checkbox - the owner's call. The server
cannot observe these variables at all (fact 17: pure rendering, no gameplay
signal), so unlike FirstPersonOnly there is no check to build; a rule with
zero detectability that a server can declare adds exactly one thing, a way
to switch the protection off. Stock clients keep the 1998 advantage - the
same honest limit QuickTurn lives with, and RequireFresh remains the one
configuration where the clamp binds everyone.

RESTORE IS LOAD-BEARING: Client.exe persists both variables to the config on
exit (shogo-re/notes/08-rendering.md), so a clamp that forgot would destroy
the player's single-player value. Which now exists on purpose: the Settings
tab gains campaign model lighting - brightness (ambient, reads as mild
fullbright) and directional (scaled by facing, keeps shading) - written as
grey triples, single player only by construction.

Untested in play: the clamp catching a mid-match retype, the restore after
quitting from inside a match, and the two new Settings fields rendering.

---

## 0.9.42 — 2026-08-04

**Respawn on the menu, body lifetime on the Host tab, saturation in Settings**

Three console features surfaced, each where its consumers are:

FragSelf becomes a RESPAWN item at the top of the in-game menu, multiplayer
only - it exists for being stuck out of the world, which is exactly when the
console is the last thing you want to be typing into. The menu reloads its
surfaces on every open (SetMenuMode), so the item tracks the match state for
free. The cooldown is SIXTY SECONDS, LOCKED, and lives server-side in the
MID_FRAG_SELF handler: the menu, the console command and a stock client
sending the message raw all meet the same gate, and at one a minute it is
useless for frag denial or feed spam. Refusals answer in chat with the
seconds remaining. Single player keeps the 1998 behaviour untouched.
Also fixed in passing: LoadSurfaces read dims for a hardcoded FIVE items -
two empty slots left over from the five-item stock menu.

BodyLifetime (server var, BodyProp.cpp, default 10s, read live; SP never
removes bodies) gets a Host tab field. Launcher-only change: the cfg key
reaches a dedicated server via LoadConfigFile and a listen server via the
push-everything design - the first dividend of that exclude-list decision.

Saturate (d3d.ren, documented in RENDERVARS.md, already on the mod
allow-list) gets a Settings checkbox - Monolith's own 2.2 readme recommends
it for levels that read too dark, and it beats raising the card's gamma.

Host tab in listen mode: Discovery now HIDDEN rather than greyed (everything
it says is about a process a listen server does not run), Server archives
gains a listen-only caption pointing at the Mods tab, and Server profiles
stay - a profile carries HostListen and is how a listen setup is saved.

ColorScale and ModelAdd deliberately NOT surfaced - the owner is
investigating; ModelAdd in particular is a see-in-the-dark advantage the
server cannot detect (fact 17), and the launcher handing it to everyone is
a decision, not a tidy-up.

Untested in play: the Respawn item end to end, the cooldown refusal message,
BodyLifetime reaching a live server, Saturate's visible effect.

---

## 0.9.41 — 2026-08-04

**a listen host can mute, and crash reports stop fighting over one file**

RconPassword comes off the listen-server exclude list. Rcon is two doors
sharing a password: the query-port responder (FreshSrv.exe, external tools -
genuinely absent on listen) and HandleRconRequest over the game connection,
which lives in Object.lto and runs on every listen server. Excluding the
password closed both, and Mute/Unmute/Spectate are rcon COMMANDS by design -
so a listen host could kick with "serv Kick <id>" but not mute at all. The
owner's question "will rcon even work on a listen server?" was better than
the shipped answer. UI un-greys the field in listen mode; tooltips now
describe both doors instead of wrongly claiming rcon is query-port-only.

Crash filenames now carry the component. On a listen server both game DLLs
install the crash handler in one process, both filters run (each chains to
the previous), and with PID-only names the second report OVERWROTE the
first - which is how 0.9.40's first listen crash shipped a .txt claiming
"minidump: not written" beside a perfectly valid .dmp: they were written by
DIFFERENT handlers. Now CShell and Object each keep their own pair.

The crash itself is recorded as BUGS.md E7: EIP=0x0000000E called from
IMUSIC25.DLL+0x3758 on a music worker thread ~70s into OF_NIGHT - the 1998
IMA music middleware, nothing of ours on the stack, not obviously
listen-specific. Workaround is the music toggle; a real fix means isolating
or replacing IMA.

---

## 0.9.40 — 2026-08-04

**the Host tab can start a listen server**

A Dedicated/Listen radio pair; every gameplay field on the tab applies to
both, because both read the same ShogoSrv.cfg. "+FreshHost 1" makes
Client.exe host from that file with no wizard dialogs: NetStart_FreshHostAuto
fills what the four Win32 dialogs used to collect, then pushes every
non-structural key to the server with "serv" (engine fact 18) - an EXCLUDE
list, not a third copy of the variable roster, so a key the launcher learns
tomorrow reaches both server kinds with no C++ change. Settings as +Var
command-line arguments were rejected outright: those land in the client
console space the server cannot read (fact 17).

No ObjectDLL change at all - FreshModRulesApply already runs whenever
ShogoServ is not hosting, so mod-rule parity for listen games came free.
That was the half of this feature 0.8.35 built without knowing it.

The real work was the archive collision: on a listen server the Mods tab
(client on/off) and the Host tab list (server load list) describe ONE
process. Starting the server now enables any selected archive that is off
and NAMES IT in the status line - never silently, which is the mistake
0.9.38 made once already. Enabled-but-unselected mods still load; that
union is what "the client is the server" means.

Honest limits, greyed not hidden: List publicly, registration, peers and
rcon live in the FreshSrv.exe process, so on listen they grey out with a
tooltip saying a listen server is join-by-IP, administered from its own
console. The Host archive list is relabelled "Server archives" (the open
question from the 2026-08-05 handoff - "mods" in two places read as one
feature duplicated).

Found in passing: RunningServers() had checked for "ShogoSrv" processes
since the 0.8.4 rename to FreshSrv.exe, so the two-servers-one-port warning
had matched nothing for a dozen releases. It now checks both names.

Untested in play: the whole listen path needs one live run - host from the
tab, serv rules landing (the console line reports the count), a second
client joining by IP, an off archive enabled with the status message.

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
