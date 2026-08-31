using System.IO;
using System.Text;

namespace ShogoLauncher.Services;

/// <summary>
/// Reads the directory of a LithTech .rez archive.
///
/// <para>
/// <b>The format is specified in Docs/public/REZFORMAT.md.</b> Read it before
/// changing anything here. It carries the layout, the four things about it
/// that are not obvious, the rules for anyone touching a reader, and how to
/// verify a change against <c>lithrez</c>.
/// </para>
/// <para>
/// There are two implementations of that spec — this one and
/// <c>Shared/FreshRez.cpp</c>, which the server needs because it cannot call
/// C#. Neither is the authority; the document is. A change to one that is
/// not a change to the document is a change that will be lost.
/// </para>
/// <para>
/// This replaced two byte-scanning heuristics that each shipped a bug of
/// their own, and the second was only visible because the first fix made the
/// numbers comparable. Both are recorded in the spec so the reasoning
/// survives the code.
/// </para>
/// </summary>
public static class RezArchive
{
    private const int BannerLength = 127;

    /// <summary>One resource inside the archive.</summary>
    /// <param name="Path">Directory path, backslash-terminated, "" at the root.</param>
    /// <param name="Ext">Type code the right way round, uppercase ("DAT", "DLL").</param>
    /// <param name="Keys">RezMgr's per-entry key string. Empty in every
    /// archive seen so far - it is the format's only metadata slot, and
    /// nothing Monolith shipped populated it.</param>
    public record RezEntry(
        string Path, string Name, string Ext, long Offset, long Size, uint Id, string Keys)
    {
        public string FullName => Path + Name + (Ext.Length > 0 ? "." + Ext : "");
    }

