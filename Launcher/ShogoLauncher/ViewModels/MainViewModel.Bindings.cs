using System.Collections.ObjectModel;
using ShogoLauncher.Services;

namespace ShogoLauncher.ViewModels;

/// <summary>
/// The Controls tab: the keybind grid, the live-vs-defaults edit target, and
/// the shipped layouts. Split out of MainViewModel.cs by tab; see
/// MainViewModel.Host.cs for the pattern.
/// </summary>
public partial class MainViewModel
{
    public ObservableCollection<BindingRow> Keybinds { get; } = new();

    public BindingStore? Bindings { get; private set; }
    public KeybindLayout Layout { get; private set; } = KeybindLayout.Load();

    public record BindingRow(string Action, string Label, string Primary, string Secondary);

    // Keybind editing target: the live config vs the restore-defaults template.
    public string[] BindTargets { get; } = { "Live bindings (autoexec.cfg)", "Defaults (defkeybd.cfg)" };

    private string _selectedBindTarget = "Live bindings (autoexec.cfg)";
    public string SelectedBindTarget
    {
        get => _selectedBindTarget;
        set { Set(ref _selectedBindTarget, value); ReloadBindings(); }
    }

    private bool _showHiddenBinds;
    public bool ShowHiddenBinds
    {
        get => _showHiddenBinds;
        set { Set(ref _showHiddenBinds, value); RebuildBindRows(); }
    }

    private bool _showBindAdvanced;
    public bool ShowBindAdvanced { get => _showBindAdvanced; set => Set(ref _showBindAdvanced, value); }

    /// <summary>Reset the live bindings (autoexec.cfg) to the defkeybd.cfg defaults (in memory; Save writes).</summary>
    public bool RestoreDefaultBindings()
    {
        if (!GameFound || Bindings is null) return false;
        if (SelectedBindTarget.StartsWith("Defaults")) return false; // already editing defaults

        var defaults = new BindingStore(System.IO.Path.Combine(GameDir!, "defkeybd.cfg"));
        if (!defaults.Loaded || defaults.AllBinds.Count == 0) return false;

        Bindings.ReplaceAllBinds(defaults.AllBinds);
        RebuildBindRows();
        return true;
    }

    // --- Shipped keybind layouts ---
    //
    // Offered as a starting point rather than imposed. "Restore Defaults"
    // above is a separate control that always means defkeybd.cfg.
    //
    // defkeybd IS listed here as well, even though that overlaps with
    // Restore Defaults. While actions are still being added to the game,
    // being able to pick the default layout explicitly - and see it beside
    // the alternatives - is worth more than avoiding the duplication.
    //
    // Discovered from the shipped Defaults folder rather than hardcoded, so
    // dropping another <name>keybd.cfg in there is the whole of adding a
    // layout - no code, no rebuild of this list.

    public record BindLayout(string Title, string Path)
    {
        // A record's generated ToString is "BindLayout { Title = X, Path = Y }",
        // and that is exactly what the closed combo box was showing after a
        // selection: DisplayMemberPath styles the items in the LIST, and the
        // selection box fell back to the object's own text. Overriding here
        // fixes it wherever the value is displayed rather than only in the one
        // template that happened to reveal it.
        public override string ToString() => Title;
    }

    private List<BindLayout>? _bindLayouts;
    public List<BindLayout> BindLayouts => _bindLayouts ??= DiscoverBindLayouts();

    private static List<BindLayout> DiscoverBindLayouts()
    {
        var list = new List<BindLayout>();
        var dir  = System.IO.Path.Combine(AppContext.BaseDirectory, "Defaults");

        if (!System.IO.Directory.Exists(dir)) return list;

        foreach (var path in System.IO.Directory.EnumerateFiles(dir, "*keybd.cfg"))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            list.Add(new BindLayout(PrettyLayoutName(name), path));
        }

