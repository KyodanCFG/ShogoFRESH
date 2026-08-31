using System.Collections.ObjectModel;
using System.ComponentModel;
using ShogoLauncher.Models;
using ShogoLauncher.Services;

namespace ShogoLauncher.ViewModels;

/// <summary>
/// The Host tab: server settings, map rotation, mods, profiles, and the
/// ShogoSrv lifecycle. Split out of MainViewModel.cs, which had grown past
/// 1200 lines spanning five unrelated tabs; the partials divide it by tab
/// so a change to hosting cannot collide with one to settings.
/// </summary>
public partial class MainViewModel
{

    // --- Host tab fields ---

    /// <summary>
    /// Listen server: Client.exe hosts while the player plays, instead of a
    /// separate FreshSrv.exe. Every gameplay field on this tab applies to
    /// both kinds - one ShogoSrv.cfg feeds both, which is the design: the
    /// dedicated server loads the file itself, and the game pushes the same
    /// keys across with "serv" when "+FreshHost 1" starts it (NetStart.cpp).
    ///
    /// What does NOT apply: browser registration, peer exchange and rcon all
    /// live in the FreshSrv.exe process, so a listen server is join-by-IP
    /// only. Those fields grey out rather than pretending.
    ///
    /// Persisted in ShogoSrv.cfg itself ("HostListen") rather than in
    /// launcher prefs - profiles then carry it, and LauncherPrefs.Save's
    /// Settings-tab-only behaviour (CLAUDE.md fact 10) never enters into it.
    /// </summary>
    /// <summary>The two defaults, one per kind of host. A listen server sits
    /// one above so the two can coexist on a machine without competing, which
    /// is also the port TrenchBroom's build-and-host already picks.</summary>
    public const int DedicatedDefaultPort = 27888;
    public const int ListenDefaultPort    = 27889;

    private bool _hostIsListen;
    public bool HostIsListen
    {
        get => _hostIsListen;
        set
        {
            Set(ref _hostIsListen, value);

            // The BOX follows the mode, rather than the launcher applying a
            // silent +1 at launch and reporting it afterwards.
            //
            // The first version kept the field on the configured port and
            // moved the real one behind the scenes, on the reasoning that the
            // field is the SETTING. True, and it meant the number on screen
            // was not the number you would tell somebody to connect to -
            // which is the only thing that field is for.
            //
            // Only the two defaults flip, and symmetrically, so a port
            // somebody actually chose is never touched: 27888 <-> 27889 and
            // nothing else. Whatever is shown is what gets saved and what
            // gets used, so there is no hidden third value anywhere.

            if (value  && HostPort == DedicatedDefaultPort) HostPort = ListenDefaultPort;
            if (!value && HostPort == ListenDefaultPort)    HostPort = DedicatedDefaultPort;

            OnPropertyChanged(nameof(HostIsDedicated));
            OnPropertyChanged(nameof(HostStartButtonText));
        }
    }

    /// <summary>The other half of the radio pair, and the IsEnabled anchor
    /// for the dedicated-only fields.</summary>
    public bool HostIsDedicated
    {
        get => !_hostIsListen;
        set => HostIsListen = !value;
    }

    public string HostStartButtonText => HostIsListen ? "Play + Host" : "Start Server";

    private string _hostName = "My Shogo Server";
    public string HostName { get => _hostName; set => Set(ref _hostName, value); }

    private int _hostPort = 27888;
    public int HostPort { get => _hostPort; set => Set(ref _hostPort, value); }

    private int _hostMaxPlayers = 16;
    public int HostMaxPlayers { get => _hostMaxPlayers; set => Set(ref _hostMaxPlayers, value); }

    /// <summary>
    /// "BotFill" - hold this many bots so the server is never empty. They
    /// hot-seat: the game code caps the target at whatever is left of
    /// MaxPlayers after the humans, so a bot always gives up its slot to an
    /// arriving player and returns when one leaves. 0 leaves bots alone
    /// entirely, so BotAdd/BotRemove still work by hand.
    /// </summary>
    private int _hostBotFill;
    public int HostBotFill { get => _hostBotFill; set => Set(ref _hostBotFill, value); }

    /// <summary>
    /// "WebRegUrl" - master server registration endpoint, or blank for none.
    ///
    /// Blank by default. The stock built-in pointed at shogo-mad.com, which
    /// has not answered since the original master went away, so every stock
    /// server has been spending one HTTP request per registration cycle on a
    /// host that can only fail.
    /// </summary>
    private string _hostWebRegUrl = "";
    public string HostWebRegUrl { get => _hostWebRegUrl; set => Set(ref _hostWebRegUrl, value); }

    /// <summary>
    /// "Peers" - other servers to swap server lists with, space separated
    /// address:port.
    ///
    /// This is what lets players find this server with no master site up:
    /// announcing to a peer adds us to ITS list too, so one working address is
    /// enough to join the whole network. peers.txt beside FreshSrv.exe merges
    /// with this rather than being replaced by it.
    /// </summary>
    private string _hostPeers = "";
    public string HostPeers { get => _hostPeers; set => Set(ref _hostPeers, value); }

    /// <summary>
    /// "CriticalHits" - allow the 5% double-damage roll in multiplayer.
    ///
    /// Off by default, which is a change from stock: criticals were always
    /// live in deathmatch, and a roll that doubles a hit with no tell and no
    /// counter decides duels invisibly.
    /// </summary>
    private bool _hostCriticalHits;
    public bool HostCriticalHits { get => _hostCriticalHits; set => Set(ref _hostCriticalHits, value); }

    /// <summary>
    /// "Intermission" - seconds of held end-of-match scoreboard before the
    /// next map. 0 = stock instant switch. Capped at 60: the board announces
    /// the next map and keeps chat live, and past a minute it is dead air.
    /// </summary>
    private int _hostIntermission = 15;
    public int HostIntermission
    {
        get => _hostIntermission;
        set { Set(ref _hostIntermission, value); OnPropertyChanged(nameof(HostIntermissionOn)); }
    }

