using System.IO;

namespace ShogoLauncher.Services;

/// <summary>
/// Checks the DTX textures inside a .rez against what the engine will actually
/// load.
///
/// <para>
/// <b>The format is specified in Docs/public/DTXFORMAT.md.</b> Read it before changing
/// anything here. There are two implementations of that spec - this one and
/// <c>Tools/png2dtx.py</c>, which writes the files this reads. Neither is the
/// authority; the document is. preflight asserts the constants agree.
/// </para>
/// <para>
/// This exists because the interesting failure is <b>silent</b>. A texture with
/// too many mipmap levels does not crash, does not fall back to
/// <c>default_texture.dtx</c>, and writes nothing to any log - the model simply
/// renders WHITE and the game carries on. A modder who ships one has no way to
/// find out why, and neither does the player who installs it. Everything else
/// here is cheap to check once the archive is being read anyway.
/// </para>
/// <para>
/// Only the 44-byte header of each entry is read. The archive directory already
/// carries every resource's size, which is what the size-model check needs, so a
/// 60MB texture pack is validated without loading a single texture.
/// </para>
/// </summary>
public static class DtxValidator
{
    private const int HeaderSize  = 44;
    private const int PaletteSize = 1024;
    private const int Version     = -2;
    private const int FlagAlpha   = 0x2;

    // A trailing section: 32 bytes of header, the name NUL-terminated at the
    // front, its payload length at +28. png2dtx.py carries sections across a
    // conversion using the same two numbers.
    private const int SectionHeaderSize = 32;
    private const int SectionLengthAt   = 28;

    /// <summary>
    /// The engine renders at most eight mipmap levels. Nine renders the model
    /// white. Measured in game 2026-08-03, bracketed at one base size: a
    /// 256x256 with 8 levels (down to 2x2) draws correctly, the same texture
    /// with 9 (down to 1x1) does not. It is a count limit and not a minimum
    /// dimension - 2x2 is fine.
    ///
    /// <para>
    /// Every one of the 4,921 textures Monolith shipped uses 4, so no amount of
    /// looking at the game's own content would ever have revealed this.
    /// </para>
    /// </summary>
    public const int MaxMipmaps = 8;

    /// <summary>
    /// Largest dimension confirmed to render. 512 and 1024 are solid; 2048 drew
    /// correctly but the process was gone from an automated check shortly after,
    /// so it is reported as untested rather than broken.
    /// </summary>
    public const int VerifiedMaxDimension = 1024;

    public enum Level { Error, Warning }

    /// <param name="Entry">Archive path of the texture, for the operator to find it.</param>
    public record Finding(Level Level, string Entry, string Message);

    /// <summary>
    /// Every problem found in the archive's textures. Empty when the archive is
    /// clean, and empty when it cannot be read at all - an unreadable archive is
    /// ModManager's business to report, not this one's, and answering "no
    /// texture problems" for a file we never parsed is the honest answer to the
    /// question actually asked.
    /// </summary>
    public static List<Finding> Validate(string rezPath, int maxFindings = 50)
    {
        var findings = new List<Finding>();

        var entries = RezArchive.TryRead(rezPath);
        if (entries is null) return findings;

        FileStream file;
        try
        {
            file = File.OpenRead(rezPath);
        }
        catch (IOException) { return findings; }
        catch (UnauthorizedAccessException) { return findings; }

        using (file)
        {
            foreach (var e in entries)
            {
                if (findings.Count >= maxFindings) break;
                if (!e.Ext.Equals("DTX", StringComparison.OrdinalIgnoreCase)) continue;

                var problem = Check(file, e);
                if (problem is not null) findings.Add(problem);
            }
        }

        return findings;
    }

