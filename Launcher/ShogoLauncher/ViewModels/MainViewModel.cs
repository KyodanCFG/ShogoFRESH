using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShogoLauncher.Services;

namespace ShogoLauncher.ViewModels;

/// <summary>
/// The core: game location, load-from-disk orchestration, dirty tracking,
/// launcher prefs, the update banner, and the status line. Everything
/// tab-shaped lives in a partial named for its tab (Servers, Mods, Setup,
/// Bindings, Settings, Host), so a change to one tab cannot collide with a
/// change to another.
/// </summary>
public partial class MainViewModel : INotifyPropertyChanged
{
    private string? _gameDir;
    public string? GameDir
    {
        get => _gameDir;
        set { Set(ref _gameDir, value); OnPropertyChanged(nameof(GameFound)); }
    }
    public bool GameFound => GameLocator.IsValidGameDir(GameDir);

    // --- Unsaved-changes tracking (Settings tab) ---

    private bool _settingsDirty;
    public bool SettingsDirty { get => _settingsDirty; private set => Set(ref _settingsDirty, value); }

    private bool _bindingsDirty;
    public bool BindingsDirty { get => _bindingsDirty; set => Set(ref _bindingsDirty, value); }

    private bool _hostDirty;
    public bool HostDirty { get => _hostDirty; private set => Set(ref _hostDirty, value); }

    private static readonly HashSet<string> HostProperties = new()
    {
        nameof(HostName), nameof(HostPort), nameof(HostMaxPlayers), nameof(HostBotFill), nameof(HostListPublicly),
        nameof(HostWebRegUrl), nameof(HostPeers),
        nameof(HostTractorBeam), nameof(HostRamming), nameof(HostQuickTurn),
        nameof(HostFallDamage), nameof(HostFallThreshold),
        nameof(HostRunSpeed), nameof(HostMissileSpeed), nameof(HostRespawnScale), nameof(HostHealScale),
        nameof(HostTimeSpeed), nameof(HostNightColor),
        // HostGravityOn and HostIntermissionOn are NOT listed, and must not be:
        // both are derived checkboxes whose setter assigns the stored property,
        // so Set()'s CallerMemberName arrives as HostGravity / HostIntermission
        // and it is those two names that have to be here. An entry for the
        // derived name can never match, which is what makes it worth a comment
        // rather than a line - it looks like tracking and is not.
        nameof(HostFragLimitOn), nameof(HostFragLimit), nameof(HostTimeLimitOn), nameof(HostTimeLimit), nameof(HostIntermission), nameof(HostGravity),
        nameof(HostRandomPickups), nameof(HostBlockedWeapons), nameof(HostBlockedItems),
        nameof(HostInfiniteAmmo), nameof(HostCriticalHits), nameof(HostRuleset), nameof(HostGameMode), nameof(HostFirstPersonOnly),
        nameof(HostMapOrder), nameof(HostRconPassword), nameof(HostRequireFresh),
        nameof(HostIsListen), nameof(HostBodyLifetime),
    };

    /// <summary>Set while loading from disk so populating fields isn't "a change".</summary>
    private bool _suppressDirty;

    private static readonly HashSet<string> SettingsProperties = new()
    {
        nameof(PlayerName), nameof(SelectedPlayerColor), nameof(SelectedMech), nameof(SelectedOnFootModel),
        nameof(MouseSensitivity), nameof(MouseInvertY), nameof(MouseSmoothness),
        nameof(ZoomSensitivity), nameof(ZoomMatchFov),
        nameof(SoundEnabled), nameof(SoundVolume), nameof(MusicEnabled), nameof(MusicVolume),
        nameof(Gore), nameof(ScreenFlash), nameof(VehicleThirdPerson),
        nameof(ScreenWidth), nameof(ScreenHeight), nameof(SelectedResolutionPreset), nameof(SelectedDetailPreset),
        nameof(SelectedDisplayMode), nameof(SelectedMonitor), nameof(SelectedFiltering),
        nameof(SelectedAntialias), nameof(Pure32Bit), nameof(CaptureMouse), nameof(VerticalSync),
        nameof(LimitFps), nameof(FpsLimit), nameof(AutoSwitch), nameof(HudAspect), nameof(HudNumberSize), nameof(ChatSound), nameof(KillFeedStyle), nameof(ClassicCampaign), nameof(ProfanityFilter), nameof(StreamerMode), nameof(Saturate), nameof(ModelAdd), nameof(ModelDirAdd),
    };

