# Running a ShogoFRESH server

Everything an operator needs, in the order you meet it. If you only read one
section, read **Moderation** — that is where nearly all of the new work is,
and where the defaults will surprise you.

---

## 1. Getting one running

The launcher's **Host** tab writes `ShogoSrv.cfg` and starts the server. That
is the whole intended path, and the tab covers every setting in this document
that has a sensible UI.

Everything below is for the cases it does not cover: a dedicated box, a
setting you want to change without restarting, and remote administration.

### The two binaries

| | |
|---|---|
| **`FreshSrv.exe`** | the dedicated server. Ours, compiled from Monolith's released `ShogoServ` source. Since 0.8.4 it installs *beside* the stock `ShogoSrv.exe` rather than over it. |
| **`ShogoSrv.exe`** | Monolith's original, left alone. If you upgraded from 0.8.3 or earlier, Setup put yours back. |

`FreshSrv.exe` still reads **`ShogoSrv.cfg`**. The config kept its name so
existing configs and saved profiles keep working.

```
FreshSrv.exe -config ShogoSrv.cfg -go
```

`-go` skips the setup wizard. `-emptyexit` shuts the server down after the
last player leaves.

### Where its files live

All in `%APPDATA%\ShogoFRESH\` — not the game folder, because a game under
Program Files is not writable without elevation and a moderation tool that
silently fails to save is worse than none.

| File | What |
|---|---|
| `Logs\freshsrv-YYYY-MM-DD.log` | everything the console printed, one file per day |
| `Logs\ShogoFRESH-crash-*.txt` / `.dmp` | only ever written on a crash |
| `Logs\bans.txt` | the ban list |
| `Logs\allow.txt` | the allowlist |
| `Logs\ban-salt.txt` | this server's hashing secret — see below |
| `Logs\matches.jsonl` | one JSON line per finished match |
| `Logs\actions.jsonl` | one JSON line every time the server acts on somebody |

**`ban-salt.txt` is load-bearing.** Every hashed entry in `bans.txt` and
`allow.txt` is salted with it. Delete it and both lists stop matching
anybody. That is also the clean way to reset if one ever leaks.

---

## 2. Moderation

The part worth understanding before you need it.

### What is on by default

| Feature | Default | Why that way |
|---|---|---|
| Votekick | **on** | the premise is that you are not there; a safeguard you must enable in advance fails at the moment it mattered |
| Idle handling | **on** | but only acts on a busy server — see below |
| Reconnect throttle | **on** | no configuration, no downside |
| Chat rate limit | **on** | protocol hygiene, content-neutral |
| Name sanitisation | **on** | multiplayer only |
| Bans | **on** | they only do something once you set one |
| Allowlist | **off** | it makes your server a closed club. That is a decision, not a setting |
| `RequireFresh` | **off** | it is the only setting here that turns people away |

### Commands

Type these into the server console, or send them over rcon.

**Who is here**

```
Players 1
```

Lists everyone with the **id** that every other command takes, their score,
and which client they are running.

**Kicking and banning**

```
Kick <id>
Ban <id> [minutes] [reason]     0 minutes = permanent
Unban <n>                        n from the Bans listing
Bans 1                           list, and re-read the file
```

`Ban` sets **both** keys it has: the pilot name and the install token. The
console tells you which case you got, every time:

```
ShogoFRESH: banned "someone" permanently
  (name + install id - survives a rename; shed by deleting one file on their machine)