        // Default first, then the rest alphabetically - so the list reads
        // as "the shipped one, and the alternatives".
        list.Sort((a, b) =>
            a.Title.StartsWith("ShogoFRESH") ? -1 :
            b.Title.StartsWith("ShogoFRESH") ?  1 :
            string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));

        return list;
    }

    private static string PrettyLayoutName(string fileName) => fileName.ToLowerInvariant() switch
    {
        "defkeybd" => "ShogoFRESH default (WASD)",
        "kyokeybd" => "Kyodan (ESDF)",
        _          => fileName,
    };

    private BindLayout? _selectedBindLayout;
    public BindLayout? SelectedBindLayout
    {
        get => _selectedBindLayout;
        set => Set(ref _selectedBindLayout, value);
    }

    /// <summary>
    /// Load the selected layout's binds into whichever file is being edited.
    /// In memory only - Save Bindings writes, same as every other edit here.
    /// </summary>
    public bool ApplySelectedBindLayout(out string error)
    {
        error = "";

        if (!GameFound || Bindings is null) { error = "No game directory."; return false; }
        if (SelectedBindLayout is null)     { error = "No layout selected."; return false; }

        if (!System.IO.File.Exists(SelectedBindLayout.Path))
        {
            error = $"{SelectedBindLayout.Title} is missing from the Defaults folder.";
            return false;
        }

        var layout = new BindingStore(SelectedBindLayout.Path);

        if (!layout.Loaded || layout.AllBinds.Count == 0)
        {
            error = $"{SelectedBindLayout.Title} could not be read, or has no bindings.";
            return false;
        }

        Bindings.ReplaceAllBinds(layout.AllBinds);
        RebuildBindRows();
        return true;
    }

    /// <summary>
    /// Apply the 0.10.8 keyboard layout to an install that predates it, once.
    ///
    /// Called at startup rather than offered as a button, because the people
    /// it is for are exactly the people who would never look for one - the
    /// keys simply are not where the documentation says. It is safe to be
    /// automatic only because KeybindMigration refuses to touch anything the
    /// player has moved; see the reasoning there.
    /// </summary>
    public void MigrateKeybindsOnce()
    {
        if (!GameFound || Prefs.KeybindsMigrated0108) return;

        int changed;
        try { changed = KeybindMigration.Apply(GameDir!); }
        catch { return; }        // a locked or unreadable config is not worth a dialog at startup

        // Marked done whether or not anything moved. A fresh install has
        // nothing to migrate and asking again every launch would be a file
        // read forever, for an answer that cannot change.
        Prefs.KeybindsMigrated0108 = true;
        try { Prefs.Save(); } catch { }

        if (changed > 0)
        {
            ReloadBindings();
            RebuildBindRows();
            Status = $"Updated {changed} keyboard binding{(changed == 1 ? "" : "s")} to the current layout "
                   + "(quick save F4, quick load F5, HUD F9). Anything you had rebound was left alone.";
        }

        MoveQuickMeleeOnce();
    }

    /// <summary>
    /// Move quick melee off F, once. Its own flag and its own call because
    /// MigrateKeybindsOnce above is already marked done on every install that
    /// has ever run the launcher - folding this into it would mean it never
    /// ran for exactly the people who have F.
    ///
    /// This is the only route: the engine has no unbind command (engine fact
    /// 3), so nothing in the game can take F away, and the runtime default in
    /// RiotStartup.cpp is guarded by a variable that is already set.
    /// </summary>
    public void MoveQuickMeleeOnce()
    {
        if (!GameFound || Prefs.QuickMeleeMovedToQ) return;

        int changed;
        try { changed = KeybindMigration.ApplyQuickMelee(GameDir!); }
        catch { return; }

        Prefs.QuickMeleeMovedToQ = true;
        try { Prefs.Save(); } catch { }

        if (changed > 0)
        {
            ReloadBindings();
            RebuildBindRows();
            Status = "Quick melee moved from F to Q.";
        }
    }

    public void ReloadBindings()
    {
        if (!GameFound) return;

        // Live bindings persist in autoexec.cfg once the game has run;
        // defkeybd.cfg is the "restore defaults" template - both editable.
        var file = SelectedBindTarget.StartsWith("Defaults") ? "defkeybd.cfg" : "autoexec.cfg";
        Bindings = new BindingStore(System.IO.Path.Combine(GameDir!, file));
        BindingsDirty = false;   // freshly read from disk
        RebuildBindRows();
    }

    public void ReloadLayout()
    {
        Layout = KeybindLayout.Load();
        RebuildBindRows();
    }

    public void RebuildBindRows()
    {
        if (Bindings is null) return;

        Keybinds.Clear();
        foreach (var action in Layout.Arrange(Bindings.Actions))
        {
            if (!ShowHiddenBinds && Layout.IsHidden(action)) continue;

            var binds = Bindings.GetBindings(action);
            Keybinds.Add(new BindingRow(
                action,
                Layout.LabelFor(action),
                binds.Count > 0 ? BindingStore.TriggerDisplay(binds[0].Device, binds[0].Trigger) : "",
                binds.Count > 1 ? BindingStore.TriggerDisplay(binds[1].Device, binds[1].Trigger) : ""));
        }
    }
}
