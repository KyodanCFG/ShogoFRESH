using ShogoLauncher.Services;

namespace ShogoLauncher.ViewModels;

/// <summary>
/// The Settings tab: player identity, input, audio (including the master
/// multiplier), gameplay toggles, resolution and the dgVoodoo display
/// options, detail presets, and the save/restore paths. Split out of
/// MainViewModel.cs by tab; see MainViewModel.Host.cs for the pattern.
/// </summary>
public partial class MainViewModel
{
    // --- Settings tab fields (subset of autoexec.cfg vars; the file
    //     preserves everything it doesn't know about) ---
    private string _playerName = "Sanjuro";
    public string PlayerName
    {
        get => _playerName;
        set
        {
            // Trimmed here rather than only on save, so the field shows what
            // will actually be used. Long names crowd the scoreboard and the
            // kill feed, which are now laid out to fit.
            var trimmed = value?.Length > PilotNameGenerator.MaxNameLength
                ? value.Substring(0, PilotNameGenerator.MaxNameLength)
                : value ?? "";

            Set(ref _playerName, trimmed);
        }
    }

    // Multiplayer identity: the stock 8 tint colors (NPC_* 1-8, NetDefs.h)
    // and the 4 mechs (NMT_* 1-4). A true color picker needs RGB support in
    // ShogoFRESH game code (roadmap phase H); until then these are swatches
    // of the engine's fixed palette.
    public record ColorChoice(string Name, string Swatch)
    {
        public override string ToString() => Name;
    }

    public ColorChoice[] PlayerColors { get; } =
    {
        new("Black", "#FF303030"), new("White", "#FFE8E8E8"), new("Red", "#FFC03030"),
        new("Green", "#FF30A030"), new("Blue", "#FF3060C0"), new("Cyan", "#FF30A0A0"),
        new("Yellow", "#FFC0C030"), new("Purple", "#FF9040C0"),
    };

    private ColorChoice _selectedPlayerColor = null!;
    public ColorChoice SelectedPlayerColor { get => _selectedPlayerColor; set => Set(ref _selectedPlayerColor, value); }

    public string[] Mechs { get; } = { "Ordog", "Enforcer", "Predator", "Akuma" };

    private string _selectedMech = "Ordog";
    public string SelectedMech { get => _selectedMech; set => Set(ref _selectedMech, value); }

    // The on-foot body, beside the mech. Kept in step with
    // Shared/FreshPlayerModels.h, which is the SERVER's list - the client
    // sends a name and the server resolves it, so a name this launcher offers
    // and a server does not know simply comes back as Sanjuro.
    //
    // Short by design. A body has to answer the 64 animation names Sanjuro
    // answers or the player looks broken in the specific ways the 1998 OTAKU
    // mod's readme describes, so a model earns a place here by having been
    // retargeted, not by existing.
    public string[] OnFootModels { get; } = { "Sanjuro" };

    private string _selectedOnFootModel = "Sanjuro";
    public string SelectedOnFootModel
    {
        get => _selectedOnFootModel;
        set => Set(ref _selectedOnFootModel, value);
    }

    private float _mouseSensitivity = 3.0f;
    public float MouseSensitivity { get => _mouseSensitivity; set => Set(ref _mouseSensitivity, value); }

    private bool _mouseInvertY;
    public bool MouseInvertY { get => _mouseInvertY; set => Set(ref _mouseInvertY, value); }

    private int _updateRate = 20;   // stock default is 6 ("modem"); LAN preset is 20
    public int UpdateRate { get => _updateRate; set => Set(ref _updateRate, value); }

    // --- Input / Audio / Gameplay (engine console vars) ---

    // "inputrate" - the in-game menu calls it SMOOTHNESS (IDS_MOUSE_INPUTRATE
    // is literally the string "smoothness"), and what the engine does with it
    // is floor the measured gap between DirectInput events. That gap is the
    // DIVISOR when movement becomes a rate, so at 0 there is no floor and a
    // 1000 Hz mouse hands it ~1 ms intervals to divide by.
    //
    // This launcher used to offer a "Raw mouse input" checkbox that wrote 0 and
    // called it unfiltered. It is not unfiltered - it is unfloored, and it is
    // the roughness the owner reported. That checkbox is gone; the value cannot
    // be taken below MOUSE_SMOOTHNESS_MIN any more.
    //
    // Monolith's own default is 30 (defaults.cfg) and their slider ran 0-40.
    // We ship the bottom of the usable range rather than their middle, because
    // 0-vs-40 is obvious in play and 10-vs-30 is not, so the cheaper setting
    // wins on the evidence available.
    public const float MOUSE_SMOOTHNESS_MIN = 10f;
    public const float MOUSE_SMOOTHNESS_MAX = 40f;

