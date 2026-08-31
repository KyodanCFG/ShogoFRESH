using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShogoLauncher.Services;

/// <summary>
/// Manages mods in the game's Custom\ directory (the official overlay
/// mechanism: .rez files and .dat levels placed there are picked up by the
/// engine). Disabled mods are renamed to *.rez.off so the engine ignores
/// them but the file stays in place.
/// </summary>
public class ModManager
{
    public record ModEntry(string Name, string Path, long SizeBytes, bool Enabled);

    /// <summary>
    /// Does this .rez carry game code rather than just assets?
    /// <para>
    /// It matters because -rez is last-wins. A mod shipping CShell.dll or
    /// Object.lto overrides ShogoFRESH's copies wholesale - not a merge, a
    /// replacement - so enabling one silently turns off every ShogoFRESH
    /// game-code change at once. Asset mods (skins, models, sounds, levels)
    /// have no such problem and layer on top quite happily.
    /// </para>
    /// <para>
    /// Two wrong answers preceded this one. First it scanned for the
    /// substrings "OBJECT", "CSHELL", "CRES" and "SRES" anywhere in the file,
    /// on the reasoning that a false alarm was the harmless direction - and a
    /// 28MB map pack scored 4,674 hits on "OBJECT" alone, because level files
    /// are full of object class names. Then it scanned for reversed type tags,
    /// which was the right question asked of the wrong bytes. It now reads the
    /// directory and asks what each entry actually IS.
    /// </para>
    /// <para>
    /// An unreadable archive answers false. It is not game code we can see, we
    /// cannot warn about what we cannot read, and refusing to list the mod at
    /// all would be worse than listing it unannotated.
    /// </para>
    /// </summary>
    public static bool ContainsGameCode(string rezPath)
    {
        var entries = RezArchive.TryRead(rezPath);
        if (entries is null) return false;

        return entries.Any(e => e.Ext is "DLL" or "LTO");
    }

    /// <summary>
    /// The four game files ShogoFRESH's own rez carries, as they appear in an
    /// archive: entry name and type code, because that is what the directory
    /// stores (see <see cref="RezArchive"/>).
    /// </summary>
    private static readonly (string Name, string Ext, string File)[] FreshGameFiles =
    {
        ("CSHELL", "DLL", "CShell.dll"),
        ("OBJECT", "LTO", "Object.lto"),
        ("CRES",   "DLL", "CRes.dll"),
        ("SRES",   "DLL", "SRes.dll"),
    };

    public static int FreshGameFileCount => FreshGameFiles.Length;

