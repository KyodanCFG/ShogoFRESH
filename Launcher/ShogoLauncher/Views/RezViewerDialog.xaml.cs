using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using ShogoLauncher.Services;

namespace ShogoLauncher.Views;

/// <summary>
/// Shows what is actually inside a .rez, read from the archive's own
/// directory rather than guessed at (see <see cref="RezArchive"/>).
///
/// <para>
/// The point is answering "what did I just install" without extracting
/// anything: whether a pack replaces retail maps or adds new ones, whether a
/// mod ships game code, whether two mods collide. Read-only - nothing here
/// writes to the archive.
/// </para>
/// </summary>
public partial class RezViewerDialog : Window
{
    /// <summary>A grid row. Keeps the entry it came from, so extraction works
    /// from the parsed directory rather than re-deriving paths from display
    /// strings.</summary>
    public record Row(RezArchive.RezEntry Entry)
    {
        public string Folder => Entry.Path.TrimEnd('\\');
        public string Name => Entry.Name;
        public string Type => Entry.Ext;
        public long Size => Entry.Size;
        public string SizeText => FormatSize(Entry.Size);
    }

    private List<Row> _all = new();
    private string _rezPath;

    /// <summary>Where the Open… picker starts. The game folder, so the
    /// archives a modder actually wants are the ones already in front of
    /// them; null falls back to wherever Windows last was.</summary>
    private readonly string? _browseRoot;

    public RezViewerDialog(string rezPath, string? browseRoot = null)
    {
        InitializeComponent();

        _rezPath     = rezPath;
        _browseRoot  = browseRoot;

        LoadArchive(rezPath);
    }

    /// <summary>
    /// Read an archive and show it. Replaces whatever was on screen, so the
    /// Open… button can move between archives without closing the window -
    /// which is the normal case for a modder pulling art out of SHOGO.REZ
    /// and sounds out of SOUND.REZ in one sitting.
    ///
    /// <para>
    /// ASYNC BECAUSE OF SIZE. The directory is scattered through the file
    /// rather than sitting in one place, so <see cref="RezArchive.TryRead"/>
    /// loads the whole thing to parse it. That is nothing for a mod and a
    /// visible freeze for the game's own archives - SHOGO.REZ is 259 MB and
    /// SOUND.REZ 141 MB - so the read goes to a worker and the window says
    /// what it is doing rather than going white.
    /// </para>
    /// </summary>
    private async void LoadArchive(string rezPath)
    {
        _rezPath = rezPath;

        var file = Path.GetFileName(rezPath);
        Title = file + " - Archive Contents";

        _all = new List<Row>();
        EntryGrid.ItemsSource   = null;
        EmptyText.Visibility    = Visibility.Collapsed;
        SummaryText.Text        = "Reading " + file + "...";

        SetControlsEnabled(false);

        var entries = await Task.Run(() => RezArchive.TryRead(rezPath));

        if (entries is null)
        {
            SummaryText.Text = file + " could not be read as a LithTech archive.";
            EmptyText.Text =
                "The file may be corrupt, still downloading, or not a .rez at all. " +
                "The game will ignore it too.";
            EmptyText.Visibility = Visibility.Visible;

            // Open… stays live: the answer to picking the wrong file is to
            // pick another one, and a dead window would mean closing and
            // starting again.
            OpenButton.IsEnabled = true;
            return;
        }

        _all = entries
            .Select(e => new Row(e))
            .OrderBy(r => r.Folder, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SummaryText.Text = Describe(file, _all);

        // What the mod says about itself, ahead of what we worked out about
        // it. Nothing else in a .rez can carry this - the format has no
        // author or description field at all.
        var manifest = ModManifest.Read(rezPath);

        if (manifest is not null)
        {
            var head = manifest.Headline;
            if (manifest.Description.Length > 0) head += " - " + manifest.Description;

            SummaryText.Text = head + "\n" + SummaryText.Text;
        }

        SetControlsEnabled(true);
        FilterBox.Text = "";
        Apply("");
    }

    private void SetControlsEnabled(bool on)
    {
        FilterBox.IsEnabled         = on;
        ExtractAllButton.IsEnabled  = on;
        ExtractSelButton.IsEnabled  = on;
        OpenButton.IsEnabled        = on;
    }

    /// <summary>
    /// The file picker, shared with the Mods tab so both doors open the same
    /// dialog with the same filter. Returns null if the user cancelled.
    /// </summary>
    public static string? PickArchive(Window? owner, string? startDir)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Open a LithTech archive",
            Filter = "LithTech archives (*.rez)|*.rez|All files (*.*)|*.*",
        };