    /// <summary>
    /// The checkbox beside the box. Unticking writes 0, which is what the
    /// server reads as "no intermission"; ticking restores a sensible value
    /// rather than whatever zero-adjacent number was last typed.
    ///
    /// Derived, not stored - 0 already means off in the config, and a second
    /// key saying the same thing is a second key that can disagree.
    /// </summary>
    public bool HostIntermissionOn
    {
        get => _hostIntermission > 0;
        set => HostIntermission = value ? (_hostIntermission > 0 ? _hostIntermission : 15) : 0;
    }

    /// <summary>
    /// "Gravity" - the world's downward acceleration, 0 = the engine's own.
    ///
    /// 2000 is what the engine uses, so that is what the box shows and what
    /// gets written when the option is off. Lower makes everything thrown,
    /// dropped and jumped hang longer; it is the one setting that changes
    /// grenade arcs and player movement together, which is exactly why it is
    /// worth exposing rather than leaving to the console.
    ///
    /// Clamped to the same range the game clamps to, so the launcher cannot
    /// write a number the server will silently refuse.
    /// </summary>
    private int _hostGravity = 2000;
    public int HostGravity
    {
        get => _hostGravity;
        set { Set(ref _hostGravity, value); OnPropertyChanged(nameof(HostGravityOn)); }
    }

    /// <summary>
    /// The checkbox beside the box. Unticking writes 0, which the server
    /// reads as "leave the engine's value alone"; ticking restores 2000
    /// rather than whatever was last typed on the way to zero.
    ///
    /// Derived rather than stored, for the reason HostIntermissionOn is.
    /// </summary>
    public bool HostGravityOn
    {
        get => _hostGravity > 0;
        set => HostGravity = value ? (_hostGravity > 0 ? _hostGravity : 2000) : 0;
    }

    /// <summary>
    /// "RconPassword" - remote console over the existing GameSpy query port.
    ///
    /// Empty means rcon is off in the server, not merely locked: an rcon query
    /// to a passwordless server gets no reply at all, so nothing advertises
    /// that the feature exists. The password crosses the network in plaintext
    /// (GameSpy v1 has no room for a challenge and stock tools must keep
    /// working), so it must not be a password used anywhere else.
    /// </summary>
    private string _hostRconPassword = "";
    public string HostRconPassword { get => _hostRconPassword; set => Set(ref _hostRconPassword, value); }

    /// <summary>
    /// "RequireFresh" - refuse clients that do not identify themselves as
    /// ShogoFRESH.
    ///
    /// Off by default and it should stay off for almost every server. It is
    /// the only RELIABLE way to make the client-side rules (quick turn,
    /// first person only) bind everyone, but the population is small enough
    /// that turning away someone who owns the game is a real cost. This is
    /// for organised matches, not for a public server.
    /// </summary>
    private bool _hostRequireFresh;
    public bool HostRequireFresh { get => _hostRequireFresh; set => Set(ref _hostRequireFresh, value); }

    private bool _hostTractorBeam = true;
    public bool HostTractorBeam { get => _hostTractorBeam; set => Set(ref _hostTractorBeam, value); }

    private bool _hostRamming = true;
    public bool HostRamming { get => _hostRamming; set => Set(ref _hostRamming, value); }

    private bool _hostQuickTurn;   // default OFF (ShogoFRESH rule; clients with the mod honor it)
    public bool HostQuickTurn { get => _hostQuickTurn; set => Set(ref _hostQuickTurn, value); }

    private bool _hostListPublicly = true;   // ServerReg: announce to the community browser
    public bool HostListPublicly { get => _hostListPublicly; set => Set(ref _hostListPublicly, value); }

    // Indexes match the RANDOMPICKUPS_* modes the server reads from
    // "RandomPickups": 0 none, 1 weapons, 2 items, 3 both separately,
    // 4 both pooled together.
    public string[] RandomPickupModes { get; } =
    {
        "None", "Weapons only", "Items only", "Weapons + items (separate)", "Weapons + items (together)",
    };

    // "Ruleset" - Classic is the untouched 1998 balance, ShogoFRESH opts
    // into ours. Index = the value the server var takes.
    // Order and text mirror FreshGameModeName() in Shared/FreshGameModes.h -
    // the INDEX is what reaches ShogoSrv.cfg, so these must stay in step with
    // GAMEMODE_NORMAL/TOWSOUT/SQUISHIE. A rotation entry may still override
    // any of this per map with "world:mode".
    // 4 and 5 are reserved for Duel and Headcount; the array is dense and
    // its INDEX is the mode number, so the two unbuilt ids are placeheld
    // rather than skipped - a gap here would host Squad Deathmatch as
    // whatever index it happened to land on.
    public string[] GameModes { get; } =
        { "Deathmatch", "TOWs Out", "Squishie", "Squishie War",
          "(reserved: Duel)", "(reserved: Headcount)", "Squad Deathmatch" };

    private string _hostGameMode = "Deathmatch";
    public string HostGameMode { get => _hostGameMode; set => Set(ref _hostGameMode, value); }

    public string[] Rulesets { get; } = { "Classic (1998 balance)", "FRESH" };

    private string _hostRuleset = "FRESH";
    public string HostRuleset { get => _hostRuleset; set => Set(ref _hostRuleset, value); }

    private bool _hostFirstPersonOnly = true;
    public bool HostFirstPersonOnly { get => _hostFirstPersonOnly; set => Set(ref _hostFirstPersonOnly, value); }

