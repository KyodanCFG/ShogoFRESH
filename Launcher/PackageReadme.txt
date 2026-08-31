=====================================================================
 ShogoFRESH:
 Shogo: Mobile Armor Division
=====================================================================

LICENCE NOTICE AND CREDITS
  ShogoFRESH by KyodanCFG
  Repo: https://github.com/KyodanCFG/ShogoFRESH

  THIS LEVEL IS NOT MADE BY OR SUPPORTED BY Monolith Productions, or any
  of its affiliates and subsidiaries.

  (That sentence is quoted verbatim, "THIS LEVEL" and all, because clause
  8(c)(iv) of Monolith's Shogo Source Tools EULA specifies those exact
  words. It also requires them on the opening screen, where they appear
  for a few seconds alongside the version. ShogoFRESH is distributed free
  of charge, as clause 8(c)(v) requires.)

WHAT IS IN THIS PACKAGE, AND WHAT IS NOT
  Everything here was built from source. Nothing Monolith shipped is
  redistributed:

    CShell.dll, Object.lto, CRes.dll, SRes.dll
        Compiled from Monolith's published v2.2 source release, with
        ShogoFRESH's changes. Shipped inside ShogoFRESH.rez.

    FreshSrv.exe
        The dedicated server, likewise compiled from the ShogoServ source
        in that same release - NOT a patched copy of the retail
        executable. It shares essentially no bytes with the ShogoSrv.exe
        in your game folder, which is why it no longer borrows its name:
        it installs BESIDE the stock server rather than over it, so your
        original is left exactly where it was.

        It still reads ShogoSrv.cfg, so any server config, saved profile
        or hand-edited file you already have keeps working.

        If you are upgrading from 0.8.3 or earlier, Setup puts your
        original ShogoSrv.exe back for you.

    Client.exe
        Untouched. It is closed-source and ShogoFRESH neither modifies
        nor ships it.

    Game content (levels, models, sounds, textures, music)
        None of it is here. ShogoFRESH reads the copy you own.

  The third-party fixes in Redist\ (dinputto8, dgVoodoo2, AM18.dll) are
  redistributed under their own licences - see Redist\README.md.

WHAT YOU NEED FIRST
  1. Shogo: Mobile Armor Division installed (Steam or GOG).
     This package contains NO game files - you must own the game.
  2. The .NET 8 Desktop Runtime (x64). If ShogoFRESH won't start,
     get it from: https://dotnet.microsoft.com/download/dotnet/8.0
     (choose ".NET Desktop Runtime 8 - Windows x64")

