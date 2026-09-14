using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace ShogoLauncher.Services;

/// <summary>
/// Downloads the two third-party wrappers (dinputto8, dgVoodoo2) from their
/// OFFICIAL release URLs, verifies every byte against pinned SHA256 digests,
/// and caches the result under %AppData%\ShogoFRESH\shims\.
///
/// WHY THIS EXISTS: the wrappers are the release zip's entire anti-virus
/// false-positive surface (an input wrapper named dinput.dll IS the
/// "keylogger" heuristic's shape; dgVoodoo has a documented history of
/// generic detections). Shipping the zip without them removes the flagged
/// surface; this service puts them back at setup time, from the authors'
/// own releases, with the provenance chain checkable end to end.
///
/// THE ARCHIVE IS NEVER WRITTEN TO DISK, and that is load-bearing, not
/// tidiness. Measured 2026-09-14 on this machine: Microsoft Defender
/// quarantines the OFFICIAL dgVoodoo2_87_4.zip the moment it lands on disk
/// (Trojan:Win32/Kepavll!rfn, a cloud verdict - the zip carries Glide
/// wrappers we never use), while the extracted MS\x86 DLLs pass clean and
/// have sat on this disk for weeks. So a fetch that saved the zip first
/// would be eaten mid-download on a default Windows install. The bytes stay
/// in memory: digest-verified there, the five needed files extracted there,
/// and only those files - the ones AV accepts - ever touch the disk.
///
/// Pinned entries are found by FILE NAME and confirmed by HASH, not by
/// archive path. The dgVoodoo zip carries four same-named builds of each
/// DLL (x86, x64, arm64 variants); the digest is the identity, so the
/// lookup cannot pick the wrong one and a layout change in a future zip
/// re-pin cannot silently break the extraction.
///
/// The pin table below is duplicated in Launcher\Redist\README.md (the
/// human-auditable copy with the release links). preflight asserts the two
/// agree - the one-fact-twice rule.
/// </summary>
public static class ShimFetchService
{
    public record PinnedFile(string Name, string Sha256);

    public record ShimSource(
        string Id,              // matches GameSetupService.FixDefinition.Id
        string Version,         // upstream release tag, part of the cache path
        string Url,             // official release asset, https, exact
        string? ArchiveSha256,  // null = Url IS the single pinned file
        PinnedFile[] Files,
        string ReleasePage);    // for the offline message

    public static readonly ShimSource[] Sources =
    {
        new("dinputto8", "1.1.100.0",
            "https://github.com/elishacloud/dinputto8/releases/download/v1.1.100.0/dinput.dll",
            ArchiveSha256: null,
            new[] { new PinnedFile("dinput.dll",
                        "f96bb1101e23a6477043a3859cbf875a990eaa62ee6849f87e967b3dd963f781") },
            "https://github.com/elishacloud/dinputto8/releases/tag/v1.1.100.0"),

        new("dgvoodoo", "2.87.4",
            "https://github.com/dege-diosg/dgVoodoo2/releases/download/v2.87.4/dgVoodoo2_87_4.zip",
            ArchiveSha256: "74aeb464d829db80e3f4aa8fae235e6e3b38fc01188776c5c2376bb0dea0956e",
            new[]
            {
                new PinnedFile("DDraw.dll",       "f373087c5b78a123d8de9cc33e660a11e3a664b31924f897febaff689429c66b"),
                new PinnedFile("D3DImm.dll",      "bfe7555d86acd9a8cf75fb6fa52984bd16531a132e5dcd166938fcd072248146"),
                new PinnedFile("D3D8.dll",        "d6e8931e785e267f926c049d4d1257b00771e2de8c57c8f39e434ddf6185df06"),
                new PinnedFile("D3D9.dll",        "db1c445f7bcf699df1e175e974c779bdc7e19a468680a44884b1ab7078888d04"),
                new PinnedFile("dgVoodooCpl.exe", "f916497c2dc4378353108c6588082f042795b62854cd3e54051c7bcba4b53d1f"),
            },
            "https://github.com/dege-diosg/dgVoodoo2/releases/tag/v2.87.4"),
    };

    public static ShimSource? SourceFor(string fixId) =>
        Sources.FirstOrDefault(s => s.Id == fixId);

    /// <summary>Versioned per pin, so bumping a pin fetches fresh instead of
    /// trusting a cache built for the old release.</summary>
    public static string CacheDir(ShimSource s) =>
        Path.Combine(AppPaths.Root, "shims", $"{s.Id}-{s.Version}");

    public class FetchException : Exception
    {
        public FetchException(string message, Exception? inner = null) : base(message, inner) { }
    }