    /// <summary>
    /// "InfiniteAmmo" - three settings rather than a toggle.
    ///
    /// Sidearms is the interesting middle: the pulse rifle and the .45s
    /// never run dry, so nobody is ever caught in the open with an empty
    /// gun, but every scarcity the map's pickups are balanced around is
    /// left intact. All is the old checkbox.
    /// </summary>
    public string[] InfiniteAmmoModes { get; } =
    {
        "Off", "Sidearms only", "All weapons",
    };

    private string _hostInfiniteAmmo = "Sidearms only";
    public string HostInfiniteAmmo { get => _hostInfiniteAmmo; set => Set(ref _hostInfiniteAmmo, value); }

    // Indexes match MAPORDER_* on the server: 0 sequential, 1 random,
    // 2 random alternating between mech and on-foot maps.
    public string[] MapOrders { get; } =
    {
        "In order (default)", "Random", "Random, alternate MCA / on-foot",
    };

    private string _hostMapOrder = "In order (default)";
    public string HostMapOrder { get => _hostMapOrder; set => Set(ref _hostMapOrder, value); }

    private string _hostRandomPickups = "None";
    public string HostRandomPickups { get => _hostRandomPickups; set => Set(ref _hostRandomPickups, value); }

    /// <summary>Weapon ids banned from spawning, as the "BlockWeapons" var wants them.</summary>
    private string _hostBlockedWeapons = "";
    public string HostBlockedWeapons
    {
        get => _hostBlockedWeapons;
        set { Set(ref _hostBlockedWeapons, value); OnPropertyChanged(nameof(HostBlockedSummary)); }
    }

    /// <summary>Item class names banned from spawning, as "BlockItems" wants them.</summary>
    private string _hostBlockedItems = "";
    public string HostBlockedItems
    {
        get => _hostBlockedItems;
        set { Set(ref _hostBlockedItems, value); OnPropertyChanged(nameof(HostBlockedSummary)); }
    }

    public string HostBlockedSummary
    {
        get
        {
            int n = BlockablePickups.CountBlocked(HostBlockedWeapons, HostBlockedItems);
            return n == 0 ? "none blocked" : n == 1 ? "1 blocked" : $"{n} blocked";
        }
    }

    /// <summary>
    /// "BodyLifetime" - seconds a dead body lies around in multiplayer
    /// before it is removed. A server variable read live (BodyProp.cpp),
    /// default 120 (the owner's call - Monolith's own first value, before
    /// 2.2 cut it to 10 for 1998 hardware); single player never removes
    /// bodies at all, so this is multiplayer-only by the game's structure.
    /// </summary>
    private int _hostBodyLifetime = 120;
    public int HostBodyLifetime { get => _hostBodyLifetime; set => Set(ref _hostBodyLifetime, value); }

    private double _hostRunSpeed = 1.1;
    public double HostRunSpeed { get => _hostRunSpeed; set => Set(ref _hostRunSpeed, value); }

    private double _hostMissileSpeed = 1.0;
    public double HostMissileSpeed { get => _hostMissileSpeed; set => Set(ref _hostMissileSpeed, value); }

    /// <summary>
    /// Fall damage, the two dials from FreshTuning.h. THRESHOLD is how far
    /// you may drop for free (stock 15), SCALE is what it costs past that
    /// (stock 5). Both are multipliers on the character's own mass.
    ///
    /// These are here rather than among the weapon tuning because they are a
    /// server RULE, not a feel setting: two reasonable hosts differ on it,
    /// and RammingDamage - the same collision system's other half - has had
    /// a host toggle since the 1998 wizard.
    ///
    /// There is no "off" scale, because 0 means "use the table" everywhere in
    /// FreshTuning.h. Raising the threshold is the off switch.
    /// </summary>
    private double _hostFallDamage = 5.0;
    public double HostFallDamage { get => _hostFallDamage; set => Set(ref _hostFallDamage, value); }

    private double _hostFallThreshold = 15.0;
    public double HostFallThreshold { get => _hostFallThreshold; set => Set(ref _hostFallThreshold, value); }

    private double _hostRespawnScale = 1.0;
    public double HostRespawnScale { get => _hostRespawnScale; set => Set(ref _hostRespawnScale, value); }

    private double _hostHealScale = 1.0;
    public double HostHealScale { get => _hostHealScale; set => Set(ref _hostHealScale, value); }

    private double _hostTimeSpeed = -1.0;
    public double HostTimeSpeed { get => _hostTimeSpeed; set => Set(ref _hostTimeSpeed, value); }

    private string _hostNightColor = "0.5 0.5 0.5";
    public string HostNightColor
    {
        get => _hostNightColor;
        set { Set(ref _hostNightColor, value); OnPropertyChanged(nameof(HostNightColorSwatch)); }
    }

    /// <summary>The night color as a WPF Color, for the swatch preview.</summary>
    public System.Windows.Media.Color HostNightColorSwatch =>
        Views.ColorPickerDialog.ToColor(HostNightColor);

    private bool _hostFragLimitOn = true;
    public bool HostFragLimitOn { get => _hostFragLimitOn; set => Set(ref _hostFragLimitOn, value); }

    private int _hostFragLimit = 45;
    public int HostFragLimit { get => _hostFragLimit; set => Set(ref _hostFragLimit, value); }

    private bool _hostTimeLimitOn = true;
    public bool HostTimeLimitOn { get => _hostTimeLimitOn; set => Set(ref _hostTimeLimitOn, value); }

    private int _hostTimeLimit = 15;
    public int HostTimeLimit { get => _hostTimeLimit; set => Set(ref _hostTimeLimit, value); }

