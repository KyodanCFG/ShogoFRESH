using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ShogoLauncher.Services;

/// <summary>
/// Line-preserving editor for dgVoodoo.conf (the dgVoodoo2 DirectX wrapper
/// installed by the community ShogoFix package). Only the display-output
/// keys are managed; everything else - comments, spacing, other sections -
/// is preserved byte-for-byte.
///
/// The 1998 engine only knows exclusive fullscreen and picks an adapter in
/// its own Display Settings dialog. With dgVoodoo wrapping DirectDraw, that
/// control moves here:
///   [General] FullScreenMode    = true/false        (fullscreen vs windowed)
///   [General] FullScreenOutput  = default | 1,2,... (which monitor)
///   [GeneralExt] WindowedAttributes = borderless, fullscreensize, alwaysontop
///   [DirectX] AppControlledScreenMode = true/false  (who owns the mode)
///
/// "Borderless fullscreen" is dgVoodoo-managed fullscreen (FullScreenMode
/// true) with AppControlledScreenMode OFF, NOT a forced borderless window -
/// the window attributes produced an off-centre, non-filling window. That
/// makes AppControlledScreenMode the only thing separating it from true
/// fullscreen, which is why the Mode getter has to read it.
/// </summary>
public class DgVoodooConfig
{
    public enum DisplayMode { Fullscreen, Windowed, BorderlessFullscreen }

    private readonly List<string> _lines = new();
    public string Path { get; }
    public bool Present { get; }

    public DgVoodooConfig(string gameDir)
    {
        Path = System.IO.Path.Combine(gameDir, "dgVoodoo.conf");
        Present = File.Exists(Path);
        if (Present) _lines.AddRange(File.ReadAllLines(Path));
    }

    public DisplayMode Mode
    {
        get
        {
            var fullscreen = !Get("General", "FullScreenMode").Equals("false", StringComparison.OrdinalIgnoreCase);

            if (!fullscreen)
            {
                // Older builds wrote borderless as a forced window; still read.
                var attrs = Get("GeneralExt", "WindowedAttributes");
                return attrs.Contains("fullscreensize", StringComparison.OrdinalIgnoreCase)
                    ? DisplayMode.BorderlessFullscreen
                    : DisplayMode.Windowed;
            }

            // FullScreenMode=true is written by BOTH fullscreen modes, so it
            // cannot tell them apart on its own. What separates them is who
            // owns the mode switch, and that is AppControlledScreenMode.
            //
            // Reading it back as plain Fullscreen was not a cosmetic bug: the
            // UI then showed "Fullscreen", and the next Save wrote the
            // Fullscreen branch - which turns AppControlledScreenMode back ON
            // and hands the display to the engine's exclusive-mode request.
            // A borderless setup therefore degraded into true exclusive
            // fullscreen on its own, which is what makes alt-tab minimise the
            // game and what stops surfaces being created in the background.

            var appControlled = !Get("DirectX", "AppControlledScreenMode")
                                    .Equals("false", StringComparison.OrdinalIgnoreCase);

            return appControlled ? DisplayMode.Fullscreen : DisplayMode.BorderlessFullscreen;
        }
        set
        {
            // AppControlledScreenMode must be OFF for anything but
            // fullscreen: with it on, dgVoodoo honors the GAME's request
            // (LithTech always asks for exclusive fullscreen) and our
            // windowed settings are ignored - the window still grabs the
            // display and alt-tab minimizes it.
            Set("DirectX", "AppControlledScreenMode", value == DisplayMode.Fullscreen ? "true" : "false");

            // Let Alt+Enter switch modes at runtime - handy when a windowed
            // session needs to go fullscreen (or vice versa) mid-test.
            Set("DirectX", "DisableAltEnterToToggleScreenMode", "false");

            // Center the window in the non-fullscreen modes; the shipped
            // conf leaves this off, which is why a borderless window landed
            // off to one side.
            Set("General", "CenterAppWindow", value == DisplayMode.Fullscreen ? "false" : "true");

            switch (value)
            {
                case DisplayMode.Fullscreen:
                    Set("General", "FullScreenMode", "true");
                    Set("GeneralExt", "WindowedAttributes", "");
                    Set("General", "KeepWindowAspectRatio", "true");
                    break;

                case DisplayMode.Windowed:
                    Set("General", "FullScreenMode", "false");
                    Set("GeneralExt", "WindowedAttributes", "");
                    Set("General", "KeepWindowAspectRatio", "true");
                    break;

                case DisplayMode.BorderlessFullscreen:
                    // dgVoodoo-managed fullscreen rather than a forced
                    // "borderless,fullscreensize" window: the window
                    // attributes produced a borderless but off-centre,
                    // non-filling window, while dgVoodoo's own fullscreen
                    // (what Alt+Enter switches to) fills the display
                    // correctly and still alt-tabs, because
                    // AppControlledScreenMode is off so the engine's
                    // exclusive-mode request is ignored.
                    Set("General", "FullScreenMode", "true");
                    Set("GeneralExt", "WindowedAttributes", "");
                    Set("General", "KeepWindowAspectRatio", "false");
                    break;
            }
        }
    }

    /// <summary>"default" or a 1-based monitor ordinal on the adapter.</summary>
    public string Monitor
    {
        get => Get("General", "FullScreenOutput") is { Length: > 0 } v ? v : "default";
        set => Set("General", "FullScreenOutput", value);
    }

