using System.ComponentModel;
using System.Windows;

namespace ShogoLauncher.Views;

/// <summary>
/// Editor for the individual detail variables. Enum-valued vars get labeled
/// dropdowns; BulletHoles (the only open-ended one) stays a numeric field.
/// </summary>
public partial class DetailDialog : Window
{
    public record Option(float Value, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>Display label + allowed choices per engine var.</summary>
    private static readonly Dictionary<string, (string Label, Option[]? Options)> Meta = new()
    {
        ["ModelLOD"]            = ("Model detail (LOD)",      new[] { new Option(0, "Low"), new Option(1, "Medium"), new Option(2, "High") }),
        ["MaxModelShadows"]     = ("Model shadows",           new[] { new Option(0, "Off"), new Option(1, "On") }),
        ["BulletHoles"]         = ("Bullet hole decals",      null), // numeric 0-2000
        ["TextureDetail"]       = ("Texture detail",          new[] { new Option(0, "Low"), new Option(1, "Medium"), new Option(2, "High") }),
        ["DynamicLightSetting"] = ("Dynamic lighting",        new[] { new Option(0, "Off"), new Option(1, "Reduced"), new Option(2, "Full") }),
        ["LightMap"]            = ("Light mapping",           new[] { new Option(0, "Off"), new Option(1, "On") }),
        ["SpecialFX"]           = ("Special effects",         new[] { new Option(0, "Low"), new Option(1, "Medium"), new Option(2, "High") }),
        ["EnvMapEnable"]        = ("Environment mapping",     new[] { new Option(0, "Off"), new Option(1, "On") }),
        ["ModelFullbrite"]      = ("Model full-bright",       new[] { new Option(0, "Off"), new Option(1, "On") }),
        ["PVWeapons"]           = ("Reduced weapon models",   new[] { new Option(0, "Off"), new Option(1, "On") }),
        ["PolyGrids"]           = ("Poly grids (water etc.)", new[] { new Option(0, "Off"), new Option(1, "On") }),
        ["CloudMapLight"]       = ("Cloud light mapping",     new[] { new Option(0, "Off"), new Option(1, "On") }),
    };

    public class VarRow : INotifyPropertyChanged
    {
        public string Name { get; init; } = "";
        public string DisplayLabel { get; init; } = "";
        public string RangeHint { get; init; } = "";
        public Option[]? Options { get; init; }
        public bool HasOptions => Options is not null;

        private Option? _selectedOption;
        public Option? SelectedOption
        {
            get => _selectedOption;
            set { _selectedOption = value; Raise(nameof(SelectedOption)); }
        }

        private string _value = "";
        public string Value
        {
            get => _value;
            set { _value = value; Raise(nameof(Value)); }
        }

        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public List<VarRow> Rows { get; }

    public DetailDialog(IEnumerable<(string Name, string Value)> values)
    {
        InitializeComponent();
        Rows = values.Select(v =>
        {
            var (label, options) = Meta.TryGetValue(v.Name, out var m) ? m : (v.Name, null);
            float.TryParse(v.Value, out var current);

            return new VarRow
            {
                Name = v.Name,
                DisplayLabel = label,
                Options = options,
                SelectedOption = options?.MinBy(o => Math.Abs(o.Value - current)),
                Value = v.Value,
                RangeHint = options is null && Services.DetailPresets.Ranges.TryGetValue(v.Name, out var r)
                    ? $"{r.Min:0} – {r.Max:0}" : "",
            };
        }).ToList();
        VarsList.ItemsSource = Rows;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in Rows)
        {
            if (row.HasOptions && row.SelectedOption is not null)
                row.Value = row.SelectedOption.Value.ToString("0.###");
            else if (float.TryParse(row.Value, out var f))
                row.Value = Services.DetailPresets.Clamp(row.Name, f).ToString("0.###");
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