    // Map rotation. Retail names carry the Worlds\Multi\ prefix in LevelN
    // entries; customs (Custom\*.dat) are bare names. Retail list = maps
    // confirmed from the v2.21 wizard + live servers (runtime-verify the
    // full retail roster); the free-text add covers anything missing.
    // Retail multiplayer maps. Names verified by scanning SHOGO.REZ (the
    // engine loads these exact spellings - note MCA_MADSKILLS with an S but
    // OF_MADSKILLZ with a Z, and OF_LOST_CAT with an underscore) and
    // cross-checked against the community map lists.
    private static readonly string[] RetailMaps =
    {
        // Mecha
        @"Worlds\Multi\MCA_12FLOZ",
        @"Worlds\Multi\MCA_AVERNUS",
        @"Worlds\Multi\MCA_ENTRANCE",
        @"Worlds\Multi\MCA_MADSKILLS",
        @"Worlds\Multi\MCA_MARITROPA1",
        @"Worlds\Multi\MCA_MARITROPA2",
        @"Worlds\Multi\MCA_MARITROPA3",
        @"Worlds\Multi\MCA_SHINARA",
        // These two were missing from this list until 0.8.21 - both are in
        // SHOGO.REZ, both are perfectly good retail deathmatch maps, and
        // neither had ever been selectable in a rotation. Found by scanning a
        // map pack and noticing it carried 22 levels against our 20.
        @"Worlds\Multi\MCA_SPIRES",
        @"Worlds\Multi\MCA_ZERO",
        // On foot
        @"Worlds\Multi\OF_COMM2",
        @"Worlds\Multi\OF_IKARI",
        @"Worlds\Multi\OF_LOST_CAT",
        @"Worlds\Multi\OF_MADSKILLZ",
        @"Worlds\Multi\OF_MIDNIGHT",
        @"Worlds\Multi\OF_NIGHT",
        @"Worlds\Multi\OF_NIZCORPSES",
        @"Worlds\Multi\OF_QUAKER",
        @"Worlds\Multi\OF_RESEARCH",
        @"Worlds\Multi\OF_SEC1",
        @"Worlds\Multi\OF_THERMAL",
        @"Worlds\Multi\OF_UNDERGROUND",
    };

    public ObservableCollection<string> AvailableMaps { get; } = new();

    /// <summary>What the last Refresh found, beside the button. Empty until
    /// one has been pressed - an unprompted count would read as a warning.
    /// Deliberately NOT in HostProperties: it reports on the tab, it is not a
    /// setting, and marking the tab dirty for it would prompt to save a
    /// string nothing writes.</summary>
    private string _mapScanSummary = "";
    public string MapScanSummary { get => _mapScanSummary; set => Set(ref _mapScanSummary, value); }
    public ObservableCollection<string> MapRotation { get; } = new();

    /// <summary>
    /// MAX_GAME_LEVELS in NetDefs.h - what NetGame can actually carry. The
    /// label states it because the limit used to be invisible until the
    /// server met it, and for one release meeting it crashed the server.
    /// </summary>
    public const int MaxRotationEntries = 100;

    /// <summary>"Rotation (12/100)" - the count against the game's own limit.</summary>
    public string RotationLabel => $"Rotation ({MapRotation.Count}/{MaxRotationEntries})";

    public class RezRow : INotifyPropertyChanged
    {
        public string Name { get; init; } = "";