    private static Finding? Check(FileStream file, RezArchive.RezEntry e)
    {
        if (e.Size < HeaderSize + PaletteSize)
            return new Finding(Level.Error, e.FullName,
                $"too small to be a texture ({e.Size} bytes)");

        var h = new byte[HeaderSize];
        try
        {
            file.Position = e.Offset;
            int got = 0;
            while (got < h.Length)
            {
                int n = file.Read(h, got, h.Length - got);
                if (n <= 0) return new Finding(Level.Error, e.FullName, "header is truncated");
                got += n;
            }
        }
        catch (IOException) { return null; }

        // Anything that is not a version -2 texture is left alone. SHOGO.REZ
        // itself holds 34 DTX-typed entries that are not textures at all - the
        // editor's texture-layout files under TEXTURES\LAYOUTS\, which carry a
        // different header entirely. Reporting those would mean every mod that
        // bundles stock content lights up with errors about Monolith's own
        // files, which is the same false-alarm failure that the "OBJECT"
        // substring scan produced 4,674 times before ModManager stopped
        // guessing. Silence on what we do not recognise; findings only on what
        // we know breaks.
        int resType = ReadI32(h, 0);
        int version = ReadI32(h, 4);

        if (resType != 0 || version != Version) return null;

        int width  = ReadU16(h, 8);
        int height = ReadU16(h, 10);
        int mips   = ReadU16(h, 12);
        int flags  = ReadI32(h, 16);

        if (!IsPowerOfTwo(width) || !IsPowerOfTwo(height))
            return new Finding(Level.Error, e.FullName,
                $"{width}x{height} - dimensions must be powers of two");

        if (mips < 1)
            return new Finding(Level.Error, e.FullName, "claims no mipmap levels");

        // The one that fails silently, so it is worth the clearest wording.
        if (mips > MaxMipmaps)
            return new Finding(Level.Error, e.FullName,
                $"{mips} mipmap levels - the engine renders at most {MaxMipmaps}. " +
                "This texture will draw WHITE in game with no other symptom");

        // THE SIZE DECIDES, NOT nSections. That field is not evidence: 24 of
        // Monolith's own version -2 textures carry a nonzero value in it -
        // 17023, 58137, 6422 - and no sections whatsoever, the file ending
        // exactly where the pixels do. Walking that many blocks would report
        // two dozen shipped textures as broken, which is the false alarm this
        // validator exists to avoid.
        //
        // This used to skip the size check entirely whenever the field was set,
        // because the trailing layout was unknown. It is known now (DTXFORMAT),
        // so the 42 textures that were exempt are checked like any other.
        long pixelsEnd = ExpectedSize(width, height, mips, (flags & FlagAlpha) != 0);

        if (e.Size < pixelsEnd)
            return new Finding(Level.Error, e.FullName,
                $"is {e.Size} bytes, but {width}x{height} with {mips} mipmaps" +
                ((flags & FlagAlpha) != 0 ? " and alpha" : "") + $" needs {pixelsEnd}");

        if (e.Size > pixelsEnd && !SectionsEndAtEntryEnd(file, e, pixelsEnd))
            return new Finding(Level.Error, e.FullName,
                $"has {e.Size - pixelsEnd} bytes after the last mipmap that are not a " +
                "valid section chain - a texture is its mipmaps, then zero or more " +
                "sections, and nothing else");

        if (width > VerifiedMaxDimension || height > VerifiedMaxDimension)
            return new Finding(Level.Warning, e.FullName,
                $"{width}x{height} is larger than anything verified to render " +
                $"({VerifiedMaxDimension}). It may well work - it is untested, not known bad");

        return null;
    }

    /// <summary>
    /// Header + palette + one byte per texel across the chain, plus half a byte
    /// per texel again when the alpha plane is present.
    ///
    /// <para>
    /// The alpha plane is packed <b>per level and rounded up</b>. A 1x1 level
    /// holds one texel, which still costs a whole byte with its high nibble
    /// unused. Rounding down instead is wrong by one byte for every texture
    /// small enough to reach a 1x1 level - which is how this was found:
    /// <c>SKINS\MULTIPLAY.DTX</c> is 1196 bytes where the floor gives 1195.
    /// </para>
    /// <para>
    /// Exact for 4,879 of Monolith's 4,921 textures. The other 42 set
    /// <c>nSections</c> and carry a trailing blob of unknown shape; callers
    /// must skip them rather than treat a size they cannot predict as an error.
    /// </para>
    /// </summary>
    public static long ExpectedSize(int width, int height, int mips, bool hasAlpha)
    {
        long total = HeaderSize + PaletteSize;

        for (int i = 0; i < mips; i++)
        {
            long w = Math.Max(1, width >> i);
            long h = Math.Max(1, height >> i);

            total += w * h;
            if (hasAlpha) total += (w * h + 1) / 2;
        }

        return total;
    }

    /// <summary>
    /// Walks the trailing sections from the end of the mipmaps and reports
    /// whether the chain lands exactly on the end of the entry.
    ///
    /// <para>
    /// Landing exactly is the whole test. A texture is its mipmaps followed by
    /// zero or more sections and nothing else, so an offset that overshoots or
    /// stops short means the trailing bytes are not sections - which is the
    /// only way to tell a real carrier from a file with rubbish in
    /// <c>nSections</c>, and there are two dozen of the latter in SHOGO.REZ.
    /// </para>
    /// </summary>
    private static bool SectionsEndAtEntryEnd(
        FileStream file, RezArchive.RezEntry e, long pixelsEnd)
    {
        var head = new byte[SectionHeaderSize];
        long at = pixelsEnd;

        while (at < e.Size)
        {
            if (at + SectionHeaderSize > e.Size) return false;

            try
            {
                file.Position = e.Offset + at;
                int got = 0;
                while (got < head.Length)
                {
                    int n = file.Read(head, got, head.Length - got);
                    if (n <= 0) return false;
                    got += n;
                }
            }
            catch (IOException) { return false; }

            // Unsigned: a length with the top bit set is nonsense rather than
            // negative, and long arithmetic keeps the walk from wrapping.
            long length = (uint)ReadI32(head, SectionLengthAt);
            at += SectionHeaderSize + length;
        }

        return at == e.Size;
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    private static int ReadU16(byte[] b, int at) => b[at] | (b[at + 1] << 8);

    private static int ReadI32(byte[] b, int at) =>
        b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24);
}
