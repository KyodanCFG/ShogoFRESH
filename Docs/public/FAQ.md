# ShogoFRESH — FAQ and troubleshooting

The questions that come up, in roughly the order a new player meets them.
If your problem isn't here: the log folder is
`%APPDATA%\ShogoFRESH\Logs` — attach the newest file when you report a
bug, it usually answers the question on its own.

---

## Installing

**Windows says "Windows protected your PC" when I run the launcher.**
That's SmartScreen, and it appears because the launcher isn't
code-signed — signing certificates are an ongoing cost this free project
doesn't carry yet, and reputation builds as more people run each release.
Click **More info**, then **Run anyway**. If you'd rather not take our
word for it: the launcher is open source, and every release zip can be
checked on VirusTotal before you run anything.

**My antivirus flagged the download.**
Two causes existed, and both are resolved. Older zips bundled two
third-party compatibility wrappers (an input shim named `dinput.dll`
and the dgVoodoo2 graphics wrapper) — exactly the shapes generic AV
heuristics misjudge. Since v0.12.2 they aren't bundled: Setup downloads
each from its author's official release and verifies every byte against
a published SHA256. Separately, one engine's generic "Keylogger"
signature misread the game's own string-resources DLL, whose
key-binding vocabulary ("Shift", "Alt"…) resembles a keylogger's
key-name table to a pattern matcher — a file that imports no input API
at all. We submitted it to Bitdefender in September 2026; their Malware
Research Team analyzed it, **confirmed the file clean**, and removed
the detection. If your scanner still flags a current release, its
definitions are behind — and we'd still like to hear about it.

**It asks for the .NET Desktop Runtime.**
The launcher needs the .NET 8 Desktop Runtime (x64), a one-time free
install from Microsoft:
https://dotnet.microsoft.com/download/dotnet/8.0 — choose ".NET Desktop
Runtime 8, Windows x64". The game itself doesn't use it, only the
launcher.

**Do I need to own Shogo?**
Yes. ShogoFRESH contains no game content — it needs your own install
from Steam or GOG (both work; the launcher finds either automatically).

**Where do I unzip it?**
Anywhere *except* the game folder. The launcher lives in its own
directory and points at your Shogo install; Setup copies what the game
folder needs, with a backup and an undo for every change.

**I'm offline / the shim download fails.**
Setup normally downloads the two wrappers from their official GitHub
releases. Without a connection, get them yourself from the pages named
in `Redist\README.md` (beside the launcher) and drop the files into
`Redist\dinputto8\` and `Redist\dgvoodoo\` — Setup uses them from there
and never needs the network.

## The game won't start, or looks wrong

**Nothing happens / it exits immediately.**
Almost always a skipped Setup step. Open the launcher's Game Setup and
apply everything: the input fix is what lets the game start at all on
modern Windows, and the graphics fix is what lets it render.

**Menus look like a plain Windows font.**
The 1998 menu art failed to load — usually because the game was started
while another window had focus. Close and start it again with the game
window in front. (The reverse is also available on purpose: `MenuFont 1`
draws menus in a modern font, which is how translated menus show
accents.)

**The game is stretched / the HUD is tiny or huge.**
Resolution and display mode live in the launcher's Settings tab; the
HUD has its own scale and an ultrawide band setting. Everything applies
without editing files.

**No music.**
Apply the music fix card in Game Setup. In multiplayer, music is off by
default for stability; `MusicInMultiplayer 1` restores it if your setup
tolerates it.

## Multiplayer

**The server browser is empty.**
The browser merges several sources, so a fully empty list usually means
UDP is blocked outbound — check your firewall has not sandboxed the
launcher. You can always add a server by address and join directly.

**I can't join a server.**
A join retries for ten seconds and then gives up. Three usual causes:
the server is behind a router with the port unforwarded (the host's
problem, not yours), a version-mismatched server (the join is refused
outright), or your own firewall. If you can see the server's player
count update, queries work and a timeout points at the game port
specifically.

**Hosting: nobody can join me.**
Forward your game port (default **27888, UDP**) plus the query port
(game port + 149) on your router, and let the launcher's "Allow in
Firewall" button add the Windows rule. Bots can hold seats until humans
arrive (`Bot fill` on the Host tab), so the server never looks dead
while it waits.

**Can unmodified 1998 clients join my server?**
Yes — both directions. Stock clients join ShogoFRESH servers and the
server's rules bind them; ShogoFRESH clients on stock servers revert to
stock behaviour automatically. Nobody needs the mod to play with people
who have it.

**What's "List publicly" vs "List on shogoservers.com"?**
"List publicly" announces to the in-game browser network. The second
checkbox also puts your server on the shogoservers.com website — a
public web page anyone can read without the game, which is why it's a
separate, off-by-default choice.

## Mods, maps, languages

**How do I install a mod?**
Drop it in `Custom\` inside the game folder — a folder of loose files
is a complete mod, and `.rez` archives work beside it. The launcher's
Mods tab lists and toggles them. Making your own is documented at
[ShogoMAKE](https://github.com/KyodanCFG/ShogoMAKE).

**Can I play in my language?**
Language packs are text files dropped into `Custom\Strings\`. German
and Spanish exist today; the contributor guide ships with ShogoMAKE if
yours doesn't yet. Set `MenuFont 1` for menus that can draw accents.

**Where are my screenshots?**
F8 in game; they land in `Save\screenshots` as JPEGs (`ScreenshotJpeg 0`
keeps the raw BMPs instead).

## Trust and legality

**Is this legal?**
The game code was released by Monolith in March 1999 with a licence
that permits distributing the compiled result for free, which is what
every release is. The engine is never modified, no game assets are
redistributed, and the launcher and tools are MIT-licensed open source.
The README's licence section carries the details.

**Was this made with AI?**
Built by one person with heavy AI assistance, every change verified in
play, and no generated art — every asset is Monolith's 1998 work. The
[changelog](../../CHANGELOG.md) records how each of 300+ releases was
found and fixed; the full statement is in the README.

## Still stuck?

Open an issue at
https://github.com/KyodanCFG/ShogoFRESH/issues with what you did, what
happened, and the newest file from `%APPDATA%\ShogoFRESH\Logs`. For
server questions, [SERVER-GUIDE.md](SERVER-GUIDE.md) covers everything
an operator meets, in the order they meet it.