    /// <summary>
    /// Every resource in the archive, or null if it is not one we can read.
    /// Null rather than an exception because the caller is usually a list
    /// being built from whatever the user dropped in Custom\, where one
    /// unreadable file should not take the rest of the list with it.
    /// </summary>
    public static List<RezEntry>? TryRead(string rezPath)
    {
        try
        {
            return Read(File.ReadAllBytes(rezPath));
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (InvalidDataException) { return null; }
    }

    /// <summary>
    /// One entry's bytes, without writing anything to disk. For the small
    /// text entries the launcher wants to read directly - a manifest is a
    /// few hundred bytes and extracting it to a temp file to read it back
    /// would be silly.
    /// </summary>
    public static byte[]? ReadEntryBytes(string rezPath, RezEntry entry)
    {
        try
        {
            using var f = File.OpenRead(rezPath);

            if (entry.Offset < 0 || entry.Size < 0 || entry.Offset + entry.Size > f.Length)
                return null;

            var buf = new byte[entry.Size];
            f.Position = entry.Offset;

            int got = 0;
            while (got < buf.Length)
            {
                int n = f.Read(buf, got, buf.Length - got);
                if (n <= 0) return null;
                got += n;
            }

            return buf;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static List<RezEntry> Read(byte[] b)
    {
        if (b.Length < BannerLength + 12 ||
            b[124] != 0x0D || b[125] != 0x0A || b[126] != 0x1A)
            throw new InvalidDataException("Not a LithTech resource file.");

        uint version  = ReadU32(b, BannerLength);
        long rootOff  = ReadU32(b, BannerLength + 4);
        long rootSize = ReadU32(b, BannerLength + 8);

        if (version != 1)
            throw new InvalidDataException($"Unsupported resource file version {version}.");
        if (rootOff + rootSize > b.Length)
            throw new InvalidDataException("Directory runs past the end of the file.");

        var found = new List<RezEntry>();

        // Offsets already visited. A malformed or hostile archive could point
        // a sub-directory at its own parent; without this the walk would
        // recurse until the stack gave out. The depth cap is the same
        // argument for a chain rather than a cycle.
        var seen = new HashSet<long>();

        Walk(b, rootOff, rootSize, "", 0, found, seen);
        return found;
    }

    private static void Walk(byte[] b, long off, long size, string path,
                             int depth, List<RezEntry> found, HashSet<long> seen)
    {
        if (depth > 16 || off < 0 || size < 0 || off + size > b.Length) return;
        if (!seen.Add(off)) return;

        long at = off, end = off + size;

        while (at + 16 <= end)
        {
            uint flag  = ReadU32(b, at);
            long dOff  = ReadU32(b, at + 4);
            long dSize = ReadU32(b, at + 8);
            at += 16;                          // flag, offset, size, time

            if (flag == 1)
            {
                if (!ReadCString(b, ref at, end, out var dirName)) return;
                Walk(b, dOff, dSize, path + dirName + "\\", depth + 1, found, seen);
            }
            else if (flag == 0)
            {
                if (at + 12 > end) return;
                uint id  = ReadU32(b, at);
                uint ext = ReadU32(b, at + 4);
                at += 12;                      // id, ext, numKeys

                if (!ReadCString(b, ref at, end, out var name)) return;
                if (!ReadCString(b, ref at, end, out var keys)) return;

                found.Add(new RezEntry(path, name, DecodeExt(ext), dOff, dSize, id, keys));
            }
            else
            {
                // Not a shape we know. Stop rather than resynchronise: a
                // wrong guess here would invent entries, and inventing
                // entries is exactly what the byte scans used to do.
                return;
            }
        }
    }

    /// <summary>
    /// The type DWORD holds four characters little-endian, so on disk they
    /// sit back to front: a level is tagged "TAD", a DLL "LLD", Object.lto
    /// "OTL". Reverse them and drop the padding NULs.
    /// </summary>
    private static string DecodeExt(uint v)
    {
        Span<char> c = stackalloc char[4];
        int n = 0;
        for (int i = 3; i >= 0; i--)
        {
            var ch = (char)((v >> (i * 8)) & 0xFF);
            if (ch != '\0') c[n++] = ch;
        }
        return new string(c[..n]).Trim().ToUpperInvariant();
    }

    private static bool ReadCString(byte[] b, ref long at, long end, out string s)
    {
        long start = at;
        while (at < end && b[at] != 0) at++;
        if (at >= end) { s = ""; return false; }        // unterminated: malformed
        s = Encoding.Latin1.GetString(b, (int)start, (int)(at - start));
        at++;                                           // step over the NUL
        return true;
    }

    private static uint ReadU32(byte[] b, long at) =>
        (uint)(b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24));

    // ----------------------------------------------------------------- //
    //  Extraction
    // ----------------------------------------------------------------- //

    /// <param name="Written">Files created.</param>
    /// <param name="Bytes">Total bytes written.</param>
    /// <param name="Renamed">Entry names that held characters Windows will
    /// not accept in a filename, or that collided with one already written.</param>
    /// <param name="Refused">Entries whose path tried to leave the
    /// destination folder. Reported rather than silently dropped - a mod
    /// containing one is a thing the operator should know about.</param>
    public record ExtractResult(
        int Written, long Bytes, List<string> Renamed, List<string> Refused);

    /// <summary>
    /// Writes the chosen entries under <paramref name="destDir"/>, recreating
    /// the archive's folder structure.
    ///
    /// <para>
    /// Entry names come out of a file somebody else made, so they are treated
    /// as hostile input. A name like <c>..\..\Windows\System32\x</c> would
    /// otherwise write wherever it liked - the zip-slip bug, which is old,
    /// well known, and still ships regularly. Every output path is resolved to
    /// a full path and checked to be inside the destination before anything is
    /// opened; anything that is not is refused and named in the result.
    /// </para>
    /// <para>
    /// Characters Windows rejects in a filename are replaced rather than
    /// refused, because that is a portability quirk of a 1998 archive rather
    /// than an attack, and losing the file would be the unhelpful answer.
    /// Collisions are suffixed for the same reason: SHOGO.REZ holds 6,135
    /// entries of which 6,000 are distinct, so overwriting on collision would
    /// quietly lose files.
    /// </para>
    /// </summary>
    public static ExtractResult Extract(
        string rezPath, IEnumerable<RezEntry> entries, string destDir,
        CancellationToken cancel = default)
    {
        var data = File.ReadAllBytes(rezPath);

        // The comparison root. GetFullPath resolves any "..", and the
        // trailing separator stops "C:\out-evil" counting as inside "C:\out".
        var root = Path.GetFullPath(destDir);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
            root += Path.DirectorySeparatorChar;

        int written = 0;
        long bytes = 0;
        var renamed = new List<string>();
        var refused = new List<string>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entries)
        {
            cancel.ThrowIfCancellationRequested();

            if (e.Offset < 0 || e.Size < 0 || e.Offset + e.Size > data.Length)
            {
                refused.Add(e.FullName + " (data outside the file)");
                continue;
            }

            if (!SafeRelativePath(e, out var relative, out bool wasCleaned))
            {
                refused.Add(e.FullName);
                continue;
            }

            var full = Path.GetFullPath(Path.Combine(root, relative));

            // The check that matters. Belt and braces with SafeRelativePath:
            // that one rejects the shapes we know, this one rejects anything
            // that lands outside regardless of how it got there.
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                refused.Add(e.FullName);
                continue;
            }

            // Distinct entries can share a name - overwriting would lose one
            // silently, which is the failure mode worth avoiding here.
            if (!taken.Add(full))
            {
                full = Uniquify(full, taken);
                wasCleaned = true;
            }

            if (wasCleaned) renamed.Add(e.FullName);

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);

            using (var to = File.Create(full))
                to.Write(data, (int)e.Offset, (int)e.Size);

            written++;
            bytes += e.Size;
        }