    /// <summary>
    /// Make modern resolutions selectable by the 1998 engine.
    ///
    /// dgVoodoo only enumerates a fixed "classic" mode list by default
    /// (640x480 ... 1024x768), so the engine never sees the native
    /// resolution and silently falls back - which is why a resolution
    /// written into autoexec.cfg appeared not to apply. ExtraEnumeratedResolutions
    /// adds modes to what the app is shown (max 16 entries); "max" is the
    /// desktop resolution, and the aspect-derived forms cover the rest.
    /// </summary>
    /// <summary>
    /// 32-bit internal rendering: dgVoodoo renders at full precision instead
    /// of emulating the 16-bit dither the 1998 mode list implies. ShogoFix's
    /// shipped conf already enables this; the toggle exists to switch the
    /// authentic 16-bit look back on.
    /// </summary>
    public bool Pure32Bit
    {
        get => Get("DirectXExt", "Dithering").Equals("forcealways", StringComparison.OrdinalIgnoreCase)
            && Get("DirectXExt", "DitheringEffect").Equals("pure32bit", StringComparison.OrdinalIgnoreCase);
        set
        {
            Set("DirectXExt", "DitheringEffect", value ? "pure32bit" : "ordered4x4");
            Set("DirectXExt", "Dithering", value ? "forcealways" : "appdriven");
        }
    }

    /// <summary>
    /// Vertical sync ([DirectX] ForceVerticalSync).
    ///
    /// Shogo renders through DirectDraw/Direct3D, so the [DirectX] key is the
    /// one that matters here; the [Glide] copy beside it is for a renderer
    /// this game never uses. Off by default because that is how dgVoodoo
    /// ships - but a 1998 engine on modern hardware runs at hundreds of
    /// frames a second, which is exactly the condition tearing shows up in.
    /// </summary>
    public bool VerticalSync
    {
        get => Get("DirectX", "ForceVerticalSync").Equals("true", StringComparison.OrdinalIgnoreCase);
        set => Set("DirectX", "ForceVerticalSync", value ? "true" : "false");
    }

    /// <summary>
    /// Confine the cursor to the game window ([General] CaptureMouse).
    /// On for normal play; off lets the cursor leave the window, which is
    /// what makes windowed mode usable for moving/resizing windows and
    /// running two clients side by side.
    /// </summary>
    public bool CaptureMouse
    {
        get => !Get("General", "CaptureMouse").Equals("false", StringComparison.OrdinalIgnoreCase);
        set
        {
            Set("General", "CaptureMouse", value ? "true" : "false");
            // FreeMouse is the [GeneralExt] counterpart for scaled/forced
            // resolutions; keep the two consistent.
            Set("GeneralExt", "FreeMouse", value ? "false" : "true");
        }
    }

    /// <summary>[DirectX] Filtering: appdriven | bilinear | linearmip | trilinear | 2..16 (AF level).</summary>
    public string Filtering
    {
        get => Get("DirectX", "Filtering") is { Length: > 0 } v ? v : "appdriven";
        set => Set("DirectX", "Filtering", value);
    }

    /// <summary>[DirectX] Antialiasing: appdriven | 2x | 4x | 8x.</summary>
    public string Antialiasing
    {
        get => Get("DirectX", "Antialiasing") is { Length: > 0 } v ? v : "appdriven";
        set => Set("DirectX", "Antialiasing", value);
    }

    public void EnableModernResolutions()
    {
        Set("DirectXExt", "DefaultEnumeratedResolutions", "all");
        Set("DirectXExt", "ExtraEnumeratedResolutions", "max, max_16_9, max_4_3, 2560x1440, 1920x1080, 1600x900, 1366x768, 1280x720");
        Set("DirectXExt", "EnumeratedResolutionBitdepths", "all");
    }

    public void Save() =>
        File.WriteAllText(Path, string.Join(Environment.NewLine, _lines) + Environment.NewLine);

    private string Get(string section, string key)
    {
        bool inSection = false;
        foreach (var line in _lines)
        {
            if (IsSectionHeader(line, out var name)) { inSection = name.Equals(section, StringComparison.OrdinalIgnoreCase); continue; }
            if (!inSection) continue;
            var m = KeyLine(key).Match(line);
            if (m.Success) return m.Groups["val"].Value.Trim();
        }
        return "";
    }

    private void Set(string section, string key, string value)
    {
        bool inSection = false;
        int sectionEnd = -1;
        for (int i = 0; i < _lines.Count; i++)
        {
            if (IsSectionHeader(_lines[i], out var name))
            {
                if (inSection) { sectionEnd = i; break; }
                inSection = name.Equals(section, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inSection) continue;

            var m = KeyLine(key).Match(_lines[i]);
            if (m.Success)
            {
                _lines[i] = m.Groups["head"].Value + value;
                return;
            }
            sectionEnd = i + 1;
        }
        if (sectionEnd >= 0) _lines.Insert(sectionEnd, $"{key,-37}= {value}");
    }

    private static bool IsSectionHeader(string line, out string name)
    {
        var m = Regex.Match(line, @"^\s*\[(?<n>[^\]]+)\]\s*$");
        name = m.Success ? m.Groups["n"].Value : "";
        return m.Success;
    }

    private static Regex KeyLine(string key) =>
        new($@"^(?<head>\s*{Regex.Escape(key)}\s*=\s*)(?<val>.*)$", RegexOptions.IgnoreCase);
}