    // GitHub refuses requests with no User-Agent. Timeout sized for a 9 MB
    // asset on a slow line, not for an API ping.
    private static readonly HttpClient Http = CreateClient();
    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ShogoFRESH-Launcher");
        return c;
    }

    /// <summary>
    /// Every pinned file present in the cache AND matching its digest.
    /// Re-hashed on every check rather than trusted once: the cache is a
    /// directory an AV or a person can reach into, and a payload that no
    /// longer matches its pin must read as absent, not installed.
    /// </summary>
    public static bool CacheComplete(ShimSource s)
    {
        var dir = CacheDir(s);
        return s.Files.All(f => FileMatches(Path.Combine(dir, f.Name), f.Sha256));
    }

    private static bool FileMatches(string path, string sha256)
    {
        try
        {
            if (!File.Exists(path)) return false;
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream))
                          .Equals(sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static string HashOf(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>
    /// Make the cache complete for one shim: download, verify, extract.
    /// Throws FetchException with a message that carries the offline path -
    /// the official page and the drop-in folder - because the person reading
    /// it is, by definition, someone whose download just failed.
    /// </summary>
    public static async Task EnsureAsync(ShimSource s, Action<string>? progress = null,
                                         string? redistDir = null)
    {
        // The conf side-car is part of "complete" for dgVoodoo even though it
        // is not pinned - without it the early return could hand back a cache
        // that GameSetupService then rejects as an incomplete payload.
        if (CacheComplete(s) &&
            (s.Id != "dgvoodoo" || File.Exists(Path.Combine(CacheDir(s), "dgVoodoo.conf"))))
            return;

        byte[] bytes;
        try
        {
            progress?.Invoke($"Downloading {s.Id} {s.Version} from the official release…");
            bytes = await Http.GetByteArrayAsync(s.Url);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new FetchException(
                $"Could not download {s.Id} {s.Version}.\n\n" +
                $"Offline path: get it yourself from the official release\n  {s.ReleasePage}\n" +
                $"and drop the file(s) into Redist\\{s.Id}\\ beside the launcher - " +
                "the Setup card uses them from there.", ex);
        }

        var extracted = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        if (s.ArchiveSha256 is null)
        {
            var pin = s.Files.Single();
            if (HashOf(bytes) != pin.Sha256)
                throw new FetchException(
                    $"{s.Id}: the downloaded {pin.Name} does not match the pinned digest. " +
                    "Not installing it. This means the release asset changed after it was " +
                    $"pinned - verify by hand at {s.ReleasePage}.");
            extracted[pin.Name] = bytes;
        }
        else
        {
            if (HashOf(bytes) != s.ArchiveSha256)
                throw new FetchException(
                    $"{s.Id}: the downloaded archive does not match the pinned digest. " +
                    $"Not extracting it. Verify by hand at {s.ReleasePage}.");

            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

            foreach (var pin in s.Files)
            {
                foreach (var entry in zip.Entries)
                {
                    if (!Path.GetFileName(entry.FullName).Equals(pin.Name, StringComparison.OrdinalIgnoreCase))
                        continue;
                    using var es = entry.Open();
                    using var buf = new MemoryStream();
                    es.CopyTo(buf);
                    var data = buf.ToArray();
                    if (HashOf(data) == pin.Sha256) { extracted[pin.Name] = data; break; }
                }
                if (!extracted.ContainsKey(pin.Name))
                    throw new FetchException(
                        $"{s.Id}: no entry in the archive matched the pinned digest for {pin.Name}. " +
                        $"The pins describe the {s.Version} release exactly, so this archive is not it.");
            }

            // dgVoodoo needs a dgVoodoo.conf beside the DLLs. The tuned one
            // ships in Redist\dgvoodoo (plain text, no AV surface) and wins;
            // the archive's stock default is the clean-checkout fallback.
            // Not pinned: it is a config, and Apply() retunes it anyway.
            if (s.Id == "dgvoodoo")
            {
                var shipped = redistDir is null ? null : Path.Combine(redistDir, "dgVoodoo.conf");
                if (shipped is not null && File.Exists(shipped))
                    extracted["dgVoodoo.conf"] = File.ReadAllBytes(shipped);
                else
                {
                    var confEntry = zip.Entries.FirstOrDefault(e =>
                        Path.GetFileName(e.FullName).Equals("dgVoodoo.conf", StringComparison.OrdinalIgnoreCase));
                    if (confEntry is not null)
                    {
                        using var es = confEntry.Open();
                        using var buf = new MemoryStream();
                        es.CopyTo(buf);
                        extracted["dgVoodoo.conf"] = buf.ToArray();
                    }
                }
            }
        }

        progress?.Invoke($"Verified {s.Id} {s.Version} against the pinned digests.");

        var dir = CacheDir(s);
        Directory.CreateDirectory(dir);
        foreach (var (name, data) in extracted)
        {
            // Write-then-move, so a failure mid-write cannot leave a partial
            // file under the real name (which the hash check would reject
            // anyway - this just avoids ever presenting one).
            var tmp = Path.Combine(dir, name + ".fetching");
            File.WriteAllBytes(tmp, data);
            File.Move(tmp, Path.Combine(dir, name), overwrite: true);
        }
    }
}
