using System.Collections.ObjectModel;
using ShogoLauncher.Models;
using ShogoLauncher.Services;

namespace ShogoLauncher.ViewModels;

/// <summary>
/// The server browser: discovery across every source, the bot filter, peer
/// exchange, manual entries, and joining. Split out of MainViewModel.cs by
/// tab; see MainViewModel.Host.cs for the pattern.
/// </summary>
public partial class MainViewModel
{
    private readonly MasterServerClient _master = new();

    public ObservableCollection<ServerInfo> Servers { get; } = new();

    private ServerInfo? _selectedServer;
    public ServerInfo? SelectedServer { get => _selectedServer; set => Set(ref _selectedServer, value); }

    // ----- Bot filter --------------------------------------------------- //
    //
    // A bot-filled server looks busy, which is the point of filling it - but
    // it makes "find a game with people in it" hard, so the browser needs a
    // way to ask. Both directions are useful: humans-only for a real match,
    // and populated-including-bots for somewhere to warm up.

    public const string BotFilterAll        = "All servers";
    public const string BotFilterHumans     = "With real players";
    public const string BotFilterNoBots     = "No bots";
    public const string BotFilterPopulated  = "Populated (bots count)";

    public string[] BotFilterLabels { get; } =
    {
        BotFilterAll, BotFilterHumans, BotFilterNoBots, BotFilterPopulated,
    };

    private string _selectedBotFilter = BotFilterAll;
    public string SelectedBotFilter
    {
        get => _selectedBotFilter;
        set
        {
            if (_selectedBotFilter == value) return;

            _selectedBotFilter = value;
            OnPropertyChanged(nameof(SelectedBotFilter));
            ServersView?.Refresh();
        }
    }

    private System.ComponentModel.ICollectionView? _serversView;

    /// <summary>
    /// The grid binds to this rather than to Servers, so filtering never
    /// removes anything from the underlying list - a filtered-out server is
    /// still there to be re-queried, kept as a favourite, and shown again
    /// when the filter changes.
    /// </summary>
    public System.ComponentModel.ICollectionView ServersView
    {
        get
        {
            if (_serversView == null)
            {
                _serversView = System.Windows.Data.CollectionViewSource.GetDefaultView(Servers);
                _serversView.Filter = PassesBotFilter;
            }

            return _serversView;
        }
    }

    private bool PassesBotFilter(object o)
    {
        if (o is not ServerInfo s) return true;

        switch (_selectedBotFilter)
        {
            case BotFilterHumans:
                return s.HumanPlayers > 0;

            case BotFilterNoBots:
                // Unknown counts as "no bots stated" and stays visible: most
                // servers will never report one, and hiding all of them would
                // make this filter look broken rather than selective.
                return s.Bots <= 0;

            case BotFilterPopulated:
                return s.TotalPlayers > 0;

            default:
                return true;
        }
    }

    public void SaveFavoritesNow() => _master.SaveFavorites(Servers);

    public async Task RefreshServersAsync()
    {
        if (Refreshing) return;
        Refreshing = true;
        Status = "Fetching server list...";

        try
        {
            // Every source, merged - master, seeds, cache, saved. Network and
            // HTML parsing entirely off the UI thread. See MasterServerClient
            // for why this is a union rather than a chain of fallbacks.
            var found = await Task.Run(() => _master.DiscoverAsync(Prefs.SeedListUrl));

            Servers.Clear();
            foreach (var s in found.Servers) Servers.Add(s);

            Status = $"Querying {Servers.Count} server(s)...";
            await Task.WhenAll(Servers.Select(QueryServerAsync));

            // Ask the servers that answered which OTHER servers they know
            // about, and query anything new. This is what makes the list
            // survivable: no master site has to be up for a live network to be
            // discoverable, only one member of it.
            var discovered = await CrawlPeersAsync();

            if (discovered > 0)
            {
                Status = $"Found {discovered} more from peers, querying...";
                await Task.WhenAll(Servers.Where(s => s.PingMs < 0 && s.Source == ServerSource.Peer)
                                          .Select(QueryServerAsync));
            }

            // Written after the queries so LastSeenUtc is current: that is what
            // lets a long-dead address age out instead of being retried for
            // ever, and what keeps a server the master forgot but which is
            // still up in tomorrow's list.
            MasterServerClient.SaveCache(Servers);

            var online = Servers.Count(s => s.Online);

            // Name the sources when the master is down, because "3 of 9
            // responding" on its own looks like the launcher is broken rather
            // than like the master site being unreachable.
            var note = found.MasterOnline
                ? ""
                : $" (master site unreachable — {found.FromCache} remembered, {found.FromSeed} seed, {found.FromSaved} saved)";

            Status = $"{online} of {Servers.Count} server(s) responding.{note}";
        }
        catch (Exception ex)
        {
            // Never fail silently into an empty list - say what broke.
            Warn($"Server refresh failed: {ex.Message}");
        }
        finally
        {
            Refreshing = false;
        }
    }

