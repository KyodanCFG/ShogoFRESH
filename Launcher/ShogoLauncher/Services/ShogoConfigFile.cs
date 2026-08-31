using System.IO;
using System.Text;

namespace ShogoLauncher.Services;

/// <summary>
/// Reads/writes Shogo's console-var config files (autoexec.cfg, DetailHi.cfg,
/// ShogoSrv.cfg). Format is one variable per line:  "VarName" "value"
/// Unknown/other lines are preserved verbatim so we never destroy anything
/// the engine wrote.
/// </summary>
public class ShogoConfigFile
{
    private record Line(string Raw, string? Key, string? Value);

    private readonly List<Line> _lines = new();
    public string Path { get; }

    public ShogoConfigFile(string path)
    {
        Path = path;
        if (!File.Exists(path)) return;

        foreach (var raw in File.ReadAllLines(path))
        {
            var parsed = Parse(raw);
            _lines.Add(parsed);
        }
    }

    private static Line Parse(string raw)
    {
        // "Key" "value"  (both always quoted in engine-written files)
        var t = raw.Trim();
        if (t.StartsWith('"'))
        {
            int k2 = t.IndexOf('"', 1);
            if (k2 > 1)
            {
                int v1 = t.IndexOf('"', k2 + 1);
                int v2 = v1 >= 0 ? t.IndexOf('"', v1 + 1) : -1;
                if (v1 >= 0 && v2 > v1)
                {
                    return new Line(raw, t[1..k2], t[(v1 + 1)..v2]);
                }
            }
        }
        return new Line(raw, null, null);
    }

    public string? Get(string key) =>
        _lines.LastOrDefault(l => string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

    public float GetFloat(string key, float fallback) =>
        float.TryParse(Get(key), out var f) ? f : fallback;

    public int GetInt(string key, int fallback) =>
        (int)GetFloat(key, fallback);

    /// <summary>Set a variable, replacing the existing line or appending a new one.</summary>
    public void Set(string key, string value)
    {
        var newLine = new Line($"\"{key}\" \"{value}\"", key, value);
        for (int i = 0; i < _lines.Count; i++)
        {
            if (string.Equals(_lines[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                _lines[i] = newLine;
                return;
            }
        }
        _lines.Add(newLine);
    }

    public void Set(string key, float value) => Set(key, value.ToString("0.000000"));
    public void Set(string key, int value) => Set(key, value.ToString());

    public void Remove(string key) =>
        _lines.RemoveAll(l => string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<KeyValuePair<string, string>> All() =>
        _lines.Where(l => l.Key is not null)
              .Select(l => new KeyValuePair<string, string>(l.Key!, l.Value ?? ""));

    public void Save()
    {
        var sb = new StringBuilder();
        foreach (var l in _lines) sb.AppendLine(l.Raw.TrimEnd());
        File.WriteAllText(Path, sb.ToString());
    }
}
