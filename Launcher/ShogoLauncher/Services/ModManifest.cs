using System.IO;
using System.Text;

namespace ShogoLauncher.Services;

/// <summary>
/// Reads the manifest a mod ships inside its .rez — see
/// <c>Shared/FreshManifest.h</c> for the format and Docs/MODLAYER.md for why
/// a mod is data rather than a second Object.lto.
///
/// <para>
/// This is the launcher's half of stage 2. Before manifests there was no way
/// to know anything about a mod at all: the RezMgr format has no author
/// field, no description, no version — nothing but a fixed banner and a
/// directory of typed entries. A mod was a filename and a size. So the
/// manifest fixes a real gap here as well as in the game.
/// </para>
/// <para>
/// The parser is a deliberate mirror of <c>Shared/FreshManifest.cpp</c>, and
/// the allow-lists in the two are checked against each other by
/// <c>Tools/preflight.py</c> — two copies of a list is exactly the thing that
/// drifts, and the symptom would be the launcher promising a mod author that
/// a setting works when the game refuses it.
/// </para>
/// </summary>
public static class ModManifest
{
    public const string Dir = "FRESHMOD";
    public const string Ext = "TXT";
    public const int    KnownFormat = 1;

    /// <summary>Client presentation variables a manifest may set. Must match
    /// <c>s_szAllowed</c> in Shared/FreshManifest.cpp.
    ///
    /// The second group is owned by the renderer (d3d.ren) rather than by us,
    /// is undocumented in the SDK, and was recovered by reverse engineering —
    /// see Docs/RENDERVARS.md for what each one means and the evidence that it
    /// is actually read.</summary>
    public static readonly string[] Allowed =
    {
        "FovX",
        "HudScale",
        "HudAspect",
        "HudTextShadow",
        "HudNumberY",
        "CutsceneHeight",
        "Gore",
        "MuzzleFlashScale",
        "ZoomSensitivity",
        "ExplosionScorch",
        "KillFeedStyle",

        // Renderer-owned — a mod's look rather than its HUD.
        "FogEnable",
        "FogR",
        "FogG",
        "FogB",
        "FogNearZ",
        "FogFarZ",
        "SkyFogNearZ",
        "SkyFogFarZ",
        "CoolFog",
        "Gamma",
        "Saturate",
        "LightSaturate",
        "DynamicLight",
        "ModelFullbrite",
        "Bilinear",
        "LodScale",
    };

    /// <summary>Gameplay rules a manifest may set, applied by FreshSrv.exe.
    /// Must match <c>s_szServerAllowed</c> in Shared/FreshManifest.cpp.
    /// Deliberately excludes the server's identity, network settings and
    /// moderation — a mod may describe the game, not take over the machine.
    /// </summary>
    public static readonly string[] ServerAllowed =
    {
        "Ruleset",
        "RandomPickups",
        "InfiniteAmmo",
        "CriticalHits",
        "BlockWeapons",
        "BlockItems",
        "TractorBeam",
        "RammingDamage",
        "RunSpeed",
        "MapOrder",
    };

    /// <param name="IsAllowed">Client presentation - CShell.dll applies it.</param>
    /// <param name="IsServer">Gameplay rule - FreshSrv.exe applies it.</param>
    public record Setting(string Name, string Value, bool IsAllowed, bool IsServer)
    {
        /// <summary>On neither list: nothing will apply it.</summary>
        public bool IsRefused => !IsAllowed && !IsServer;
    }

    public record Manifest(
        int Format, string Name, string Author, string Description,
        List<Setting> Settings)
    {
        public bool IsNewer => Format > KnownFormat;
        public int RefusedCount => Settings.Count(s => s.IsRefused);

        /// <summary>"Squishie 2.2 by Wraith", for a list column.</summary>
        public string Headline =>
            Author.Length > 0 ? $"{Name} by {Author}" : Name;
    }

    public static bool IsAllowed(string name) =>
        Allowed.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static bool IsServerSetting(string name) =>
        ServerAllowed.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The first manifest in an archive, or null if it has none. First rather
    /// than all: a rez is one mod, and the folder holds several files only so
    /// that two separate mods do not collide on one filename.
    /// </summary>
    public static Manifest? Read(string rezPath)
    {
        var entries = RezArchive.TryRead(rezPath);
        if (entries is null) return null;

        foreach (var e in entries.Where(e =>
                     e.Ext.Equals(Ext, StringComparison.OrdinalIgnoreCase) &&
                     e.Path.TrimEnd('\\').Equals(Dir, StringComparison.OrdinalIgnoreCase)))
        {
            var bytes = RezArchive.ReadEntryBytes(rezPath, e);
            if (bytes is null || bytes.Length == 0 || bytes.Length > 64 * 1024) continue;

            // Latin-1: the game reads these as bytes, and guessing UTF-8 here
            // would show different text than the game does.
            var parsed = Parse(Encoding.Latin1.GetString(bytes));
            if (parsed is not null) return parsed;
        }

        return null;
    }

    /// <summary>
    /// Parse manifest text. Null when there is no usable <c>FreshMod</c> key —
    /// that is how a stray readme in the folder is told from a manifest.
    /// </summary>
    public static Manifest? Parse(string text)
    {
        int format = 0;
        string name = "", author = "", description = "";
        var settings = new List<Setting>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//")) continue;

            var tok = Tokenise(line);
            if (tok.Count == 0) continue;

            switch (tok[0].ToLowerInvariant())
            {
                case "freshmod":
                    if (tok.Count > 1) int.TryParse(tok[1], out format);
                    break;
                case "name":        if (tok.Count > 1) name        = tok[1]; break;
                case "author":      if (tok.Count > 1) author      = tok[1]; break;
                case "description": if (tok.Count > 1) description = tok[1]; break;
                case "set":
                    if (tok.Count > 2) settings.Add(new Setting(tok[1], tok[2], IsAllowed(tok[1]), IsServerSetting(tok[1])));
                    break;
            }
        }

        // A newer format is still a manifest - the game applies what it
        // understands rather than refusing, and the launcher should describe
        // what the game will do, not something stricter.
        if (format < 1) return null;

        return new Manifest(format, name, author, description, settings);
    }

    /// <summary>Whitespace-separated, with double quotes grouping. Same rule
    /// as CopyToken in Shared/FreshManifest.cpp.</summary>
    private static List<string> Tokenise(string line)
    {
        var outp = new List<string>();
        int i = 0;

        while (i < line.Length)
        {
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            if (i >= line.Length) break;

            var sb = new StringBuilder();

            if (line[i] == '"')
            {
                i++;
                while (i < line.Length && line[i] != '"') sb.Append(line[i++]);
                if (i < line.Length) i++;
            }
            else
            {
                while (i < line.Length && line[i] != ' ' && line[i] != '\t') sb.Append(line[i++]);
            }

            outp.Add(sb.ToString());
        }

        return outp;
    }
}
