using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ShogoLauncher.Services;

namespace ShogoLauncher.ViewModels;

/// <summary>
/// The Mods tab: the Custom\ scan and the per-mod row, including the
/// manifest readout and the game-code conflict notes. Split out of
/// MainViewModel.cs by tab; see MainViewModel.Host.cs for the pattern.
/// </summary>
public partial class MainViewModel
{
    public ObservableCollection<ModRow> Mods { get; } = new();

    /// <summary>
    /// Re-scan Custom\ for mods.
    ///
    /// The list used to be built once, on load, so dropping a .rez in while
    /// the launcher was open meant closing and reopening it - which is
    /// exactly what somebody trying a mod out is doing.
    /// </summary>
    public void RefreshMods()
    {
        Mods.Clear();

        if (string.IsNullOrEmpty(GameDir)) return;

        var mgr = new ModManager(GameDir!);

        foreach (var m in mgr.ListMods()) Mods.Add(new ModRow(mgr, m, err => Status = err));
    }

    /// <summary>Mod list row; toggling Enabled renames the file immediately.</summary>
    public class ModRow : INotifyPropertyChanged
    {
        private readonly ModManager _mgr;
        private ModManager.ModEntry _entry;
        private readonly Action<string> _reportError;

        public ModRow(ModManager mgr, ModManager.ModEntry entry, Action<string> reportError)
        {
            _mgr = mgr; _entry = entry; _reportError = reportError;
        }

        public string Name => _entry.Name;

        /// <summary>
        /// The name without its extension, for display only.
        ///
        /// Name itself must NOT lose the extension: ShowRezContents decides
        /// whether a row is an archive or a single level by asking whether it
        /// ends in .rez, and the disabled state is expressed by renaming to
        /// .off - both would break. A .off row keeps a visible marker, because
        /// "disabled" is the one thing about a mod's filename a player needs
        /// to see.
        /// </summary>
        public string DisplayName
        {
            get
            {
                var n = _entry.Name;
                if (n.EndsWith(".rez", StringComparison.OrdinalIgnoreCase))
                    return n.Substring(0, n.Length - 4);
                if (n.EndsWith(".rez.off", StringComparison.OrdinalIgnoreCase))
                    return n.Substring(0, n.Length - 8) + "  (off)";
                return n;
            }
        }
        public long SizeBytes => _entry.SizeBytes;

        /// <summary>Full path on disk, for the contents viewer.</summary>
        public string Path => _entry.Path;

        /// <summary>
        /// The size a person can read. "29302011" tells you nothing at a
        /// glance; "27.9 MB" tells you it is a map pack rather than a skin.
        /// </summary>
        public string SizeText
        {
            get
            {
                double n = _entry.SizeBytes;

                if (n >= 1024 * 1024) return $"{n / (1024 * 1024):0.#} MB";
                if (n >= 1024)        return $"{n / 1024:0} KB";

                return $"{n:0} bytes";
            }
        }

        // Worked out once - it reads the whole file.
        private bool? _hasCode;
        private List<string>? _overlap;
        private bool _manifestRead;
        private ModManifest.Manifest? _manifest;
        private List<DtxValidator.Finding>? _texture;

        /// <summary>
        /// What the mod says about itself, or null. Until manifests there was
        /// nowhere in a .rez to put a name or an author - the format has no
        /// field for either - so this is the first time the launcher can show
        /// anything about a mod beyond its filename and size.
        /// </summary>
        public ModManifest.Manifest? Manifest
        {
            get
            {
                if (!_manifestRead)
                {
                    _manifestRead = true;
                    _manifest = ModManifest.Read(_entry.Path);
                }
                return _manifest;
            }
        }

        /// <summary>Column text: "Squishie 2.2 by Wraith", empty when the mod
        /// does not describe itself.</summary>
        public string ManifestHeadline => Manifest?.Headline ?? string.Empty;

        /// <summary>Row tooltip - the description, plus anything the game will
        /// refuse, so a mod author sees the problem without launching.</summary>
        public string ManifestDetail
        {
            get
            {
                var m = Manifest;
                if (m is null) return string.Empty;

                var sb = new System.Text.StringBuilder();
                sb.Append(m.Headline);
                if (m.Description.Length > 0) sb.Append("\n\n").Append(m.Description);

                var client = m.Settings.Where(s => s.IsAllowed).ToList();
                if (client.Count > 0)
                {
                    sb.Append("\n\nSets (client): ");
                    sb.Append(string.Join(", ", client.Select(s => $"{s.Name} {s.Value}")));
                }

                // Server rules apply only when hosting with FreshSrv.exe -
                // worth separating, because a player wondering why nothing
                // changed in single player has their answer here.
                var server = m.Settings.Where(s => s.IsServer).ToList();
                if (server.Count > 0)
                {
                    sb.Append("\n\nSets (server rules, applied when hosting): ");
                    sb.Append(string.Join(", ", server.Select(s => $"{s.Name} {s.Value}")));
                }

                var refused = m.Settings.Where(s => s.IsRefused).ToList();
                if (refused.Count > 0)
                {
                    sb.Append("\n\nREFUSED — a manifest may not change these: ");
                    sb.Append(string.Join(", ", refused.Select(s => s.Name)));
                }

                if (m.IsNewer)
                    sb.Append($"\n\nWritten for a newer ShogoFRESH (format {m.Format}) - " +
                              "this build will apply what it understands.");

                return sb.ToString();
            }
        }

