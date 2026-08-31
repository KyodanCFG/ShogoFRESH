using System.IO;

namespace ShogoLauncher.Services;

/// <summary>
/// Central %AppData% location. Renamed ShogoLauncher -> ShogoFRESH in 0.2.0;
/// MigrateIfNeeded moves an old folder so favorites, layouts, prefs, and fix
/// manifests survive the rename.
/// </summary>
public static class AppPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShogoFRESH");

    private static string LegacyRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShogoLauncher");

    /// <summary>
    /// Put a starter motd.md in the data folder if there is not one already.
    /// </summary>
    /// <remarks>
    /// Here rather than in the zip payload precisely because the zip extracts
    /// over the game folder: a motd.md shipped there would overwrite whatever
    /// an admin had written, on every single update, and losing someone's
    /// house rules to an upgrade is a far worse failure than having no
    /// default. %APPDATA% is never touched by extraction, so a file seeded
    /// here survives.
    ///
    /// Written once and never again. "If absent" is the whole contract: an
    /// admin who deletes it has said no, and this must not argue - the game
    /// still has a compiled-in default for that case.
    /// </remarks>
    public static void SeedMotdIfAbsent()
    {
        try
        {
            var path = Path.Combine(Root, "motd.md");
            if (File.Exists(path)) return;

            Directory.CreateDirectory(Root);

            File.WriteAllText(path, string.Join(Environment.NewLine, new[]
            {
                "# Welcome",
                "",
                "House rules:",
                "",
                "- No harassment, slurs or abuse",
                "- No cheating or exploiting",
                "- Play to win, not to ruin someone's evening",
                "",
                "**Admins can mute, kick and ban.**",
                "",
                "Being new here is fine - everyone is relearning this game.",
                "",
                "Edit this file to say something of your own.",
                "",
            }));
        }
        catch
        {
            // A data folder we cannot write is not a reason to fail to start.
            // The game carries its own default for exactly this.
        }
    }

    public static void MigrateIfNeeded()
    {
        try
        {
            if (Directory.Exists(LegacyRoot) && !Directory.Exists(Root))
                Directory.Move(LegacyRoot, Root);
        }
        catch (IOException) { /* both exist or locked - new root wins, old left alone */ }
    }
}
