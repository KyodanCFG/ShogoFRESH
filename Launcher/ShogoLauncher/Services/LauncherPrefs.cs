using System;
using System.IO;
using System.Text.Json;

namespace ShogoLauncher.Services;

/// <summary>
/// Launcher-side preferences (advanced launch flags, extra args) persisted to
/// %AppData%\ShogoLauncher\launcher.json. These are passed to Client.exe on
/// the command line, not written into game config files.
///
/// The flag set matches what Monolith's own launchers pass:
/// +DisableMovies, +DisableMusic, +DisableSound, +DisableFog,
/// +DisableLightMap, +DisableModelFB, +DisableDx6Cmds, +DisableLines,
/// +EnableOptSurf, +EnableTripBuf, +EnableTjuncs (poly gap fixing),
/// +EnablePixDub, plus -rez / -multiplayer / -windowtitle switches.
///
/// Two of them turned out to be traps, and both are worth knowing before
/// adding another flag on the strength of where it appears:
///
/// Monolith's two launchers disagree about the fog flag - one spells it
/// "DisableFox" - while the SDK source the game is built from reads
/// "DisableFog". A flag appearing in a launcher is not evidence that
/// anything READS it.
///
/// "+EnableMipSharp" was removed outright: both of Monolith's launchers
/// pass it and nothing anywhere consumes it, the SDK source included. It
/// was a dead control in 1998 too.
/// </summary>
public class LauncherPrefs
{
    // --- Persistence ---
    //
    // Every property here saves on change.
    //
    // Save() used to be called by hand, and only the Settings tab called it.
    // Anything bound as {Binding Prefs.X} from another tab therefore appeared
    // to work and then reverted, and each property that needed to persist had
    // to remember to opt in. Two already had, in two different ways:
    // FreshTakesPriority wrapped itself in the view model, and CheckForUpdates
    // did a load-modify-save against a SEPARATE instance - which meant the
    // shared one still held the stale value, so the next Settings save wrote
    // it straight back over the top. A setting that silently un-sets itself
    // is a worse failure than one that never saved.
    //
    // Doing it here rather than per-property means a new pref is persistent
    // by default and cannot be forgotten.

    private bool _autoSave;   // false until Load() finishes - see below

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;

