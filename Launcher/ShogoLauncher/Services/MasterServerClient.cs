using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShogoLauncher.Models;

namespace ShogoLauncher.Services;

/// <summary>
/// Server discovery: a UNION of independent sources, none of which is load
/// bearing on its own.
///
/// Shogo has already outlived one master server (shogo-mad.com) and currently
/// depends on a second (shogoservers.com). Treating either as THE source of
/// truth means that the day it goes away, every server still running becomes
/// unfindable even though nothing is wrong with them. So nothing here is a
/// fallback for anything else - every source contributes on every refresh and
/// the results are merged:
///
/// 1. shogoservers.com - the live community master (NetworkDLS, registering
///    servers since Dec 2020). No documented API, so we scrape address:port
///    out of the list HTML. It is the richest source while it lives: it knows
///    names, maps and player counts for servers that never answer UDP.
///
/// 2. A seed list - addresses shipped with the launcher (Defaults\
///    seed-servers.json) plus an optional remote copy. These never expire and
///    do not need to be current; ONE reachable address is enough to bootstrap,
///    which is what makes the rest of this survivable.
///
/// 3. The local cache of everything seen before. Previously consulted only
///    when the master failed; it now always contributes, so a server the
///    master has forgotten but which is still up stays in the list.
///
/// 4. Saved, favourited and manually added servers.
///
/// Every merged address is then queried directly, so an entry from any source
/// only reads as online if it actually answers. That is also the defence
/// against a bad source: a stale or poisoned list costs one failed UDP query
/// and nothing else. The servers themselves are a source too
/// (ServerSource.Peer): after the direct queries, every server that answered
/// is asked for the peers it knows (MainViewModel.CrawlPeersAsync), so the
/// network can describe itself with no master at all.
/// </summary>
public partial class MasterServerClient
{
    private const string MasterUrl = "https://shogoservers.com/";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    [GeneratedRegex(@"(?<ip>\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b)[:\s]+(?<port>\d{4,5})\b")]
    private static partial Regex AddressPattern();

    // One server row on shogoservers.com: name link, address cell, players
    // link ("N of M"), then the next plain cell is the map. Verified against
    // the live page HTML 2026-07-26.
    [GeneratedRegex(
        """ServerDetail\?Id=[^"]+">(?<name>[^<]+)</a>.*?(?<ip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}):(?<port>\d{4,5}).*?ActivePlayers\?Id=[^"]+">(?<cur>\d+)\s+of\s+(?<max>\d+)</a>.*?size="2">(?<map>[^<>]{1,64})</font>""",
        RegexOptions.Singleline)]
    private static partial Regex ServerRowPattern();

    public static string FavoritesPath =>
        Path.Combine(AppPaths.Root,
                     "favorites.json");

    public static string CachePath =>
        Path.Combine(AppPaths.Root,
                     "master-cache.json");

    /// <summary>Bootstrap addresses shipped beside the launcher.</summary>
    public static string SeedPath =>
        Path.Combine(AppContext.BaseDirectory, "Defaults", "seed-servers.json");

    /// <summary>
    /// A cached address that has not answered in this long is dropped rather
    /// than re-queried for ever. Generous on purpose: a server that is down
    /// for a fortnight's maintenance should not have to be rediscovered, and
    /// the cost of keeping a dead address is one UDP timeout per refresh.
    /// </summary>
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromDays(60);

    public record FetchResult(List<ServerInfo> Servers, bool FromCache);

    /// <summary>
    /// What a discovery pass found, and which sources actually produced
    /// anything - so the UI can say "master down, 4 from cache" rather than
    /// silently showing a short list.
    /// </summary>
    public record DiscoveryResult(
        List<ServerInfo> Servers,
        bool MasterOnline,
        int FromMaster,
        int FromSeed,
        int FromCache,
        int FromSaved);

