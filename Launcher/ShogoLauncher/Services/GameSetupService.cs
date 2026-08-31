using System.IO;
using System.Text.Json;

namespace ShogoLauncher.Services;

/// <summary>
/// Detects, applies, and undoes the game fixes that make Shogo run on
/// modern Windows, plus the ShogoFRESH mod overlay.
///
/// Payloads live in Redist\&lt;fix&gt;\ next to the launcher exe (see
/// Redist\README.md; prepare-redist.ps1 populates it). Applying a fix
/// backs up any file it overwrites and records a manifest under
/// %AppData%\ShogoLauncher\fixes\, so Undo restores the exact prior state.
/// A fix that is present in the game dir without a manifest was installed
/// outside the launcher (e.g. by hand or by ShogoFix) - detected as
/// installed, but Undo is disabled rather than guessing.
///
/// DirectPlay is special: it is a Windows optional feature, not files we
/// copy - detection checks SysWOW64\dplay.dll and enabling runs DISM
/// elevated (user-initiated UAC).
/// </summary>
public class GameSetupService
{
    // NewerInstalled: the game directory holds files NEWER than this
    // launcher's payload. That is a test build somebody delivered by hand,
    // and re-applying would silently revert it - see NewerThanPayload.
    public enum FixStatus { Installed, InstalledExternally, NotInstalled, UpdateAvailable, PayloadMissing, NewerInstalled }

    // Optional: the fix is complete without it. Everything else in Files is
    // required, and a fix missing any required file reports PayloadMissing
    // rather than installing half of itself. The font is the first file we
    // ship conditionally (its licence has to be there to ship it at all), and
    // a server that falls back to Cascadia Mono is not a broken server.
    // Guarded: refuse to overwrite this file when the game directory's copy
    // is NEWER than ours. OPT-IN, and deliberately so - see NewerThanPayload
    // for what a blanket guard did on its first outing. Only files this
    // payload OWNS outright belong here: our game code, which is what gets
    // hand-swapped for a test build. Never a config the game or launcher
    // rewrites, and never an ancillary file that can drift.
    public record FixFile(string Name, bool SkipIfExists = false, bool Optional = false,
                          bool Guarded = false);

    public record FixDefinition(
        string Id,
        string Title,
        string Description,
        FixFile[] Files,
        string[] DetectFiles,
        bool DetectByContent = false,    // hash-compare against payload (for files stock also ships)

        // Files this fix USED to install and no longer does. Named
        // explicitly rather than inferred from "in the manifest but not in
        // Files", because several fixes legitimately back up files they do
        // not ship - the defaults fix edits autoexec.cfg and backs it up
        // whole - and inferring would have restored those on every
        // re-install. See the retirement pass in Install().
        string[]? Retired = null);