    /// <summary>
    /// Peer exchange: ask every server that answered for the servers IT knows,
    /// and add anything new to the list. Returns how many were added.
    ///
    /// Bounded on purpose. One hop, not a recursive crawl - at Shogo's scale
    /// every server's list is essentially the whole network, so a second hop
    /// costs a round of UDP to learn nothing. A hard cap on additions stops a
    /// misbehaving or malicious server turning a refresh into thousands of
    /// queries.
    ///
    /// Nothing learned here is trusted: a peer address is only an address, and
    /// it appears as offline unless it answers a query of our own. That is what
    /// makes accepting addresses from strangers safe.
    /// </summary>
    private async Task<int> CrawlPeersAsync()
    {
        const int MaxNewFromPeers = 128;

        var known = new HashSet<string>(
            Servers.Select(s => s.DisplayAddress), StringComparer.OrdinalIgnoreCase);

        // Only ask servers that actually responded; an address that did not
        // answer \status\ will not answer \peers\ either.
        var live = Servers.Where(s => s.Online).ToList();
        if (live.Count == 0) return 0;

        var lists = await Task.WhenAll(
            live.Select(s => GameSpyQueryClient.QueryPeersAsync(s.Address, s.Port)));

        var added = 0;

        foreach (var peers in lists)
        {
            foreach (var (addr, port) in peers)
            {
                if (added >= MaxNewFromPeers) return added;

                var key = $"{addr}:{port}";
                if (!known.Add(key)) continue;

                Servers.Add(new ServerInfo
                {
                    Address = addr,
                    Port = port,
                    Name = key,
                    Source = ServerSource.Peer,
                });

                added++;
            }
        }

        return added;
    }

    private static async Task QueryServerAsync(ServerInfo server)
    {
        var result = await GameSpyQueryClient.QueryAsync(server.Address, server.Port);

        if (result is null)
        {
            // No UDP answer. Don't mark offline if the master site already
            // vouched for it - community servers push-register and typically
            // don't answer queries at all.
            server.PingMs = -1;
            return;
        }

        server.Online = true;
        server.PingMs = result.PingMs;
        server.LastSeenUtc = DateTime.UtcNow;
        foreach (var kv in result.Values) server.RawInfo[kv.Key] = kv.Value;

        if (result.Values.TryGetValue("hostname", out var host)) server.Name = host;
        if (result.Values.TryGetValue("mapname", out var map)) server.Map = map;
        if (result.Values.TryGetValue("gametype", out var gt)) server.GameType = gt;
        if (result.Values.TryGetValue("numplayers", out var np) && int.TryParse(np, out var n)) server.Players = n;
        if (result.Values.TryGetValue("maxplayers", out var mp) && int.TryParse(mp, out var m)) server.MaxPlayers = m;

        server.Bots = ReadBotCount(result.Values, server.Name);
    }

    /// <summary>
    /// How many of a server's players are bots, or -1 if it does not say.
    ///
    /// There is no official field for this. The engine's GameSpy responder is
    /// inside Client.exe, which we cannot modify, and it counts
    /// connected clients - bots are game objects, not clients, so numplayers
    /// is the human count and a bot-filled server reads as nearly empty.
    ///
    /// Two sources, in order of preference:
    ///
    /// 1. A "bots" (or "numbots") key in the \status\ response. Nothing sends
    ///    this today. It is checked first because it is what a cooperating
    ///    master server or a future server build would use, and costs nothing
    ///    to look for.
    ///
    /// 2. A tag in the server name. This is the only channel that survives
    ///    the unmodifiable responder, so it is what ShogoFRESH hosts actually
    ///    use, and it has the advantage of travelling through every browser
    ///    and every master server with no cooperation from either.
    /// </summary>
    private static int ReadBotCount(Dictionary<string, string> values, string? name)
    {
        foreach (var key in new[] { "bots", "numbots", "botcount" })
        {
            if (values.TryGetValue(key, out var b) && int.TryParse(b, out var nb) && nb >= 0) return nb;
        }

        return ParseBotTag(name);
    }

    /// <summary>
    /// Pull the bot count out of a "[5 bots]" / "[5b]" tag in a server name.
    /// Returns -1 when there is none.
    /// </summary>
    public static int ParseBotTag(string? name)
    {
        if (string.IsNullOrEmpty(name)) return -1;

        var m = System.Text.RegularExpressions.Regex.Match(
            name, @"\[(\d{1,3})\s*(?:b|bots?)\]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return (m.Success && int.TryParse(m.Groups[1].Value, out var n)) ? n : -1;
    }

    /// <summary>Manually add a server to the list (not favorited; the user
    /// pins it with the Fav checkbox). Persists across refreshes.</summary>
    public void AddManualServer(string address, int port)
    {
        if (Servers.Any(s => s.Address == address && s.Port == port)) { Status = "Server already in the list."; return; }
        var s = new ServerInfo { Address = address, Port = port, Name = $"{address}:{port}", IsManual = true };
        Servers.Add(s);
        SaveFavoritesNow();
        _ = QueryServerAsync(s);
        Status = $"Added {address}:{port} to the list.";
    }

    /// <summary>
    /// Drop a manually added server from the list. Master-server entries are
    /// left alone: the next refresh would bring them straight back, so a
    /// delete that does not stick is worse than no delete at all.
    /// </summary>
    public void RemoveSelectedServer()
    {
        if (SelectedServer is not ServerInfo s) return;

        if (!s.IsManual)
        {
            Warn("Only manually added servers can be removed - the rest come from the master list.");
            return;
        }

        var label = s.DisplayAddress;

        Servers.Remove(s);
        SaveFavoritesNow();

        Status = $"Removed {label} from the list.";
    }

    public void JoinSelected()
    {
        if (!GameFound || SelectedServer is null) return;
        var launcher = new GameLaunchService(GameDir!)
        {
            // Where ShogoFRESH.rez sits relative to the Custom\ mods.
            FreshTakesPriority = Prefs.FreshTakesPriority,
        };
        // -multiplayer (from the official launcher's switch set) boots
        // straight into the MP wizard, where our injected Ip0 entry is
        // at the top of the TCP/IP list.
        launcher.LaunchGame(SelectedServer.DisplayAddress, Prefs.BuildArgs(),
                            multiplayer: true);
        Status = $"Connecting to {SelectedServer.DisplayAddress}...";
    }
}
