using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ShogoLauncher.Services;

/// <summary>
/// Queries Shogo servers over UDP using the GameSpy v1 protocol
/// (backslash-delimited key\value pairs). Shogo servers answer \status\ on
/// the game port itself (the engine forwards unknown packets to the GameSpy
/// responder) and, for stock ShogoSrv, also on game port + 149.
///
/// Multi-packet responses carry \queryid\N.M and the last packet contains
/// \final\.
/// </summary>
public static class GameSpyQueryClient
{
    public record QueryResult(Dictionary<string, string> Values, long PingMs);

    public static Task<QueryResult?> QueryAsync(string host, int port, int timeoutMs = 2000) =>
        QueryAsync(host, port, @"\status\", timeoutMs);

    /// <summary>
    /// Ask a ShogoFRESH server which other servers it knows about.
    ///
    /// Answers arrive as peer_0..peer_N (each "addr:port") plus numpeers. Only
    /// ShogoFRESH's rebuilt dedicated server implements this - a stock server,
    /// or a listen server, simply does not reply, which is indistinguishable
    /// from a timeout and needs no special handling.
    ///
    /// The point is that discovery survives any single master site: reach one
    /// live server, ask it for peers, and the network describes itself.
    /// </summary>
    public static async Task<List<(string Address, int Port)>> QueryPeersAsync(
        string host, int port, int timeoutMs = 1500)
    {
        var peers = new List<(string, int)>();

        var result = await QueryAsync(host, port, @"\peers\", timeoutMs);
        if (result == null) return peers;

        foreach (var kv in result.Values)
        {
            if (!kv.Key.StartsWith("peer_", StringComparison.OrdinalIgnoreCase)) continue;

            // "addr:port". Split on the LAST colon so an IPv6 literal, if one
            // ever turns up here, does not get cut in half.
            int split = kv.Value.LastIndexOf(':');
            if (split <= 0 || split == kv.Value.Length - 1) continue;

            var addr = kv.Value[..split].Trim();
            if (addr.Length == 0) continue;

            if (!int.TryParse(kv.Value[(split + 1)..], out var p)) continue;
            if (p is < 1024 or > 65535) continue;

            peers.Add((addr, p));
        }

        return peers;
    }

    private static async Task<QueryResult?> QueryAsync(string host, int port, string request, int timeoutMs)
    {
        try
        {
            using var udp = new UdpClient();
            udp.Connect(host, port);

            var sw = Stopwatch.StartNew();
            await udp.SendAsync(Encoding.ASCII.GetBytes(request));

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool sawFinal = false;
            long ping = -1;

            using var cts = new CancellationTokenSource(timeoutMs);
            while (!sawFinal)
            {
                UdpReceiveResult r;
                try
                {
                    r = await udp.ReceiveAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break; // timeout - return whatever we have (or null below)
                }

                if (ping < 0) ping = sw.ElapsedMilliseconds;

                var text = Encoding.ASCII.GetString(r.Buffer);
                sawFinal |= ParsePairs(text, values);
            }

            if (values.Count == 0) return null;
            return new QueryResult(values, ping < 0 ? sw.ElapsedMilliseconds : ping);
        }
        catch (SocketException)
        {
            return null;
        }
    }

    /// <summary>Parse \key\value pairs into dict; returns true if \final\ was present.</summary>
    private static bool ParsePairs(string text, Dictionary<string, string> into)
    {
        bool final = false;
        var parts = text.Split('\\');
        // parts[0] is empty (leading backslash); pairs follow as key,value,key,value...
        for (int i = 1; i + 1 <= parts.Length - 1; i += 2)
        {
            var key = parts[i];
            var value = i + 1 < parts.Length ? parts[i + 1] : "";

            if (key.Equals("final", StringComparison.OrdinalIgnoreCase)) { final = true; i--; continue; }
            if (key.Equals("queryid", StringComparison.OrdinalIgnoreCase)) continue;
            if (key.Length == 0) continue;

            into[key] = value;
        }
        if (text.Contains(@"\final\", StringComparison.OrdinalIgnoreCase)) final = true;
        return final;
    }
}
