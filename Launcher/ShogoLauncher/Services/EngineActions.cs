using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShogoLauncher.Services;

/// <summary>
/// The engine's action registry lives in defaults.cfg as "AddAction &lt;name&gt;
/// &lt;commandId&gt;" lines, and it is a loose file rather than something inside
/// SHOGO.REZ. A rangebind naming an action the engine has never been told
/// about binds to nothing at all - silently, with no error - so any command
/// ShogoFRESH adds has to be registered here before a key can reach it.
///
/// Ids must match Shared\RiotCommandIDs.h.
/// </summary>
public static class EngineActions
{
    private static readonly (string Name, int Id)[] Required =
    {
        ("Reload",  89),   // COMMAND_ID_RELOAD
        ("Holster", 85),   // COMMAND_ID_HOLSTER
        ("SecondaryHold",   82),   // COMMAND_ID_SECONDARY_HOLD
        ("SecondaryToggle", 83),   // COMMAND_ID_SECONDARY_TOGGLE
        ("QuickMelee",      86),   // COMMAND_ID_QUICKMELEE
    };

    /// <summary>Fallback binding if defkeybd.cfg has none for a new action.</summary>
    private static readonly (string Name, string Device, string Trigger)[] DefaultBinds =
    {
        ("Reload",  "##keyboard", "##19"),   // R
        ("Holster", "##keyboard", "##34"),   // G - unbound in both shipped layouts
        ("QuickMelee", "##keyboard", "##16"),   // Q - also unbound in both
        // Mouse 2. Held by the grapple since 1998, which moves to mouse 4 in
        // both shipped layouts - so this is a fallback for a keybind file
        // that predates the move, not a second home for the button.
        ("SecondaryToggle", "##mouse", "Button 1"),
    };

    /// <summary>
    /// Append any missing ShogoFRESH actions to defaults.cfg. Idempotent, and
    /// it only ever adds lines, so hand edits and the stock entries survive.
    /// Returns how many were added.
    /// </summary>
    public static int EnsureRegistered(string gameDir)
    {
        var path = Path.Combine(gameDir, "defaults.cfg");
        if (!File.Exists(path)) return 0;

        string[] lines;
        try   { lines = File.ReadAllLines(path); }
        catch (IOException) { return 0; }

        var missing = Required
            .Where(a => !lines.Any(l => IsActionLine(l, a.Name)))
            .ToList();

        if (missing.Count == 0) return 0;

        var added = missing.Select(a => $"AddAction {a.Name} {a.Id}");

        try
        {
            File.AppendAllLines(path, added);
        }
        catch (IOException) { return 0; }

        return missing.Count;
    }

    /// <summary>
    /// Give any newly added action a key in the live autoexec.cfg if nothing
    /// is bound to it yet.
    ///
    /// Registering the action is only half the job: an install whose
    /// autoexec.cfg predates the control has no rangebind naming it, and the
    /// launcher only writes bindings the user sets by hand - so without this
    /// the key silently does nothing on every existing install. The default
    /// is taken from defkeybd.cfg so there is one source of truth.
    ///
    /// Only ever adds, and only when the action is entirely unbound, so a
    /// deliberate rebind (or a deliberate unbind, then rebind elsewhere) is
    /// never overwritten. Returns how many bindings were added.
    /// </summary>
    public static int EnsureDefaultBindings(string gameDir)
    {
        var autoexec = Path.Combine(gameDir, "autoexec.cfg");
        if (!File.Exists(autoexec)) return 0;

        string[] live;
        try   { live = File.ReadAllLines(autoexec); }
        catch (IOException) { return 0; }

        var defaults = ReadLinesOrEmpty(Path.Combine(gameDir, "defkeybd.cfg"));

        var toAdd = new List<string>();

        foreach (var (name, device, trigger) in DefaultBinds)
        {
            if (live.Any(l => MentionsAction(l, name))) continue;

            var shipped = defaults.FirstOrDefault(l => MentionsAction(l, name));

            toAdd.Add(shipped ?? $"rangebind \"{device}\" \"{trigger}\" 0.000000 0.000000 \"{name}\"");
        }

        if (toAdd.Count == 0) return 0;

        try   { File.AppendAllLines(autoexec, toAdd); }
        catch (IOException) { return 0; }

        return toAdd.Count;
    }

    private static string[] ReadLinesOrEmpty(string path)
    {
        try   { return File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>(); }
        catch (IOException) { return Array.Empty<string>(); }
    }

    /// <summary>Does this rangebind line name the given action?</summary>
    private static bool MentionsAction(string line, string action)
    {
        var trimmed = line.Trim();

        if (!trimmed.StartsWith("rangebind", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("bind", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Match the quoted action anywhere on the line rather than only at
        // the very end: trailing whitespace or an inline comment used to
        // make an existing binding invisible to this check, and - worse -
        // could make a missing one look present.
        return trimmed.IndexOf($"\"{action}\"", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsActionLine(string line, string action)
    {
        var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 2
            && parts[0].Equals("AddAction", StringComparison.OrdinalIgnoreCase)
            && parts[1].Equals(action, StringComparison.OrdinalIgnoreCase);
    }
}