    public static readonly FixDefinition[] Fixes =
    {
        new(
            "dinputto8",
            "Input fix (dinputto8)",
            "Translates the engine's 1998 DirectInput calls to DirectInput8. Without it the game fails to start on modern Windows. Zlib-licensed, github.com/elishacloud/dinputto8.",
            new[] { new FixFile("dinput.dll") },
            new[] { "dinput.dll" }),
        new(
            "dgvoodoo",
            "Graphics fix (dgVoodoo2)",
            "Wraps the engine's DirectDraw/Direct3D onto Direct3D 11: modern resolutions, windowed/borderless modes, monitor selection (see Settings). Freeware by Dege.",
            new[]
            {
                new FixFile("DDraw.dll"), new FixFile("D3DImm.dll"),
                new FixFile("D3D8.dll"), new FixFile("D3D9.dll"),
                new FixFile("dgVoodooCpl.exe"),
                new FixFile("dgVoodoo.conf", SkipIfExists: true), // never clobber user's tuned config
            },
            new[] { "DDraw.dll", "D3DImm.dll" }),
        new(
            "am18",
            "Music fix (AM18.dll)",
            "Patched in-game music DLL from the community ShogoFix package - the version stock installs ship fails to play the soundtrack on modern Windows. Detected by content: stock also has an AM18.dll, so only a byte-identical match counts as installed.",
            new[] { new FixFile("AM18.dll") },
            new[] { "AM18.dll" },
            DetectByContent: true),
        new(
            "shogofresh",
            "ShogoFRESH mod",
            "The modernized game code: widescreen/FOV, HUD scaling, bug fixes, server rules. Ships as ShogoFRESH.rez, loaded ahead of the base archive - retail keeps its game code inside SHOGO.REZ, so loose DLLs alone are ignored by the engine. Loose copies are installed too (harmless). Includes the rebuilt dedicated server, installed as FreshSrv.exe so it sits BESIDE the stock ShogoSrv.exe instead of replacing it: fixed query responder, bot support, configurable master registration. It still reads ShogoSrv.cfg, so existing server configs and profiles keep working.",
            new[]
            {
                // Guarded: these six ARE the test build. Everything the
                // re-staging trap destroys is in this list, and nothing that
                // is not our own compiled output belongs in it.
                new FixFile("ShogoFRESH.rez", Guarded: true),
                new FixFile("CShell.dll", Guarded: true), new FixFile("Object.lto", Guarded: true),
                new FixFile("CRes.dll", Guarded: true), new FixFile("SRes.dll", Guarded: true),
                new FixFile(HostService.ServerExe, Guarded: true),

                // Fira Code for the server console, loaded from beside the exe
                // with AddFontResourceEx(FR_PRIVATE) rather than installed -
                // see ShogoServ/FreshTheme.cpp. Optional in both directions:
                // prepare-redist.ps1 ships the pair or neither (SIL OFL 1.1
                // requires the licence to travel with the font), and the
                // server falls back to Cascadia Mono when it is absent.
                new FixFile("FiraCode-Regular.ttf", Optional: true),
                new FixFile("FiraCode-OFL.txt",     Optional: true),
            },
            new[] { "ShogoFRESH.rez" },
            Retired: new[] { HostService.LegacyServerExe }),
        new(
            "defaults",
            "Recommended defaults",
            "Curated defaults: WASD movement in defkeybd.cfg (arrows stay as secondary binds; 'restore defaults' in-game and in Keybinds uses this) and recommended settings merged into autoexec.cfg (20/s net rate, mouse look, High detail). Your other settings are untouched; Undo restores both files exactly.",
            new[] { new FixFile("defkeybd.cfg") },
            Array.Empty<string>()),   // no file-presence detection; manifest decides
    };

    public string GameDir { get; }
    public string RedistRoot { get; }

    private static string ManifestDir =>
        Path.Combine(AppPaths.Root,
                     "fixes");

    public GameSetupService(string gameDir)
    {
        GameDir = gameDir;
        RedistRoot = Path.Combine(AppContext.BaseDirectory, "Redist");
    }

    // The "defaults" fix ships in Defaults\ (checked into the repo/package),
    // not in Redist\ (binary payloads populated separately).
    private string PayloadDir(FixDefinition fix) =>
        fix.Id == "defaults"
            ? Path.Combine(AppContext.BaseDirectory, "Defaults")
            : Path.Combine(RedistRoot, fix.Id);
    private string ManifestPath(FixDefinition fix) => Path.Combine(ManifestDir, fix.Id + ".json");
    private string BackupDir(FixDefinition fix) => Path.Combine(GameDir, "ShogoFRESH_Backup", fix.Id);

    private bool PayloadHas(FixDefinition fix, FixFile f) =>
        File.Exists(Path.Combine(PayloadDir(fix), f.Name));

    public bool HasPayload(FixDefinition fix) =>
        fix.Files.Where(f => !f.Optional).All(f => PayloadHas(fix, f));

    public FixStatus GetStatus(FixDefinition fix)
    {
        // No detect files = only the manifest can prove installation
        // (e.g. the defaults fix, whose effect is merged config vars).
        if (fix.DetectFiles.Length == 0)
        {
            if (!HasPayload(fix)) return File.Exists(ManifestPath(fix)) ? FixStatus.Installed : FixStatus.PayloadMissing;
            if (!File.Exists(ManifestPath(fix))) return FixStatus.NotInstalled;
            // Installed; a changed shipped payload (e.g. new default binds)
            // means there is an update to re-apply.
            return PayloadCurrent(fix) ? FixStatus.Installed : FixStatus.UpdateAvailable;
        }

        bool detected = fix.DetectByContent
            ? fix.DetectFiles.All(f => ContentMatchesPayload(fix, f))
            : fix.DetectFiles.All(f => File.Exists(Path.Combine(GameDir, f)));

        if (detected)
        {
            if (HasPayload(fix) && !PayloadCurrent(fix))
            {
                // Which way round is it? "Different" is not "older" - a
                // hand-delivered test build is different too, and reporting
                // that as an update is what invites the click that destroys
                // it. Refusing inside Apply alone would be a dialog somebody
                // dismisses mid-playtest; not offering the button cannot be
                // missed.
                return NewerThanPayload(fix).Count > 0
                     ? FixStatus.NewerInstalled
                     : FixStatus.UpdateAvailable;
            }
            return File.Exists(ManifestPath(fix)) ? FixStatus.Installed : FixStatus.InstalledExternally;
        }
        return HasPayload(fix) ? FixStatus.NotInstalled : FixStatus.PayloadMissing;
    }

