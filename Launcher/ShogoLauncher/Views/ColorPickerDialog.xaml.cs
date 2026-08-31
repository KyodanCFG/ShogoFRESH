using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShogoLauncher.Views;

/// <summary>
/// Picker for the engine's "R G B" world-color strings, where each channel
/// is a 0-1 float (e.g. WorldColorNight ".2 .2 .9"). WPF has no built-in
/// color picker and this format isn't hex, so it's three channel sliders
/// with a live preview.
/// </summary>
public partial class ColorPickerDialog : Window
{
    public string ColorString { get; private set; }

    public ColorPickerDialog(string current)
    {
        InitializeComponent();

        var (r, g, b) = Parse(current);
        RedSlider.Value = r;
        GreenSlider.Value = g;
        BlueSlider.Value = b;
        ColorString = Format(r, g, b);
        UpdatePreview();
    }

    /// <summary>Parse "R G B" floats; falls back to neutral grey.</summary>
    public static (double R, double G, double B) Parse(string s)
    {
        var parts = (s ?? "").Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
        double Get(int i) =>
            parts.Length > i && double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? System.Math.Clamp(v, 0, 1)
                : 0.5;
        return (Get(0), Get(1), Get(2));
    }

    public static Color ToColor(string s)
    {
        var (r, g, b) = Parse(s);
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static string Format(double r, double g, double b) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1:0.##} {2:0.##}", r, g, b);

    private void UpdatePreview()
    {
        if (Preview is null) return;
        Preview.Background = new SolidColorBrush(Color.FromRgb(
            (byte)(RedSlider.Value * 255),
            (byte)(GreenSlider.Value * 255),
            (byte)(BlueSlider.Value * 255)));
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePreview();

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string tag) return;
        var (r, g, b) = Parse(tag);
        RedSlider.Value = r;
        GreenSlider.Value = g;
        BlueSlider.Value = b;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ColorString = Format(RedSlider.Value, GreenSlider.Value, BlueSlider.Value);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