    /// <summary>
    /// Every source, merged. See the class comment for why this is a union and
    /// not a chain of fallbacks.
    /// </summary>
    public async Task<DiscoveryResult> DiscoverAsync(string? remoteSeedUrl = null)
    {
        var master    = await FetchMasterListAsync();
        var seeds     = await LoadSeedsAsync(remoteSeedUrl);
        var cached    = LoadCache();
        var saved     = LoadFavorites();

        // Merged lowest-priority first, so a richer source overwrites a poorer
        // one: the cache knows an address, the master knows who is playing on
        // it. ServerSource's numeric order is that priority.
        var merged = new Dictionary<string, ServerInfo>(StringComparer.OrdinalIgnoreCase);

        void Absorb(IEnumerable<ServerInfo> from, ServerSource source)
        {
            foreach (var s in from)
            {
                s.Source = source;

                if (!merged.TryGetValue(s.DisplayAddress, out var existing))
                {
                    merged[s.DisplayAddress] = s;
                    continue;
                }

                // Same address from two sources. Keep the richer record, but
                // never lose the user's own flags - a favourite that also
                // appears on the master is still a favourite.
                bool keepNew = source > existing.Source;

                if (keepNew)
                {
                    s.IsFavorite  = existing.IsFavorite || s.IsFavorite;
                    s.IsManual    = existing.IsManual   || s.IsManual;
                    s.LastSeenUtc = s.LastSeenUtc ?? existing.LastSeenUtc;
                    merged[s.DisplayAddress] = s;
                }
                else
                {
                    existing.IsFavorite  = existing.IsFavorite || s.IsFavorite;
                    existing.IsManual    = existing.IsManual   || s.IsManual;
                    existing.LastSeenUtc = existing.LastSeenUtc ?? s.LastSeenUtc;
                }
            }
        }

        // THIS MACHINE, always probed. A hosted FreshSrv (27888) or listen
        // server (27889) on the same box never appears on any master or peer
        // list from in here, so without these two the most common test setup
        // - launcher and server side by side - showed an empty browser.
        // Absorbed at Cache priority so a real record of the same address
        // from any live source wins the merge; the Servers tab drops the
        // pair again after querying if nothing local answered, so they are
        // rows only when they are real.
        var lan = new[]
        {
            new ServerInfo { Address = "127.0.0.1", Port = 27888 },
            new ServerInfo { Address = "127.0.0.1", Port = 27889 },
        };

        Absorb(cached, ServerSource.Cache);
        Absorb(lan,    ServerSource.Cache);
        Absorb(seeds,  ServerSource.Seed);
        Absorb(saved,  ServerSource.Saved);
        Absorb(master.Servers, ServerSource.Master);

        // Manual entries are flagged rather than sourced, so restore that
        // label after the merge - it is the most useful thing to show.
        foreach (var s in merged.Values)
        {
            if (s.IsManual) s.Source = ServerSource.Manual;
        }

        return new DiscoveryResult(
            merged.Values.ToList(),
            MasterOnline: !master.FromCache && master.Servers.Count > 0,
            FromMaster: master.Servers.Count,
            FromSeed: seeds.Count,
            FromCache: cached.Count,
            FromSaved: saved.Count);
    }

    /// <summary>
    /// Bootstrap addresses: the shipped file, plus an optional remote copy so
    /// the community can publish new entry points without a launcher release.
    ///
    /// Failures are silent by design - a seed list is a convenience, and a
    /// missing or unreachable one must never be the reason a refresh fails.
    /// </summary>
    private async Task<List<ServerInfo>> LoadSeedsAsync(string? remoteSeedUrl)
    {
        var seeds = new Dictionary<string, ServerInfo>(StringComparer.OrdinalIgnoreCase);

        void Add(IEnumerable<SeedEntry>? entries)
        {
            if (entries == null) return;

            foreach (var e in entries)
            {
                if (string.IsNullOrWhiteSpace(e.Address)) continue;
                if (e.Port is < 1024 or > 65535) continue;

                var key = $"{e.Address}:{e.Port}";
                if (seeds.ContainsKey(key)) continue;

                seeds[key] = new ServerInfo
                {
                    Address = e.Address.Trim(),
                    Port = e.Port,
                    Name = string.IsNullOrWhiteSpace(e.Name) ? key : e.Name!.Trim(),
                };
            }
        }

        try
        {
            if (File.Exists(SeedPath))
                Add(JsonSerializer.Deserialize<List<SeedEntry>>(File.ReadAllText(SeedPath)));
        }
        catch (JsonException) { }
        catch (IOException) { }

        if (!string.IsNullOrWhiteSpace(remoteSeedUrl))
        {
            try
            {
                var json = await Http.GetStringAsync(remoteSeedUrl);
                Add(JsonSerializer.Deserialize<List<SeedEntry>>(json));
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            catch (JsonException) { }
        }

        return seeds.Values.ToList();
    }

    private record SeedEntry(string Address, int Port, string? Name);

    /// <summary>
    /// Fetch the master list; on failure (or an empty page) fall back to the
    /// last successful result cached on disk, so a flaky master or network
    /// blip never makes known servers vanish from the browser.
    /// </summary>
    public async Task<FetchResult> FetchMasterListAsync()
    {
        var servers = new List<ServerInfo>();
        try
        {
            var html = await Http.GetStringAsync(MasterUrl);
            var seen = new HashSet<string>();

            // Structured rows first: the master site knows name/players/map
            // (servers push-register; they do not answer UDP queries).
            foreach (Match m in ServerRowPattern().Matches(html))
            {
                var ip = m.Groups["ip"].Value;
                var port = int.Parse(m.Groups["port"].Value);
                if (!seen.Add($"{ip}:{port}")) continue;
                var name = System.Net.WebUtility.HtmlDecode(m.Groups["name"].Value.Trim());

                servers.Add(new ServerInfo
                {
                    Address = ip,
                    Port = port,
                    Name = name,
                    Players = int.Parse(m.Groups["cur"].Value),
                    MaxPlayers = int.Parse(m.Groups["max"].Value),
                    Map = m.Groups["map"].Value.Trim(),
                    Online = true,

                    // The master list carries no bot field, so the only source
                    // here is a tag in the name. Read now so the count shows
                    // before any UDP query has answered - community servers
                    // push-register and often never answer one at all.
                    Bots = ViewModels.MainViewModel.ParseBotTag(name),
                });
            }

            // Fallback: any additional bare addresses in the page.
            foreach (Match m in AddressPattern().Matches(html))
            {
                var ip = m.Groups["ip"].Value;
                var port = int.Parse(m.Groups["port"].Value);
                if (port is < 1024 or > 65535) continue;
                if (!seen.Add($"{ip}:{port}")) continue;
                servers.Add(new ServerInfo { Address = ip, Port = port, Name = $"{ip}:{port}" });
            }
        }
        catch (HttpRequestException) { /* master unreachable - fall through to cache */ }
        catch (TaskCanceledException) { }

        // No cache write here any more, and no cache READ either. Both belong
        // to DiscoverAsync, which owns the union - this method's job is now
        // only "what does the master say", so an empty answer means the master
        // is down rather than that discovery failed.
        return new FetchResult(servers, FromCache: servers.Count == 0);
    }

    /// <summary>
    /// Remember everything we know about, not just what the master said.
    ///
    /// Called after the queries have run so LastSeenUtc is current, which is
    /// what lets a long-dead address be pruned rather than re-queried for ever.
    /// Seeds are excluded: they are shipped bootstrap addresses and belong in
    /// their own file, not copied into a cache that then ages them out.
    /// </summary>
    public static void SaveCache(IEnumerable<ServerInfo> servers)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);

