using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ShogoLauncher.Services;

/// <summary>
/// Reads and writes the game's live input bindings.
///
/// The engine persists bindings to autoexec.cfg (and ships defaults in
/// defkeybd.cfg) as console commands:
///   AddAction Fire 8
///   enabledevice "##keyboard"
///   rangebind "##keyboard" "##29" 0.000000 0.000000 "Fire"
///   rangebind "##mouse" "Button 0" 0.000000 0.000000 "Fire"
///   rangebind "##mouse" "Wheel" -10000.0 -0.1 "PrevWeapon" 0.1 10000.0 "NextWeapon"
///
/// Notes from real configs:
///  - Keyboard triggers are "##&lt;DIK scancode&gt;" or plain character names.
///  - Mouse button object names are LOCALIZED DirectInput names ("Przycisk 0"
///    on a Polish system); learned from the file, English "Button N" fallback.
///  - The wheel is the mouse Z axis: a rangebind on object "Wheel" with a
///    negative range group for wheel-down and a positive group for wheel-up,
///    both optionally on one line. Internally we model the two directions as
///    pseudo-triggers WHEEL_UP / WHEEL_DOWN and recombine on save.
///  - Mouse axis binds (Axis1/2/3) are preserved verbatim, never edited.
///  - Multiple binds per action are legal (primary/secondary in the UI).
///  - No modifier/chord support exists in the engine.
/// </summary>
public partial class BindingStore
{
    public const string WheelUp = "WHEEL_UP";
    public const string WheelDown = "WHEEL_DOWN";
    private const string WheelObject = "Wheel";

    public record BindEntry(string Device, string Trigger, string Action, float RangeLo = 0f, float RangeHi = 0f);

    [GeneratedRegex("""^\s*rangebind\s+"(?<dev>[^"]+)"\s+"?(?<trig>##\d+|[^"]*)"?\s+(?<lo>-?[\d.]+)\s+(?<hi>-?[\d.]+)\s+"(?<action>[^"]*)"(?:\s+(?<lo2>-?[\d.]+)\s+(?<hi2>-?[\d.]+)\s+"(?<action2>[^"]*)")?\s*$""")]
    private static partial Regex BindLine();

    [GeneratedRegex("""^\s*AddAction\s+(?<name>\S+)\s+(?<id>-?\d+)\s*$""")]
    private static partial Regex ActionLine();

    private static readonly string[] HiddenActions = { "Axis1", "Axis2", "Axis3", "LeaveUnassigned" };

    private readonly List<string> _lines = new();
    private readonly List<BindEntry> _binds = new();           // editable
    private readonly List<string> _preservedBindLines = new(); // axis binds etc, kept verbatim
    private readonly Dictionary<int, string> _mouseButtonNames = new();

    public List<string> Actions { get; } = new();
    public string ConfigPath { get; }

    /// <param name="configPath">autoexec.cfg (live bindings) or defkeybd.cfg
    /// (the restore-defaults template) - same line grammar in both.</param>
    public BindingStore(string configPath)
    {
        ConfigPath = configPath;
        if (!File.Exists(ConfigPath)) return;

        foreach (var raw in File.ReadAllLines(ConfigPath))
        {
            _lines.Add(raw);

            var am = ActionLine().Match(raw);
            if (am.Success)
            {
                var name = am.Groups["name"].Value;
                if (!HiddenActions.Contains(name, StringComparer.OrdinalIgnoreCase) && !Actions.Contains(name))
                    Actions.Add(name);
                continue;
            }

            var bm = BindLine().Match(raw);
            if (!bm.Success) continue;

            var device = bm.Groups["dev"].Value;
            var trigger = bm.Groups["trig"].Value.Trim();

            var groups = new List<(float lo, float hi, string action)>
            {
                (Parse(bm.Groups["lo"].Value), Parse(bm.Groups["hi"].Value), bm.Groups["action"].Value),
            };
            if (bm.Groups["action2"].Success)
                groups.Add((Parse(bm.Groups["lo2"].Value), Parse(bm.Groups["hi2"].Value), bm.Groups["action2"].Value));

            bool isWheel = device.Equals("##mouse", StringComparison.OrdinalIgnoreCase)
                        && trigger.Equals(WheelObject, StringComparison.OrdinalIgnoreCase);

            foreach (var (lo, hi, action) in groups)
            {
                if (HiddenActions.Contains(action, StringComparer.OrdinalIgnoreCase) || action.Length == 0)
                {
                    _preservedBindLines.Add(raw);
                    continue;
                }

                if (isWheel)
                {
                    // Positive range = wheel up (Z+), negative = wheel down.
                    var dir = hi > 0f ? WheelUp : WheelDown;
                    _binds.Add(new BindEntry(device, dir, action));
                }
                else
                {
                    _binds.Add(new BindEntry(device, trigger, action, lo, hi));

                    if (device.Equals("##mouse", StringComparison.OrdinalIgnoreCase))
                    {
                        var m = Regex.Match(trigger, @"(\d+)\s*$");
                        if (m.Success) _mouseButtonNames[int.Parse(m.Groups[1].Value)] = trigger;
                    }
                }
            }
        }

        foreach (var b in _binds)
            if (!Actions.Contains(b.Action)) Actions.Add(b.Action);
    }