```

**Read that line.** It is not decoration. Bans are a deterrent tier, not a
wall, and the ways round them in the order people find them are: a rename is
beaten (the ban holds a per-install id, not a name), deleting one file is
**not** beaten, and a modified client beats anything a client tells us about
itself. Run bans as friction against the casual case, never as a guarantee.

---

### What a ban can and cannot do

Worth five minutes before you rely on one. A moderation tool that overstates
itself is worse than none, because an admin who believes somebody is gone
stops watching for them.

**The one sentence that matters most: `RequireFresh 1` is the only
configuration in which bans genuinely bind everyone on the server.** A stock
1998 client sends no install id — it cannot, it predates the idea — so it can
only ever be name-banned, and a rename beats that in seconds. If your server
admits stock clients, your bans are advisory for exactly those clients. That
is the real trade: a server that wants bans to stick has to turn away stock
clients, and only you can decide which matters more to your door.

**What a ban CAN do.** Convert the common nuisance — kicked, back thirty
seconds later as "Bob2" — into somebody who has to know a file exists on
their own machine and delete it. With `RequireFresh 1`, that covers everyone
present. In practice this ends the casual case, which is nearly every case.

**What a ban CANNOT do:**

- **Identify a machine.** The id is a random number in a text file the
  player owns — *deliberately* not derived from hardware. No machine GUID,
  no MAC address, no disk serial. A hardware fingerprint would harden
  nothing that matters (see the next point) and would cost the privacy
  posture this whole design is built on.
- **Bind anyone determined.** Every identity in this protocol is something
  the client *says*. The 1998 engine never exposes a network address to the
  game code at all, so there is no server-side measurement to fall back on —
  a modified client can claim any id, or none. This is a ceiling, not a bug,
  and no setting moves it.
- **Follow anyone to another server.** `bans.txt` stores a hash salted with
  a secret only your server knows, so two operators cannot discover they
  banned the same install even by comparing files. The same property that
  protects players from cross-server tracking means there is no such thing
  as a community-wide ban list. That is a trade this design makes on
  purpose, in the players' favour.

**"What if they just host their own server?"** They can, and that is the
system working, not failing. A ban means *not welcome here* — it is not
excommunication, and there is no central authority to appeal to when a
power like that lands on the wrong person. Their server is theirs: your ban
list does not apply to it, and players who join it can leave it. If a
hostile server ever needs delisting from the public browser, that is a
question for whoever runs the master list, not a setting in this file.

---

**The allowlist** (`AllowList 1`)

```
Allow <id>                       somebody who IS here
AllowAdd <token> [note]          somebody who is NOT
AllowRemove <n>
Allowed 1
```

`AllowAdd` is the necessary one, because of the circularity: to be added by
id you must be connected, and you cannot connect because you are not on the
list. A player finds their own token in `client-id.txt` in their
`%APPDATA%\ShogoFRESH` folder and sends it to you however you already talk.

**Turning this on implies requiring ShogoFRESH clients** — a stock client
sends no token and can never be listed. The server says so on the first level.

> **The trap to avoid:** `AllowList 1` with an empty list means nobody can
> join, including you if you are on a dedicated box. The server warns loudly,
> once per level, with the path and the way out. On a *listen* server the host
> is always admitted, so you cannot lock yourself out of your own game.

**Diagnostics**

```
Rcon "WeaponDebug 1"
```

Server-side debug channels can only be switched on this way. Typing
`WeaponDebug 1` at your own console reaches the *client's* half only — the
engine keeps the server's console variables in a space the header describes
as one "the user can't access at all". In single player the game sends them
automatically; on a server, rcon is the door.

### Watching without playing

```
Rcon "Spectate on"          become a spectator, follow the first player
Rcon "Spectate <id>"        follow somebody specific
Rcon "Spectate off"         stop, and respawn
```

Authenticated by rcon, deliberately — it is the only channel a client cannot
talk its way into, because a client can set its own console variables but
cannot know your password.

A spectator **is not a player anywhere the game counts players**: no
scoreboard entry, not in the human count, not toward the votekick quorum or
the idle busy-check, not in the frag limit, not in the match record, and not
idle-kicked for standing still. They ride the position of whoever they are
following and keep their own look direction.

> **The one caveat.** They still occupy an engine connection — there is no
> way to connect without one — so on a genuinely full server the engine will
> still refuse. If you want a guaranteed way in, set `MaxPlayers` one above
> your intended player count. The game's own accounting keeps that extra one
> from ever becoming a player.

### Votekick — what your players can do

Typed in **chat**, so it works from a stock client too:

```
!who              ids, in chat
!votekick <id>    or !vk
!yes / !no
```

30 seconds, needs 3 humans (bots do not count and cannot vote), one vote at a
time, 2 minutes before the same person can call another, 5 minutes' immunity
for surviving one. Passing kicks **and** bans for 15 minutes.

Every vote and its tally goes to the console and therefore the log. **The
failed ones are what you want to read** — that is where an unfair majority
shows up.

`VoteKick 0` turns it off. The abuse it cannot prevent is a clique voting out
somebody they merely dislike; that has a social fix, not a technical one.

### Idle handling

Warned at 3 minutes, disconnected at 5 — **but only when the server is at 75%
of `MaxPlayers` or above.**

That condition is the whole design. An idle player on a quiet server harms
nobody, and disconnecting them makes the server *emptier*, which is the
opposite of what a small community needs. The rule exists to settle a fight
over the last slot, so it only runs when there is one.

Moving, turning, firing or talking all count as being present — deliberately
generous, because the failure this must never have is ejecting somebody who
was playing. The warning goes to that player alone; telling everyone would
just invite a pile-on.

`IdleKick 0` turns it off.

### The fire checks — measure before you arm

`FireRateCheck` and `FirePosCheck` are **off by default and have never been
calibrated**. Off does not mean idle: since 0.8.12 both still measure and
report under `WeaponDebug`, so you can find out what they *would* do before
letting them do it.

```
Rcon "WeaponDebug 1"        the only way to switch on a server-side channel
```

Play a normal session with your regulars, on the connections they actually
have, then read the log. `firerate:` lines say who went over the cap and how
often; `firepos:` lines give the worst claimed-position error per player.

**If your regulars trip them, leave them off and say so.** A check that fires
on honest players is worse than no check — every false positive spends trust
you will need later. If nobody trips them, arm with a ceiling comfortably
above the worst you measured.

When armed, the penalty is a ladder: the shot is refused, at ten violations
that player alone is told why, and `FireRateBan <n>` adds a 10 minute ban
that is **0 (never) by default**. `FireRateExecute` — which killed the player
— was removed in 0.8.12; a config that still sets it is told it does nothing.

### Chat

A token bucket (6 lines, refilling one every two seconds), a 160-character
cap on relay, and the same line three times in a row dropped.

**Excess is dropped silently.** Not answered — that reply is itself chat, it
arrives exactly when the channel is congested, and it tells a flooder where
the threshold is. The sender still sees their own line locally, so nothing
looks broken to them.

There is **no server-side content filter on chat**, deliberately. Chat is
opt-in to read and every player can already filter it on their own screen;
imposing one player's preference on everybody is not the server's job. Names
are the opposite case — see below.

### Names

Sanitised at join, multiplayer only: control characters and anything above
ASCII 126 stripped (the 1998 font cannot draw them, and a newline breaks the
scoreboard columns), runs of spaces collapsed, length capped, and a wordlist
hit **renamed** to `PilotNNN`.

Renamed rather than refused, because a refused connection looks like a broken
server. Renamed rather than starred, because `****` on a scoreboard is not an
improvement and advertises a filter to play with. The player is told in their
own chat, and it is logged.

A name is the one place a server-side content rule is justified: it is
imposed on everyone who looks, whether they wanted to read it or not.

---

## 3. Remote console

Two doors.

**From the query port**, for external tools — see the readme for the packet
form.

**From in the game**, for you:

```
RconPassword "something"     once per session
Rcon "Players 1"             per command
```

Replies come back as chat.

> **Blank `RconPassword` disables rcon entirely** and a query gets *no reply
> at all* — the only safe default for an unauthenticated UDP service. The
> password crosses the network in plaintext; treat it as a lock on a shed,
> not on a safe.

Wrong passwords are rate limited, and every rcon command and its output also
appears in your own console — capture is a copy, not a move.

### When somebody asks why they were kicked

`actions.jsonl` has one line per action, with the numbers that were in force
at the time and the twenty lines of chat around it:

```json
{"when":"2026-08-01T22:14:03","action":"idle-kick","player":"Someone",
 "why":"idle 5 min (limit 5), server 14/16 which is over the 75% mark",
 "context":[...]}