            var now = DateTime.UtcNow;

            var entries = servers
                .Where(s => s.Source != ServerSource.Seed)
                .Select(s => new CacheEntry(
                    s.Address, s.Port, s.Name, s.Map, s.Players, s.MaxPlayers,
                    s.Online ? now : s.LastSeenUtc))
                .Where(e => e.LastSeenUtc == null || now - e.LastSeenUtc.Value < CacheMaxAge)
                .ToList();

            File.WriteAllText(CachePath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException) { }
    }

    private List<ServerInfo> LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return new();
            var entries = JsonSerializer.Deserialize<List<CacheEntry>>(File.ReadAllText(CachePath)) ?? new();

            var now = DateTime.UtcNow;

            return entries
                // A never-seen entry is kept: it came from an older cache
                // format that had no timestamp, and dropping those would
                // silently empty a returning user's browser.
                .Where(e => e.LastSeenUtc == null || now - e.LastSeenUtc.Value < CacheMaxAge)
                .Select(e => new ServerInfo
                {
                    Address = e.Address,
                    Port = e.Port,
                    Name = e.Name ?? $"{e.Address}:{e.Port}",
                    Map = e.Map ?? "",
                    Players = e.Players,
                    MaxPlayers = e.MaxPlayers,
                    LastSeenUtc = e.LastSeenUtc,
                }).ToList();
        }
        catch (JsonException) { return new(); }
        catch (IOException) { return new(); }
    }

    private record CacheEntry(string Address, int Port, string? Name, string? Map, int Players, int MaxPlayers,
                              DateTime? LastSeenUtc = null);

    /// <summary>
    /// Saved servers: everything the user added manually (whether or not
    /// favorited) plus any master-list server they favorited.
    /// </summary>
    public List<ServerInfo> LoadFavorites()
    {
        try
        {
            if (!File.Exists(FavoritesPath)) return new();
            var entries = JsonSerializer.Deserialize<List<FavoriteEntry>>(File.ReadAllText(FavoritesPath)) ?? new();
            return entries.Select(e => new ServerInfo
            {
                Address = e.Address,
                Port = e.Port,
                Name = e.Name ?? $"{e.Address}:{e.Port}",
                IsFavorite = e.IsFavorite ?? true,   // older files: everything was a favorite
                IsManual = e.IsManual ?? true,
            }).ToList();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void SaveFavorites(IEnumerable<ServerInfo> servers)
    {
        var dir = Path.GetDirectoryName(FavoritesPath)!;
        Directory.CreateDirectory(dir);
        var entries = servers
            .Where(s => s.IsManual || s.IsFavorite)
            .Select(s => new FavoriteEntry(s.Address, s.Port, s.Name, s.IsFavorite, s.IsManual))
            .ToList();
        File.WriteAllText(FavoritesPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    private record FavoriteEntry(string Address, int Port, string? Name, bool? IsFavorite = null, bool? IsManual = null);
}
