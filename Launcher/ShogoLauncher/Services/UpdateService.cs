using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShogoLauncher.Services;

/// <summary>
/// Checks GitHub Releases for a newer ShogoFRESH.
///
/// GitHub Releases is the distribution point because it is free, versioned,
/// CDN-backed and readable without a token - the anonymous API allowance is
/// far more than a once-per-day launcher check needs.
///
/// Two rules this follows, both because an update check must never be worse
/// than no update check:
///
///   - it never blocks anything. Every failure path - offline, rate limited,
///     GitHub down, malformed JSON - resolves to "no update known", and the
///     launcher carries on exactly as it would have;
///   - it never installs anything by itself. It reports; the player decides.
///     Silent self-modification of somebody's game directory is not ours to
///     do.
/// </summary>
public static class UpdateService
{
    /// <summary>Where releases are published. Owner/repo, nothing else.</summary>
    public const string Repository = "KyodanCFG/ShogoFRESH";

    private const string LatestUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";

    /// <summary>Once a day is plenty; a launcher restart is not news.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(20);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public sealed record UpdateInfo(Version Version, string Name, string PageUrl, string Notes)
    {
        public string DisplayVersion => $"v{Version.Major}.{Version.Minor}.{Version.Build}";
    }

    /// <summary>The running launcher's version, from the assembly.</summary>
    public static Version CurrentVersion
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, v.Build);
        }
    }

    /// <summary>
    /// Returns the newer release, or null when there isn't one - including
    /// every failure case. Never throws.
    /// </summary>
    /// <param name="prefs">
    /// The caller's LauncherPrefs, so this shares one instance with the rest
    /// of the launcher. It used to Load() its own, which meant the throttle
    /// stamp it wrote below lived only in that copy - and the next save from
    /// the view model's instance wrote the older stamp back over it, costing
    /// an extra update check. Same divergence the CheckForUpdates property
    /// had. Passing null keeps the old behaviour for any caller without one.
    /// </param>
    public static async Task<UpdateInfo?> CheckAsync(bool force = false, LauncherPrefs? prefs = null)
    {
        try
        {
            prefs ??= LauncherPrefs.Load();

            if (!force && !prefs.CheckForUpdates) return null;

            if (!force && prefs.LastUpdateCheckUtc is DateTime last
                       && DateTime.UtcNow - last < CheckInterval)
            {
                return null;
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, LatestUrl);

            // GitHub rejects requests with no User-Agent outright.
            req.Headers.Add("User-Agent", $"ShogoFRESH-Launcher/{CurrentVersion}");
            req.Headers.Add("Accept", "application/vnd.github+json");

            using var resp = await Http.SendAsync(req);

            // Record the attempt even on failure, so a GitHub outage cannot
            // turn into a request every single launch. LauncherPrefs saves on
            // change now, so the assignment is the write.
            prefs.LastUpdateCheckUtc = DateTime.UtcNow;

            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean() && !prefs.AcceptPrereleases) return null;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";

            var version = ParseVersion(tag);
            if (version is null || version <= CurrentVersion) return null;

            var name  = root.TryGetProperty("name", out var n) ? n.GetString() ?? tag : tag;
            var url   = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

            return new UpdateInfo(version, string.IsNullOrWhiteSpace(name) ? tag : name, url, notes);
        }
        catch (Exception)
        {
            // Offline, DNS failure, timeout, rate limit, bad JSON - all the
            // same outcome: we simply don't know of an update.
            return null;
        }
    }

    /// <summary>"v0.4.0", "0.4.0", "v0.4" -> Version. Null if unparseable.</summary>
    public static Version? ParseVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var cleaned = tag.Trim().TrimStart('v', 'V');

        // Drop any suffix a tag might carry ("0.4.0-beta1").
        var cut = cleaned.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut > 0) cleaned = cleaned.Substring(0, cut);

        var parts = cleaned.Split('.');
        if (parts.Length == 0) return null;

        var numbers = new List<int>();
        foreach (var part in parts.Take(3))
        {
            if (!int.TryParse(part, out var value)) return null;
            numbers.Add(value);
        }

        while (numbers.Count < 3) numbers.Add(0);

        return new Version(numbers[0], numbers[1], numbers[2]);
    }
}