        /// <summary>Display only. Name stays intact - it is what goes on the
        /// -rez command line, so trimming it there would stop the archive
        /// loading.</summary>
        public string DisplayName =>
            Name.EndsWith(".rez", StringComparison.OrdinalIgnoreCase)
                ? Name.Substring(0, Name.Length - 4)
                : Name;

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set { _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public ObservableCollection<RezRow> AvailableRez { get; } = new();

    /// <summary>Populate host-tab lists from the game dir + existing ShogoSrv.cfg
    /// (or, before one exists, the shipped server defaults - so toggles like
    /// Grapple=on / QuickTurn=off come from Defaults\server-settings.cfg).</summary>
    public void LoadHostState()
    {
        if (!GameFound) return;

        // A fresh Shogo install already ships a ShogoSrv.cfg (with, among
        // other things, TractorBeam 0 - which is why grapple kept reverting
        // to off). So "does the file exist" is not a usable test for
        // first-time setup; we look for a marker the launcher itself writes.
        var cfgPath = System.IO.Path.Combine(GameDir!, "ShogoSrv.cfg");
        bool bOurs = System.IO.File.Exists(cfgPath) &&
                     new ShogoConfigFile(cfgPath).GetInt(HostService.ConfigMarker, 0) != 0;

        bool bFirstTime = !bOurs;
        if (bFirstTime)
        {
            var shipped = System.IO.Path.Combine(AppContext.BaseDirectory, "Defaults", "server-settings.cfg");
            if (System.IO.File.Exists(shipped)) cfgPath = shipped;
        }
        var cfg = new ShogoConfigFile(cfgPath);

        // No server configured yet: name it after the pilot rather than
        // leaving everyone hosting an identically-named server.
        HostName = bFirstTime && !string.IsNullOrWhiteSpace(PlayerName)
            ? $"{PlayerName}'s ShogoFRESH Server"
            : cfg.Get("ServerName") ?? HostName;
        var port = cfg.GetInt("Port", 0);
        HostPort = port == 0 ? 27888 : port;
        HostMaxPlayers = cfg.GetInt("MaxPlayers", 16);
        HostBotFill    = cfg.GetInt("BotFill", 0);
        HostWebRegUrl  = cfg.Get("WebRegUrl") ?? "";
        HostPeers      = cfg.Get("Peers") ?? "";
        HostTractorBeam = cfg.GetInt("TractorBeam", 1) != 0;
        HostRamming = cfg.GetInt("RammingDamage", 1) != 0;
        HostQuickTurn = cfg.GetInt("QuickTurn", 0) != 0;
        HostListPublicly = cfg.GetInt("ServerReg", 1) != 0;
        HostRandomPickups = RandomPickupModes[Math.Clamp(cfg.GetInt("RandomPickups", 0), 0, RandomPickupModes.Length - 1)];
        HostBlockedWeapons = BlockablePickups.NormalizeWeapons(cfg.Get("BlockWeapons") ?? "8 21");
        HostBlockedItems = BlockablePickups.NormalizeItems(cfg.Get("BlockItems") ?? "");
        HostInfiniteAmmo = InfiniteAmmoModes[Math.Clamp(cfg.GetInt("InfiniteAmmo", 1), 0, InfiniteAmmoModes.Length - 1)];
        HostCriticalHits = cfg.GetInt("CriticalHits", 0) != 0;
        HostIntermission = cfg.GetInt("Intermission", 15);
        // 2000 rather than 0: it is the engine's own value (FreshTuning.h records
        // the default as -2000 on Y, expressed positive here), so a fresh config
        // shows a real number instead of a zero that reads as "broken". Writing
        // 2000 and writing nothing produce the same world - ApplyWorldGravity
        // returns early at 0 and sets -2000 otherwise - so this changes what the
        // field SAYS, not what the server does.
        HostGravity = cfg.GetInt("Gravity", 2000);
        HostRconPassword = cfg.Get("RconPassword") ?? "";
        HostRequireFresh = cfg.GetInt("RequireFresh", 0) != 0;
        HostIsListen   = cfg.GetInt("HostListen", 0) != 0;
        HostMapOrder = MapOrders[Math.Clamp(cfg.GetInt("MapOrder", 0), 0, MapOrders.Length - 1)];
        HostFirstPersonOnly = cfg.GetInt("FirstPersonOnly", 1) != 0;
        HostGameMode = GameModes[Math.Clamp(cfg.GetInt("GameMode", 0), 0, GameModes.Length - 1)];
        HostRuleset = Rulesets[Math.Clamp(cfg.GetInt("Ruleset", 1), 0, Rulesets.Length - 1)];
        HostBodyLifetime = cfg.GetInt("BodyLifetime", 120);
        HostRunSpeed = cfg.GetFloat("RunSpeed", 1.1f);
        HostMissileSpeed = cfg.GetFloat("MissileSpeed", 1.0f);
        HostFallDamage = cfg.GetFloat("FallDamage", 5.0f);
        HostFallThreshold = cfg.GetFloat("FallThreshold", 15.0f);
        HostRespawnScale = cfg.GetFloat("RespawnScale", 1.0f);
        HostHealScale = cfg.GetFloat("HealScale", 1.0f);
        HostTimeSpeed = cfg.GetFloat("WorldTimeSpeed", -1.0f);
        HostNightColor = cfg.Get("WorldColorNight") ?? "0.5 0.5 0.5";
        int endType = cfg.GetInt("EndType", 3);   // frags + time by default
        HostFragLimitOn = (endType & 1) != 0;
        HostTimeLimitOn = (endType & 2) != 0;
        HostFragLimit = cfg.GetInt("EndFrags", 45);
        HostTimeLimit = cfg.GetInt("EndTime", 15);

        // Existing rotation. Entries written by 0.9.46-0.9.55 carried a
        // "Custom\" or "Custom\maps\mp\" prefix from the era when the missing
        // mount was being diagnosed; bare is the canonical form (the mount
        // puts Custom's contents at the tree root), so stale prefixes are
        // stripped here and the next save writes a clean rotation - which
        // also retires the server's "would not start - started as" fallback
        // line for these entries. Worlds\ paths are retail and stay.
        MapRotation.Clear();
        int numLevels = cfg.GetInt("NumLevels", 0);
        for (int i = 0; i < numLevels && i < 256; i++)
        {
            var lvl = cfg.Get($"Level{i}");
            if (string.IsNullOrWhiteSpace(lvl)) continue;

            if (lvl.StartsWith(@"Custom\", StringComparison.OrdinalIgnoreCase))
                lvl = lvl[(lvl.LastIndexOf('\\') + 1)..];

            MapRotation.Add(lvl);
        }

        // Available maps. Extracted so the Refresh button can rebuild JUST
        // this list without touching the rotation the operator is editing -
        // re-running the whole load would silently discard unsaved edits.
        ScanAvailableMaps();

        // Available rez mods: Custom\*.rez, pre-checked if in the cfg.
        var selectedRez = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int numRez = cfg.GetInt("NumRezFiles", 0);
        for (int i = 0; i < numRez && i < 64; i++)
            if (cfg.Get($"RezFile{i}") is { Length: > 0 } r) selectedRez.Add(r);

        // Disabled archives are listed here too, and deliberately.
        //
        // The Mods tab's on/off is a CLIENT decision - which archives your own
        // game launches with. This list is a SERVER decision, and a dedicated
        // server is usually not even the same machine. Hiding a mod from the
        // host because the local client has it switched off conflates two
        // unrelated questions, and it is how the Host tab lost its whole list
        // when the mod default changed.

        // Its own local now: the map scan that used to declare this moved out
        // to ScanAvailableMaps so Refresh could call it alone.
        var customDir = System.IO.Path.Combine(GameDir!, "Custom");

        AvailableRez.Clear();
        if (System.IO.Directory.Exists(customDir))
            foreach (var rez in System.IO.Directory.EnumerateFiles(customDir, "*.rez*").OrderBy(f => f))
            {
                var file = System.IO.Path.GetFileName(rez);

                if (!file.EndsWith(".rez", System.StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith(".rez.off", System.StringComparison.OrdinalIgnoreCase)) continue;

                // Always the real archive name. What the server writes into
                // ShogoSrv.cfg must be loadable, and ".rez.off" is not.
                var name = file.EndsWith(".off", System.StringComparison.OrdinalIgnoreCase)
                         ? file[..^4] : file;

                AvailableRez.Add(new RezRow { Name = name, Selected = selectedRez.Contains(name) });
            }
    }

    /// <summary>Write ShogoSrv.cfg from the Host tab without starting the server.</summary>
    public void SaveHostSettings()
    {
        if (!GameFound) { Warn("Cannot save - game directory not set."); return; }

        WriteHostConfig();
        HostDirty = false;

        // Say so when a value was clamped. Silently rewriting what somebody
        // typed is how "I set bot fill to 60 and it ignored me" happens - the
        // field just shows a different number afterwards with no explanation.
        Status = _lastClamps.Count > 0
            ? "Saved to ShogoSrv.cfg — adjusted: " + string.Join("; ", _lastClamps)
            : "Server settings saved to ShogoSrv.cfg.";

        StatusIsError = _lastClamps.Count > 0;
    }

    /// <summary>What WriteHostConfig had to clamp on the last save.</summary>
    private readonly List<string> _lastClamps = new();

    /// <summary>Clamp, and record it if the value actually moved.</summary>
    /// <param name="because">
    /// Where the ceiling came from, when it is not a fixed engine limit. A
    /// bare "limit 0-16" reads as a bug when the documented maximum is 48;
    /// naming max players turns it into an explanation.
    /// </param>
    private int ClampReport(int value, int lo, int hi, string label, string? because = null)
    {
        var clamped = Math.Clamp(value, lo, hi);

        if (clamped != value)
        {
            var why = because is null ? "" : $" - {because}";
            _lastClamps.Add($"{label} {value} -> {clamped} (limit {lo}-{hi}{why})");
        }

        return clamped;
    }

    /// <summary>
    /// The same, for a value the game clamps as a float. 0 passes through
    /// untouched: it is the "use the table" sentinel every FreshTuning.h
    /// variable accepts, and clamping it up to the minimum would turn
    /// "leave this alone" into a real and very different number.
    /// </summary>
    private double ClampReport(double value, double lo, double hi, string label, string? because = null)
    {
        if (value == 0.0) return value;

        var clamped = Math.Clamp(value, lo, hi);

        if (clamped != value)
        {
            var why = because is null ? "" : $" - {because}";
            _lastClamps.Add($"{label} {value:0.##} -> {clamped:0.##} (limit {lo:0.##}-{hi:0.##}{why})");
        }

        return clamped;
    }

    /// <summary>
    /// Rebuild the "Available" list from what is on disk right now: retail
    /// maps, Custom\maps\mp, loose Custom\*.dat, and the levels inside any
    /// Custom\*.rez.
    ///
    /// Separate from LoadHostState so it can be called again while the tab is
    /// open. A mapper compiles a map, alt-tabs, and expects to see it - and
    /// before this the only way was to restart the launcher, because the scan
    /// ran once inside the full load. Re-running THAT would rebuild the
    /// rotation from the saved cfg and throw away whatever had been arranged
    /// since, which is a worse answer than no refresh at all.
    ///
    /// Does not touch MapRotation, HostDirty, or anything read from the cfg.
    /// </summary>
    public void ScanAvailableMaps()
    {
        if (!GameFound) return;

        // Available maps: retail + Custom\*.dat.
        AvailableMaps.Clear();
        foreach (var m in RetailMaps) AvailableMaps.Add(m);
        var customDir = System.IO.Path.Combine(GameDir!, "Custom");

        // BARE NAMES, because that is what the mounts produce.
        //
        // The launcher passes "-rez Custom" plus the maps\sp and maps\mp
        // folders (see GameLaunchService.BuildRezArgs); every mount's
        // contents land at the file-tree root, so a map is its bare name on
        // every client wherever it sits on disk. The FOLDERS only decide
        // which lists a map appears in: maps\mp and loose Custom\ are
        // offered here, maps\sp is the single-player menu's and deliberately
        // absent. Deduped case-insensitively because two folders can hold
        // the same name - the tree flattens them to one world anyway.

        // Remember each map's real on-disk location for display only. The
        // lists still store bare names (what the server loads); this just lets
        // MapDisplayConverter show "Custom\maps\mp\X" so the operator can tell
        // an MP folder map from a loose one from a packed one at a glance.
        Services.MapDisplayConverter.FolderByBare.Clear();

        if (System.IO.Directory.Exists(customDir))
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var customMpDir = System.IO.Path.Combine(customDir, "maps", "mp");
            if (System.IO.Directory.Exists(customMpDir))
                foreach (var dat in System.IO.Directory.EnumerateFiles(customMpDir, "*.dat").OrderBy(f => f))
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(dat);
                    if (seen.Add(name))
                    {
                        AvailableMaps.Add(name);
                        Services.MapDisplayConverter.FolderByBare[name] = @"Custom\maps\mp";
                    }
                }

            foreach (var dat in System.IO.Directory.EnumerateFiles(customDir, "*.dat").OrderBy(f => f))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(dat);
                if (seen.Add(name))
                {
                    AvailableMaps.Add(name);
                    Services.MapDisplayConverter.FolderByBare[name] = "Custom";
                }
            }

            // And the levels inside any .rez, which is how map packs actually
            // ship. Without this a pack could be installed and working while
            // every one of its maps stayed invisible to the rotation list -
            // the only way in was hand-editing ShogoSrv.cfg. See
            // ModManager.ListLevels for the caveat.

            foreach (var rez in System.IO.Directory.EnumerateFiles(customDir, "*.rez").OrderBy(f => f))
                foreach (var lvl in Services.ModManager.ListLevels(rez))
                    if (!AvailableMaps.Contains(lvl, StringComparer.OrdinalIgnoreCase))
                    {
                        AvailableMaps.Add(lvl);
                        Services.MapDisplayConverter.FolderByBare[lvl] =
                            @"Custom\" + System.IO.Path.GetFileName(rez);
                    }
        }
    }

    private HostService WriteHostConfig()
    {
        _lastClamps.Clear();

        // MAX_MULTI_PLAYERS = 128 in the game code (NetDefs.h).
        HostMaxPlayers = ClampReport(HostMaxPlayers, 2, 128, "max players");

        // 48 is MAX_BOT_SLOTS in FreshBot.h; more than MaxPlayers is
        // harmless but pointless, since the fill is capped by the free
        // slots anyway. Whichever of the two is doing the limiting gets
        // named, because "limit 0-16" against a documented maximum of 48
        // otherwise reads as the launcher being wrong.
        var nBotCeiling = Math.Min(48, HostMaxPlayers);

        HostBotFill    = ClampReport(HostBotFill, 0, nBotCeiling, "bot fill",
                                     nBotCeiling < 48
                                         ? $"max players is {HostMaxPlayers}"
                                         : "48 bot slots is the engine maximum");
        HostIntermission = ClampReport(HostIntermission, 0, 60, "intermission");

        // 0 is a real choice (bodies vanish at once); five minutes is past
        // any use a corpse has and into "the map is a morgue".
        HostBodyLifetime = ClampReport(HostBodyLifetime, 0, 300, "body lifetime");

        // MAX_GAME_LEVELS in NetDefs.h - the rotation lives in a fixed-size
        // array inside NetGame, which is shared with the server application.
        //
        // Writing more than this used to be silent, and the server did not
        // survive it: its startup path copied the whole list into that array
        // with no bound and overflowed into the globals behind it. That is
        // fixed on the server side too, but the launcher has no business
        // writing a rotation it knows cannot be loaded - and the operator
        // needs to be TOLD which maps are not going to play.

        const int MaxRotation = 100;      // MAX_GAME_LEVELS in NetDefs.h
        const int MaxEntryLen = 63;       // NML_ROTATION - 1

        if (MapRotation.Count > MaxRotation)
        {
            _lastClamps.Add($"map rotation {MapRotation.Count} -> {MaxRotation} " +
                            $"(the game carries {MaxRotation} maps; the rest were dropped)");

            while (MapRotation.Count > MaxRotation) MapRotation.RemoveAt(MapRotation.Count - 1);
        }

        // A rotation entry longer than the game's own field would be cut short
        // on load, and a map name silently missing its tail is a map that does
        // not load. Say so here rather than letting the server discover it.

        var tooLong = MapRotation.Where(m => m.Length > MaxEntryLen).ToList();

        if (tooLong.Count > 0)
        {
            _lastClamps.Add($"{tooLong.Count} rotation entr{(tooLong.Count == 1 ? "y is" : "ies are")} " +
                            $"longer than {MaxEntryLen} characters and will be cut short " +
                            $"(first: {tooLong[0]})");
        }

        // 0 stays 0 - it means "do not touch the engine's value" and is
        // not a gravity of zero. Only a number the user actually chose
        // gets pulled into range.

        if (HostGravity != 0)
            HostGravity = ClampReport(HostGravity, 200, 6000, "gravity");

        // Mirrors FRESHTUNE_FALL_* in Shared/FreshTuning.h. Duplicated in two
        // languages, so preflight asserts the four numbers still agree - the
        // game would clamp anyway, silently, and a host who typed 2000 would
        // never learn the ceiling is 1000.

        HostFallDamage    = ClampReport(HostFallDamage, 0.05, 50.0, "fall damage");
        HostFallThreshold = ClampReport(HostFallThreshold, 1.0, 1000.0, "fall threshold");

        var host = new HostService(GameDir!);
        host.WriteConfig(new HostService.HostOptions(
            ServerName: HostName,
            Port: HostPort,
            MaxPlayers: HostMaxPlayers,
            BotFill: HostBotFill,
            WebRegUrl: HostWebRegUrl?.Trim() ?? "",
            Peers: HostPeers?.Trim() ?? "",
            TractorBeam: HostTractorBeam,
            RammingDamage: HostRamming,
            FallDamage: HostFallDamage,
            FallThreshold: HostFallThreshold,
            QuickTurn: HostQuickTurn,
            RunSpeed: HostRunSpeed,
            MissileSpeed: HostMissileSpeed,
            RespawnScale: HostRespawnScale,
            HealScale: HostHealScale,
            WorldTimeSpeed: HostTimeSpeed,
            WorldColorNight: HostNightColor,
            ListPublicly: HostListPublicly,
            RandomPickups: Math.Max(0, Array.IndexOf(RandomPickupModes, HostRandomPickups)),
            BlockedWeapons: HostBlockedWeapons,
            BlockedItems: HostBlockedItems,
            InfiniteAmmo: Math.Max(0, Array.IndexOf(InfiniteAmmoModes, HostInfiniteAmmo)),
            CriticalHits: HostCriticalHits,
            Intermission: HostIntermission,
            Gravity: HostGravity,
            RconPassword: HostRconPassword?.Trim() ?? "",
            RequireFresh: HostRequireFresh,
            MapOrder: Math.Max(0, Array.IndexOf(MapOrders, HostMapOrder)),
            FirstPersonOnly: HostFirstPersonOnly,
            GameMode: Math.Max(0, Array.IndexOf(GameModes, HostGameMode)),
            Ruleset: Math.Max(0, Array.IndexOf(Rulesets, HostRuleset)),
            BodyLifetime: HostBodyLifetime,
            FragLimitOn: HostFragLimitOn,
            FragLimit: HostFragLimit,
            TimeLimitOn: HostTimeLimitOn,
            TimeLimit: HostTimeLimit,
            Levels: MapRotation.ToList(),
            RezFiles: AvailableRez.Where(r => r.Selected).Select(r => r.Name).ToList(),
            Listen: HostIsListen));
        return host;
    }

    // ----- Server profiles (named copies of ShogoSrv.cfg) -----

    public ObservableCollection<string> ServerProfileNames { get; } = new();

    private string? _selectedServerProfile;
    public string? SelectedServerProfile { get => _selectedServerProfile; set => Set(ref _selectedServerProfile, value); }

    private string _newProfileName = "";
    public string NewProfileName { get => _newProfileName; set => Set(ref _newProfileName, value); }

    public void RefreshServerProfiles(string? keepSelected = null)
    {
        ServerProfileNames.Clear();
        foreach (var n in ServerProfiles.List()) ServerProfileNames.Add(n);

        SelectedServerProfile = keepSelected is not null && ServerProfileNames.Contains(keepSelected)
            ? keepSelected
            : ServerProfileNames.FirstOrDefault();
    }

    /// <summary>Write the current Host tab out, then file it under a name.</summary>
    public void SaveServerProfile(string name)
    {
        if (!GameFound || string.IsNullOrWhiteSpace(name)) return;

        WriteHostConfig();
        HostDirty = false;

        ServerProfiles.Save(GameDir!, name);
        RefreshServerProfiles(name);

        Status = $"Saved server profile \"{name}\".";
    }

    public void LoadServerProfile(string name)
    {
        if (!GameFound || !ServerProfiles.Exists(name)) return;

        ServerProfiles.Load(GameDir!, name);
        LoadHostState();
        HostDirty = false;

        Status = $"Loaded server profile \"{name}\". Start Server to run it.";
    }

    /// <summary>How many ShogoSrv processes are already up.</summary>
    public int RunningServerCount() => HostService.RunningServers().Length;

    public void StartHosting()
    {
        if (!GameFound) { Status = "Cannot host - game directory not set."; return; }

        var host = WriteHostConfig();
        HostDirty = false;

        if (HostIsListen) { StartListenServer(); return; }

        host.StartServer();
        Status = $"ShogoSrv started on port {HostPort}. Forward UDP {HostPort} on your router to be reachable.";
    }

    /// <summary>
    /// Start hosting inside the game itself: Client.exe with "+FreshHost 1",
    /// which reads the ShogoSrv.cfg just written and hosts with no wizard.
    /// </summary>
    private void StartListenServer()
    {
        // On a listen server the client IS the server, so the Host tab's
        // archive list and the Mods tab's on/off finally describe one
        // process and must agree. The rule: starting the server ENABLES the
        // archives it needs, and says which - never silently, which is the
        // mistake 0.9.38 made once already. Client-side mods that are
        // enabled but unselected still load too; that is what "the client
        // is the server" means, and their gameplay rules travel by the
        // modrules file exactly as they do in single player.

        var enabled = new List<string>();

        try
        {
            var mm = new ModManager(GameDir!);
            var mods = mm.ListMods();

            foreach (var row in AvailableRez.Where(r => r.Selected))
            {
                var mod = mods.FirstOrDefault(m =>
                    m.Name.Equals(row.Name, StringComparison.OrdinalIgnoreCase));

                if (mod is { Enabled: false })
                {
                    mm.SetEnabled(mod, true);
                    enabled.Add(mod.Name);
                }
            }
        }
        catch (System.IO.IOException ex)
        {
            // A selected archive we could not switch on would silently be
            // missing from the server. Stop and say so instead.
            Warn($"Could not enable a selected archive: {ex.Message}");
            return;
        }

        if (enabled.Count > 0) RefreshMods();   // the Mods tab must show what just happened

        // A dedicated server on this machine reads the SAME ShogoSrv.cfg, so
        // without this the listen server asks for a port that is already
        // bound and dies with "Unable to bind to the requested port" while
        // the status line below claims it started. Move rather than refuse:
        // the player asked to play, and the port is an implementation detail
        // right up until they need to tell somebody how to connect - which is
        // why the move is stated rather than done quietly.

        // A LISTEN SERVER DEFAULTS ONE PORT UP, so the two kinds of host on a
        // machine stop competing by default instead of only after a collision.
        //
        // Avoiding the clash at all is better than detecting it. The search
        // below still runs and is still the backstop, but a mapper with a
        // dedicated server up should not have to see a port move at all - and
        // TrenchBroom's build-and-host already picks 27889 for exactly this
        // reason, so this makes the launcher agree with the editor rather than
        // inventing a third convention.
        //
        // "Configured" is unambiguous here: WriteConfig stores 27888 as 0
        // ("Port 0 means default"), so a config never holds 27888 explicitly
        // and HostPort == 27888 can only mean the player left it alone. Any
        // other value is a deliberate choice and is used as given.

        // No hidden +1 here any more - the Host tab's port box already reads
        // 27889 the moment Listen is selected, so the number on screen is the
        // number that gets used. This is only the collision backstop.

        int wanted = HostPort;
        int port   = HostService.FreeListenPort(wanted);

        // The game would otherwise take the config's port, which is HostPort -
        // so the override has to travel whenever we landed anywhere else.

        string sPortArg = port != HostPort ? $"+hostport {port} " : "";

        new GameLaunchService(GameDir!)
        {
            FreshTakesPriority = Prefs.FreshTakesPriority,
        }
        // multiplayer: true - hosting a listen server IS a multiplayer
        // launch, so E7's music driver is kept out of the process unless
        // MusicInMultiplayer says otherwise. See
        // GameLaunchService.ShouldDisableMusicForMultiplayer.
        .LaunchGame(null, ("+FreshHost 1 " + sPortArg + Prefs.BuildArgs()).Trim(),
                    multiplayer: true);

        // One case left to report. The "we moved you off the dedicated
        // default" message is gone because there is nothing to explain any
        // more - the box already showed 27889 before the button was pressed.

        string sMoved = port != wanted
            ? $" Port {wanted} was already in use, so this game is on {port} instead."
            : "";

        Status = enabled.Count > 0
            ? $"Listen server starting on port {port} — enabled {string.Join(", ", enabled)} so the game loads them.{sMoved} Forward UDP {port} to be reachable."
            : $"Listen server starting on port {port} — you are hosting while you play.{sMoved} Forward UDP {port} to be reachable.";
    }
}