    /// <summary>Every payload file (except never-clobbered ones) is byte-identical in the game dir.</summary>
    private bool PayloadCurrent(FixDefinition fix) =>
        fix.Files
           // An optional file the payload does not carry cannot be out of
           // date - without this, a build packaged without the font would
           // read as "update available" forever and never become current.
           .Where(f => !f.SkipIfExists && (!f.Optional || PayloadHas(fix, f)))
           .All(f => ContentMatchesPayload(fix, f.Name));

    /// <summary>
    /// Thrown instead of overwriting a game directory that holds something
    /// newer than we ship.
    /// </summary>
    public class NewerBuildInstalledException : Exception
    {
        public NewerBuildInstalledException(string message) : base(message) { }
    }

    /// <summary>
    /// Which payload files does the game directory hold a NEWER copy of?
    ///
    /// WHY THIS EXISTS, and it is not hypothetical. A hand-delivered test
    /// build looks EXACTLY like an out-of-date install: the game-dir file
    /// differs from the payload, so the card says "Update available", and
    /// applying it reverts the build under test. It has cost three playtest
    /// rounds - two on 2026-08-26 during the dims-trim work, recorded in
    /// SCALE.md where it silently undid a build between rounds two and
    /// three, and at least one since. The tester never finds out; they bank
    /// a result gathered from code that was not the code under test.
    ///
    /// MODIFICATION TIME IS THE DISCRIMINATOR, not version. A version check
    /// would be cleaner and cannot see the case that actually happens: the
    /// surgical rez swap - extract, replace the DLLs from Dist, repack -
    /// leaves the manifest version untouched, so the install still claims to
    /// be the release it started as. Its mtime does not lie: File.Copy
    /// preserves it, so a payload file carries the timestamp of the release
    /// build while a hand-placed file carries the moment it was placed.
    ///
    /// One second of grace, because a file copied from the payload can land
    /// a tick after its source and two files from one build step can
    /// straddle a second boundary. Anything genuinely newer clears it by
    /// minutes.
    ///
    /// GUARDED FILES ONLY, and this narrowing is the whole of 0.10.81.
    /// Applied to every payload file, the check misfired immediately and in
    /// two different ways on its first release:
    ///
    ///   - "Recommended defaults" could not be applied at all, because the
    ///     game directory's autoexec.cfg is rewritten by the ENGINE on exit
    ///     and by the launcher on save. It is therefore always newer than the
    ///     shipped copy and always different, so the guard fired every time.
    ///   - The ShogoFRESH card refused to offer an update, because ONE file -
    ///     FiraCode-OFL.txt, a licence text - was newer and differed. A
    ///     licence file blocked the game code.
    ///
    /// Both share a cause: the guard assumed every payload file is one WE own
    /// and nothing else touches. That is true of the rez and the DLLs and
    /// false of everything merged, generated or incidental. And because one
    /// tripped file disables the whole card, the least important file in a
    /// payload got a veto over the most important.
    ///
    /// So the guard is now opt-in per file rather than blanket. A guard that
    /// BLOCKS things should be explicit about what it blocks.
    /// </summary>
    public IReadOnlyList<string> NewerThanPayload(FixDefinition fix)
    {
        var newer = new List<string>();

        foreach (var f in fix.Files)
        {
            if (f.SkipIfExists) continue;
            if (!f.Guarded) continue;

            var src = Path.Combine(PayloadDir(fix), f.Name);
            var dest = Path.Combine(GameDir, f.Name);

            if (!File.Exists(src) || !File.Exists(dest)) continue;
            if (ContentMatchesPayload(fix, f.Name)) continue;   // same bytes, nothing to lose

            if (File.GetLastWriteTimeUtc(dest) > File.GetLastWriteTimeUtc(src).AddSeconds(1))
                newer.Add(f.Name);
        }

        return newer;
    }