    // --- Update banner (see UpdateService) ---

    private UpdateService.UpdateInfo? _availableUpdate;
    public UpdateService.UpdateInfo? AvailableUpdate
    {
        get => _availableUpdate;
        private set
        {
            _availableUpdate = value;
            OnPropertyChanged(nameof(AvailableUpdate));
            OnPropertyChanged(nameof(UpdateAvailable));
            OnPropertyChanged(nameof(UpdateBannerText));
        }
    }

    public bool UpdateAvailable => AvailableUpdate is not null;

    // These two used to carry their own persistence, in two different ways,
    // because Prefs.Save() was only called from the Settings tab. LauncherPrefs
    // now saves on change itself, so both are plain pass-throughs and a new
    // pref cannot forget to persist.

    /// <summary>
    /// Whether ShogoFRESH.rez loads after the Custom\ mods and so wins any
    /// file both contain.
    /// </summary>
    public bool FreshTakesPriority
    {
        get => Prefs.FreshTakesPriority;
        set
        {
            if (Prefs.FreshTakesPriority == value) return;

            Prefs.FreshTakesPriority = value;
            OnPropertyChanged(nameof(FreshTakesPriority));
        }
    }

    /// <summary>
    /// Opt-out, stored in launcher.json rather than the game config.
    ///
    /// Reads and writes the SHARED Prefs instance. It used to load a separate
    /// one, change that, and save it - which left the shared instance holding
    /// the old value, so the next Prefs.Save() from the Settings tab wrote the
    /// setting straight back over the top.
    /// </summary>
    public bool CheckForUpdates
    {
        get => Prefs.CheckForUpdates;
        set
        {
            if (Prefs.CheckForUpdates == value) return;

            Prefs.CheckForUpdates = value;
            OnPropertyChanged(nameof(CheckForUpdates));
        }
    }

    // --- Language pack (see LanguagePacks) ---
    //
    // Applies on selection rather than on Save, like FreshTakesPriority: a
    // language is a fact about the install, not a pending edit, and the
    // game reads it on the next world load with no restart.

    public List<LanguagePacks.Choice> LanguageChoices { get; } = LanguagePacks.Available();

    public LanguagePacks.Choice SelectedLanguage
    {
        get => LanguageChoices.FirstOrDefault(c =>
                   string.Equals(c.FileName, string.IsNullOrEmpty(Prefs.LanguagePack) ? null : Prefs.LanguagePack,
                                 StringComparison.OrdinalIgnoreCase))
               ?? LanguageChoices[0];
        set
        {
            if (value is null || Equals(SelectedLanguage, value)) return;

            Prefs.LanguagePack = value.FileName ?? "";
            OnPropertyChanged(nameof(SelectedLanguage));
            if (GameFound)
            {
                try
                {
                    LanguagePacks.Apply(GameDir!, value.FileName);
                    Status = value.FileName is null
                        ? "Language pack removed - the game is back on its built-in English."
                        : $"Language set to {value.Display}. Applies from the next world load - no restart needed.";
                }
                catch (Exception ex)
                {
                    Status = $"Could not install the language pack: {ex.Message}";
                }
            }
        }
    }

    public string UpdateBannerText => AvailableUpdate is null
        ? ""
        : $"ShogoFRESH {AvailableUpdate.DisplayVersion} is available "
        + $"(you have v{UpdateService.CurrentVersion.Major}.{UpdateService.CurrentVersion.Minor}.{UpdateService.CurrentVersion.Build}).";

    /// <summary>
    /// Ask GitHub whether there is a newer release. Safe to call and ignore:
    /// it never throws and resolves to "no update" on every failure, so a
    /// launcher with no internet behaves exactly as it always did.
    /// </summary>
    public async Task CheckForUpdatesAsync(bool force = false)
    {
        // Pass our instance so the throttle stamp it writes is the one we hold.
        var info = await UpdateService.CheckAsync(force, Prefs);

        if (info is not null)
        {
            AvailableUpdate = info;
            Status = UpdateBannerText;
        }
        else if (force)
        {
            Status = "No update available - this is the latest release.";
        }
    }