INSTALL
  1. Unzip this folder anywhere (NOT into the game directory).
  2. Run ShogoFRESH.exe.
  3. The Game Setup window opens automatically on a fresh install.
     Click "Enable All". This applies, with automatic backups:
       - dinputto8 (input fix - the game won't start without it)
       - dgVoodoo2 (graphics: modern resolutions, windowed/borderless,
         monitor selection)
       - ShogoFRESH mod (widescreen FOV, bug fixes, server rules)
       - Recommended defaults (modern keybinds applied to your live
         bindings, native resolution, Ultra detail, 20/s net rate)
     and enables the DirectPlay Windows feature (UAC prompt - needed
     for multiplayer).
  4. Close Setup and play. You do NOT need to run the game's original
     launcher first.

The game main menu shows the ShogoFRESH version when the mod is active.
Every fix has an Undo in Game Setup (Settings -> Game Setup...) that
restores the exact previous state.

UPDATING
  ShogoFRESH checks GitHub Releases for a newer version at most once a
  day. It never blocks startup and never installs anything by itself.
  When you unzip a newer release over the top, Game Setup detects that
  the files it previously installed have changed and offers to update
  them - the cards that need attention move to the top.

WHAT'S IN SHOGOFRESH
  Play      - server browser (shogoservers.com + favorites), one-click
              join straight into the multiplayer wizard
  Host      - full dedicated-server setup: limits, game variables,
              map rotation and rotation order, ruleset, blocked
              pickups, server mods, saved server profiles, and a live
              player list you can kick from; registers with the
              community master automatically
  Mods      - enable/disable .rez mods; enabled mods load with the game
  Keybinds  - full rebinding incl. mouse buttons and wheel (the
              in-game menu can't), two binds per action
  Settings  - pilot name/color/mech, sensitivity, resolution, detail
              presets, display mode/monitor (via dgVoodoo), launch
              flags, Game Setup

IN-GAME
  The HUD scales with your resolution and the interface key cycles it
  between full, figures-only and off. Kills appear top right, pickups
  top left, chat along the bottom. Hold the scoreboard key to see the
  full player list with the server name, current map and limits.

  Useful console variables (client):
    BackgroundRender  1 = keep rendering and stay connected while the
                  game window is behind another application, instead of
                  freezing on the last frame. Input is ignored while the
                  window is not in front, so it is safe to type
                  elsewhere. Off by default; mainly for running two
                  clients side by side. EXPERIMENTAL.
    HudScale      HUD size; 0 or unset scales with the display
    HudAspect     hold the HUD inside a centred 1.333/1.777/2.333 band
                  instead of the far screen edges (for ultrawide)
    HudTextShadow drop shadow behind HUD text (on by default)
    ClassicCampaign  1 = the 1998 single-player tuning: original
                  magazines (the sniper's 2-round clip returns), 1998
                  drops and carry limits, criticals at 4x for everyone,
                  no explosion falloff, AI that fires on sight, 1998
                  gibbing. Bug fixes and presentation stay. Also a
                  checkbox in Settings.
    ProfanityFilter  star out profanity in chat and player names on YOUR
                  screen only; nothing on the wire changes and nobody
                  else's game is affected. On by default, including on an
                  install this launcher has never written to
    StreamerMode  1 = anonymise the session for broadcast: chat HUD hidden
                  and its chirp silenced, other players shown under
                  generated names that stay put for as long as you are on
                  the server, and a fresh random name picked for you on
                  every connect so a viewer cannot follow you back. The
                  name you set in Settings is untouched and returns the
                  moment the mode is off. Off by default
    FovX          horizontal field of view

  Weapon tuning (EXPERIMENTAL - these are being dialled in):
    These three are SERVER variables, so type them with a "serv"
    in front - "serv GrenadeVelocity 1200" - in single player or a
    game you are hosting. On a dedicated server, set them in the
    server console or over rcon, without the "serv". All three
    default to 0, which means "leave the weapon alone".

    GrenadeVelocity  how hard both grenades are thrown, in units per
                  second, 100 to 4000. They currently leave at 2000
                  (energy) and 750 (Kato) - the energy grenade is
                  travelling as fast as a TOW missile
    GrenadeAngle  degrees to throw UP, relative to where you are
                  aiming, -45 to 60. There is no arc at all right now,
                  which is why a grenade lands near your feet
    HandgunReload seconds to reload the .45 and the MAC-10, 0.10 to
                  5.00. Currently 1.10 and 1.25

    If you find numbers that feel right, say so and they become the
    defaults - at which point these variables go back to doing nothing.

SERVER OPERATORS
  These go in ShogoSrv.cfg or the server console; the Host tab writes
  the common ones for you.

    Ruleset 0|1        0 = Classic (the 1998 weapon balance),
                       1 = ShogoFRESH (rebalanced magazines, and a
                       multiplayer-only reserve-ammo economy)
    MapOrder 0|1|2     0 = in order, 1 = random,
                       2 = random, alternating mech and on-foot
    Intermission <s>   seconds of held scoreboard between the match
                       ending and the next map (default 15, max 60,
                       0 = the stock instant switch). The next map is
                       announced there; chat stays live
    CriticalHits 0|1   the 5% double-damage roll. OFF by default, which
                       is a change from stock: it was always live in
                       deathmatch, and a roll that doubles a hit with no
                       tell and no counter decides duels invisibly
    RandomPickups 0-4  reroll pickup placement at level start
    BlockWeapons <ids> ban weapons; the level is rebuilt with same-tier
                       replacements so its layout survives
    BlockItems <names> the same for health, armour and powerups
    InfiniteAmmo 0|1   endless reserve ammo (TOW, grenades, Red Riot,
                       Juggernaut and Spider are excluded on purpose)
    BotAdd <n>         add player bots - they score, hold a scoreboard
                       slot, patrol the level and respawn into the
                       same slot. Up to 48.
    BotAddNpc <n>      add target-practice bots instead: no scoreboard
                       presence, shown as "Enemy" in the kill feed
    BotRemove 1        clear all bots
    Players 1          list who is connected, with their kick id
    Kick <id>          disconnect one player
    RconPassword <pw>  enable remote console (below). BLANK = off
    RequireFresh 0|1   refuse clients that are not running ShogoFRESH.
                       OFF by default and it should usually stay off -
                       see MIXED CLIENTS below
    QuickTurnCheck 0|1 spot the instant 180 coming from a stock client
                       and take away the shot that followed it. Off by
                       default; see MIXED CLIENTS
    FirstPersonCheck 0|1  refuse shots fired from the chase camera on a
                       first-person-only server. ON by default; see
                       MIXED CLIENTS
    Rcon / RconPassword   client console: remote administration from
                       inside the game (above)

MIXED CLIENTS - WHAT A STOCK CLIENT STILL GETS
  Stock (non-ShogoFRESH) clients can join a ShogoFRESH server. They
  ignore the rules message safely and play by whatever the server
  enforces on its own side, which is most of it: reload pauses, the
  intermission freeze, blocked weapons, ammo economy and the ruleset all
  live in the server and bind everyone equally.

  Two rules do NOT, because they are decisions the client makes about
  its own view and its own input, and no server can reach them:

    Quick turn (QuickTurn 0)      a stock client keeps the instant 180
    1st person only               a stock client keeps the chase camera

  That is backwards - it rewards NOT installing the mod - so there are
  three ways to close it.

    RequireFresh 1
      Reliable. A ShogoFRESH client announces itself on joining; anyone
      who has not done so about 20 seconds after entering the world is
      disconnected with a message saying why. This is the right answer
      for organised matches and the wrong answer for a public server:
      the player base is small, and turning away somebody who owns the
      game costs more than the advantage does.

    QuickTurnCheck 1
      Softer, and only about the 180. The server watches the rotation
      every client already sends and looks for half a circle arriving
      out of near-stillness, which a hand cannot produce. Three of those
      inside twenty seconds and fire is refused for a moment afterwards
      - the turn still happens, the shot it was for does not. Nobody is
      kicked and nobody is told off.

      It is a HEURISTIC and it is off by default, because clients send
      their rotation only about seven times a second and at that rate a
      real fast spin can look the same. Before switching it on, run the
      server for an evening with WeaponDebug 1: every detection is
      reported whether the check is armed or not, so you can see exactly
      what it would have done to your regulars first. ShogoFRESH clients
      are never checked - they have already given the turn up.

    FirstPersonCheck 1  (ON by default)
      The chase camera half, and this one is NOT a guess. Every client
      states which view it is in, in every update, because the server
      needs to know - so a stock client firing from chase view is a fact,
      not an inference. The shot is refused and the player is told, every
      few seconds, that the server is first person only. They can fix it
      with one keypress, which is exactly why they get told: silence
      would just read as a broken server.

      This can only ever do anything on a server that set FirstPersonOnly
      in the first place, and ShogoFRESH clients never reach it - they do
      not leave first person there at all.

  "Players 1" now names each player's client, so "why is that rule not
  binding them" has a one-line answer.

REMOTE CONSOLE (RCON)
  Set RconPassword in ShogoSrv.cfg - or the Remote console field on the
  Host tab. There are two ways in.

  From inside the game (easiest, and the safer of the two). Join the
  server, open the console and type:

      RconPassword "the password"        (once per session)
      Rcon "Players 1"                   (per command)

  The reply arrives as chat lines, visible only to you. A command is
  "<var> <value>", which covers every administrative command ShogoFRESH
  has: Players 1, Kick 3, BotAdd 2, BotRemove 1, BotFill 6,
  BlockWeapons "5 8", Intermission 20, RequireFresh 1 and the rest.
  This route sends the password only to a server you are already
  connected to, and it is the one to prefer.

  From outside the game, for external tools: the server also answers a
  GameSpy-style query on its query port (the game port + 149, the same
  port the browser uses for server info):

      
con\<password>\<command>

  and replies with rcon_0..N, rcon_lines and rcon_status. Off-the-shelf
  rcon clients generally will NOT work with this: they speak Source,
  Quake or Battlefield rcon, and Shogo speaks none of those. Anything
  that can send a raw UDP string and show the reply will do.

  READ THIS BEFORE TURNING IT ON. The password crosses the network in
  PLAINTEXT by either route. The 1998 query protocol is unencrypted UDP text with nowhere
  to put a challenge-response, and it has to keep working for the stock
  server browsers, so this cannot be fixed without breaking them. So:

    - use a password you do not use anywhere else, for anything
    - assume anyone who can watch the connection can read it
    - leave RconPassword blank unless you are actually using rcon.
      Blank does not merely reject attempts - it switches the feature
      off in the server, and an rcon query gets no reply at all, so
      nothing advertises that it exists

  Wrong passwords are rate limited (one answer every couple of seconds
  per address, shared with the other query types) so the port cannot be
  used to guess passwords quickly or to bounce traffic at someone else.

SERVER DISCOVERY WITHOUT A MASTER SERVER
  Shogo has already outlived one master server (shogo-mad.com) and now
  relies on a second (shogoservers.com). If finding servers depends on one
  site, the day that site stops answering every server still running
  becomes unfindable - even though nothing is wrong with them.

  So the launcher has no master server. It has SOURCES, merged on every
  refresh, and it queries each address itself before showing it as online:

    - shogoservers.com, while it is up (still the best source when it is)
    - Defaults\seed-servers.json, plus an optional URL set in Settings
      (see Defaults\seed-servers.README.txt)
    - everything it has seen before, aged out after 60 days
    - anything you saved or typed in
    - PEERS: other servers, asked directly (below)

  The Source column shows where each entry came from.

  For server hosts - peers.txt
    The rebuilt FreshSrv.exe answers a "peers" query with the servers it
    knows about, so a launcher that reaches ONE live server can discover
    the rest. To join in, put one "address:port" per line in peers.txt
    beside FreshSrv.exe (blank lines and #/; comments ignored), or set
    Peers "addr:port addr:port" in ShogoSrv.cfg:

        # peers.txt
        203.0.113.10:27888
        shogo.example.org:27888

    Learning is two-way: your server introduces itself to everyone listed,
    and they add you in turn - so you only need ONE existing server's
    address to become visible through all of them. Hostnames work and are
    the better choice; DNS can be repointed without anyone editing files.

    A server only ever announces ITSELF - the address is taken from the
    packet, not its contents - so nobody can inject a third party into
    anyone's list. Peer answers are size-capped and rate-limited so the
    server cannot be used to amplify traffic at someone else.

CUSTOMIZING THIS PACKAGE (for re-distributors)
  Defaults\  - shipped defaults, all plain text:
      keybind-layout.json        keybind list labels/order/visibility
      keybind-layout.<lang>.json localized variants (auto-picked by
                                 the user's Windows display language)
      defkeybd.cfg               default key bindings (modern layout;
                                 applied to live bindings on setup)
      client-settings.cfg        settings merged into autoexec.cfg
      server-settings.cfg        seed for newly created ShogoSrv.cfg
  Redist\    - fix payloads (see Redist\README.md for sources/licenses)

CREDITS
  Game & 1999 mod source release: Monolith Productions
  Widescreen groundwork: Cristobal (Stainless Steel mod)
  dinputto8: github.com/elishacloud/dinputto8 (Zlib license)
  dgVoodoo2: Dege (dege.freeweb.hu)
  Community master server: NetworkDLS (shogoservers.com)
  ShogoFRESH: Kyodan
  Repo: https://github.com/KyodanCFG/ShogoFRESH
=====================================================================
