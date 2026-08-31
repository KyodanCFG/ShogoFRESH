using System.IO;
using Microsoft.Win32;

namespace ShogoLauncher.Services;

/// <summary>
/// Finds the Shogo installation directory (Client.exe + SHOGO.REZ).
/// </summary>
public static class GameLocator
{
    private static readonly string[] CandidatePaths =
    {
        @"C:\Program Files (x86)\Steam\steamapps\common\Shogo Mobile Armor Division",
        @"C:\Program Files\Steam\steamapps\common\Shogo Mobile Armor Division",
        @"C:\GOG Games\Shogo",
        @"C:\Games\Shogo",
    };

    public static bool IsValidGameDir(string? dir) =>
        !string.IsNullOrWhiteSpace(dir)
        && File.Exists(Path.Combine(dir, "Client.exe"))
        && File.Exists(Path.Combine(dir, "SHOGO.REZ"));

    /// <summary>
    /// Turn a folder somebody picked into a game directory, forgiving the two
    /// near misses people actually make, and explaining the failure when it
    /// is not one of them.
    ///
    /// <para>
    /// The near misses are picking the PARENT (<c>steamapps\common</c>, or a
    /// <c>Games</c> folder) and picking a CHILD (<c>Custom\</c>, which the
    /// modding guides send people into). Both are one hop from right, and
    /// answering "that folder is not a Shogo installation" to either is
    /// technically true and useless - the folder they meant is in plain
    /// sight. Only one level is searched in each direction: past that this
    /// stops being a correction and becomes a disk scan.
    /// </para>
    /// <para>
    /// When nothing is found, <paramref name="problem"/> names the file that
    /// was missing rather than saying "invalid". A folder holding Client.exe
    /// but no SHOGO.REZ is a broken or partial install and the person needs
    /// to know which of the two it is.
    /// </para>
    /// </summary>
    /// <returns>The resolved directory, or null.</returns>
    public static string? ResolvePickedDir(string? picked, out string problem)
    {
        problem = "";

        if (string.IsNullOrWhiteSpace(picked) || !Directory.Exists(picked))
        {
            problem = "That folder does not exist.";
            return null;
        }

        if (IsValidGameDir(picked)) return picked;

        // One level down: they picked the library folder, not the game.
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(picked))
                if (IsValidGameDir(sub)) return sub;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        // One level up: they picked Custom\, or Sound\, inside the install.
        try
        {
            var parent = Directory.GetParent(picked)?.FullName;
            if (IsValidGameDir(parent)) return parent;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        bool hasExe = File.Exists(Path.Combine(picked, "Client.exe"));
        bool hasRez = File.Exists(Path.Combine(picked, "SHOGO.REZ"));

        problem = (hasExe, hasRez) switch
        {
            (true, false) => "That folder has Client.exe but no SHOGO.REZ, so the install is "
                           + "incomplete. Verify or reinstall the game and try again.",
            (false, true) => "That folder has SHOGO.REZ but no Client.exe, so the install is "
                           + "incomplete. Verify or reinstall the game and try again.",
            _             => "No Shogo installation there. The folder wanted is the one holding "
                           + "Client.exe and SHOGO.REZ - usually named "
                           + "\"Shogo Mobile Armor Division\".",
        };

        return null;
    }

    /// <summary>
    /// Locate the game: explicit setting first, then Steam library folders,
    /// then GOG's registry, then well-known paths.
    /// </summary>
    public static string? Locate(string? configuredPath = null)
    {
        if (IsValidGameDir(configuredPath)) return configuredPath;

        foreach (var steamDir in EnumerateSteamLibraries())
        {
            var p = Path.Combine(steamDir, "steamapps", "common", "Shogo Mobile Armor Division");
            if (IsValidGameDir(p)) return p;
        }

        foreach (var p in EnumerateGogInstalls())
            if (IsValidGameDir(p)) return p;

        foreach (var p in CandidatePaths)
            if (IsValidGameDir(p)) return p;

        return null;
    }

    /// <summary>
    /// Install paths GOG Galaxy recorded, from HKLM\SOFTWARE\...\GOG.com\Games.
    ///
    /// Every installed game gets a subkey named after its product id with a
    /// "path" value. We enumerate all of them and let IsValidGameDir decide
    /// rather than hardcoding Shogo's id: the id is not documented anywhere
    /// we control, it differs between regional releases, and a wrong guess
    /// fails silently. Checking for Client.exe + SHOGO.REZ cannot false-match.
    ///
    /// Before this, GOG had a single hardcoded guess (C:\GOG Games\Shogo) and
    /// any other install location had to be pointed at by hand, while Steam
    /// got full library enumeration.
    /// </summary>
    private static IEnumerable<string> EnumerateGogInstalls()
    {
        // Both views: the 32-bit node is where GOG writes on 64-bit Windows,
        // but a 32-bit OS or an older Galaxy uses the plain path.
        string[] roots =
        {
            @"SOFTWARE\WOW6432Node\GOG.com\Games",
            @"SOFTWARE\GOG.com\Games",
        };

        foreach (var root in roots)
        {
            RegistryKey? games = null;
            try { games = Registry.LocalMachine.OpenSubKey(root); }
            catch { /* registry unavailable or access denied */ }

            if (games is null) continue;

            using (games)
            {
                string[] ids;
                try { ids = games.GetSubKeyNames(); }
                catch { continue; }

                foreach (var id in ids)
                {
                    string? path = null;

                    try
                    {
                        using var game = games.OpenSubKey(id);
                        // Galaxy has used both spellings over the years.
                        path = (game?.GetValue("path") ?? game?.GetValue("PATH")) as string;
                    }
                    catch { /* skip this entry */ }

                    if (!string.IsNullOrWhiteSpace(path)) yield return path;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateSteamLibraries()
    {
        string? steamRoot = null;
        try
        {
            steamRoot = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        }
        catch { /* registry unavailable */ }

        if (steamRoot is null || !Directory.Exists(steamRoot)) yield break;
        yield return steamRoot;

        // Additional libraries are listed in libraryfolders.vdf ("path" "D:\\SteamLibrary").
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;

        foreach (var line in File.ReadLines(vdf))
        {
            var t = line.Trim();
            if (!t.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = t.Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && Directory.Exists(parts[^1].Replace(@"\\", @"\")))
                yield return parts[^1].Replace(@"\\", @"\");
        }
    }
}
