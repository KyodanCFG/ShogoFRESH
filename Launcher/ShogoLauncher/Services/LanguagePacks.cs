using System.IO;

namespace ShogoLauncher.Services;

/// <summary>
/// Language packs: string-overlay files (Custom\Strings\*.txt, see
/// Docs/public/LOCALIZATION.md) that the launcher ships and installs.
///
/// A language IS just a file - drop deutsch.txt in Custom\Strings\ and the
/// game speaks German on the next world load, no DLL, no restart. What makes
/// this a launcher feature is the merge rule: the game merges EVERY file in
/// Strings\, and two full language packs present at once contest all 1,049
/// stock ids with the winner decided by enumeration order. So exactly one
/// pack may be installed at a time, and a picker that swaps files is the
/// right owner of that rule.
///
/// Ownership is by NAME: the launcher removes only files that exist in its
/// own Languages\ folder. A mapper's MyMod.txt (ids 50000+) or a community
/// translation under another name is never touched - mod strings and a
/// language coexist because their ids cannot collide.
/// </summary>
public static class LanguagePacks
{
    public record Choice(string Display, string? FileName)
    {
        public override string ToString() => Display;
    }

    public static string PacksDir =>
        Path.Combine(AppContext.BaseDirectory, "Languages");

    private static string StringsDir(string gameDir) =>
        Path.Combine(gameDir, "Custom", "Strings");

    /// <summary>
    /// "English (stock)" plus one entry per shipped pack. The display name
    /// comes from a "# language: X" line in the pack's header, so a pack
    /// names itself and adding one is a file drop, not a code change.
    /// </summary>
    public static List<Choice> Available()
    {
        var list = new List<Choice> { new("English (stock)", null) };
        if (!Directory.Exists(PacksDir)) return list;

        foreach (var path in Directory.GetFiles(PacksDir, "*.txt").OrderBy(p => p))
        {
            string display = Path.GetFileNameWithoutExtension(path);
            // Latin1, not the UTF-8 default: packs are Windows-1252 (the
            // game reads raw bytes through cp1252 fonts), and a UTF-8 read
            // would turn the n-tilde in "Espanol"'s display name into a
            // replacement character.
            foreach (var line in File.ReadLines(path, System.Text.Encoding.Latin1).Take(10))
            {
                var t = line.TrimStart('#', ' ', '\t');
                if (t.StartsWith("language:", StringComparison.OrdinalIgnoreCase))
                {
                    display = t["language:".Length..].Trim();
                    break;
                }
            }
            list.Add(new Choice(display, Path.GetFileName(path)));
        }
        return list;
    }

    /// <summary>
    /// Install <paramref name="fileName"/> (null = stock English) and remove
    /// every OTHER launcher-owned pack. Copy rather than move, and always
    /// overwrite: a launcher update carries corrected packs, and the copy in
    /// the game folder must not be older than the launcher that claims to
    /// have installed it.
    /// </summary>
    public static void Apply(string gameDir, string? fileName)
    {
        var dir = StringsDir(gameDir);
        var owned = Directory.Exists(PacksDir)
            ? Directory.GetFiles(PacksDir, "*.txt").Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string?>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir, "*.txt"))
            {
                var name = Path.GetFileName(f);
                if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase)) continue;

                // Owned by NAME - or by SELF-DECLARATION. A pack names
                // itself with a "# language:" header, and a file that says
                // "I am a language pack" is bound by the one-pack rule
                // whatever it is called. Ownership-by-current-list alone
                // left a legacy english.txt (a name a former build shipped
                // and this one does not) contesting all 1,049 stock ids
                // with whichever pack was picked - the winner fell to
                // engine enumeration order, so German loaded and Spanish
                // did not (2026-09-03). A mapper's MyMod.txt has no such
                // header and ids 50000+ anyway; it is never touched.

                if (owned.Contains(name) || DeclaresItselfALanguagePack(f))
                {
                    File.Delete(f);
                }
            }
        }

        if (fileName is null) return;
        Directory.CreateDirectory(dir);
        File.Copy(Path.Combine(PacksDir, fileName), Path.Combine(dir, fileName), overwrite: true);
    }

    // Does this file's header say "# language: ..."? Same ten-line,
    // Latin1 read as Available()'s display-name scan, so the two cannot
    // disagree about what counts as a pack.
    private static bool DeclaresItselfALanguagePack(string path)
    {
        try
        {
            foreach (var line in File.ReadLines(path, System.Text.Encoding.Latin1).Take(10))
            {
                var t = line.TrimStart('#', ' ', '\t');
                if (t.StartsWith("language:", StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch { /* unreadable = not ours to judge, leave it */ }
        return false;
    }

    /// <summary>
    /// Startup path: re-assert the saved choice, but ONLY when one is saved.
    /// With no choice this must do nothing at all - deleting pack-named
    /// files a person placed by hand, on a preference they never set, is
    /// exactly the kind of tidying that loses somebody's work.
    /// </summary>
    public static void Refresh(string gameDir, string languagePack)
    {
        if (string.IsNullOrEmpty(languagePack)) return;
        try { Apply(gameDir, languagePack); }
        catch { /* a locked or missing file must not stop the launcher opening */ }
    }
}