        public bool ConflictsWithFresh =>
            _hasCode ??= ModManager.ContainsGameCode(_entry.Path);

        private List<string> Overlap =>
            _overlap ??= ModManager.OverlappingGameFiles(_entry.Path);

        /// <summary>
        /// True when the mod carries SOME of ShogoFRESH's game files but not
        /// all of them - the case worth calling out separately, because -rez
        /// resolves per file. See ModManager.OverlappingGameFiles.
        /// </summary>
        public bool PartiallyOverlapsFresh =>
            Overlap.Count > 0 && Overlap.Count < ModManager.FreshGameFileCount;

        /// <summary>
        /// Shown beside the mod. Empty for the asset mods, which are most of
        /// them and which layer on top of ShogoFRESH without trouble.
        ///
        /// <para>
        /// Three cases, not two. A mod replacing all four game files is a
        /// clean swap - its game instead of ours. A mod replacing some of them
        /// produces a mixture neither project has run, which is the one that
        /// needs saying out loud because it fails quietly. And a mod carrying a
        /// DLL that is not one of ours at all is the engine's business, not
        /// something that displaces ShogoFRESH.
        /// </para>
        /// </summary>
        public string ConflictNote
        {
            get
            {
                if (!ConflictsWithFresh) return string.Empty;

                if (Overlap.Count == 0)
                    return "Ships a DLL, but none of ShogoFRESH's game files - nothing of ours is displaced";

                if (!PartiallyOverlapsFresh)
                    return "Replaces ShogoFRESH's game code entirely - unless ShogoFRESH is set to take priority";

                var missing = string.Join(", ", ModManager.MissingGameFiles(_entry.Path));

                return $"MIXES with ShogoFRESH: replaces {Overlap.Count} of " +
                       $"{ModManager.FreshGameFileCount} game files, ours still supply {missing}. " +
                       "Untested combination - tick ShogoFRESH takes priority to avoid it";
            }
        }

        private List<DtxValidator.Finding> TextureFindings =>
            _texture ??= DtxValidator.Validate(_entry.Path);

        /// <summary>True when a texture in this mod will not render correctly.</summary>
        public bool HasTextureErrors =>
            TextureFindings.Any(f => f.Level == DtxValidator.Level.Error);

        public bool HasTextureNote => TextureFindings.Count > 0;

        /// <summary>
        /// Shown beside the mod when a texture will not load. Empty for the vast
        /// majority of mods, including every one made with Monolith's own tools.
        ///
        /// <para>
        /// This is here because the failure it catches is invisible from inside
        /// the game: a texture with too many mipmap levels renders WHITE, with no
        /// crash, no fallback and nothing in any log. Told at install time, an
        /// author can fix it; found in play, it looks like a broken model and
        /// gets blamed on the engine, or on ShogoFRESH.
        /// </para>
        /// <para>
        /// Errors are listed ahead of warnings, and at most three are named -
        /// a pack built by a script that gets this wrong gets it wrong for every
        /// texture, and a list of four hundred identical lines helps nobody find
        /// the mistake.
        /// </para>
        /// </summary>
        public string TextureNote
        {
            get
            {
                var all = TextureFindings;
                if (all.Count == 0) return string.Empty;

                var errors = all.Where(f => f.Level == DtxValidator.Level.Error).ToList();
                var shown  = (errors.Count > 0 ? errors : all).Take(3).ToList();

                var sb = new System.Text.StringBuilder();
                sb.Append(errors.Count > 0
                    ? $"{errors.Count} texture{(errors.Count == 1 ? "" : "s")} will not render correctly:"
                    : $"{all.Count} texture note{(all.Count == 1 ? "" : "s")}:");

                foreach (var f in shown)
                    sb.Append($"\n  {f.Entry} - {f.Message}");

                int more = (errors.Count > 0 ? errors.Count : all.Count) - shown.Count;
                if (more > 0) sb.Append($"\n  ...and {more} more");

                return sb.ToString();
            }
        }

        /// <summary>
        /// One line for the Notes column. The full list lives in the row
        /// tooltip - a grid cell is the wrong place for four hundred entry
        /// names, but it is the right place to say that something is wrong.
        /// </summary>
        public string TextureSummary
        {
            get
            {
                var all = TextureFindings;
                if (all.Count == 0) return string.Empty;

                int errors = all.Count(f => f.Level == DtxValidator.Level.Error);

                if (errors > 0)
                    return $"{errors} texture{(errors == 1 ? "" : "s")} will not render - hover for detail";

                return $"{all.Count} texture note{(all.Count == 1 ? "" : "s")} - hover for detail";
            }
        }

        /// <summary>The Notes column: game-code conflicts and texture problems
        /// are both things the operator wants to see before enabling a mod.</summary>
        public string NotesText =>
            string.Join("  ", new[] { ConflictNote, TextureSummary }
                              .Where(s => !string.IsNullOrEmpty(s)));

        /// <summary>The row tooltip: what the mod says about itself, then
        /// anything wrong with it.</summary>
        public string RowDetail =>
            string.Join("\n\n", new[] { ManifestDetail, TextureNote }
                                .Where(s => !string.IsNullOrEmpty(s)));

        public bool Enabled
        {
            get => _entry.Enabled;
            set
            {
                if (_entry.Enabled == value) return;
                try
                {
                    _entry = _mgr.SetEnabled(_entry, value);
                }
                catch (System.IO.IOException ex)
                {
                    _reportError(ex.Message);
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