```

That third field is the point. "Kicked for idling" is an assertion; the line
above is something you can check, and disagree with if it was wrong.

Written for admin bans, ban enforcement, allowlist refusals, idle kicks and
votekicks — **including votes that failed**, which is where an unfair
majority shows up. Nothing is written on a quiet session.

It collects nothing new: chat already reaches your console and your console
already reaches the day log. This just puts the relevant twenty lines in one
place. Keep it more privately than `matches.jsonl` — one is a record of play,
the other a record of trouble.

---

## 4. Match records

`matches.jsonl`, one JSON object per line:

```json
{"started":"2026-07-31T21:04:12","seconds":540,"map":"MCA_Cargo","ruleset":"fresh",
 "players":[{"name":"Kyodan","frags":12,"deaths":7,"bot":false,"client":"0.8.11"},
            {"name":"Baku","frags":9,"bot":true}]}
```

JSON *Lines*, not a document, so a server that is killed leaves a file that
still parses. Bots are tagged — a leaderboard needs to drop them, an activity
graph needs them. The `client` field is the one thing no other Shogo server
can tell you: how many of your regulars actually installed the mod.

Written when a match **ends**, not when a level changes.

---

## 5. Settings quick reference

Everything the Host tab writes, plus the ones only reachable from the console.

### Match

| Var | Default | |
|---|---|---|
| `MaxPlayers` | 16 | 2–128 |
| `BotFill` | 0 | held population; capped by `MaxPlayers` |
| `BotTag` | | mark the server name as having bots |
| `EndType` / `EndFrags` / `EndTime` | | frag and time limits |
| `Intermission` | 10 | scoreboard hold, seconds. 0 = the stock instant switch |
| `MapOrder` | 0 | 0 sequential, 1 random, 2 random alternating mech/on-foot |

### Rules

| Var | Default | |
|---|---|---|
| `Ruleset` | | 0 Classic (1998 balance), 1 ShogoFRESH |
| `InfiniteAmmo` | 0 | 0 off, 1 sidearms, 2 everything |
| `CriticalHits` | 0 | the 5% double-damage roll. Off because it decides duels invisibly |
| `QuickTurn` | 0 | |
| `FirstPersonOnly` | 0 | |
| `TractorBeam` / `RammingDamage` | on | |
| `RandomPickups` | 0 | reroll pickups at level start |
| `BlockWeapons` / `BlockItems` | | ban specific pickups for the level |
| `RunSpeed` / `MissileSpeed` / `RespawnScale` / `HealScale` | | multipliers |

### Moderation

| Var | Default | |
|---|---|---|
| `VoteKick` | **1** | |
| `IdleKick` | **1** | only acts above 75% full |
| `AllowList` | **0** | closed server. Read section 2 first |
| `RequireFresh` | 0 | refuse stock clients |
| `RconPassword` | *(blank)* | blank = rcon fully disabled |

### Discovery

| Var | |
|---|---|
| `ServerName`, `Port`, `NetService` | |
| `Peers` | space-separated `address:port` list; `peers.txt` beside the exe also works and merges |
| `WebRegUrl` | **blank by default** — nothing is contacted unless you fill it in |

---

## 6. If something goes wrong

**Nobody can join.** Check `AllowList` and `RequireFresh` first — both turn
people away by design, and the allowlist warning is in the log at level start.

**The server crashed.** `%APPDATA%\ShogoFRESH\Logs\ShogoFRESH-crash-*.txt`.
Since 0.8.5 it carries breadcrumbs — world starts, level changes, player
counts — so the report says what was happening, not just where it stopped.
Send the whole file; the `.dmp` beside it is worth more than any description.

**A known crash:** on 0.8.1, a client quitting a match could take the
dedicated server down — a null dereference inside `server.dll`, which is
Monolith's engine binary and not something we have the source to. If you hit
it, the breadcrumbs are the useful part.

**Nothing is being logged.** The log is written from the moment the server
starts. If `Logs\` does not exist at all, `%APPDATA%` was not readable and it
fell back to a `Logs` folder beside the exe.

---

## 7. Where the reasoning lives

This guide says *what*. For *why*:

- [BIBLE.md](BIBLE.md) — how the client, server and engine actually fit
  together, component by component.

The moderation design and the dedicated-server audit are working documents in
the development tree and are not published. Nothing in them changes how you
operate a server — they are the reasoning and the outstanding-work list behind
what this guide already describes. **The one thing worth carrying over is the
accounting above:** every moderation tier here has a known way round it, and
this guide names them where they matter rather than leaving them to a document
you cannot open.
