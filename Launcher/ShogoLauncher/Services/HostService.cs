using System;
using System.Diagnostics;
using System.IO;

namespace ShogoLauncher.Services;

/// <summary>
/// Hosts a dedicated server via FreshSrv.exe.
///
/// Per ShogoSrv.txt (v2.2):
///  - "-config myfile.cfg" selects a config file (default ShogoSrv.cfg)
///  - "-go" skips the setup dialogs and runs immediately with saved settings
///  - "-emptyexit" shuts the server down 60s after the last player leaves
///  - srv_send.txt in the game dir "tells Shogo where to send registration
///    messages" - this is the hook the shogoservers.com community master
///    uses. TODO: confirm the exact registration URL with NetworkDLS and
///    default it here.
///
/// The .cfg uses the same "Var" "value" format as autoexec.cfg. The
/// authoritative var names come from a config the server itself has written;
/// until we generate one on a real run (runtime-verify), we only edit vars we
/// know from ShogoSrv.txt and the ShogoServ source (NetStart.cpp/CVarTrack).
/// </summary>
public class HostService
{
    public string GameDir { get; }

    public HostService(string gameDir) => GameDir = gameDir;

    public string SrvSendPath => Path.Combine(GameDir, "srv_send.txt");

    /// <summary>
    /// <summary>
    /// Our dedicated server binary.
    ///
    /// Renamed from ShogoSrv.exe in 0.8.4. We compile this from Monolith's
    /// released server source, so it is our binary to name - and under the
    /// old name it installed OVER the stock ShogoSrv.exe rather than beside
    /// it, which meant replacing a shipped executable to run our own.
    ///
    /// The CONFIG deliberately keeps its name. ShogoSrv.cfg is what every
    /// existing server config, saved profile and hand-edited file already
    /// is, and renaming it would orphan all of them to no purpose.
    /// </summary>
    public const string ServerExe = "FreshSrv.exe";

    /// <summary>The name we used before 0.8.4, kept so an upgrade can clean
    /// up after itself. See GameSetupService.</summary>
    public const string LegacyServerExe = "ShogoSrv.exe";

    /// Marker var proving a ShogoSrv.cfg was written by ShogoFRESH. A stock
    /// install ships its own ShogoSrv.cfg, so file existence alone can't
    /// distinguish "configured by us" from "Monolith's defaults".
    /// </summary>
    public const string ConfigMarker = "ShogoFRESHConfig";

    public record HostOptions(
        string ServerName,
        int Port = 27888,
        int MaxPlayers = 16,
        int BotFill = 0,
        string WebRegUrl = "",
        string Peers = "",
        bool TractorBeam = true,
        // DoubleJump is deliberately absent. The 1998 wizard wrote it, ShogoSrv
        // relays it, and NOTHING has ever read it - not stock movement code,
        // not ours (checked back to the original source drop). Every Shogo
        // server since 1998 has been toggling a checkbox connected to nothing,
        // so the launcher no longer offers it. Existing cfg values are simply
        // left alone.
        bool RammingDamage = true,
        // The other half of the collision system RammingDamage governs. The
        // engine reports a fall and a shove into a wall as the same touch
        // against the world, so these two move both - which is why they sit
        // beside ramming rather than among the weapon tuning.
        //
        // Defaults are the stock values, not the "0 = use the table" sentinel
        // the game also accepts. The launcher writes what the box shows and
        // the box shows what the game does, which is worth more here than the
        // ability to spell "unset" - and preflight asserts these two literals
        // still match FreshTuning.h.
        double FallDamage = 5.0,
        double FallThreshold = 15.0,
        bool QuickTurn = true,          // enforcement lands in ShogoFRESH game code; var written now
        double RunSpeed = 1.1,
        double MissileSpeed = 1.0,
        double RespawnScale = 1.0,
        double HealScale = 1.0,
        double WorldTimeSpeed = -1.0,   // -1 = day/night cycle off
        string WorldColorNight = "0.5 0.5 0.5",
        bool ListPublicly = true,        // ServerReg: announce to the community browser
        int RandomPickups = 0,           // 0 none, 1 weapons, 2 items, 3 both separate, 4 both together
        string BlockedWeapons = "",      // weapon ids banned from spawning, e.g. "5 8"
        string BlockedItems = "",        // item classes banned, e.g. "FirstAid_50 ArmorRepair_500"
        int InfiniteAmmo = 0,            // 0 off, 1 sidearms only, 2 all weapons
        bool CriticalHits = false,       // 5% double-damage roll; off by default in ShogoFRESH
        int Intermission = 15,           // seconds of held final scoreboard; 0 = stock instant switch
        int Gravity = 0,                 // world gravity; 0 = leave the engine default alone
        string RconPassword = "",        // remote console; EMPTY = disabled outright, no reply to any rcon query
        bool RequireFresh = false,       // refuse clients that do not identify themselves as ShogoFRESH
        int GameMode = 0,                // 0 Deathmatch, 1 TOWs Out, 2 Squishie
        int Ruleset = 1,                 // 0 Classic (1998 balance), 1 ShogoFRESH
        int BodyLifetime = 120,          // seconds dead bodies persist in multiplayer (BodyProp.cpp)
        bool FirstPersonOnly = false,    // forbid the chase camera (ShogoFRESH clients)
        int MapOrder = 0,                // 0 in order, 1 random, 2 random alternating MCA/OF
        bool FragLimitOn = true,
        int FragLimit = 30,
        bool TimeLimitOn = false,
        int TimeLimit = 15,
        IReadOnlyList<string>? Levels = null,    // full rotation; null/empty = keep existing
        IReadOnlyList<string>? RezFiles = null,  // Custom\ rez names to load
        bool Listen = false,                     // host inside Client.exe (+FreshHost) instead of FreshSrv.exe
        bool EmptyExit = false,
        string FirstLevel = @"Worlds\Multi\MCA_ENTRANCE",
        string? RegistrationUrl = null);

