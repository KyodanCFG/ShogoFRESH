using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ShogoLauncher.Services;

/// <summary>
/// Parses Shogo keybind config files (defkeybd.cfg / joystick.cfg).
/// Binding lines look like:
///   rangebind "##keyboard" "##57" 0.000000 0.000000 "Jump"
/// where ##57 is a DirectInput (DIK_*) scancode. Note that retail
/// defkeybd.cfg contains lines with a missing closing quote on the scancode
/// (e.g. "##9 0.000000 ...) — the parser tolerates that, since the engine does.
/// </summary>
public partial class KeybindFile
{
    public record Binding(string Device, int ScanCode, string Action)
    {
        public string KeyName => DikNames.TryGetValue(ScanCode, out var n) ? n : $"scan {ScanCode}";
    }

    [GeneratedRegex("""^\s*rangebind\s+"(?<dev>[^"]+)"\s+"##(?<code>\d+)"?\s+[\d.\-]+\s+[\d.\-]+\s+"(?<action>[^"]+)"\s*$""")]
    private static partial Regex BindLine();

    private readonly List<string> _rawLines = new();
    public List<Binding> Bindings { get; } = new();
    public string Path { get; }

    public KeybindFile(string path)
    {
        Path = path;
        if (!File.Exists(path)) return;

        foreach (var raw in File.ReadAllLines(path))
        {
            _rawLines.Add(raw);
            var m = BindLine().Match(raw);
            if (m.Success)
            {
                Bindings.Add(new Binding(
                    m.Groups["dev"].Value,
                    int.Parse(m.Groups["code"].Value),
                    m.Groups["action"].Value));
            }
        }
    }

    /// <summary>
    /// Rewrite the file with a binding changed to a new scancode.
    /// TODO(scaffold): full editor UI + conflict detection; currently a
    /// straight scancode swap on the matching action line.
    /// </summary>
    public void Rebind(string action, int newScanCode)
    {
        for (int i = 0; i < _rawLines.Count; i++)
        {
            var m = BindLine().Match(_rawLines[i]);
            if (m.Success && m.Groups["action"].Value.Equals(action, StringComparison.OrdinalIgnoreCase))
            {
                _rawLines[i] = _rawLines[i].Replace($"##{m.Groups["code"].Value}", $"##{newScanCode}");
            }
        }
        var sb = new StringBuilder();
        foreach (var l in _rawLines) sb.AppendLine(l.TrimEnd());
        File.WriteAllText(Path, sb.ToString());
    }

    /// <summary>DirectInput DIK_* scancode names (keyboard).</summary>
    public static readonly Dictionary<int, string> DikNames = new()
    {
        [1] = "Esc", [2] = "1", [3] = "2", [4] = "3", [5] = "4", [6] = "5", [7] = "6",
        [8] = "7", [9] = "8", [10] = "9", [11] = "0", [12] = "-", [13] = "=",
        [14] = "Backspace", [15] = "Tab",
        [16] = "Q", [17] = "W", [18] = "E", [19] = "R", [20] = "T", [21] = "Y",
        [22] = "U", [23] = "I", [24] = "O", [25] = "P", [26] = "[", [27] = "]",
        [28] = "Enter", [29] = "Left Ctrl",
        [30] = "A", [31] = "S", [32] = "D", [33] = "F", [34] = "G", [35] = "H",
        [36] = "J", [37] = "K", [38] = "L", [39] = ";", [40] = "'", [41] = "`",
        [42] = "Left Shift", [43] = "\\",
        [44] = "Z", [45] = "X", [46] = "C", [47] = "V", [48] = "B", [49] = "N",
        [50] = "M", [51] = ",", [52] = ".", [53] = "/", [54] = "Right Shift",
        [55] = "Numpad *", [56] = "Left Alt", [57] = "Space", [58] = "Caps Lock",
        [59] = "F1", [60] = "F2", [61] = "F3", [62] = "F4", [63] = "F5",
        [64] = "F6", [65] = "F7", [66] = "F8", [67] = "F9", [68] = "F10",
        [69] = "Num Lock", [70] = "Scroll Lock",
        [71] = "Numpad 7", [72] = "Numpad 8", [73] = "Numpad 9", [74] = "Numpad -",
        [75] = "Numpad 4", [76] = "Numpad 5", [77] = "Numpad 6", [78] = "Numpad +",
        [79] = "Numpad 1", [80] = "Numpad 2", [81] = "Numpad 3", [82] = "Numpad 0",
        [83] = "Numpad .", [87] = "F11", [88] = "F12",
        [156] = "Numpad Enter", [157] = "Right Ctrl", [181] = "Numpad /",
        [183] = "PrtScr", [184] = "Right Alt", [197] = "Pause",
        [199] = "Home", [200] = "Up", [201] = "Page Up", [203] = "Left",
        [205] = "Right", [207] = "End", [208] = "Down", [209] = "Page Down",
        [210] = "Insert", [211] = "Delete",
    };
}