        field = value;
        if (_autoSave) Save();
    }

    // ShogoFRESH.rez loads after the Custom\ mods, so it wins any file both
    // contain. On by default: our rez holds only the four game DLLs, so this
    // costs asset mods nothing and only blocks mods that ship game code -
    // which would have replaced ShogoFRESH wholesale and silently.
    private bool _freshTakesPriority = true;
    public bool FreshTakesPriority { get => _freshTakesPriority; set => Set(ref _freshTakesPriority, value); }

    // The 0.10.8 keyboard layout, applied once to an install that predates it.
    // See KeybindMigration - the engine has no unbind command, so this is the
    // only route, and it is deliberately a one-shot rather than something
    // enforced on every launch: a player who moves a key back means it.
    private bool _keybindsMigrated0108;
    public bool KeybindsMigrated0108 { get => _keybindsMigrated0108; set => Set(ref _keybindsMigrated0108, value); }

    // Quick melee F -> Q, applied once. A SECOND flag rather than a reuse of
    // the one above: that one is already true on every install that has run
    // the launcher, so a migration hung off it would never run for anybody.
    private bool _quickMeleeMovedToQ;
    public bool QuickMeleeMovedToQ { get => _quickMeleeMovedToQ; set => Set(ref _quickMeleeMovedToQ, value); }

    // --- Audio -------------------------------------------------------
    //
    // The master multiplier and the two slider POSITIONS.
    //
    // The game has no concept of a master volume: autoexec.cfg holds one
    // number per channel, and what we write there is the PRODUCT. So the
    // three values a person actually set cannot be recovered from the game
    // config alone - master 50 with sound at 100 writes exactly what master
    // 100 with sound at 50 writes - and they are kept here instead.
    //
    // autoexec is still read on a fresh profile, with master at 100, so an
    // existing install opens showing the volumes it already had.

    private float _masterVolume = 100f;
    public float MasterVolume { get => _masterVolume; set => Set(ref _masterVolume, value); }

    private float _soundVolumePercent = -1f;   // -1 = never saved; read autoexec
    public float SoundVolumePercent { get => _soundVolumePercent; set => Set(ref _soundVolumePercent, value); }

    private float _musicVolumePercent = -1f;
    public float MusicVolumePercent { get => _musicVolumePercent; set => Set(ref _musicVolumePercent, value); }

    private bool _skipMovies = true;   // modern default: skip 55MB of intro videos
    public bool SkipMovies { get => _skipMovies; set => Set(ref _skipMovies, value); }

    private bool _disableMusic;
    public bool DisableMusic { get => _disableMusic; set => Set(ref _disableMusic, value); }

    private bool _disableSound;
    public bool DisableSound { get => _disableSound; set => Set(ref _disableSound, value); }

    private bool _disableFog;
    public bool DisableFog { get => _disableFog; set => Set(ref _disableFog, value); }

    private bool _tripleBuffering = true;
    public bool TripleBuffering { get => _tripleBuffering; set => Set(ref _tripleBuffering, value); }

    private bool _polyGapFixing;
    public bool PolyGapFixing { get => _polyGapFixing; set => Set(ref _polyGapFixing, value); }


    private bool _optimizedSurfaces;
    public bool OptimizedSurfaces { get => _optimizedSurfaces; set => Set(ref _optimizedSurfaces, value); }

    /// <summary>
    /// Cheat: offer every retail level for direct loading. The campaign
    /// menu grows a Campaign Levels entry, which is otherwise absent -
    /// there is no partial version of this, because the game tracks no
    /// notion of which levels you have reached.
    ///
    /// Read by CLoadLevelMenu::Init, which checks the EnableRetailLevels
    /// console var - the switch has always been in the game, with nothing
    /// in the interface to reach it.
    /// </summary>
    private bool _unlockAllLevels;
    public bool UnlockAllLevels { get => _unlockAllLevels; set => Set(ref _unlockAllLevels, value); }

    /// <summary>
    /// Optional URL to a JSON list of bootstrap server addresses, unioned with
    /// the shipped Defaults\seed-servers.json.
    ///
    /// Exists so the community can publish new entry points without waiting
    /// for a launcher release - and so it can be repointed if whoever hosts it
    /// goes away, which is the whole reason discovery has more than one source.
    /// Blank by default: an unset URL is skipped, not an error.
    ///
    /// Format: [ { "Address": "1.2.3.4", "Port": 27888, "Name": "optional" } ]
    /// </summary>
    private string _seedListUrl = "";
    public string SeedListUrl { get => _seedListUrl; set => Set(ref _seedListUrl, value); }

    private string _extraArgs = "";
    public string ExtraArgs { get => _extraArgs; set => Set(ref _extraArgs, value); }

    /// <summary>
    /// Installed language pack file name (Languages\*.txt), "" = stock
    /// English. Stored here rather than inferred from Custom\Strings\ so a
    /// launcher update can re-copy its corrected pack - see LanguagePacks.
    /// </summary>
    private string _languagePack = "";
    public string LanguagePack { get => _languagePack; set => Set(ref _languagePack, value); }

    private string? _gameDirOverride;
    public string? GameDirOverride { get => _gameDirOverride; set => Set(ref _gameDirOverride, value); }

    // --- Update checking (see UpdateService) ---

    /// <summary>Opt-out for the once-a-day GitHub Releases check.</summary>
    private bool _checkForUpdates = true;
    public bool CheckForUpdates { get => _checkForUpdates; set => Set(ref _checkForUpdates, value); }

    /// <summary>Offer beta tags too. Off: only full releases are reported.</summary>
    private bool _acceptPrereleases;
    public bool AcceptPrereleases { get => _acceptPrereleases; set => Set(ref _acceptPrereleases, value); }

    /// <summary>Throttle. Written even on failure so an outage cannot cause a request per launch.</summary>
    private DateTime? _lastUpdateCheckUtc;
    public DateTime? LastUpdateCheckUtc { get => _lastUpdateCheckUtc; set => Set(ref _lastUpdateCheckUtc, value); }

    public static string PrefsPath =>
        Path.Combine(AppPaths.Root,
                     "launcher.json");

    public static LauncherPrefs Load()
    {
        LauncherPrefs prefs;

        try
        {
            // Auto-save is off while this runs, or deserialising the file
            // would write it back once per property on the way in.
            prefs = File.Exists(PrefsPath)
                ? JsonSerializer.Deserialize<LauncherPrefs>(File.ReadAllText(PrefsPath)) ?? new()
                : new();
        }
        catch (JsonException) { prefs = new(); }

        prefs._autoSave = true;
        return prefs;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefsPath)!);
        File.WriteAllText(PrefsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Compose the Client.exe command line from these prefs.</summary>
    public string BuildArgs(bool multiplayer = false)
    {
        var parts = new List<string>();
        if (multiplayer) parts.Add("-multiplayer");
        if (SkipMovies) parts.Add("+DisableMovies 1");
        if (DisableMusic) parts.Add("+DisableMusic 1");
        if (DisableSound) parts.Add("+DisableSound 1");
        // "DisableFog", not "DisableFox" - and the typo was Monolith's, which
        // is why the wrong one looked authoritative.
        //
        // Monolith's two launchers disagree by a letter: one passes
        // "DisableFox". The SDK source the game is built from reads
        // "DisableFog" and turns it into the renderer's FogEnable 0
        // (RiotStartup.cpp). This launcher faithfully copied the broken
        // half, so the option has never done anything in anybody's hands.
        //
        // Both halves are ours now, so it is spelled the way the thing that
        // READS it is spelled. Ticking the box will start actually disabling
        // fog, which is what anyone who ticked it already believed.
        if (DisableFog) parts.Add("+DisableFog 1");
        if (TripleBuffering) parts.Add("+EnableTripBuf 1");
        if (PolyGapFixing) parts.Add("+EnableTjuncs 1");
        if (OptimizedSurfaces) parts.Add("+EnableOptSurf 1");
        if (UnlockAllLevels) parts.Add("+EnableRetailLevels 1");
        if (!string.IsNullOrWhiteSpace(ExtraArgs)) parts.Add(ExtraArgs.Trim());
        return string.Join(" ", parts);
    }
}

