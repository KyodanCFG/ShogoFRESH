using System.IO;

namespace ShogoLauncher.Services;

/// <summary>
/// Move an existing install onto the 0.10.8 keyboard layout, once.
///
/// <para>
/// <b>Why this cannot be done the way every other bind change has been.</b>
/// The precedent is <c>RiotStartup.cpp</c>: register the action, bind it from
/// the game, guard it with a console variable so it happens once. That works
/// for GIVING a key to something. It cannot TAKE one away, because the engine
/// has no command that removes a binding - established by reading the
/// console's command table out of <c>Client.exe</c>, which is contiguous and
/// holds exactly <c>AddAction</c>, <c>RangeScale</c>, <c>Scale</c>,
/// <c>Bind</c>, <c>RangeBind</c>, <c>EnableDevice</c>,
/// <c>ListInputDevices</c>, <c>ShowInputDevices</c> and
/// <c>ShowDeviceObjects</c>. There is no unbind, under any spelling.
/// </para>
/// <para>
/// So a console-side migration could only ever have added F4 alongside the F6
/// that was already there. The launcher has the advantage the game does not:
/// it owns <c>autoexec.cfg</c> as a text file and can simply not write a line.
/// </para>
/// <para>
/// <b>Nothing is moved that the player has touched.</b> Every change is
/// conditional on the binding still being exactly what the old defaults
/// shipped - one binding, on the historical key. Somebody who put quicksave on
/// F1 years ago keeps F1, and somebody who deliberately bound arrow-key
/// turning keeps that too. "Restore Defaults" remains the button for people
/// who want the whole layout replaced; this is for everybody who would never
/// press it and should not have to.
/// </para>
/// </summary>
public static class KeybindMigration
{
    private const string Keyboard = "##keyboard";

    /// <summary>Actions the 0.10.8 layout ships unbound, with the key each
    /// one held before. Cleared only when still on that key.</summary>
    private static readonly (string Action, string OldKey)[] Retired =
    {
        ("Left",           "##203"),   // arrow keys: turning, which the mouse does
        ("Right",          "##205"),
        ("Strafe",         "##56"),    // left alt, a modifier for a thing WASD does
        ("LookDown",       "##209"),
        ("LookUp",         "##201"),
        ("MouseAim",       "##53"),
        ("CenterView",     "##207"),
        ("DecScreenRect",  "##12"),    // resizes a viewport that no longer resizes
        ("IncScreenRect",  "##13"),
    };

    /// <summary>Actions that moved. Applied only when still on the old key
    /// AND the new key is free, so a migration can never take a key away
    /// from something the player put there.</summary>
    private static readonly (string Action, string OldKey, string NewKey)[] Moved =
    {
        ("QuickSave",       "##64", "##62"),   // F6 -> F4
        ("QuickLoad",       "##65", "##63"),   // F7 -> F5
        ("InterfaceToggle", "##21", "##67"),   // Y  -> F9
    };

    /// <summary>
    /// Returns the number of bindings changed, or 0 if there was nothing to
    /// do. Does not save prefs - the caller owns that, because
    /// <c>LauncherPrefs.Save()</c> is otherwise only called from the Settings
    /// tab and a pref written anywhere else quietly reverts.
    /// </summary>
    /// <summary>Quick melee moved from F to Q for 0.10.46. Its own pass and
    /// its own flag, because Apply() above is long since marked done on every
    /// install that has ever run the launcher - reusing that flag would mean
    /// this never runs for exactly the people who need it.</summary>
    private static readonly (string Action, string OldKey, string NewKey)[] MovedQuickMelee =
    {
        ("QuickMelee", "##33", "##16"),   // F -> Q
    };

    /// <summary>
    /// Move quick melee off F, once, for an install that got it there in
    /// 0.10.44/45.
    ///
    /// Separate from <see cref="Apply"/> for one reason and it is not
    /// tidiness: that migration is flagged done for everybody, so anything
    /// added to its tables from now on is dead code. A migration needs a flag
    /// of its own or it is not a migration.
    ///
    /// Same refusals as the 0.10.8 pass - untouched-only, and never steals a
    /// key that something else holds.
    /// </summary>
    public static int ApplyQuickMelee(string gameDir)
    {
        var path = Path.Combine(gameDir, "autoexec.cfg");
        if (!File.Exists(path)) return 0;

        var store = new BindingStore(path);
        if (!store.Loaded) return 0;

        int changed = 0;

        foreach (var (action, oldKey, newKey) in MovedQuickMelee)
        {
            if (!IsUntouched(store, action, oldKey)) continue;
            if (IsTaken(store, newKey, action)) continue;

            store.SetBinding(action, 0, Keyboard, newKey);
            changed++;
        }

        if (changed > 0) store.Save();

        return changed;
    }

    public static int Apply(string gameDir)
    {
        var path = Path.Combine(gameDir, "autoexec.cfg");
        if (!File.Exists(path)) return 0;          // a fresh install seeds from defkeybd.cfg already

        var store = new BindingStore(path);
        if (!store.Loaded) return 0;

        int changed = 0;

        foreach (var (action, oldKey) in Retired)
        {
            if (!IsUntouched(store, action, oldKey)) continue;

            store.ClearBinding(action, 0);
            changed++;
        }

        foreach (var (action, oldKey, newKey) in Moved)
        {
            if (!IsUntouched(store, action, oldKey)) continue;

            // Refuse to steal. SetBinding would happily take the new key off
            // whatever holds it and report what it stole; that is right for
            // somebody pressing a key in the controls menu and wrong for
            // something happening to them at startup.
            if (IsTaken(store, newKey, action)) continue;

            store.SetBinding(action, 0, Keyboard, newKey);
            changed++;
        }

        if (changed > 0) store.Save();

        return changed;
    }

    /// <summary>
    /// True when the action is bound exactly once, on the keyboard, to the key
    /// the old defaults gave it. Anything else - moved, cleared, given a
    /// second binding, put on a mouse button - means the player has an opinion
    /// about it and it is left alone.
    /// </summary>
    private static bool IsUntouched(BindingStore store, string action, string oldKey)
    {
        var binds = store.GetBindings(action);

        return binds.Count == 1
            && binds[0].Device.Equals(Keyboard, StringComparison.OrdinalIgnoreCase)
            && binds[0].Trigger.Equals(oldKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTaken(BindingStore store, string key, string exceptAction) =>
        store.AllBinds.Any(b =>
            b.Device.Equals(Keyboard, StringComparison.OrdinalIgnoreCase) &&
            b.Trigger.Equals(key, StringComparison.OrdinalIgnoreCase) &&
            !b.Action.Equals(exceptAction, StringComparison.OrdinalIgnoreCase));
}