        if (!string.IsNullOrEmpty(startDir) && Directory.Exists(startDir))
            dlg.InitialDirectory = startDir;

        return dlg.ShowDialog(owner) == true ? dlg.FileName : null;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var picked = PickArchive(this, _browseRoot);
        if (picked is not null) LoadArchive(picked);
    }

    /// <summary>
    /// The one-line answer to "what is this?", so the grid is confirmation
    /// rather than homework. Game code is called out because -rez is
    /// last-wins: a mod carrying CShell.dll or Object.lto replaces
    /// ShogoFRESH's game code wholesale rather than merging with it.
    /// </summary>
    private static string Describe(string file, List<Row> rows)
    {
        if (rows.Count == 0) return file + " is a valid archive, but it is empty.";

        var byType = rows.GroupBy(r => r.Type)
                         .OrderByDescending(g => g.Count())
                         .Select(g => $"{g.Count()} {(g.Key.Length > 0 ? g.Key : "untyped")}")
                         .Take(6);

        var sb = new StringBuilder();
        sb.Append($"{rows.Count:N0} files, {FormatSize(rows.Sum(r => r.Size))} uncompressed — ");
        sb.Append(string.Join(", ", byType));
        sb.Append('.');

        var levels = rows.Count(r => r.Type == "DAT" &&
                                     r.Folder.Equals(@"WORLDS\MULTI", StringComparison.OrdinalIgnoreCase));
        if (levels > 0)
            sb.Append($"  {levels} multiplayer level{(levels == 1 ? "" : "s")}, selectable in the Host tab's rotation.");

        AppendCodeVerdict(sb, rows);

        return sb.ToString();
    }

    /// <summary>
    /// -rez resolves last-wins PER FILE, so "carries game code" splits into a
    /// clean swap and a mixture. Named here as well as on the Mods tab,
    /// because this window is where somebody looks when they want to know
    /// exactly what a mod is made of.
    /// </summary>
    private static void AppendCodeVerdict(StringBuilder sb, List<Row> rows)
    {
        if (!rows.Any(r => r.Type is "DLL" or "LTO")) return;

        // Entry name, type code, and the filename to show - "CSHELL" titlecased
        // by rule would read "Cshell", which is not what the file is called.
        var ours = new[]
        {
            ("CSHELL", "DLL", "CShell.dll"),
            ("OBJECT", "LTO", "Object.lto"),
            ("CRES",   "DLL", "CRes.dll"),
            ("SRES",   "DLL", "SRes.dll"),
        };

        var have = ours.Where(o => rows.Any(r =>
                       r.Type == o.Item2 &&
                       r.Name.Equals(o.Item1, StringComparison.OrdinalIgnoreCase))).ToList();

        if (have.Count == 0)
        {
            sb.Append("  Ships a DLL, but none of ShogoFRESH's four game files - nothing of ours is displaced.");
        }
        else if (have.Count == ours.Length)
        {
            sb.Append("  Contains GAME CODE: all four files, so enabling this replaces ShogoFRESH's " +
                      "game code entirely rather than layering on top of it.");
        }
        else
        {
            var missing = string.Join(", ", ours.Except(have).Select(o => o.Item3));

            sb.Append($"  MIXES with ShogoFRESH: carries {have.Count} of the four game files, so " +
                      $"ours would still supply {missing}. That pairing has never been run by either " +
                      "project - set ShogoFRESH to take priority to avoid it.");
        }
    }

    private void Apply(string filter)
    {
        var rows = string.IsNullOrWhiteSpace(filter)
            ? _all
            : _all.Where(r =>
                  r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                  r.Folder.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                  r.Type.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        EntryGrid.ItemsSource = rows;

        // A filter that matches nothing looks identical to a broken viewer,
        // so say which it is.
        if (_all.Count > 0 && rows.Count == 0)
        {
            EmptyText.Text = $"Nothing in this archive matches \"{filter}\".";
            EmptyText.Visibility = Visibility.Visible;
        }
        else if (_all.Count > 0)
        {
            EmptyText.Visibility = Visibility.Collapsed;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024 / 1024:0.0} GB";
        if (bytes >= 1024 * 1024)         return $"{bytes / 1024.0 / 1024:0.0} MB";
        if (bytes >= 1024)                return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }

    private void Filter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_all.Count > 0) Apply(FilterBox.Text);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (EntryGrid.ItemsSource is not IEnumerable<Row> rows) return;

        // Same order as the columns, so a paste lines up with what was on
        // screen when it was copied.
        var sb = new StringBuilder();
        foreach (var r in rows)
            sb.AppendLine($"{r.Name}\t{r.Type}\t{r.Folder}\t{r.Size}");

        try { Clipboard.SetText(sb.ToString()); }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another process can hold the clipboard open; not worth a dialog.
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ExtractAll_Click(object sender, RoutedEventArgs e) =>
        Extract(_all, "the whole archive");

    private void ExtractSelected_Click(object sender, RoutedEventArgs e)
    {
        var rows = EntryGrid.SelectedItems.Cast<Row>().ToList();

        if (rows.Count == 0)
        {
            MessageBox.Show(this,
                "Highlight the rows to extract first. Ctrl+A selects everything currently listed, "
                + "so a filter plus Ctrl+A extracts just the matches.",
                "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Extract(rows, $"{rows.Count} selected file{(rows.Count == 1 ? "" : "s")}");
    }

    /// <summary>
    /// Asks where, then writes. Runs off the UI thread because extracting
    /// SHOGO.REZ is 6,135 files and 247 MB, which would otherwise lock the
    /// window solid for long enough to look like a hang.
    /// </summary>
    private async void Extract(List<Row> rows, string what)
    {
        var pick = new Microsoft.Win32.OpenFolderDialog
        {
            Title = $"Extract {what} to...",
            Multiselect = false,
        };

        if (pick.ShowDialog(this) != true) return;

        var dest = pick.FolderName;
        var entries = rows.Select(r => r.Entry).ToList();

        ExtractAllButton.IsEnabled = ExtractSelButton.IsEnabled = false;
        var wasCursor = Cursor;
        Cursor = System.Windows.Input.Cursors.Wait;

        try
        {
            var result = await Task.Run(() => RezArchive.Extract(_rezPath, entries, dest));
            Cursor = wasCursor;
            ShowResult(result, dest);
        }
        catch (Exception ex)
        {
            Cursor = wasCursor;
            MessageBox.Show(this,
                $"Extraction stopped: {ex.Message}",
                "Extract", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            Cursor = wasCursor;
            ExtractAllButton.IsEnabled = ExtractSelButton.IsEnabled = true;
        }
    }

    private void ShowResult(RezArchive.ExtractResult r, string dest)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{r.Written:N0} file{(r.Written == 1 ? "" : "s")} written " +
                      $"({FormatSize(r.Bytes)}) to");
        sb.AppendLine(dest);

        // Both of these are reported rather than swallowed. A renamed file is
        // a mild surprise the user should be able to find again; a refused one
        // means the archive asked to write outside the folder it was given,
        // which is worth knowing about the mod that did it.
        if (r.Renamed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{r.Renamed.Count} renamed - the name repeated, or held characters " +
                          "Windows will not accept in a filename:");
            foreach (var n in r.Renamed.Take(8)) sb.AppendLine("   " + n);
            if (r.Renamed.Count > 8) sb.AppendLine($"   ...and {r.Renamed.Count - 8} more");
        }

        if (r.Refused.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{r.Refused.Count} REFUSED - these entries tried to write outside the " +
                          "folder you chose, or pointed at data outside the archive:");
            foreach (var n in r.Refused.Take(8)) sb.AppendLine("   " + n);
            if (r.Refused.Count > 8) sb.AppendLine($"   ...and {r.Refused.Count - 8} more");
        }

        MessageBox.Show(this, sb.ToString(), "Extract complete",
                        MessageBoxButton.OK,
                        r.Refused.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }
}
