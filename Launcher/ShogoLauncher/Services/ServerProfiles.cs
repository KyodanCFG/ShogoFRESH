using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShogoLauncher.Services;

/// <summary>
/// Named copies of ShogoSrv.cfg.
///
/// A profile is just the config file itself rather than a parsed subset, so
/// anything hand-edited into ShogoSrv.cfg - including vars the launcher has
/// no UI for - survives a round trip. Saving copies the live config out;
/// loading copies one back over it and re-reads the Host tab.
/// </summary>
public static class ServerProfiles
{
    public static string Dir => Path.Combine(AppPaths.Root, "ServerProfiles");

    /// <summary>Profile names (no extension), alphabetical.</summary>
    public static List<string> List()
    {
        try
        {
            if (!Directory.Exists(Dir)) return new List<string>();

            return Directory.GetFiles(Dir, "*.cfg")
                            .Select(Path.GetFileNameWithoutExtension)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Select(n => n!)
                            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                            .ToList();
        }
        catch (IOException) { return new List<string>(); }
    }

    public static string PathFor(string name) => Path.Combine(Dir, Sanitize(name) + ".cfg");

    public static bool Exists(string name) => File.Exists(PathFor(name));

    /// <summary>Copy the live ShogoSrv.cfg into a named profile.</summary>
    public static void Save(string gameDir, string name)
    {
        Directory.CreateDirectory(Dir);
        File.Copy(Path.Combine(gameDir, "ShogoSrv.cfg"), PathFor(name), overwrite: true);
    }

    /// <summary>Copy a named profile over the live ShogoSrv.cfg.</summary>
    public static void Load(string gameDir, string name)
    {
        File.Copy(PathFor(name), Path.Combine(gameDir, "ShogoSrv.cfg"), overwrite: true);
    }

    public static void Delete(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
    }

    public static void Export(string name, string destPath) =>
        File.Copy(PathFor(name), destPath, overwrite: true);

    /// <summary>Bring an outside .cfg in as a profile; returns the name it was filed under.</summary>
    public static string Import(string sourcePath)
    {
        Directory.CreateDirectory(Dir);

        var name = Sanitize(Path.GetFileNameWithoutExtension(sourcePath));
        if (string.IsNullOrWhiteSpace(name)) name = "Imported";

        // Don't quietly clobber an existing profile of the same name.
        var candidate = name;
        for (int i = 2; File.Exists(PathFor(candidate)); i++) candidate = $"{name} ({i})";

        File.Copy(sourcePath, PathFor(candidate));

        return candidate;
    }

    private static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";

        var cleaned = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());

        return cleaned.Trim();
    }
}