    private float _mouseSmoothness = MOUSE_SMOOTHNESS_MIN;
    public float MouseSmoothness
    {
        get => _mouseSmoothness;
        set => Set(ref _mouseSmoothness, Math.Clamp(value, MOUSE_SMOOTHNESS_MIN, MOUSE_SMOOTHNESS_MAX));
    }

    // "ZoomSensitivity" - how fast the mouse moves while zoomed, as a
    // percentage of normal. 1998 divided by ten for EVERY zoom regardless of
    // how far it magnified, so one constant served the sniper rifle at FOV 10
    // and the assault rifle at FOV 40 alike.
    //
    // Match FOV writes 0, which tells the game to scale by the actual
    // magnification instead. The percentage is remembered while it is on, so
    // unticking returns you to your own number rather than to the default.

    // 100 rather than 10 for the slider's starting point. Match FOV is the
    // default, so reaching the slider at all is a deliberate opt-out - and
    // landing that person on 1998's tenth, the value this whole setting
    // exists because it was wrong, is the least useful place to start. 1:1
    // is the neutral position to drag down from.
    private float _zoomSensitivity = 100f;
    public float ZoomSensitivity { get => _zoomSensitivity; set => Set(ref _zoomSensitivity, value); }

    private bool _zoomMatchFov = true;
    public bool ZoomMatchFov
    {
        get => _zoomMatchFov;
        set { Set(ref _zoomMatchFov, value); OnPropertyChanged(nameof(ShowZoomSlider)); }
    }

    public bool ShowZoomSlider => !ZoomMatchFov;

    private bool _musicEnabled = true;       // "MusicEnable"
    public bool MusicEnabled { get => _musicEnabled; set => Set(ref _musicEnabled, value); }

    // The engine's own ceiling is 90 - RiotSettings.cpp clamps SoundVolume
    // there with the stock 1998 comment "hack to keep sound volume
    // reasonable", presumably guarding against clipping in its mixer. We keep
    // the clamp and present it as a PERCENTAGE instead, so the slider's 100%
    // is the loudest the game will actually go rather than a number that
    // silently does nothing above 90.
    public const float VolumeCeiling = 90f;