/// <summary>
/// The game's detail presets, extracted from DetailLo/Md/Hi.cfg, plus an
/// "Ultra" tier. Most vars are 0/1/2 enums already maxed by High; Ultra
/// raises the only open-ended one (bullet-hole decal count).
/// </summary>
public static class DetailPresets
{
    // "Custom" = current config doesn't match any preset; never applied.
    public static readonly string[] Names = { "Custom", "Low", "Medium", "High", "Ultra" };

    public static readonly string[] Vars =
    {
        "ModelLOD", "MaxModelShadows", "BulletHoles", "TextureDetail",
        "DynamicLightSetting", "LightMap", "SpecialFX", "EnvMapEnable",
        "ModelFullbrite", "PVWeapons", "PolyGrids", "CloudMapLight",
    };

    private static readonly Dictionary<string, float[]> Values = new()
    {
        //           LOD  Shdw Holes  Tex  DynL LMap SFX  Env  MdFB PVW  PGrd Cloud
        ["Low"]    = new[] { 0f, 0f,   10f, 0f, 0f,  0f,  0f,  0f,  0f,  1f,  0f,  0f },
        ["Medium"] = new[] { 1f, 0f,  150f, 1f, 2f,  1f,  1f,  0f,  0f,  0f,  1f,  0f },
        ["High"]   = new[] { 2f, 1f,  300f, 2f, 2f,  1f,  2f,  1f,  1f,  0f,  1f,  1f },
        ["Ultra"]  = new[] { 2f, 1f, 1000f, 2f, 2f,  1f,  2f,  1f,  1f,  0f,  1f,  1f },
    };

    public static void Apply(ShogoConfigFile cfg, string preset)
    {
        if (!Values.TryGetValue(preset, out var vals)) return;
        for (int i = 0; i < Vars.Length; i++)
            cfg.Set(Vars[i], vals[i]);
    }

    /// <summary>
    /// Safe value ranges per var. Everything except BulletHoles is an enum
    /// the engine indexes with (0/1 toggles, 0-2 levels) - out-of-range
    /// values are unsafe. BulletHoles is a decal count; generous cap.
    /// </summary>
    public static readonly Dictionary<string, (float Min, float Max)> Ranges = new()
    {
        ["ModelLOD"] = (0, 2),
        ["MaxModelShadows"] = (0, 1),
        ["BulletHoles"] = (0, 2000),
        ["TextureDetail"] = (0, 2),
        ["DynamicLightSetting"] = (0, 2),
        ["LightMap"] = (0, 1),
        ["SpecialFX"] = (0, 2),
        ["EnvMapEnable"] = (0, 1),
        ["ModelFullbrite"] = (0, 1),
        ["PVWeapons"] = (0, 1),
        ["PolyGrids"] = (0, 1),
        ["CloudMapLight"] = (0, 1),
    };

    public static float Clamp(string name, float value) =>
        Ranges.TryGetValue(name, out var r) ? Math.Clamp(value, r.Min, r.Max) : value;

    /// <summary>Which preset the config currently matches, or "Custom".</summary>
    public static string Detect(ShogoConfigFile cfg)
    {
        foreach (var (name, vals) in Values)
        {
            bool all = true;
            for (int i = 0; i < Vars.Length && all; i++)
                all = Math.Abs(cfg.GetFloat(Vars[i], float.NaN) - vals[i]) < 0.001f;
            if (all) return name;
        }
        return "Custom";
    }
}
