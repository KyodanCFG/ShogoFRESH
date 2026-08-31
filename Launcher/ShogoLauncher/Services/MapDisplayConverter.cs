using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace ShogoLauncher.Services;

/// <summary>
/// Display form for map rotation entries. The stored value is ALWAYS the
/// canonical form the server loads (bare name for customs - fact 22 - or the
/// Worlds\Multi\ path for retail); this converter only changes how it looks in
/// the two map lists, never what is written to ShogoSrv.cfg.
///
///  - retail:  Worlds\Multi\DM_Foo   ->  DM_Foo *
///  - custom:  MP_ArenaOF            ->  Custom\maps\mp\MP_ArenaOF
///
/// The folder for a custom map is not knowable from the name (the mounts
/// flatten every source to one bare world), so <see cref="FolderByBare"/> is
/// filled by the Host-tab scan, which is the only place that still sees which
/// directory - or which .rez - each map actually came from. A map with no
/// entry (e.g. one named in the cfg but no longer on disk) shows bare.
/// </summary>
public class MapDisplayConverter : IValueConverter
{
    private const string RetailPrefix = @"Worlds\Multi\";

    /// <summary>bare map name -> its on-disk location prefix, e.g. "Custom\maps\mp".
    /// Rebuilt each Host-tab scan. Display-only; case-insensitive.</summary>
    public static readonly Dictionary<string, string> FolderByBare =
        new(System.StringComparer.OrdinalIgnoreCase);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value as string ?? "";

        if (s.StartsWith(RetailPrefix, StringComparison.OrdinalIgnoreCase))
            return s[RetailPrefix.Length..] + " *";

        // A rotation entry may carry a ":mode" suffix (world:mode). Split it
        // off so the lookup sees the world, then put it back on the display.
        int colon = s.IndexOf(':');
        string world = colon >= 0 ? s[..colon] : s;
        string suffix = colon >= 0 ? s[colon..] : "";

        // Reduce any stale "Custom\..." prefix an old cfg may still hold to the
        // bare name the mounts actually produce - the same key the scan stored.
        int bs = world.LastIndexOf('\\');
        string bare = bs >= 0 ? world[(bs + 1)..] : world;

        return FolderByBare.TryGetValue(bare, out var folder) && folder.Length > 0
            ? folder + "\\" + bare + suffix
            : bare + suffix;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