    /// <summary>
    /// Write launcher-managed settings into a server config file.
    /// Var names verified against a ShogoSrv.cfg written by the
    /// shogoservers.com v2.21 (NTDLS) server wizard - notably it is
    /// "ServerName", the rotation is NumLevels + Level0..N (Level0 with the
    /// Worlds\Multi\ prefix for retail maps), Port 0 means default 27888,
    /// and ServerReg toggles that build's website registration.
    /// </summary>
    public string WriteConfig(HostOptions opt, string configName = "ShogoSrv.cfg")
    {
        var path = Path.Combine(GameDir, configName);

        // Brand-new config: seed from the shipped server defaults so a
        // packaged release carries curated hosting settings out of the box.
        if (!File.Exists(path))
        {
            var seed = Path.Combine(AppContext.BaseDirectory, "Defaults", "server-settings.cfg");
            if (File.Exists(seed)) File.Copy(seed, path);
        }

        var cfg = new ShogoConfigFile(path);

        cfg.Set(ConfigMarker, 1);
        cfg.Set("ServerName", opt.ServerName);
        cfg.Set("Port", opt.Port == 27888 ? 0 : opt.Port);
        cfg.Set("MaxPlayers", opt.MaxPlayers);
        cfg.Set("BotFill", opt.BotFill);
        cfg.Set("WebRegUrl", opt.WebRegUrl);
        cfg.Set("Peers", opt.Peers);
        cfg.Set("GameType", 1);                 // deathmatch

        // EndType: 1 = frags observed from the v2.21 wizard (frags checked,
        // time unchecked). 2 = time, 3 = both, 0 = never are the presumed
        // companions (runtime-verify).
        int endType = (opt.FragLimitOn ? 1 : 0) | (opt.TimeLimitOn ? 2 : 0);
        cfg.Set("EndType", endType);
        cfg.Set("EndFrags", opt.FragLimit);
        cfg.Set("EndTime", opt.TimeLimit);

        // RETIRE the console overrides when writing the limits.
        //
        // The two limits are the only settings in this file that exist under
        // two names: the launcher writes EndFrags/EndTime, and the server
        // reads FragLimit/TimeLimit first and only falls back to these when
        // they are zero - that fallback lives in the server shell's own
        // time-limit accessor. (Named by behaviour rather than by class: this
        // file is published, and leakcheck blocks a game-code identifier in a
        // source file even where it would pass in prose.) The engine has no
        // SetGameInfo, so those variables are how the Options dialog and rcon
        // change a limit at all - they are not going away.
        //
        // What made that a bug is that the server SAVES its console variables
        // on exit. So one "Rcon TimeLimit 1" outlived its session, silently
        // outranked whatever the launcher showed, and the launcher kept
        // reporting 15 while every match ended after one minute - which is
        // exactly what happened.
        //
        // Writing the limits here means the launcher is the most recent
        // writer, so it clears the override rather than leaving a stale one
        // to win. The reverse direction is handled on the server: an rcon
        // change is pushed back into NetGame, which is what gets saved into
        // EndFrags/EndTime on exit. Whoever wrote last is what everyone sees.
        cfg.Set("FragLimit", 0);
        cfg.Set("TimeLimit", 0);

        cfg.Set("NetService", 0);
        cfg.Set("ServiceName", "Internet TCP/IP");
        cfg.Set("TractorBeam", opt.TractorBeam ? 1 : 0);
        cfg.Set("RammingDamage", opt.RammingDamage ? 1 : 0);
        cfg.Set("FallDamage", (float)opt.FallDamage);
        cfg.Set("FallThreshold", (float)opt.FallThreshold);
        cfg.Set("QuickTurn", opt.QuickTurn ? 1 : 0);   // honored by ShogoFRESH game code
        cfg.Set("RandomPickups", opt.RandomPickups);
        cfg.Set("BlockWeapons", opt.BlockedWeapons ?? "");
        cfg.Set("BlockItems", opt.BlockedItems ?? "");
        cfg.Set("InfiniteAmmo", opt.InfiniteAmmo);
        cfg.Set("CriticalHits", opt.CriticalHits ? 1 : 0);
        cfg.Set("Intermission", opt.Intermission);
        cfg.Set("Gravity", opt.Gravity);

        // RconPassword travels in the clear on every rcon query - GameSpy v1
        // is plain UDP text with nowhere to put a challenge, and stock clients
        // have to keep parsing the same protocol. Empty disables rcon in the
        // server rather than merely rejecting attempts, which is the right
        // default for anyone not using it.
        cfg.Set("RconPassword", opt.RconPassword ?? "");

        // RequireFresh is the only reliable way to make the client-side
        // rules bind everyone - a stock client cannot be asked to give up
        // its own quick turn or chase camera. It is also the only setting
        // here that turns people away, so it stays off unless asked for.
        cfg.Set("RequireFresh", opt.RequireFresh ? 1 : 0);

        // Which kind of server the Host tab last asked for. Read only by the
        // launcher (and deliberately ignored by the game's own cfg reader),
        // but stored HERE so a server profile carries it.
        cfg.Set("HostListen", opt.Listen ? 1 : 0);
        cfg.Set("GameMode", opt.GameMode);
        cfg.Set("Ruleset", opt.Ruleset);
        cfg.Set("BodyLifetime", opt.BodyLifetime);
        cfg.Set("FirstPersonOnly", opt.FirstPersonOnly ? 1 : 0);
        cfg.Set("MapOrder", opt.MapOrder);
        cfg.Set("RunSpeed", (float)opt.RunSpeed);
        cfg.Set("MissileSpeed", (float)opt.MissileSpeed);
        cfg.Set("RespawnScale", (float)opt.RespawnScale);
        cfg.Set("HealScale", (float)opt.HealScale);
        cfg.Set("WorldTimeSpeed", (float)opt.WorldTimeSpeed);
        cfg.Set("WorldColorNight", opt.WorldColorNight);
        cfg.Set("UpdateInfo", 1);
        cfg.Set("ServerReg", opt.ListPublicly ? 1 : 0);
        cfg.Set("SaveGameLevels", 1);

        // Map rotation: a provided list replaces the stored one wholesale.
        if (opt.Levels is { Count: > 0 })
        {
            for (int i = 0; i < 256; i++) cfg.Remove($"Level{i}");
            cfg.Set("NumLevels", opt.Levels.Count);
            for (int i = 0; i < opt.Levels.Count; i++) cfg.Set($"Level{i}", opt.Levels[i]);
        }
        else if (cfg.GetInt("NumLevels", 0) == 0)
        {
            cfg.Set("NumLevels", 1);
            cfg.Set("Level0", opt.FirstLevel);
        }

        // Additional rez files. Var names confirmed from the ShogoServ
        // source (NetStart.cpp): NumRezFiles + RezFile0..N.
        //
        // ShogoFRESH.rez goes first so the dedicated server runs OUR
        // Object.lto (server rules, anti-cheat vars); selected Custom mods
        // follow and can still override it.
        for (int i = 0; i < 64; i++) cfg.Remove($"RezFile{i}");

        var rez = new List<string>();
        if (File.Exists(Path.Combine(GameDir, "ShogoFRESH.rez"))) rez.Add("ShogoFRESH.rez");
        rez.AddRange((opt.RezFiles ?? Array.Empty<string>()).Where(r =>
            !r.Equals("ShogoFRESH.rez", StringComparison.OrdinalIgnoreCase)));

        cfg.Set("NumRezFiles", rez.Count);
        for (int i = 0; i < rez.Count; i++) cfg.Set($"RezFile{i}", rez[i]);

        if (opt.EmptyExit) cfg.Set("EmptyExit", 1);
        cfg.Save();

        // Stock-2.2 registration hook (srv_send.txt); the NTDLS v2.21 build
        // registers on its own when ServerReg=1.
        if (!string.IsNullOrWhiteSpace(opt.RegistrationUrl))
            File.WriteAllText(SrvSendPath, opt.RegistrationUrl);

        return path;
    }

