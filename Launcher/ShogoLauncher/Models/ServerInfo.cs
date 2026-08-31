using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShogoLauncher.Models;

/// <summary>
/// Where a browser entry was learned from.
///
/// Discovery is a union of independent sources rather than one master server,
/// so that no single one going away takes the server list with it. Recording
/// which source an address came from is what makes that visible - and what
/// lets a refresh say "the master is down but here is everything else".
///
/// Order matters: higher wins when the same address arrives from two sources,
/// because a source with live player counts should not be overwritten by one
/// that only knows an address.
/// </summary>
public enum ServerSource
{
    /// <summary>Remembered from a previous session.</summary>
    Cache = 0,
    /// <summary>A shipped or downloaded bootstrap address.</summary>
    Seed = 1,
    /// <summary>Learned from another server's peer list (phase 2).</summary>
    Peer = 2,
    /// <summary>Saved or favourited by the user.</summary>
    Saved = 3,
    /// <summary>Typed in by the user.</summary>
    Manual = 4,
    /// <summary>shogoservers.com.</summary>
    Master = 5,
}

/// <summary>
/// One row in the server browser. Static identity (address) comes from the
/// master list or favorites; live fields are filled by a GameSpy query.
/// </summary>
public class ServerInfo : INotifyPropertyChanged
{
    public string Address { get; init; } = "";
    public int Port { get; init; } = 27888;

    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _map = "";
    public string Map { get => _map; set => Set(ref _map, value); }

    private string _gameType = "";
    public string GameType { get => _gameType; set => Set(ref _gameType, value); }

    private int _players;
    public int Players { get => _players; set => Set(ref _players, value); }

    private int _maxPlayers;
    public int MaxPlayers { get => _maxPlayers; set => Set(ref _maxPlayers, value); }

    /// <summary>
    /// How many of Players are bots, or -1 when the server does not say.
    ///
    /// The engine's GameSpy responder counts connected clients, and bots are
    /// game objects rather than clients, so numplayers is the HUMAN count -
    /// meaning a bot-filled server looks empty to every browser including
    /// this one. See BotsKnown for where the figure comes from instead.
    /// </summary>
    private int _bots = -1;
    public int Bots
    {
        get => _bots;
        set { Set(ref _bots, value); OnPropertyChanged(nameof(PlayerSummary)); OnPropertyChanged(nameof(HumanPlayers)); }
    }

    public bool BotsKnown => _bots >= 0;

    /// <summary>
    /// People, as opposed to bodies. This is exactly what the engine reports
    /// as numplayers - bots are not clients, so they were never in it.
    /// </summary>
    public int HumanPlayers => Players;

    /// <summary>Everyone in the game, bots included.</summary>
    public int TotalPlayers => Players + (_bots > 0 ? _bots : 0);

    /// <summary>
    /// "6/12" or "6/12 (5 bots)".
    ///
    /// Occupancy first because that is the question the column is read to
    /// answer, bots in brackets because the honest follow-up is "how many of
    /// those are real". Showing humans only would hide a live game; showing
    /// the total alone would misrepresent one.
    /// </summary>
    public string PlayerSummary =>
        _bots > 0 ? $"{TotalPlayers}/{MaxPlayers} ({_bots} bot{(_bots == 1 ? "" : "s")})"
                  : $"{Players}/{MaxPlayers}";

    private ServerSource _source = ServerSource.Cache;
    public ServerSource Source
    {
        get => _source;
        set { Set(ref _source, value); OnPropertyChanged(nameof(SourceLabel)); }
    }

    public string SourceLabel => _source switch
    {
        ServerSource.Master => "Master",
        ServerSource.Manual => "Added",
        ServerSource.Saved  => "Saved",
        ServerSource.Peer   => "Peer",
        ServerSource.Seed   => "Seed",
        _                   => "Cached",
    };

    /// <summary>
    /// When this address last answered a query, UTC. Persisted so an entry
    /// that has been dead for a long time can be dropped from the cache
    /// instead of being re-queried for ever.
    /// </summary>
    public DateTime? LastSeenUtc { get; set; }

    private long _pingMs = -1;
    public long PingMs { get => _pingMs; set => Set(ref _pingMs, value); }

    private bool _online;
    public bool Online { get => _online; set => Set(ref _online, value); }

    private bool _isFavorite;
    public bool IsFavorite { get => _isFavorite; set => Set(ref _isFavorite, value); }

    /// <summary>Manually added by the user (persists across refreshes even unfavorited).</summary>
    public bool IsManual { get; set; }

    /// <summary>Raw key/values from the last \status\ response (rules, player list, etc).</summary>
    public Dictionary<string, string> RawInfo { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string DisplayAddress => $"{Address}:{Port}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // The summary is computed from Players, MaxPlayers and Bots, so any of
        // the three moving has to re-raise it or the column shows a stale
        // figure after a refresh.
        if (name is nameof(Players) or nameof(MaxPlayers))
        {
            OnPropertyChanged(nameof(PlayerSummary));
            OnPropertyChanged(nameof(TotalPlayers));
            OnPropertyChanged(nameof(HumanPlayers));
        }
    }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
