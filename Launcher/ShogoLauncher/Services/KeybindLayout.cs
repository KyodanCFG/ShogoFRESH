using System.IO;
using System.Text.Json;

namespace ShogoLauncher.Services;

/// <summary>
/// User-customizable presentation layer for the Keybinds tab: display labels,
/// row order, and visibility per action. Persisted as editable JSON in
/// %AppData%\ShogoLauncher\keybind-layout.json — relabeling doubles as
/// localization of the keybind list (full launcher localization builds on the
/// same pattern later).
/// </summary>
public class KeybindLayout
{
    /// <summary>Actions hidden from the list (still bound in the config).</summary>
    public List<string> Hidden { get; set; } = new();

    /// <summary>Display order; actions not listed follow in config order.</summary>
    public List<string> Order { get; set; } = new();

    /// <summary>Display label per action; unlisted actions show prettified names.</summary>
    public Dictionary<string, string> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string LayoutPath =>
        Path.Combine(AppPaths.Root,
                     "keybind-layout.json");

    public static KeybindLayout Load()
    {
        try
        {
            if (File.Exists(LayoutPath))
            {
                var saved = JsonSerializer.Deserialize<KeybindLayout>(File.ReadAllText(LayoutPath));
                if (saved is not null)
                {
                    // MERGE IN ANYTHING THE SAVED FILE PREDATES.
                    //
                    // This file is written on first run and then owned by the
                    // player, so a layout saved before an action existed has
                    // no row for it - and Arrange() puts unlisted actions at
                    // the END, which is how two new controls added under Fire
                    // turned up at the bottom of the list instead.
                    //
                    // Same shape as the runtime AddAction problem in the game
                    // (engine fact 3): shipping a new default is not enough on
                    // its own, because existing installs already have a file.
                    // Their edits are kept; only genuinely absent entries are
                    // added, in the position the shipped layout gives them.
                    saved.MergeNewFrom(Default());
                    return saved;
                }
            }
        }
        catch (JsonException) { }

        // First run: prefer a shipped default from Defaults\ - localized
        // variant (keybind-layout.<lang>.json, matched to the UI culture)
        // first, then the neutral one - so packagers can distribute
        // translated keybind labels with the zip.
        var defaultsDir = Path.Combine(AppContext.BaseDirectory, "Defaults");
        var lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        foreach (var candidate in new[]
                 {
                     Path.Combine(defaultsDir, $"keybind-layout.{lang}.json"),
                     Path.Combine(defaultsDir, "keybind-layout.json"),
                 })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                var shipped = JsonSerializer.Deserialize<KeybindLayout>(File.ReadAllText(candidate));
                if (shipped is null) continue;
                shipped.Save(); // materialize into AppData for user editing
                return shipped;
            }
            catch (JsonException) { }
        }

        var def = Default();
        def.Save(); // materialize so the user has a file to edit
        return def;
    }

    /// <summary>
    /// Add anything from <paramref name="newer"/> this layout has never heard
    /// of, without disturbing what the player has arranged.
    ///
    /// An action missing from Order is inserted after the same neighbour it
    /// follows in the newer layout, so a control added "under Fire" lands
    /// under Fire rather than at the bottom. A missing label is only filled
    /// in where the player has not set one.
    /// </summary>
    public void MergeNewFrom(KeybindLayout newer)
    {
        for (int i = 0; i < newer.Order.Count; i++)
        {
            var action = newer.Order[i];
            if (Order.Contains(action, StringComparer.OrdinalIgnoreCase)) continue;

            // Land it after whichever of its predecessors this layout knows,
            // walking back until one is found. Falls through to the end,
            // which is the honest answer for an action with no known anchor.
            int at = Order.Count;

            for (int j = i - 1; j >= 0; j--)
            {
                var idx = Order.FindIndex(a =>
                    string.Equals(a, newer.Order[j], StringComparison.OrdinalIgnoreCase));

                if (idx >= 0) { at = idx + 1; break; }
            }

            Order.Insert(at, action);
        }

        foreach (var kv in newer.Labels)
        {
            if (!Labels.ContainsKey(kv.Key)) Labels[kv.Key] = kv.Value;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
        File.WriteAllText(LayoutPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public string LabelFor(string action) =>
        Labels.TryGetValue(action, out var l) ? l : Prettify(action);

    public bool IsHidden(string action) =>
        Hidden.Contains(action, StringComparer.OrdinalIgnoreCase);

    /// <summary>Order actions: explicit order list first, then the rest as-is.</summary>
    public IEnumerable<string> Arrange(IEnumerable<string> actions)
    {
        var pool = actions.ToList();
        foreach (var o in Order)
        {
            var hit = pool.FirstOrDefault(a => a.Equals(o, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) { pool.Remove(hit); yield return hit; }
        }
        foreach (var rest in pool) yield return rest;
    }

    private static string Prettify(string action) => action switch
    {
        "Forward" => "Move Forward",
        "Backward" => "Move Backward",
        "StrafeLeft" => "Move Left",
        "StrafeRight" => "Move Right",
        "Duck" => "Crouch",
        // Single player has no scoreboard, so the same key opens the
        // mission log there. Stock bound the log to F1 and made it the one
        // action you could not rebind.
        "FragCount" => "Scoreboard / Mission Log",
        "SendMessage" => "Chat",
        "VehicleModeToggle" => "Transform (Mech/Vehicle)",
        "TractorBeam" => "Grapple",
        "DoubleJump" => "Double Jump",
        "TurnAround" => "Quick Turn",
        "Speed" => "Walk (hold)",
        "RunLock" => "Run (toggle)",
        "LookUp" => "Look Up",
        "LookDown" => "Look Down",
        "CenterView" => "Center View",
        "ChaseView" => "Third Person Toggle",
        "InterfaceToggle" => "Toggle HUD",
        "ShowOrdinance" => "Show Weapons/Ammo",
        "NextWeapon" => "Next Weapon",
        "PrevWeapon" => "Previous Weapon",
        "QuickSave" => "Quick Save",
        "QuickLoad" => "Quick Load",
        "ScreenShot" => "Screenshot",
        _ when action.StartsWith("Weapon_") => $"Weapon Slot {action[7..]}",
        _ => action,
    };

    private static KeybindLayout Default() => new()
    {
        // Mirrors Defaults\keybind-layout.json (the shipped canonical layout):
        // legacy keyboard-look/turn actions and internal toggles hidden.
        Hidden = new List<string>
        {
            "Left", "Right", "LookUp", "LookDown", "CenterView", "Strafe",
            "MouseAim", "CursorTog", "DecScreenRect", "IncScreenRect", "LeaveUnassigned",
        },
        // Labels the in-game controls menu also uses, so the two lists do not
        // disagree about what a row is called. Everything else prettifies
        // from the action name, which is right for the stock actions.
        Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Fire"]            = "Primary Fire",
            ["SecondaryHold"]   = "Secondary Fire/Zoom (hold)",
            ["SecondaryToggle"] = "Secondary Fire/Zoom (toggle)",
            ["QuickMelee"]      = "Quick melee (hold)",
        },
        Order = new List<string>
        {
            "Forward", "Backward", "StrafeLeft", "StrafeRight",
            "Jump", "DoubleJump", "Duck", "Speed", "RunLock", "TurnAround",
            "Fire", "SecondaryHold", "SecondaryToggle", "Reload",
            "NextWeapon", "PrevWeapon", "Holster", "QuickMelee", "TractorBeam", "VehicleModeToggle",
            "Weapon_0", "Weapon_1", "Weapon_2", "Weapon_3", "Weapon_4",
            "Weapon_5", "Weapon_6", "Weapon_7", "Weapon_8", "Weapon_9",
            "ChaseView", "SendMessage", "FragCount", "ShowOrdinance", "InterfaceToggle",
            "QuickSave", "QuickLoad", "ScreenShot",
        },
    };
}