        return new ExtractResult(written, bytes, renamed, refused);
    }

    /// <summary>
    /// The entry's path as something safe to write under a root, or false if
    /// it cannot be made safe. <paramref name="cleaned"/> reports that
    /// characters had to be substituted.
    /// </summary>
    private static bool SafeRelativePath(RezEntry e, out string relative, out bool cleaned)
    {
        relative = "";
        cleaned = false;

        var name = e.Name + (e.Ext.Length > 0 ? "." + e.Ext : "");
        var parts = new List<string>();

        foreach (var raw in (e.Path + name).Split('\\', '/'))
        {
            if (raw.Length == 0) continue;

            // "." is noise; ".." is the attack; a drive-qualified or rooted
            // segment would escape as well. None of these appear in an
            // archive written by lithrez.
            if (raw == ".") continue;
            if (raw == ".." || raw.Contains(':')) return false;

            var clean = raw;
            foreach (var bad in Path.GetInvalidFileNameChars())
                if (clean.Contains(bad)) { clean = clean.Replace(bad, '_'); cleaned = true; }

            // Windows will not create these under any extension, and a
            // request for one is not a filename we can honour.
            var stem = clean.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
                (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
                 char.IsDigit(stem[3])))
            {
                clean = "_" + clean;
                cleaned = true;
            }

            // Trailing dots and spaces are silently stripped by Windows,
            // which would turn two distinct names into one.
            var trimmed = clean.TrimEnd('.', ' ');
            if (trimmed.Length == 0) return false;
            if (trimmed != clean) cleaned = true;

            parts.Add(trimmed);
        }

        if (parts.Count == 0) return false;

        relative = Path.Combine(parts.ToArray());
        return true;
    }

    private static string Uniquify(string full, HashSet<string> taken)
    {
        var dir  = Path.GetDirectoryName(full)!;
        var stem = Path.GetFileNameWithoutExtension(full);
        var ext  = Path.GetExtension(full);

        for (int n = 2; ; n++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({n}){ext}");
            if (taken.Add(candidate)) return candidate;
        }
    }
}