    public void DismissUpdate() => AvailableUpdate = null;

    public void OpenUpdatePage()
    {
        var url = AvailableUpdate?.PageUrl;
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) { Warn($"Could not open the release page: {ex.Message}"); }
    }

    private string _status = "Ready.";
    public string Status { get => _status; set { Set(ref _status, value); StatusIsError = false; } }

    private bool _statusIsError;
    public bool StatusIsError { get => _statusIsError; private set => Set(ref _statusIsError, value); }

    /// <summary>Status message rendered in the warning color.</summary>
    public void Warn(string message)
    {
        Set(ref _status, message, nameof(Status));
        StatusIsError = true;
    }

    private bool _refreshing;
    public bool Refreshing { get => _refreshing; set => Set(ref _refreshing, value); }

    // Advanced launch flags (persisted launcher-side, passed on the command line).
    public LauncherPrefs Prefs { get; } = LauncherPrefs.Load();

    public MainViewModel()
    {
        // The rotation label carries a live count, so it has to be told when
        // the collection behind it changes - adding a map raises no property
        // change of its own.
        MapRotation.CollectionChanged += (_, _) => OnPropertyChanged(nameof(RotationLabel));

        GameDir = GameLocator.Locate();
        if (GameDir is null)
            Status = "Shogo installation not found - set the game directory.";
        else
            LoadFromGameDir();
    }

    public void LoadFromGameDir()
    {
        if (!GameFound) return;
        _suppressDirty = true;
        try { LoadFromGameDirCore(); }
        finally { _suppressDirty = false; SettingsDirty = false; }
    }

    private void LoadFromGameDirCore()
    {
        // Re-assert the chosen language pack, so a launcher update's
        // corrected pack reaches the game folder. Does nothing when no
        // language was ever chosen.
        LanguagePacks.Refresh(GameDir!, Prefs.LanguagePack);

        // The engine only knows the actions defaults.cfg registers, so any
        // command ShogoFRESH adds has to be declared there before a key
        // binding can reach it.
        int nActions = EngineActions.EnsureRegistered(GameDir!);
        int nBinds   = EngineActions.EnsureDefaultBindings(GameDir!);

        // Say so. This ran silently before, and when it quietly did nothing
        // the symptom was a key that simply did not work in game with no
        // indication anywhere that a step had been skipped.
        if (nActions > 0 || nBinds > 0)
        {
            Status = $"Registered {nActions} new control(s) and bound {nBinds} default key(s).";
        }

        var autoexec = new ShogoConfigFile(System.IO.Path.Combine(GameDir!, "autoexec.cfg"));

        // Replace the stock "Sanjuro" default with a generated pilot name so
        // servers aren't full of identical Sanjuros. A name the user chose
        // is kept.
        //
        // The generated name is written back immediately rather than waiting
        // for Save: the field looks correct the moment the launcher opens, so
        // nobody thinks to press Save, and the game would then read a
        // NetPlayerName that isn't there and fall back to "Sanjuro".
        var currentName = autoexec.Get("NetPlayerName");
        bool bGenerated = string.IsNullOrWhiteSpace(currentName) ||
                          currentName.Equals("Sanjuro", StringComparison.OrdinalIgnoreCase);

        PlayerName = bGenerated ? PilotNameGenerator.Generate() : currentName!;

        if (bGenerated)
        {
            autoexec.Set("NetPlayerName", PlayerName);
            autoexec.Save();
        }
        MouseSensitivity = autoexec.GetFloat("MouseSensitivity", 3.0f);
        MouseInvertY     = autoexec.GetInt("MouseInvertYAxis", 0) != 0;

        // 0 is the Match-FOV mode rather than a sensitivity of zero, so it
        // must not land in the slider - it would read as "no mouse at all".
        var fZoomSens    = autoexec.GetFloat("ZoomSensitivity", 0f);
        ZoomMatchFov     = fZoomSens == 0f;
        if (!ZoomMatchFov) ZoomSensitivity = fZoomSens;
        UpdateRate       = autoexec.GetInt("UpdateRate", 6);
        ScreenWidth      = autoexec.GetInt("screenwidth", 640);
        ScreenHeight     = autoexec.GetInt("screenheight", 480);

        // Stock-era resolutions in the config mean nobody chose one yet -
        // default the fields to the native display (written on Save).
        if (ScreenWidth < 1024)
        {
            (ScreenWidth, ScreenHeight) = NativeDisplay.Primary();
        }

        // Select the matching dropdown entry (native first) so the combo
        // never shows blank on boot.
        SelectPresetForCurrentResolution();

        // An install that predates this carries inputrate 0. The setter floors
        // it, so opening the launcher once is enough to retire that value.
        MouseSmoothness = autoexec.GetFloat("inputrate", MOUSE_SMOOTHNESS_MIN);
        MusicEnabled    = autoexec.GetFloat("MusicEnable", 1f) != 0f;
        // Slider POSITIONS come from the launcher prefs, because autoexec
        // holds the product of slider and master and those cannot be told
        // apart. On a profile that predates the master slider the prefs are
        // -1, so the game config seeds them and master stays at 100 - an
        // existing install opens showing the volumes it already had.

        MusicVolume = Math.Clamp(autoexec.GetFloat("MusicVolume", 40f), 0f, 90f);

        if (Prefs.MusicVolumePercent >= 0f) MusicVolumePercent = Prefs.MusicVolumePercent;
        else                                Prefs.MusicVolumePercent = MusicVolumePercent;
        SoundEnabled    = autoexec.GetFloat("SoundEnable", 1f) != 0f;
        SoundVolume = autoexec.GetFloat("SoundVolume", 40f);

        if (Prefs.SoundVolumePercent >= 0f) SoundVolumePercent = Prefs.SoundVolumePercent;
        else                                Prefs.SoundVolumePercent = SoundVolumePercent;
        Gore            = IndexToChoice(GoreModes, autoexec.GetFloat("Gore", 2f), 2);
        ScreenFlash     = autoexec.GetFloat("ScreenFlash", 1f) != 0f;
        VehicleThirdPerson = autoexec.GetFloat("VehicleThirdPerson", 1f) != 0f;
        AutoSwitch = AutoSwitchModes[Math.Clamp((int)autoexec.GetFloat("AutoSwitch", 2f), 0, 3)];
        HudAspect = NearestHudAspect(autoexec.GetFloat("HudAspect", 0f));
        KillFeedStyle = IndexToChoice(KillFeedStyles, autoexec.GetFloat("KillFeedStyle", 2f), 2);
        ClassicCampaign = autoexec.GetFloat("ClassicCampaign", 0f) != 0f;
        ProfanityFilter = autoexec.GetFloat("ProfanityFilter", 1f) != 0f;
        Saturate        = autoexec.GetFloat("Saturate", 0f) != 0f;

        // "50.000000 50.000000 50.000000" - the engine writes three floats;
        // the launcher's grey control reads the first.
        static int FirstComponent(string? v) =>
            float.TryParse((v ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), out var f)
                ? Math.Clamp((int)f, 0, 255) : 0;

        ModelAdd    = FirstComponent(autoexec.Get("ModelAdd"));
        ModelDirAdd = FirstComponent(autoexec.Get("ModelDirAdd"));
        StreamerMode = autoexec.GetFloat("StreamerMode", 0f) != 0f;
        HudNumberSize = autoexec.GetFloat("HudNumberSize", 18f);
        ChatSound     = autoexec.GetFloat("ChatSound", 1f) != 0f;
        // MaxFPS is the truth; the Net* pair is what this control used to
        // write and is kept only so an existing autoexec still reads sensibly.
        int nMaxFps     = autoexec.GetInt("MaxFPS", -1);
        LimitFps        = nMaxFps > 0 || (nMaxFps < 0 && autoexec.GetInt("NetLimitFps", 1) != 0);
        FpsLimit        = nMaxFps > 0 ? nMaxFps : autoexec.GetInt("NetFpsLimit", 120);

        SelectedDetailPreset = DetailPresets.Detect(autoexec);

        // COLOUR AND MECH ARE ROLLED ON A FRESH INSTALL, same reasoning as
        // the pilot name above: a stock install puts every player in a blue
        // Ordog, so an unconfigured server is eight identical machines in
        // eight identical liveries and nobody can tell who they are shooting.
        //
        // The colour is the one that matters. Multiplayer spawns set
        // FLAG_MODELTINT (PlayerObj), so the engine has been tinting player
        // models by NetPlayerColor since 1998 - the uniform already exists
        // and the default simply never varied. Rolling it turns a feature
        // that was there all along into one you can see.
        //
        // ABSENT means fresh, and the default VALUE is not treated as a
        // choice. Blue is 5 and Ordog is 1, so once written, "unset" and
        // "deliberately picked the default" are indistinguishable - which is
        // the trap the name generator avoids by testing for absent OR
        // "Sanjuro". Here only ABSENT rolls: someone who picked blue keeps
        // blue, someone who never opened the launcher gets a roll. The cost
        // is that a player who chose the defaults before ever saving is
        // re-rolled once; the alternative is re-rolling everyone who
        // genuinely likes blue, on every launch.
        //
        // Written back immediately, for the same reason the name is: the
        // fields must read correctly the moment the launcher opens, or nobody
        // thinks to press Save and the game reads a value that is not there.

        bool bRollIdentity = autoexec.Get("NetPlayerColor") is null;

        int color = autoexec.GetInt("NetPlayerColor", 5);
        int mech  = autoexec.GetInt("NetMech", 1);

        if (bRollIdentity)
        {
            // Random.Shared is fine here: cosmetic, once per install, and
            // nothing downstream depends on the sequence.
            color = Random.Shared.Next(1, PlayerColors.Length + 1);
            mech  = Random.Shared.Next(1, Mechs.Length + 1);
        }

        SelectedPlayerColor = PlayerColors[Math.Clamp(color, 1, PlayerColors.Length) - 1];
        if (mech >= 1 && mech <= Mechs.Length) SelectedMech = Mechs[mech - 1];

        if (bRollIdentity)
        {
            autoexec.Set("NetPlayerColor", color);
            autoexec.Set("NetMech", mech);
            autoexec.Save();
        }

        // The on-foot body comes back BY NAME. An unrecognised one falls back
        // to Sanjuro rather than being kept: the list here is what this build
        // can offer, and showing a name it does not have would promise a body
        // the player will not get.
        var onFoot = autoexec.Get("OnFootModel");
        SelectedOnFootModel =
            OnFootModels.FirstOrDefault(
                m => string.Equals(m, onFoot, StringComparison.OrdinalIgnoreCase))
            ?? OnFootModels[0];

        var dgv = new DgVoodooConfig(GameDir!);
        DgVoodooPresent = dgv.Present;
        if (dgv.Present)
        {
            SelectedDisplayMode = dgv.Mode switch
            {
                DgVoodooConfig.DisplayMode.Windowed => "Windowed",
                DgVoodooConfig.DisplayMode.BorderlessFullscreen => "Borderless fullscreen",
                _ => "Fullscreen",
            };
            SelectedMonitor = MonitorToLabel(dgv.Monitor);
            Pure32Bit = dgv.Pure32Bit;
            VerticalSync = dgv.VerticalSync;
            // Read back from the GAME side, which is the half that actually
            // does anything. A config where the two disagree - an older
            // install, or a hand edit - should show what the player will get.
            CaptureMouse = dgv.CaptureMouse && autoexec.GetFloat("FreeMouse", 0f) == 0f;
            SelectedFiltering = FilteringChoices.FirstOrDefault(c => c.Value == dgv.Filtering).Label ?? "App-driven";
            SelectedAntialias = AntialiasChoices.FirstOrDefault(c => c.Value == dgv.Antialiasing).Label ?? "Off (app-driven)";
        }

        RefreshMods();

        ReloadBindings();
        RefreshSetup();
        LoadHostState();

        Status = $"Game found: {GameDir}";
    }

    public void LaunchGameOnly()
    {
        if (!GameFound) return;
        new GameLaunchService(GameDir!)
        {
            FreshTakesPriority = Prefs.FreshTakesPriority,
        }
        .LaunchGame(null, Prefs.BuildArgs());
        Status = "Game launched (main menu; single player and multiplayer from there).";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);

        if (_suppressDirty || name is null) return;
        if (SettingsProperties.Contains(name)) SettingsDirty = true;
        else if (HostProperties.Contains(name)) HostDirty = true;
    }
}
