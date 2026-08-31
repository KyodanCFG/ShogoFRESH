using System.Collections.ObjectModel;
using System.ComponentModel;
using ShogoLauncher.Services;

namespace ShogoLauncher.ViewModels;

/// <summary>
/// The Game Setup tab: the fix cards (DirectPlay, dgVoodoo, ShogoFRESH
/// itself, recommended defaults) and their install/undo lifecycle. Split out
/// of MainViewModel.cs by tab; see MainViewModel.Host.cs for the pattern.
/// </summary>
public partial class MainViewModel
{
    public class FixRow : INotifyPropertyChanged
    {
        public GameSetupService.FixDefinition Definition { get; init; } = null!;
        public string Title => Definition.Title;
        public string Description => Definition.Description;

        /// <summary>
        /// The two cards ShogoFRESH itself updates over time (the mod and
        /// the recommended defaults). They float to the top once initial
        /// setup is done, and their Apply gets the stronger highlight.
        /// </summary>
        public bool IsUpdateProne => Definition.Id is "shogofresh" or "defaults";

        private GameSetupService.FixStatus _status;
        public GameSetupService.FixStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                Raise(nameof(Status));
                Raise(nameof(StatusText));
                Raise(nameof(CanApply));
                Raise(nameof(CanUndo));
            }
        }

        public string StatusText => Status switch
        {
            GameSetupService.FixStatus.Installed => "Installed",
            GameSetupService.FixStatus.InstalledExternally => "Installed (outside launcher)",
            GameSetupService.FixStatus.NotInstalled => "Not installed",
            GameSetupService.FixStatus.UpdateAvailable => "Update available",
            GameSetupService.FixStatus.NewerInstalled => "Newer build installed - not overwriting",
            _ => "Payload missing (see Redist\\README.md)",
        };

        /// <summary>
        /// Versions, for the cards that have one. Reads as "v0.8.60" when
        /// what is installed is current, and "v0.8.59 -> v0.8.60" when it is
        /// not - so "Update available" says WHICH update.
        ///
        /// Empty for the third-party fixes, whose versions are the upstream
        /// project's and not ours to claim, and empty when the install
        /// predates the manifest recording one. Saying nothing beats saying
        /// something invented.
        /// </summary>
        public string VersionText { get; set; } = "";

        // NewerInstalled is deliberately absent from CanApply: the whole
        // point is that the button is not there to be clicked. A dialog after
        // the fact is a dialog somebody dismisses mid-playtest.
        public bool CanApply => Status is GameSetupService.FixStatus.NotInstalled
                                       or GameSetupService.FixStatus.UpdateAvailable;
        public bool CanUndo => Status is GameSetupService.FixStatus.Installed
                                      or GameSetupService.FixStatus.UpdateAvailable
                                      or GameSetupService.FixStatus.NewerInstalled;

        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public ObservableCollection<FixRow> Fixes { get; } = new();

    private bool _directPlayEnabled;
    public bool DirectPlayEnabled { get => _directPlayEnabled; set => Set(ref _directPlayEnabled, value); }

    /// <summary>
    /// Anything on the Setup list still needs attention.
    ///
    /// <para>
    /// GameFound LEADS, and it has to. Every fix needs somewhere to install
    /// to, so with no game found the list is empty - and an empty list used
    /// to read as "nothing needs attention". On a machine with DirectPlay
    /// already enabled and Shogo not installed, both halves of the old test
    /// were false and the setup window never opened at all; on a machine
    /// without DirectPlay it opened showing that one card and nothing else,
    /// explaining neither what was wrong nor what to do. The absence of the
    /// game is the most important thing this window can say.
    /// </para>
    /// </summary>
    public bool SetupNeeded =>
        !GameFound ||
        !DirectPlayEnabled ||
        // NewerInstalled is NOT setup-needed. Somebody running a test build
        // has a MORE current install than the launcher, not a broken one, and
        // nagging them to finish setup for the length of a playtest is how a
        // warning becomes something you learn to ignore.
        Fixes.Any(f => f.Status is GameSetupService.FixStatus.NotInstalled
                                or GameSetupService.FixStatus.UpdateAvailable
                                or GameSetupService.FixStatus.PayloadMissing);

    /// <summary>
    /// Drives the "Shogo not detected" panel. A property rather than a
    /// negation at each binding site, so the panel and everything it hides
    /// cannot disagree.
    /// </summary>
    public bool GameMissing => !GameFound;

    private string _locateMessage = "";
    /// <summary>Why the last folder someone picked was not accepted. Empty
    /// until they have actually picked one - an error shown before any
    /// attempt reads as a failure they caused.</summary>
    public string LocateMessage { get => _locateMessage; private set => Set(ref _locateMessage, value); }

    public bool HasLocateMessage => !string.IsNullOrEmpty(LocateMessage);

    /// <summary>
    /// Take a folder the user chose and adopt it if it is (or contains, or
    /// sits inside) a Shogo installation. Returns true when the game is now
    /// found, so the caller can close a dialog or move on.
    /// </summary>
    public bool TryAdoptGameDir(string? picked)
    {
        var found = GameLocator.ResolvePickedDir(picked, out var problem);

        if (found is null)
        {
            LocateMessage = problem;
            return false;
        }

        LocateMessage = "";
        GameDir = found;

        // PERSISTED HERE, EXPLICITLY. Prefs.Save() is otherwise only called
        // from the Settings tab, so a pref set anywhere else appears to work
        // and is gone on the next launch - and a player who had to go and
        // find their install by hand should not be asked twice. Also means
        // automatic detection is skipped next time, which is the point: the
        // folder they named beats anything a search would guess.
        Prefs.GameDirOverride = found;
        try { Prefs.Save(); } catch { /* a read-only profile is not worth failing the locate over */ }

        // The rest of the launcher reads from disk on demand and none of it
        // ran while there was nowhere to read from, so everything has to be
        // loaded now rather than at the next tab switch.
        LoadFromGameDir();
        RefreshSetup();

        OnPropertyChanged(nameof(GameMissing));
        OnPropertyChanged(nameof(SetupNeeded));

        Status = $"Shogo found at {found}.";
        return true;
    }

    /// <summary>Re-run automatic detection, for someone who has just
    /// installed the game with the launcher already open.</summary>
    public bool RetryDetect()
    {
        var found = GameLocator.Locate(Prefs.GameDirOverride);

        if (found is null)
        {
            LocateMessage = "Still nothing. Checked the Steam libraries, GOG's registry and the "
                          + "usual install paths. If the game is somewhere else, point at it below.";
            return false;
        }

        return TryAdoptGameDir(found);
    }

    public void RefreshSetup()
    {
        DirectPlayEnabled = GameSetupService.IsDirectPlayEnabled();

        Fixes.Clear();

        OnPropertyChanged(nameof(GameMissing));

        if (!GameFound) return;

        var svc = new GameSetupService(GameDir!);

        var rows = GameSetupService.Fixes
            .Select(def => new FixRow
            {
                Definition = def,
                Status = svc.GetStatus(def),
                VersionText = BuildVersionText(svc, def),
            })
            .ToList();

        // First-time setup reads top to bottom as an install sequence, so
        // the required fixes lead. Once everything is installed, the cards
        // that actually change over time - the mod itself and the
        // recommended defaults - move to the top, because from then on
        // "is there an update?" is what this window is opened for.

        bool bInitialSetupDone = !rows.Any(r => r.Status == GameSetupService.FixStatus.NotInstalled);

        if (bInitialSetupDone)
        {
            rows = rows.OrderByDescending(r => r.IsUpdateProne).ToList();
        }

        foreach (var row in rows) Fixes.Add(row);
    }

    /// <summary>
    /// What to show beside a card's name. Only the ShogoFRESH-maintained
    /// cards have a version of ours to show; the rest are upstream projects
    /// whose versions we do not track.
    /// </summary>
    private static string BuildVersionText(GameSetupService svc, GameSetupService.FixDefinition def)
    {
        if (!(def.Id is "shogofresh" or "defaults")) return "";

        var available = GameSetupService.VersionString();
        var installed = svc.InstalledVersion(def);

        if (svc.GetStatus(def) == GameSetupService.FixStatus.UpdateAvailable)
        {
            return installed is null ? $"→ v{available}"
                                     : $"v{installed} → v{available}";
        }

        return installed is null ? "" : $"v{installed}";
    }


    public void ApplyFix(FixRow row)
    {
        if (!GameFound) return;
        bool ok = false;
        string error = "";
        try
        {
            new GameSetupService(GameDir!).Apply(row.Definition);
            ok = true;
        }
        catch (GameSetupService.NewerBuildInstalledException ex)
        {
            // Reached only by a force path or a race - the button is hidden
            // for this status. Kept because a refusal that lives only in the
            // UI is one code path away from not existing.
            error = ex.Message;
        }
        catch (Exception ex) { error = ex.Message; }

        LoadFromGameDir();   // fixes change configs (dgVoodoo, bindings, settings) - re-read everything
        if (ok) Status = $"{row.Title} installed (backups in ShogoFRESH_Backup\\).";
        else Warn($"{row.Title}: {error}");
    }

    public void UndoFix(FixRow row)
    {
        if (!GameFound) return;
        bool ok = false;
        string error = "";
        try
        {
            new GameSetupService(GameDir!).Undo(row.Definition);
            ok = true;
        }
        catch (Exception ex) { error = ex.Message; }

        LoadFromGameDir();
        if (ok) Status = $"{row.Title} removed; previous files restored.";
        else Warn($"{row.Title}: {error}");
    }
}