    /// <summary>
    /// Add an inbound Windows Firewall rule for the server: UDP on the game
    /// port and the GameSpy query port (game port + 149, the stock ShogoSrv
    /// responder). Runs netsh elevated - the user gets one UAC prompt.
    /// Re-running replaces the rule (delete + add) so a port change is safe.
    /// </summary>
    public static void AddFirewallRule(int gamePort)
    {
        int queryPort = gamePort + 149;
        var cmd =
            $"netsh advfirewall firewall delete rule name=\"ShogoFRESH Server\" & " +
            $"netsh advfirewall firewall add rule name=\"ShogoFRESH Server\" dir=in action=allow protocol=UDP localport={gamePort},{queryPort}";

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {cmd}",
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        Process.Start(psi);
    }

    /// <summary>
    /// Server instances already running. Two servers on one port silently
    /// fight over the socket, so it is worth asking before starting another.
    ///
    /// Both names: ours is FreshSrv.exe (renamed in 0.8.4), but a stock
    /// ShogoSrv.exe somebody runs by hand fights over the port just the
    /// same. This checked only "ShogoSrv" from the rename until the listen
    /// server landed, so the warning had matched nothing for a dozen
    /// releases - found because the listen path leans on it too.
    /// </summary>
    /// <summary>
    /// A UDP port the listen server can actually bind, starting from the one
    /// the config asks for.
    ///
    /// A listen server and a dedicated server read the SAME ShogoSrv.cfg, so
    /// hosting from the launcher while FreshSrv.exe is already running asks
    /// for a port that is taken and the game answers "ERROR - Unable to bind
    /// to the requested port" - while the launcher's own status line
    /// cheerfully names the port it just failed on. The game gained
    /// "+hostport" for this; this is the half that decides what to pass.
    ///
    /// The test is whether the port is BOUND, not whether a server process
    /// exists, because those are different questions and the difference is
    /// the trap. The running server read its port when IT started; the
    /// launcher may have rewritten the config since, so the config no longer
    /// says where that server is. Asking the network stack sidesteps the
    /// whole problem and also catches anything else on the port.
    ///
    /// Returns the wanted port when it is free - so the overwhelmingly normal
    /// case passes no override at all and behaves exactly as it did before
    /// this existed. Returns the wanted port on failure too: better to let
    /// the game report a bind error than to silently host somewhere the
    /// player was not told about.
    /// </summary>
    public static int FreeListenPort(int wanted, int nTries = 20)
    {
        try
        {
            var taken = new HashSet<int>(
                System.Net.NetworkInformation.IPGlobalProperties
                      .GetIPGlobalProperties()
                      .GetActiveUdpListeners()
                      .Select(e => e.Port));

            for (int i = 0; i < nTries; i++)
            {
                int port = wanted + i;
                if (port > 65534) break;

                // The query port is gamePort + 149 (see StartServer), so a
                // candidate has to clear BOTH or the server comes up unable
                // to answer the browser and nothing says why.
                if (!taken.Contains(port) && !taken.Contains(port + 149))
                    return port;
            }
        }
        catch { }

        return wanted;
    }

    public static Process[] RunningServers()
    {
        try
        {
            return Process.GetProcessesByName("FreshSrv")
                          .Concat(Process.GetProcessesByName("ShogoSrv"))
                          .ToArray();
        }
        catch { return Array.Empty<Process>(); }
    }

    public Process StartServer(string configName = "ShogoSrv.cfg", bool skipDialogs = true, bool emptyExit = false)
    {
        var args = $"-config {configName}";
        if (skipDialogs) args += " -go";
        if (emptyExit) args += " -emptyexit";

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(GameDir, ServerExe),
            WorkingDirectory = GameDir,
            Arguments = args,
            UseShellExecute = true,
        };
        return Process.Start(psi)!;
    }
}