    private float _musicVolume = 40f;        // "MusicVolume", engine units 0-90
    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            Set(ref _musicVolume, value);
            OnPropertyChanged(nameof(MusicVolumePercent));
            OnPropertyChanged(nameof(EffectiveMusicText));
        }
    }

    /// <summary>What the slider shows: 0-100, where 100 is the engine's 90.</summary>
    public float MusicVolumePercent
    {
        get => _musicVolume / VolumeCeiling * 100f;
        set => MusicVolume = Math.Clamp(value, 0f, 100f) / 100f * VolumeCeiling;
    }

    private bool _soundEnabled = true;       // "SoundEnable"
    public bool SoundEnabled { get => _soundEnabled; set => Set(ref _soundEnabled, value); }

    private float _soundVolume = 40f;        // "SoundVolume", engine units 0-90
    public float SoundVolume
    {
        get => _soundVolume;
        set
        {
            Set(ref _soundVolume, value);
            OnPropertyChanged(nameof(SoundVolumePercent));
            OnPropertyChanged(nameof(EffectiveSoundText));
        }
    }

    public float SoundVolumePercent
    {
        get => _soundVolume / VolumeCeiling * 100f;
        set => SoundVolume = Math.Clamp(value, 0f, 100f) / 100f * VolumeCeiling;
    }

    /// <summary>
    /// A multiplier over the other two, not a third hand on the same dial.
    ///
    /// <para>
    /// It used to DRAG the sound and music sliders, scaling them to keep their
    /// balance. That works and it is the wrong model: pulling master down and
    /// back up is lossy — 80% scaled to 50% and back is 80% only if nothing
    /// clamped on the way — and the sliders no longer show what the person
    /// chose, they show what master last did to them.
    /// </para>
    /// <para>
    /// So master multiplies instead. Sound and music stay exactly where they
    /// were put; what reaches the game is the product, worked out at save.
    /// Sound 50% and music 80% under a master of 50% write 25% and 40%, and
    /// the sliders still read 50 and 80 — because that is still what the
    /// person asked for.
    /// </para>
    /// <para>
    /// Persisted in the launcher's own prefs rather than a game config,
    /// because the game has no master volume to store it in: autoexec.cfg
    /// holds one number per channel and it is the product. Master 50 with
    /// sound at 100 writes exactly what master 100 with sound at 50 writes,
    /// so the three values a person set cannot be recovered from the game
    /// config alone.
    /// </para>
    /// </summary>
    public float MasterVolume
    {
        get => Prefs.MasterVolume;
        set
        {
            var want = Math.Clamp(value, 0f, 100f);
            if (Math.Abs(want - Prefs.MasterVolume) < 0.01f) return;

            Prefs.MasterVolume = want;      // saves itself; see LauncherPrefs

            OnPropertyChanged(nameof(MasterVolume));
            OnPropertyChanged(nameof(EffectiveSoundText));
            OnPropertyChanged(nameof(EffectiveMusicText));
        }
    }

    /// <summary>
    /// What the game will actually be given, since the sliders no longer show
    /// it. Without this the master slider is invisible in its effect until you
    /// launch the game and listen.
    /// </summary>
    public string EffectiveSoundText => EffectiveText(SoundVolumePercent);
    public string EffectiveMusicText => EffectiveText(MusicVolumePercent);

    private string EffectiveText(float sliderPercent)
    {
        var master = Prefs.MasterVolume;

        if (master >= 99.99f) return string.Empty;      // nothing to explain

        return $"→ {sliderPercent * master / 100f:0}%";
    }

    /// <summary>Slider position times the master, as engine units 0-90. The
    /// one place the multiplication happens.</summary>
    private float EffectiveEngineVolume(float sliderPercent) =>
        Math.Clamp(sliderPercent, 0f, 100f) / 100f
        * Math.Clamp(Prefs.MasterVolume, 0f, 100f) / 100f
        * VolumeCeiling;

    // "Gore" - three-state now. Realistic gives machines sparks and smoke
    // where they used to bleed: mechs, vehicles and the shock troopers,
    // which are armoured shells however human-shaped they are. Full is the
    // 1998 behaviour and stays the default.
    public string[] GoreModes { get; } = { "Off", "Realistic", "Full (default)" };

    private string _gore = "Full (default)";
    public string Gore { get => _gore; set => Set(ref _gore, value); }

    private bool _screenFlash = true;        // "ScreenFlash"
    public bool ScreenFlash { get => _screenFlash; set => Set(ref _screenFlash, value); }

    private bool _vehicleThirdPerson = true; // "VehicleThirdPerson" (ShogoFRESH CShell feature)
    public bool VehicleThirdPerson { get => _vehicleThirdPerson; set => Set(ref _vehicleThirdPerson, value); }

    // "HudAspect" - hold the edge-anchored HUD inside a centred band of
    // this aspect ratio. 0 = use the whole screen.
    public string[] HudAspects { get; } =
    {
        "Full screen (default)", "4:3", "16:9", "21:9",
    };

    private static readonly float[] HudAspectValues = { 0f, 4f / 3f, 16f / 9f, 21f / 9f };

    private string _hudAspect = "Full screen (default)";
    public string HudAspect { get => _hudAspect; set => Set(ref _hudAspect, value); }

    // "KillFeedStyle" - what the multiplayer kill feed shows between the
    // killer's and the victim's name. Index = the console value.
    //
    // The weapon icon is the default: the feed is asked "what killed them",
    // and the ammo icon answers what the weapon was loaded with instead -
    // several weapons share an ammo type, so it cannot always tell them
    // apart. Both icon sets have been in SHOGO.REZ since 1998.
    public string[] KillFeedStyles { get; } =
    {
        "Ammo icon", "Weapon icon", "Text (default)",
    };

    private string _killFeedStyle = "Text (default)";
    public string KillFeedStyle { get => _killFeedStyle; set => Set(ref _killFeedStyle, value); }

    // "ClassicCampaign" - restore the 1998 single-player tuning in one move.
    // The doctrine (what reverts, what stays) lives beside the ruleset gates
    // in Shared/WeaponDefs.h: Classic restores the 1998 tuning, never the
    // 1998 defects. Presentation is untouched either way.
    private bool _classicCampaign;
    public bool ClassicCampaign { get => _classicCampaign; set => Set(ref _classicCampaign, value); }

    // "ProfanityFilter" - star out profanity in chat and names, display-side
    // only. Default ON, and the game code treats a MISSING var as on too, so
    // an install this launcher never touched is still filtered.
    private bool _profanityFilter = true;
    public bool ProfanityFilter { get => _profanityFilter; set => Set(ref _profanityFilter, value); }

    // "ModelAdd" / "ModelDirAdd" - engine variables adding light to every
    // model (shogo-re/notes/08-rendering.md): ModelAdd raises the ambient
    // term (every surface equally - reads as fullbright), ModelDirAdd the
    // directional term (surfaces facing the level's light - shading kept).
    // The launcher writes one grey value as "n n n".
    //
    // SINGLE PLAYER ONLY BY CONSTRUCTION: the game zeroes both for the
    // duration of any multiplayer world and restores them after (see the
    // FreshModelAdd clamp in RiotClientShell.cpp) - in multiplayer this is
    // seeing players who are standing in the dark, and the server cannot
    // even observe it, let alone stop a stock client doing it.
    private int _modelAdd;
    public int ModelAdd { get => _modelAdd; set => Set(ref _modelAdd, value); }

    private int _modelDirAdd;
    public int ModelDirAdd { get => _modelDirAdd; set => Set(ref _modelDirAdd, value); }

    // "Saturate" - the renderer's extra colour saturation (a d3d.ren
    // variable, default off; documented in Docs/RENDERVARS.md, one reader).
    // Lightmapped world surfaces only, purely cosmetic, client-side.
    // Surfaced because Monolith's own v2.2 readme recommends it for levels
    // that read too dark - it beats pushing the whole card's gamma around.
    private bool _saturate;
    public bool Saturate { get => _saturate; set => Set(ref _saturate, value); }

    // "StreamerMode" - anonymise a session for broadcast: chat hidden and
    // silenced, other players given stable generated aliases, our own name
    // randomised per connect. Default OFF - it is a deliberate act, not a
    // setting anybody wants applied by surprise. The name typed into Player
    // is never overwritten, so switching the mode off restores it for free.
    private bool _streamerMode;
    public bool StreamerMode { get => _streamerMode; set => Set(ref _streamerMode, value); }

    // "HudNumberSize" - design-space pixel height of the HUD numbers before
    // the HUD scale is applied. 24 matches the stock digit art closely.
    // 18, matching the game's own fallback in CPlayerStats::EnsureHudFont and
    // the shipped client-settings.cfg. It was 24 here and 18 in both of those,
    // so on an install whose autoexec.cfg lacks the key the launcher showed 24
    // and SAVING wrote 24 - silently overriding a deliberately chosen default.
    // The game's comment explains the choice: 18 keeps the ammo readout clear
    // of the plate art now the figures are drawn as text rather than blitted.
    private float _hudNumberSize = 18f;
    public float HudNumberSize { get => _hudNumberSize; set => Set(ref _hudNumberSize, value); }

    // "AutoSwitch" - a client preference; CShell sends it to the server on
    // world entry so it applies wherever you play. Index = the value.
    public string[] AutoSwitchModes { get; } = { "Never", "If new", "If better (default)", "Always" };

    private bool _chatSound = true;      // "ChatSound"
    public bool ChatSound { get => _chatSound; set => Set(ref _chatSound, value); }

    private string _autoSwitch = "If better (default)";
    public string AutoSwitch { get => _autoSwitch; set => Set(ref _autoSwitch, value); }

    // Frame rate limiter (engine's own multiplayer FPS cap vars).
    private bool _limitFps = true;
    public bool LimitFps { get => _limitFps; set => Set(ref _limitFps, value); }

    private int _fpsLimit = 120;
    public int FpsLimit { get => _fpsLimit; set => Set(ref _fpsLimit, value); }

    private int _screenWidth = 1920;
    public int ScreenWidth { get => _screenWidth; set => Set(ref _screenWidth, value); }

    private int _screenHeight = 1080;
    public int ScreenHeight { get => _screenHeight; set => Set(ref _screenHeight, value); }

    // Resolution preset list: native display resolution first, then common modes.
    public string[] ResolutionPresets { get; } = BuildResolutionPresets();

    private static string[] BuildResolutionPresets()
    {
        var (nw, nh) = NativeDisplay.Primary();
        var native = $"{nw} x {nh}  (native)";
        var common = new[]
        {
            "3840 x 2160", "3440 x 1440", "2560 x 1440", "1920 x 1080",
            "1680 x 1050", "1600 x 900", "1366 x 768", "1280 x 1024",
            "1280 x 720", "1024 x 768", "800 x 600", "640 x 480",
        };
        return new[] { native }.Concat(common).ToArray();
    }

    private string? _selectedResolutionPreset;
    public string? SelectedResolutionPreset
    {
        get => _selectedResolutionPreset;
        set
        {
            Set(ref _selectedResolutionPreset, value);
            if (value is null) return;
            var m = System.Text.RegularExpressions.Regex.Match(value, @"(\d+)\s*x\s*(\d+)");
            if (m.Success)
            {
                ScreenWidth = int.Parse(m.Groups[1].Value);
                ScreenHeight = int.Parse(m.Groups[2].Value);
            }
        }
    }

    /// <summary>Point the dropdown at whichever preset matches the current W x H.</summary>
    private void SelectPresetForCurrentResolution()
    {
        var match = ResolutionPresets.FirstOrDefault(p =>
        {
            var m = System.Text.RegularExpressions.Regex.Match(p, @"(\d+)\s*x\s*(\d+)");
            return m.Success && int.Parse(m.Groups[1].Value) == ScreenWidth
                             && int.Parse(m.Groups[2].Value) == ScreenHeight;
        });
        Set(ref _selectedResolutionPreset, match, nameof(SelectedResolutionPreset));
    }

    // Detail preset ("Keep current" = leave the 12 detail vars untouched).
    public string[] DetailPresetNames => DetailPresets.Names;

    private string _selectedDetailPreset = "Custom";
    public string SelectedDetailPreset { get => _selectedDetailPreset; set => Set(ref _selectedDetailPreset, value); }

    // --- Detail preset advanced editing ---

    public record DetailVar(string Name, string Value);

    public List<DetailVar> GetDetailValues()
    {
        // Reflect the pending preset selection, not just what's on disk -
        // otherwise the Advanced modal shows stale values until Save.
        if (SelectedDetailPreset != "Custom")
        {
            var staged = new ShogoConfigFile(System.IO.Path.Combine(GameDir!, "autoexec.cfg"));
            DetailPresets.Apply(staged, SelectedDetailPreset); // in-memory only; not saved
            return DetailPresets.Vars
                .Select(v => new DetailVar(v, staged.GetFloat(v, 0f).ToString("0.###")))
                .ToList();
        }

        var cfg = new ShogoConfigFile(System.IO.Path.Combine(GameDir!, "autoexec.cfg"));
        return DetailPresets.Vars
            .Select(v => new DetailVar(v, cfg.GetFloat(v, 0f).ToString("0.###")))
            .ToList();
    }

    public void SaveDetailValues(IEnumerable<DetailVar> values)
    {
        var cfg = new ShogoConfigFile(System.IO.Path.Combine(GameDir!, "autoexec.cfg"));
        foreach (var v in values)
            if (float.TryParse(v.Value, out var f)) cfg.Set(v.Name, DetailPresets.Clamp(v.Name, f));
        cfg.Save();
        SelectedDetailPreset = DetailPresets.Detect(cfg);
        Status = "Detail settings saved.";
    }

    // --- Display output via dgVoodoo (present when ShogoFix is installed) ---

    private bool _dgVoodooPresent;
    public bool DgVoodooPresent { get => _dgVoodooPresent; private set => Set(ref _dgVoodooPresent, value); }

    public string[] DisplayModes { get; } = { "Fullscreen", "Windowed", "Borderless fullscreen" };

    private string _selectedDisplayMode = "Fullscreen";
    public string SelectedDisplayMode { get => _selectedDisplayMode; set => Set(ref _selectedDisplayMode, value); }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    private const int SM_CMONITORS = 80;

    // "default" plus one 1-based ordinal per monitor actually attached.
    // (Picking an ordinal beyond the real count isn't dangerous - dgVoodoo
    // falls back to the default output - but there's no reason to offer it.)
    // Rendering quality (dgVoodoo [DirectX] section). Display label -> conf value.
    public static readonly (string Label, string Value)[] FilteringChoices =
    {
        ("App-driven", "appdriven"), ("Bilinear", "bilinear"), ("Trilinear", "trilinear"),
        ("AF 2x", "2"), ("AF 4x", "4"), ("AF 8x", "8"), ("AF 16x", "16"),
    };
    public string[] FilteringLabels { get; } = FilteringChoices.Select(c => c.Label).ToArray();

    private string _selectedFiltering = "AF 16x";
    public string SelectedFiltering { get => _selectedFiltering; set => Set(ref _selectedFiltering, value); }

    public static readonly (string Label, string Value)[] AntialiasChoices =
    {
        ("Off (app-driven)", "appdriven"), ("MSAA 2x", "2x"), ("MSAA 4x", "4x"), ("MSAA 8x", "8x"),
    };
    public string[] AntialiasLabels { get; } = AntialiasChoices.Select(c => c.Label).ToArray();

    private string _selectedAntialias = "MSAA 4x";
    public string SelectedAntialias { get => _selectedAntialias; set => Set(ref _selectedAntialias, value); }

    private bool _pure32Bit = true;
    public bool Pure32Bit { get => _pure32Bit; set => Set(ref _pure32Bit, value); }

    private bool _captureMouse = true;
    public bool CaptureMouse { get => _captureMouse; set => Set(ref _captureMouse, value); }

    private bool _verticalSync;
    /// <summary>dgVoodoo [DirectX] ForceVerticalSync - stops tearing, which a
    /// 1998 engine at several hundred frames a second will otherwise show.</summary>
    public bool VerticalSync { get => _verticalSync; set => Set(ref _verticalSync, value); }

    // Displayed "Default", stored "default" - the value goes straight into
    // dgVoodoo.conf, which expects the lower-case form, so only the label is
    // capitalised. MonitorFromLabel/MonitorToLabel are the only crossing
    // points; everything else keeps working in stored values.
    public const string MonitorDefaultLabel = "Default";
    public const string MonitorDefaultValue = "default";

    public string[] MonitorChoices { get; } =
        new[] { MonitorDefaultLabel }
        .Concat(Enumerable.Range(1, Math.Max(1, GetSystemMetrics(SM_CMONITORS))).Select(i => i.ToString()))
        .ToArray();

    private static string MonitorToLabel(string stored) =>
        string.Equals(stored, MonitorDefaultValue, StringComparison.OrdinalIgnoreCase)
            ? MonitorDefaultLabel : stored;

    private static string MonitorFromLabel(string label) =>
        string.Equals(label, MonitorDefaultLabel, StringComparison.OrdinalIgnoreCase)
            ? MonitorDefaultValue : label;

    private string _selectedMonitor = MonitorDefaultLabel;
    public string SelectedMonitor { get => _selectedMonitor; set => Set(ref _selectedMonitor, value); }

    public void SaveSettings()
    {
        if (!GameFound) { Status = "Cannot save - game directory not set."; return; }

        var autoexec = new ShogoConfigFile(System.IO.Path.Combine(GameDir!, "autoexec.cfg"));
        autoexec.Set("NetPlayerName", PlayerName);
        autoexec.Set("NetPlayerColor", Math.Max(0, Array.IndexOf(PlayerColors, SelectedPlayerColor)) + 1);
        autoexec.Set("NetMech", Array.IndexOf(Mechs, SelectedMech) + 1);
        // BY NAME, not by index: the server owns the list, and an index would
        // silently mean a different body the moment the two lists differ.
        autoexec.Set("OnFootModel", SelectedOnFootModel);
        autoexec.Set("MouseSensitivity", MouseSensitivity);
        autoexec.Set("MouseInvertYAxis", MouseInvertY ? 1 : 0);
        autoexec.Set("ZoomSensitivity", ZoomMatchFov ? 0f : ZoomSensitivity);
        // Net update rate is managed, not user-exposed. THIS IS THE CLIENT'S
        // OWN SEND RATE, settled by reverse engineering 2026-08-18 and written
        // up in Docs/NETRATES.md: Client.exe reads it, clamps it to [2,60] and
        // writes it into the outgoing packet. The server's variable of the same
        // name has no readers at all.
        //
        // 30 because that is the server's tick, which is a CONSTANT and not
        // 1.0/UpdateRate as engine fact 14 used to say - so a client sending
        // faster than 30 is sending into a server that cannot look more often
        // than 30 times a second. That is the ceiling this comment used to
        // defer to runtime calibration, and it turned out to be readable.
        //
        // Was 20, the 1998 "LAN" preset; stock is 6, for 56k modems.
        autoexec.Set("UpdateRate", 30);
        autoexec.Set("UpdateRateInitted", 1);

        // Client->server send rate. Stock default is 7/sec (modem era), and
        // in multiplayer the client only transmits its rotation at this
        // rate - which is what makes MP mouse look feel laggy next to
        // single player (SP sends every frame). ~20 bytes/update, so 30/sec
        // is under 1 KB/s.
        autoexec.Set("CSendRate", 30f);
        autoexec.Set("screenwidth", ScreenWidth);
        autoexec.Set("screenheight", ScreenHeight);
        autoexec.Set("screendepth", 16);   // 16-bit color: the engine's own mode list is 16-bit

        autoexec.Set("inputrate", Math.Clamp(MouseSmoothness, MOUSE_SMOOTHNESS_MIN, MOUSE_SMOOTHNESS_MAX));
        autoexec.Set("MusicEnable", MusicEnabled ? 1 : 0);
        // The engine tops out at 90; clamping keeps a hand-edited config
        // from putting the slider somewhere it cannot represent.
        // The product, worked out here and nowhere else. The sliders keep
        // showing what the person chose; the game gets what master makes of
        // it. See MasterVolume.

        Prefs.MusicVolumePercent = MusicVolumePercent;
        Prefs.SoundVolumePercent = SoundVolumePercent;

        autoexec.Set("MusicVolume", (int)Math.Clamp(EffectiveEngineVolume(MusicVolumePercent), 0f, 90f));
        autoexec.Set("SoundEnable", SoundEnabled ? 1 : 0);
        autoexec.Set("SoundVolume", EffectiveEngineVolume(SoundVolumePercent));
        // "Capture mouse" has to be written on BOTH sides. dgVoodoo's
        // CaptureMouse only governs dgVoodoo's own clipping; Shogo's engine
        // calls ClipCursor for itself whenever it thinks it is active, so
        // unticking the box used to change a file and release nothing.
        // FreeMouse is the game-side half - see ClientShellDLL/FreshFocus.cpp.
        autoexec.Set("FreeMouse", CaptureMouse ? 0 : 1);

        autoexec.Set("Gore", Math.Max(0, Array.IndexOf(GoreModes, Gore)));
        autoexec.Set("ScreenFlash", ScreenFlash ? 1 : 0);
        autoexec.Set("VehicleThirdPerson", VehicleThirdPerson ? 1 : 0);
        autoexec.Set("AutoSwitch", Math.Max(0, Array.IndexOf(AutoSwitchModes, AutoSwitch)));
        autoexec.Set("HudAspect", HudAspectValues[Math.Max(0, Array.IndexOf(HudAspects, HudAspect))]);
        autoexec.Set("KillFeedStyle", Math.Max(0, Array.IndexOf(KillFeedStyles, KillFeedStyle)));
        autoexec.Set("ClassicCampaign", ClassicCampaign ? 1 : 0);
        autoexec.Set("ProfanityFilter", ProfanityFilter ? 1 : 0);
        autoexec.Set("Saturate", Saturate ? 1 : 0);

        ModelAdd    = Math.Clamp(ModelAdd, 0, 255);
        ModelDirAdd = Math.Clamp(ModelDirAdd, 0, 255);
        autoexec.Set("ModelAdd", $"{ModelAdd} {ModelAdd} {ModelAdd}");
        autoexec.Set("ModelDirAdd", $"{ModelDirAdd} {ModelDirAdd} {ModelDirAdd}");
        autoexec.Set("StreamerMode", StreamerMode ? 1 : 0);
        autoexec.Set("HudNumberSize", Math.Clamp(HudNumberSize, 10f, 60f));
        autoexec.Set("ChatSound", ChatSound ? 1 : 0);
        // MaxFPS is the engine's frame limiter - entry 60 of its console
        // table, with six references inside its own limiter, so it is live.
        //
        // This control used to write NetLimitFps / NetFpsLimit instead. Those
        // are real variables and the game reads them, but ONLY to fill in the
        // 1998 multiplayer host dialog (NetStart.cpp s_bLimitFps / s_nFpsLimit,
        // used at two places, both dialog controls). Nothing in the engine caps
        // a frame from them, and MaxFPS appeared nowhere in this project at
        // all - so the launcher's frame limiter never limited anything.
        //
        // Both are still written: the net ones so the in-game dialog keeps
        // agreeing with the launcher, MaxFPS so the number does something.
        // Unlimited is written as 0, which is what the engine's own default is.
        autoexec.Set("NetLimitFps", LimitFps ? 1 : 0);
        autoexec.Set("NetFpsLimit", Math.Clamp(FpsLimit, 30, 1000));
        autoexec.Set("MaxFPS", LimitFps ? Math.Clamp(FpsLimit, 30, 1000) : 0);

        if (SelectedDetailPreset != "Custom")
            DetailPresets.Apply(autoexec, SelectedDetailPreset);

        autoexec.Save();
        Prefs.Save();

        if (DgVoodooPresent)
        {
            var dgv = new DgVoodooConfig(GameDir!);
            // Keep modern modes enumerated - without this the engine can
            // only pick from dgVoodoo's classic list and drops our choice.
            dgv.EnableModernResolutions();
            dgv.Mode = SelectedDisplayMode switch
            {
                "Windowed" => DgVoodooConfig.DisplayMode.Windowed,
                "Borderless fullscreen" => DgVoodooConfig.DisplayMode.BorderlessFullscreen,
                _ => DgVoodooConfig.DisplayMode.Fullscreen,
            };
            dgv.Monitor = MonitorFromLabel(SelectedMonitor);
            dgv.Pure32Bit = Pure32Bit;
            dgv.VerticalSync = VerticalSync;
            dgv.CaptureMouse = CaptureMouse;
            dgv.Filtering = FilteringChoices.FirstOrDefault(c => c.Label == SelectedFiltering).Value ?? "appdriven";
            dgv.Antialiasing = AntialiasChoices.FirstOrDefault(c => c.Label == SelectedAntialias).Value ?? "appdriven";
            dgv.Save();
        }
        SettingsDirty = false;
        Status = SelectedDetailPreset == "Custom"
            ? "Settings saved to autoexec.cfg (detail vars left as-is)."
            : $"Settings saved ({SelectedDetailPreset} detail preset applied).";
    }

    /// <summary>Reset the Settings tab to ShogoFRESH's shipped recommended values (in memory; Save writes).</summary>
    public void RestoreSettingsDefaults()
    {
        var shipped = System.IO.Path.Combine(AppContext.BaseDirectory, "Defaults", "client-settings.cfg");
        if (!System.IO.File.Exists(shipped)) { Warn("Shipped defaults not found in Defaults\\."); return; }

        var cfg = new ShogoConfigFile(shipped);

        MouseSensitivity = cfg.GetFloat("MouseSensitivity", MouseSensitivity);
        MouseInvertY     = cfg.GetFloat("MouseInvertYAxis", 0f) != 0f;
        MouseSmoothness  = cfg.GetFloat("inputrate", MOUSE_SMOOTHNESS_MIN);

        var fZoom        = cfg.GetFloat("ZoomSensitivity", 0f);
        ZoomMatchFov     = fZoom == 0f;
        if (!ZoomMatchFov) ZoomSensitivity = fZoom;
        SoundEnabled     = cfg.GetFloat("SoundEnable", 1f) != 0f;
        SoundVolume      = cfg.GetFloat("SoundVolume", 40f);
        MusicEnabled     = cfg.GetFloat("MusicEnable", 1f) != 0f;
        MusicVolume      = cfg.GetFloat("MusicVolume", 40f);
        Gore             = IndexToChoice(GoreModes, cfg.GetFloat("Gore", 2f), 2);
        ScreenFlash      = cfg.GetFloat("ScreenFlash", 1f) != 0f;
        VehicleThirdPerson = cfg.GetFloat("VehicleThirdPerson", 1f) != 0f;
        AutoSwitch = AutoSwitchModes[Math.Clamp((int)cfg.GetFloat("AutoSwitch", 2f), 0, 3)];
        HudAspect = NearestHudAspect(cfg.GetFloat("HudAspect", 0f));
        KillFeedStyle = IndexToChoice(KillFeedStyles, cfg.GetFloat("KillFeedStyle", 2f), 2);
        ClassicCampaign = cfg.GetFloat("ClassicCampaign", 0f) != 0f;
        ProfanityFilter = cfg.GetFloat("ProfanityFilter", 1f) != 0f;
        StreamerMode = cfg.GetFloat("StreamerMode", 0f) != 0f;
        HudNumberSize = cfg.GetFloat("HudNumberSize", 18f);
        ChatSound     = cfg.GetFloat("ChatSound", 1f) != 0f;
        UpdateRate       = cfg.GetInt("UpdateRate", 30);
        int nSeedMaxFps  = cfg.GetInt("MaxFPS", -1);
        LimitFps         = nSeedMaxFps > 0 || (nSeedMaxFps < 0 && cfg.GetInt("NetLimitFps", 1) != 0);
        FpsLimit         = nSeedMaxFps > 0 ? nSeedMaxFps : cfg.GetInt("NetFpsLimit", 120);
        SelectedDetailPreset = DetailPresets.Detect(cfg);

        // Display: native resolution and the ShogoFRESH display defaults.
        (ScreenWidth, ScreenHeight) = NativeDisplay.Primary();
        SelectPresetForCurrentResolution();
        SelectedDisplayMode = "Borderless fullscreen";
        SelectedMonitor = MonitorDefaultLabel;
        SelectedFiltering = "AF 16x";
        SelectedAntialias = "MSAA 4x";
        Pure32Bit = true;
        CaptureMouse = true;

        SettingsDirty = true;
        Status = "Recommended defaults loaded - click Save Settings to apply.";
    }

    /// <summary>
    /// Map a stored 0-based index back to its label, falling back to a
    /// default when the file holds something out of range - a hand-edited
    /// or future value must not leave the dropdown blank.
    /// </summary>
    private static string IndexToChoice(string[] choices, float stored, int fallback)
    {
        int i = (int)stored;
        if (i < 0 || i >= choices.Length) i = fallback;
        return choices[i];
    }

    /// <summary>Map a stored HudAspect ratio back to its label (hand-edited values snap to the closest).</summary>
    private string NearestHudAspect(float value)
    {
        if (value <= 0f) return HudAspects[0];

        int best = 0;
        for (int i = 1; i < HudAspectValues.Length; i++)
        {
            if (Math.Abs(HudAspectValues[i] - value) < Math.Abs(HudAspectValues[best] - value)) best = i;
        }
        return HudAspects[best];
    }
}