    /// <summary>
    /// Which of ShogoFRESH's four game files this archive also carries.
    ///
    /// <para>
    /// "Contains game code" is not the whole question, because -rez resolves
    /// LAST-WINS PER FILE rather than per archive. A mod carrying all four
    /// replaces ShogoFRESH cleanly - you get its game, we step aside. A mod
    /// carrying only some of them produces a MIXTURE: its files win, ours fill
    /// the gaps, and the result is a build neither project has ever run.
    /// </para>
    /// <para>
    /// This is not hypothetical. Squishie 2.2 - a well-known 1999 mod - ships
    /// CShell.dll, CRes.dll and Object.lto but no SRes.dll, so loading it ahead
    /// of ShogoFRESH pairs its server game code with OUR server string
    /// resources. Strings are looked up by number, so nothing crashes; the text
    /// is just quietly wrong. Silent and wrong is the failure worth naming.
    /// </para>
    /// </summary>
    public static List<string> OverlappingGameFiles(string rezPath)
    {
        var entries = RezArchive.TryRead(rezPath);
        if (entries is null) return new List<string>();

        return FreshGameFiles
            .Where(f => entries.Any(e =>
                e.Ext == f.Ext &&
                e.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(f => f.File)
            .ToList();
    }

    /// <summary>The ShogoFRESH game files this archive does NOT carry - the
    /// ones that would survive from our copy and mix with the mod's.</summary>
    public static List<string> MissingGameFiles(string rezPath)
    {
        var have = OverlappingGameFiles(rezPath);

        return FreshGameFiles.Select(f => f.File)
                             .Where(f => !have.Contains(f))
                             .ToList();
    }

    /// <summary>
    /// Multiplayer level names inside a .rez, as <c>Worlds\Multi\NAME</c>.
    ///
    /// <para>
    /// The rotation list used to offer retail maps plus loose
    /// <c>Custom\*.dat</c> files only, so a map pack shipped the normal way -
    /// as a single .rez - had every one of its levels invisible. You could
    /// install it, the engine would happily load the maps, and there was no
    /// way to put one in a rotation without hand-editing ShogoSrv.cfg.
    /// </para>
    /// <para>
    /// The path comes from the archive's own directory tree now rather than
    /// being assumed: a level is any DAT-typed entry under Worlds\Multi. The
    /// two byte-scan versions of this both shipped bugs - the first found zero
    /// entries because it looked for ".dat" when the format stores a reversed
    /// type code, the second dropped whichever map was last in the directory
    /// because it searched forwards for a tag that sits behind the name.
    /// </para>
    /// </summary>
    public static List<string> ListLevels(string rezPath)
    {
        var entries = RezArchive.TryRead(rezPath);
        if (entries is null) return new List<string>();

        var found = entries
            .Where(e => e.Ext == "DAT" &&
                        e.Path.Equals(@"WORLDS\MULTI\", StringComparison.OrdinalIgnoreCase))
            .Select(e => @"Worlds\Multi\" + e.Name.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        found.Sort(StringComparer.OrdinalIgnoreCase);

        return found;
    }

    public string CustomDir { get; }

    public ModManager(string gameDir) => CustomDir = System.IO.Path.Combine(gameDir, "Custom");

    /// <summary>
    /// Mods in <c>Custom\</c>. Archives only — loose <c>.dat</c> maps are not
    /// mods and are listed by the map rotation and the in-game level menus
    /// instead.
    /// </summary>
    /// <remarks>
    /// A newly-seen archive is disabled on sight. Dropping a .rez into
    /// Custom\ and having it silently join the next launch is how a load
    /// order changes without anybody deciding it did — <c>-rez</c> is
    /// last-wins, and a mod carrying game code can take over the whole
    /// install. Turning something on is a choice; it should be made in the
    /// launcher rather than by the filesystem.
    ///
    /// "Seen" is remembered in <c>seen-mods.txt</c> beside the other launcher
    /// data, so this happens once per file and not on every scan. A mod the
    /// player then enables stays enabled, because it is already known.
    /// </remarks>
    public List<ModEntry> ListMods()
    {
        var mods = new List<ModEntry>();
        if (!Directory.Exists(CustomDir)) return mods;

        var seen = LoadSeen();
        var added = false;

        // FIRST RUN IS NOT "EVERYTHING IS NEW".
        //
        // Without this, upgrading to the release that introduced this list
        // finds no seen-mods.txt, decides every mod already installed is new,
        // and turns the lot off. Which is exactly what it did: nine mods
        // disabled at once, and they vanished from the Host tab's rez list in
        // the same moment.
        //
        // So the first scan RECORDS and changes nothing. "New" only means new
        // relative to a list that exists, because that is the only kind of
        // new we can actually observe.

        var firstRun = !File.Exists(SeenPath);

        foreach (var f in Directory.EnumerateFiles(CustomDir))
        {
            var name = System.IO.Path.GetFileName(f);
            var path = f;

            if (name.EndsWith(".rez", StringComparison.OrdinalIgnoreCase))
            {
                // First time we have laid eyes on this one: off it goes.
                if (!firstRun && !seen.Contains(name))
                {
                    seen.Add(name);
                    added = true;

                    try
                    {
                        var off = f + ".off";
                        if (!File.Exists(off))
                        {
                            File.Move(f, off);
                            mods.Add(new ModEntry(name, off, new FileInfo(off).Length, Enabled: false));
                            continue;
                        }
                    }
                    catch (IOException)
                    {
                        // In use, read-only, whatever - list it as it is
                        // rather than failing the whole scan over one file.
                    }
                }

                if (seen.Add(name)) added = true;

                mods.Add(new ModEntry(name, path, new FileInfo(path).Length, Enabled: true));
            }
            else if (name.EndsWith(".rez.off", StringComparison.OrdinalIgnoreCase))
            {
                var bare = name[..^4];
                if (seen.Add(bare)) added = true;

                mods.Add(new ModEntry(bare, path, new FileInfo(path).Length, Enabled: false));
            }
        }

        if (added) SaveSeen(seen);

        return mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string SeenPath => System.IO.Path.Combine(AppPaths.Root, "seen-mods.txt");

    private static HashSet<string> LoadSeen()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (File.Exists(SeenPath))
                foreach (var line in File.ReadAllLines(SeenPath))
                    if (line.Trim().Length > 0) set.Add(line.Trim());
        }
        catch { /* an unreadable list means everything looks new, which is the safe direction */ }

        return set;
    }

    private static void SaveSeen(HashSet<string> seen)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllLines(SeenPath, seen.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }
        catch { /* failing to remember is not a reason to fail to list */ }
    }

    /// <summary>Toggle a mod on/off; returns the updated entry.</summary>
    public ModEntry SetEnabled(ModEntry mod, bool enabled)
    {
        if (mod.Enabled == enabled) return mod;

        var target = enabled
            ? mod.Path[..^4]            // strip ".off"
            : mod.Path + ".off";

        if (File.Exists(target)) throw new IOException($"Target already exists: {target}");
        File.Move(mod.Path, target);
        return mod with { Path = target, Enabled = enabled };
    }

    public void InstallMod(string sourceFile)
    {
        Directory.CreateDirectory(CustomDir);
        var dest = System.IO.Path.Combine(CustomDir, System.IO.Path.GetFileName(sourceFile));
        File.Copy(sourceFile, dest, overwrite: false);
    }
}