    private static float Parse(string s) =>
        float.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f;

    public bool Loaded => _lines.Count > 0;

    public IReadOnlyList<BindEntry> GetBindings(string action) =>
        _binds.Where(b => b.Action.Equals(action, StringComparison.OrdinalIgnoreCase)).ToList();

    public string MouseButtonName(int index) =>
        _mouseButtonNames.TryGetValue(index, out var n) ? n : $"Button {index}";

    /// <summary>
    /// Bind a trigger to an action slot (0 = primary, 1 = secondary). If the
    /// trigger was bound to other actions, it is stolen from them; the list
    /// of actions it was unbound from is returned so the UI can say so.
    /// </summary>
    public List<string> SetBinding(string action, int slot, string device, string trigger)
    {
        var stolenFrom = _binds
            .Where(b => b.Device.Equals(device, StringComparison.OrdinalIgnoreCase) &&
                        b.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase) &&
                        !b.Action.Equals(action, StringComparison.OrdinalIgnoreCase))
            .Select(b => b.Action)
            .Distinct()
            .ToList();

        _binds.RemoveAll(b =>
            b.Device.Equals(device, StringComparison.OrdinalIgnoreCase) &&
            b.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase) &&
            !b.Action.Equals(action, StringComparison.OrdinalIgnoreCase));

        var mine = _binds.Where(b => b.Action.Equals(action, StringComparison.OrdinalIgnoreCase)).ToList();

        if (mine.Any(b => b.Device.Equals(device, StringComparison.OrdinalIgnoreCase) &&
                          b.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase)))
            return stolenFrom;

        var entry = new BindEntry(device, trigger, action);
        if (slot < mine.Count)
            _binds[_binds.IndexOf(mine[slot])] = entry;
        else
            _binds.Add(entry);

        return stolenFrom;
    }

    public void ClearBinding(string action, int slot)
    {
        var mine = _binds.Where(b => b.Action.Equals(action, StringComparison.OrdinalIgnoreCase)).ToList();
        if (slot < mine.Count) _binds.Remove(mine[slot]);
    }

    /// <summary>Replace every editable bind (used by "restore defaults").</summary>
    public void ReplaceAllBinds(IEnumerable<BindEntry> binds)
    {
        _binds.Clear();
        _binds.AddRange(binds);
    }

    public IReadOnlyList<BindEntry> AllBinds => _binds;

    public void Save()
    {
        var output = new List<string>();
        bool bindBlockWritten = false;

        foreach (var raw in _lines)
        {
            var bm = BindLine().Match(raw);
            bool isEditableBind = false;
            // defkeybd.cfg writes enabledevice without quotes; autoexec with.
            bool isEnableDevice = Regex.IsMatch(raw, """^\s*enabledevice\s+"?##(keyboard|mouse)"?\s*$""", RegexOptions.IgnoreCase);

            if (bm.Success)
            {
                var action = bm.Groups["action"].Value;
                isEditableBind = !HiddenActions.Contains(action, StringComparer.OrdinalIgnoreCase) && action.Length > 0;
            }

            // A line that CLAIMS to be a bind but does not parse is dropped,
            // not preserved.
            //
            // This is the only self-repair in the file and it earns its keep:
            // a malformed bind is invisible to the parser, so it never reaches
            // _binds, never appears in the UI, and the "else" below used to
            // copy it through verbatim on every save - permanently. One
            // turned up in a live autoexec.cfg as
            //
            //     rangebind "##keyboard" "##16"
            //
            // with no ranges and no action: the key the player had just
            // rebound quick melee to, next to the old binding which was still
            // intact. The engine rewrites this file on exit in its own style
            // and something in that path dropped the action name; whatever
            // wrote it, nothing here could ever clean it up, so the rebind
            // looked like it had silently failed and the old key kept working.
            //
            // Narrow on purpose. It fires only when the line opens with
            // rangebind/bind AND fails the grammar AND carries no quoted
            // action at all - and a bind with no action cannot be doing
            // anything, because the action is the entire point of one.

            bool isBrokenBind = !bm.Success && LooksLikeBind(raw) && !HasQuotedAction(raw);

            if (isEditableBind || isEnableDevice)
            {
                if (!bindBlockWritten)
                {
                    WriteBindBlock(output);
                    bindBlockWritten = true;
                }
            }
            else if (!isBrokenBind)
            {
                output.Add(raw);
            }
        }

        if (!bindBlockWritten) WriteBindBlock(output);

        var sb = new StringBuilder();
        foreach (var l in output) sb.AppendLine(l.TrimEnd());
        File.WriteAllText(ConfigPath, sb.ToString());
    }

    private void WriteBindBlock(List<string> output)
    {
        output.Add("enabledevice \"##keyboard\"");
        foreach (var b in _binds.Where(b => b.Device.Equals("##keyboard", StringComparison.OrdinalIgnoreCase)))
            output.Add(FormatBind(b));

        output.Add("enabledevice \"##mouse\"");
        foreach (var b in _binds.Where(b =>
                     b.Device.Equals("##mouse", StringComparison.OrdinalIgnoreCase) &&
                     b.Trigger is not (WheelUp or WheelDown)))
            output.Add(FormatBind(b));

        // Wheel: recombine the two pseudo-directions into rangebind range
        // groups (positive = up, negative = down), one line per direction
        // pair to keep it simple and engine-compatible.
        var ups = _binds.Where(b => b.Trigger == WheelUp).ToList();
        var downs = _binds.Where(b => b.Trigger == WheelDown).ToList();
        int n = Math.Max(ups.Count, downs.Count);
        for (int i = 0; i < n; i++)
        {
            var up = i < ups.Count ? ups[i] : null;
            var down = i < downs.Count ? downs[i] : null;
            var sb = new StringBuilder($"rangebind \"##mouse\" \"{WheelObject}\"");
            if (down is not null) sb.Append($" -10000.000000 -0.100000 \"{down.Action}\"");
            if (up is not null) sb.Append($" 0.100000 10000.000000 \"{up.Action}\"");
            output.Add(sb.ToString());
        }

        foreach (var l in _preservedBindLines)
            output.Add(l);
    }

    /// <summary>Does this line open as a binding, whether or not it parses?</summary>
    private static bool LooksLikeBind(string line)
    {
        var t = line.TrimStart();

        return t.StartsWith("rangebind", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("bind", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Is there a quoted action on this line - anything quoted that is not a
    /// device or a "##scancode" trigger?
    ///
    /// Deliberately generous: it decides whether a line that failed the
    /// grammar is worth KEEPING, and the safe answer to "I am not sure what
    /// this is" is to keep it. Only a line with nothing action-shaped on it
    /// at all is treated as debris.
    /// </summary>
    private static bool HasQuotedAction(string line)
    {
        foreach (Match m in Regex.Matches(line, "\"([^\"]*)\""))
        {
            var v = m.Groups[1].Value.Trim();

            if (v.Length == 0) continue;
            if (v.StartsWith("##", StringComparison.Ordinal)) continue;   // device or scancode
            if (float.TryParse(v, System.Globalization.CultureInfo.InvariantCulture, out _)) continue;

            return true;
        }

        return false;
    }

    private static string FormatBind(BindEntry b) =>
        $"rangebind \"{b.Device}\" \"{b.Trigger}\" {b.RangeLo:0.000000} {b.RangeHi:0.000000} \"{b.Action}\"";

    /// <summary>Human-readable trigger name ("##57" -> "Space", wheel pseudo-triggers, mouse buttons).</summary>
    public static string TriggerDisplay(string device, string trigger)
    {
        if (trigger == WheelUp) return "Wheel Up";
        if (trigger == WheelDown) return "Wheel Down";

        if (trigger.StartsWith("##") && int.TryParse(trigger[2..], out var code))
            return KeybindFile.DikNames.TryGetValue(code, out var n) ? n : $"scan {code}";

        if (device.Equals("##mouse", StringComparison.OrdinalIgnoreCase))
        {
            var m = Regex.Match(trigger, @"(\d+)\s*$");
            if (m.Success) return $"Mouse {int.Parse(m.Groups[1].Value) + 1}";
            return trigger;
        }

        return trigger;
    }
}