    private bool ContentMatchesPayload(FixDefinition fix, string fileName)
    {
        var dest = Path.Combine(GameDir, fileName);
        var src = Path.Combine(PayloadDir(fix), fileName);
        if (!File.Exists(dest) || !File.Exists(src)) return false;
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var a = File.OpenRead(dest);
        using var b = File.OpenRead(src);
        return sha.ComputeHash(a).AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.HashData(b));
    }

    // Version is nullable and last so manifests written before it existed
    // still deserialize - they simply report no installed version, which is
    // the honest answer rather than a guessed one.
    private record Manifest(List<string> Created, List<BackupEntry> BackedUp, string? Version = null);
    private record BackupEntry(string Dest, string Backup);

    public void Apply(FixDefinition fix) => Apply(fix, force: false);

    /// <param name="force">
    /// Overwrite even a newer install. The Setup tab never passes true; it
    /// exists so a caller that has told the user what they are about to lose
    /// can still go ahead.
    /// </param>
    public void Apply(FixDefinition fix, bool force)
    {
        // REFUSE rather than warn. A loud line gets missed mid-playtest -
        // that is the SCALE.md history - and the cost of missing it is a
        // whole round of results gathered from the wrong code. A refusal
        // cannot be missed.
        if (!force)
        {
            var newerFiles = NewerThanPayload(fix);

            if (newerFiles.Count > 0)
            {
                throw new NewerBuildInstalledException(
                    fix.Title + ": the game directory holds a NEWER build than this launcher ships (" +
                    string.Join(", ", newerFiles) +
                    "). Not overwriting - that is how a hand-delivered test build gets silently " +
                    "reverted mid-playtest. Re-install the release over it if you really want the " +
                    "shipped version back.");
            }
        }

        if (!HasPayload(fix)) throw new FileNotFoundException($"Payload missing for {fix.Title} (see Redist\\README.md).");

        // Re-applying with a manifest = UPDATE: the game-dir files are our
        // own previous versions, so they must NOT be backed up as
        // "originals" - the original pre-ShogoFRESH backups are preserved
        // and Undo still restores true stock state.
        Manifest? previous = null;
        if (File.Exists(ManifestPath(fix)))
        {
            try { previous = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(ManifestPath(fix))); }
            catch (JsonException) { }
        }
        bool isUpdate = previous is not null;

        var created = previous?.Created ?? new List<string>();
        var backedUp = previous?.BackedUp ?? new List<BackupEntry>();
        Directory.CreateDirectory(BackupDir(fix));

        bool Tracked(string dest) =>
            created.Contains(dest, StringComparer.OrdinalIgnoreCase) ||
            backedUp.Any(b => b.Dest.Equals(dest, StringComparison.OrdinalIgnoreCase));

        foreach (var f in fix.Files)
        {
            var src = Path.Combine(PayloadDir(fix), f.Name);
            var dest = Path.Combine(GameDir, f.Name);

            if (f.Optional && !File.Exists(src)) continue;

            if (File.Exists(dest))
            {
                if (f.SkipIfExists) continue;
                if (!Tracked(dest))
                {
                    var backup = Path.Combine(BackupDir(fix), f.Name);
                    File.Copy(dest, backup, overwrite: true);
                    backedUp.Add(new BackupEntry(dest, backup));
                }
            }
            else if (!Tracked(dest))
            {
                created.Add(dest);
            }
            File.Copy(src, dest, overwrite: true);
        }

        // Retire files this fix used to ship and no longer does.
        //
        // Without this, dropping a file from the list silently abandons our
        // copy of it in the game folder: the manifest keeps pointing at it,
        // so Undo still cleans up correctly, but until then the player has a
        // stale ShogoFRESH binary sitting there under a name we no longer
        // write to. That is what the 0.8.4 ShogoSrv.exe -> FreshSrv.exe
        // rename would otherwise have done to everyone upgrading.
        //
        // A backed-up file is restored to what was there before us; a file
        // we created is simply removed. Either way it leaves the manifest,
        // so this happens once rather than on every re-install.

        foreach (var name in fix.Retired ?? Array.Empty<string>())
        {
            var dest = Path.Combine(GameDir, name);

            try
            {
                var entry = backedUp.FirstOrDefault(
                    b => b.Dest.Equals(dest, StringComparison.OrdinalIgnoreCase));

                if (entry is not null)
                {
                    if (File.Exists(entry.Backup)) File.Copy(entry.Backup, dest, overwrite: true);
                    backedUp.Remove(entry);
                }
                else if (created.RemoveAll(
                             c => c.Equals(dest, StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    if (File.Exists(dest)) File.Delete(dest);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        // dgVoodoo ships enumerating only classic modes; without this the
        // engine can't see (or keep) a modern resolution.
        if (fix.Id == "dgvoodoo")
        {
            var dgv = new DgVoodooConfig(GameDir);
            if (dgv.Present)
            {
                dgv.EnableModernResolutions();
                dgv.Mode = DgVoodooConfig.DisplayMode.BorderlessFullscreen;  // modern default
                dgv.Pure32Bit = true;          // full-precision rendering (ShogoFix default kept)
                dgv.Filtering = "16";          // 16x anisotropic - free on any modern GPU
                dgv.Antialiasing = "4x";       // moderate MSAA default; adjustable in Settings
                dgv.Save();
            }
        }

        // The defaults fix additionally edits autoexec.cfg - back up the
        // whole file first so Undo is exact. Three steps: merge the
        // recommended vars, default the resolution to the native display,
        // and push the shipped default keybinds into the LIVE binding block
        // (so a fresh install plays with the curated binds immediately,
        // not only after an in-game "restore defaults").
        if (fix.Id == "defaults")
        {
            var autoexec = Path.Combine(GameDir, "autoexec.cfg");
            if (File.Exists(autoexec))
            {
                if (!Tracked(autoexec))
                {
                    var backup = Path.Combine(BackupDir(fix), "autoexec.cfg");
                    File.Copy(autoexec, backup, overwrite: true);
                    backedUp.Add(new BackupEntry(autoexec, backup));
                }

                var target = new ShogoConfigFile(autoexec);

                var settingsSrc = Path.Combine(PayloadDir(fix), "client-settings.cfg");
                if (File.Exists(settingsSrc))
                    foreach (var kv in new ShogoConfigFile(settingsSrc).All())
                        target.Set(kv.Key, kv.Value);

                var (nativeW, nativeH) = NativeDisplay.Primary();
                target.Set("screenwidth", nativeW);
                target.Set("screenheight", nativeH);
                target.Save();

                var defaultBinds = new BindingStore(Path.Combine(PayloadDir(fix), "defkeybd.cfg"));
                if (defaultBinds.Loaded && defaultBinds.AllBinds.Count > 0)
                {
                    var live = new BindingStore(autoexec);
                    if (live.Loaded)
                    {
                        live.ReplaceAllBinds(defaultBinds.AllBinds);
                        live.Save();
                    }
                }
            }
        }

        Directory.CreateDirectory(ManifestDir);
        File.WriteAllText(ManifestPath(fix),
            JsonSerializer.Serialize(new Manifest(created, backedUp, VersionString()),
                                     new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// The launcher's own version, which IS the payload's version - they are
    /// built and shipped together, so there is nothing separate to read.
    /// </summary>
    public static string VersionString()
    {
        var v = UpdateService.CurrentVersion;
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>
    /// What version installed this fix, or null when it was installed before
    /// the manifest recorded one (or not by the launcher at all).
    /// </summary>
    public string? InstalledVersion(FixDefinition fix)
    {
        try
        {
            var path = ManifestPath(fix);
            if (!File.Exists(path)) return null;

            return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path))?.Version;
        }
        catch (JsonException) { return null; }
        catch (IOException)   { return null; }
    }

    /// <summary>Restore the pre-Apply state. Only valid for launcher-installed fixes.</summary>
    public void Undo(FixDefinition fix)
    {
        var path = ManifestPath(fix);
        if (!File.Exists(path)) throw new InvalidOperationException($"{fix.Title} was not installed by this launcher.");

        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path))!;

        foreach (var created in manifest.Created)
            if (File.Exists(created)) File.Delete(created);

        foreach (var b in manifest.BackedUp)
            if (File.Exists(b.Backup)) File.Copy(b.Backup, b.Dest, overwrite: true);

        File.Delete(path);
    }

    // ----- DirectPlay (Windows optional feature, not a file copy) -----

    public static bool IsDirectPlayEnabled()
    {
        // The feature installs dplayx.dll + dpwsockx.dll into SysWOW64
        // (dplay.dll was the DX5-era name and never appears on Win10/11).
        var sysWow = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        return File.Exists(Path.Combine(sysWow, "dplayx.dll"));
    }

    /// <summary>Launch the elevated DISM enable (user gets the UAC prompt).</summary>
    public static void EnableDirectPlay()
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dism.exe",
            Arguments = "/Online /Enable-Feature /FeatureName:DirectPlay /All",
            Verb = "runas",
            UseShellExecute = true,
        };
        System.Diagnostics.Process.Start(psi);
    }
}
