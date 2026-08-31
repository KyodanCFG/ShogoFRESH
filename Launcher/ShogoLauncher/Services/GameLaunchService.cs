using System.Diagnostics;
using System.IO;

namespace ShogoLauncher.Services;

/// <summary>
/// Launches Client.exe, optionally pre-wiring a server address to join.
///
/// Join mechanism: Shogo's TCP/IP join dialog is populated from the CIpMgr
/// favorites list, which persists as console vars in autoexec.cfg:
///   "Ip0" "1.2.3.4:27888" ... "IpCount" "N"
/// We push the target address into slot 0 so it is the first entry in the
/// game's join dialog.
///
/// TODO(runtime-verify): LithTech may also accept console commands on the
/// command line (e.g. "+connect <addr>"); verify against the real client and
/// prefer that for one-click joins. Until verified we only pass +Var
/// assignments, which are documented behavior.
/// </summary>
public class GameLaunchService
{
    public string GameDir { get; }

    public GameLaunchService(string gameDir) => GameDir = gameDir;

    public string AutoexecPath => Path.Combine(GameDir, "autoexec.cfg");

    /// <summary>
    /// Whether ShogoFRESH.rez loads after the Custom\ mods - and so wins any
    /// file both contain - or before them. Default on: a mod carrying game
    /// code replaces ShogoFRESH wholesale, and doing that silently is worse
    /// than not doing it at all.
    /// </summary>
    public bool FreshTakesPriority { get; set; } = true;

    /// <summary>Insert an address at the top of the in-game TCP/IP favorites list.</summary>
    public void AddJoinAddress(string hostPort)
    {
        var cfg = new ShogoConfigFile(AutoexecPath);

        // Shift existing entries down, drop duplicates of the new address.
        // Read a little past the write cap so an older, longer list is
        // trimmed rather than half-read and re-grown.
        const int MaxRememberedAddressesRead = 256;

        var existing = new List<string>();
        int count = cfg.GetInt("IpCount", 0);
        for (int i = 0; i < count && i < MaxRememberedAddressesRead; i++)
        {
            var v = cfg.Get($"Ip{i}");
            if (!string.IsNullOrWhiteSpace(v) && !v.Equals(hostPort, StringComparison.OrdinalIgnoreCase))
                existing.Add(v);
        }

        // Kept short on purpose, and 256 was too many.
        //
        // These become Ip0..IpN, which the in-game multiplayer wizard rolls
        // into ONE query string capped at 2040 characters
        // (ClientShellDLL/NetStart.cpp). At roughly 22 characters an entry
        // that is about ninety addresses - so a long list silently truncated
        // there, and used to take LAN discovery with it. The shell now
        // reserves room for the broadcast, but a list that overflows the
        // buffer still loses its own tail without saying so.
        //
        // Only Ip0 is load-bearing: it is the address one-click join puts at
        // the top of the wizard's list. The rest are history, and a history
        // that cannot fit is not history. A hostname is longer than a dotted
        // quad, so this leaves generous headroom rather than sitting just
        // under the arithmetic.
        const int MaxRememberedAddresses = 48;

        var all = new List<string> { hostPort };
        all.AddRange(existing);
        if (all.Count > MaxRememberedAddresses)
            all = all.Take(MaxRememberedAddresses).ToList();

        for (int i = 0; i < all.Count; i++) cfg.Set($"Ip{i}", all[i]);
        cfg.Set("IpCount", all.Count);
        cfg.Save();
    }

    /// <summary>
    /// The -rez arguments Client.exe requires (without them the engine
    /// reports "no game resources specified" - Monolith's own launcher
    /// composed these too).
    /// Order: SHOGO.REZ, SOUND.REZ, official map packs (SHOGOP*.REZ),
    /// then every ENABLED mod in Custom\ - which is what makes the Mods
    /// tab's enable/disable actually control what loads.
    /// </summary>
    public string BuildRezArgs()
    {
        var parts = new List<string>();

        void AddRez(string relative)
        {
            if (File.Exists(Path.Combine(GameDir, relative)))
                parts.Add($"-rez \"{relative}\"");
        }

        AddRez("SHOGO.REZ");
        AddRez("SOUND.REZ");
        foreach (var p in Directory.EnumerateFiles(GameDir, "SHOGOP*.REZ").OrderBy(f => f))
            AddRez(Path.GetFileName(p));

        // ShogoFRESH goes after the base archives either way, so it always
        // beats their CShell.dll/Object.lto. Where it sits relative to
        // Custom\ is the interesting part, and it is the player's call.
        //
        // Last (the default): ShogoFRESH always wins. This costs the asset
        // mods nothing, because ShogoFRESH.rez holds only the four game DLLs
        // and no art, sound or levels - so skins, models and map packs still
        // override the base game exactly as they did. It only blocks mods
        // that ship game code, which could never have coexisted anyway.
        //
        // Before Custom: a code mod takes over completely, which is what you
        // want when running a total conversion you would rather have than
        // ShogoFRESH.

        if (!FreshTakesPriority) AddRez("ShogoFRESH.rez");

        // THE DIRECTORY ITSELF, mounted as an archive - "-rez custom" is in
        // Monolith's own launcher (shogo-re/notes/02-launch-dll.md, step 3),
        // and it is the entire mechanism by which a loose Custom\*.dat map
        // reaches the client. A directory mounts like a rez: its contents
        // appear at the file-tree ROOT, so Custom\OF_Vision.dat becomes the
        // world "OF_Vision" - which is why stock rotations hold bare names.
        //
        // This launcher never passed it, and the consequence took an evening
        // to diagnose: GetFileList returned null for everything loose, the
        // single-player custom list was empty, and a custom map in a rotation
        // loaded on the server then dropped every client that joined. The
        // engine could always do it; our command line was what said no.

        var customDir = Path.Combine(GameDir, "Custom");
        if (Directory.Exists(customDir))
        {
            parts.Add("-rez \"Custom\"");

            // The sorted map folders mount too - VERIFIED in play 2026-08-04
            // by hand-adding the mount through Extra Args: a nested directory
            // mounts exactly like Custom\ itself, contents at the tree root.
            //
            // The mount is what makes a map LOADABLE; it says nothing about
            // which game it is for. Every mount flattens to the same root, so
            // the sp/mp split lives entirely in the LISTS: the launcher's
            // rotation list reads maps\mp, the game's single-player menu
            // excludes it - unless the same name is ALSO in maps\sp, which
            // declares the map both-games and keeps it in the menu
            // (LoadLevelMenu.cpp; ZZ_Showcase is why). Same-named maps in two
            // folders collide at the root; last mount wins, so a both-games
            // map should be the same bytes in both folders.

            foreach (var sub in new[] { @"Custom\maps\sp", @"Custom\maps\mp" })
                if (Directory.Exists(Path.Combine(GameDir, sub)))
                    parts.Add($"-rez \"{sub}\"");

            foreach (var rez in Directory.EnumerateFiles(customDir, "*.rez").OrderBy(f => f))
                parts.Add($"-rez \"Custom\\{Path.GetFileName(rez)}\"");
        }

        if (FreshTakesPriority) AddRez("ShogoFRESH.rez");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Should this multiplayer launch pass "+DisableMusic 1"?
    ///
    /// E7 is a crash inside Monolith's 1998 music middleware, on a worker
    /// thread the middleware starts itself, with nothing of ours anywhere on
    /// the stack. Five occurrences on file, every one of them within seconds
    /// of a multiplayer world entry.
    ///
    /// WHY THE EXISTING MITIGATION IS NOT ENOUGH, which is the whole reason
    /// this exists: "MusicInMultiplayer 0" (the default since 0.9.63) skips
    /// InitPlayLists for a multiplayer world, and the fifth occurrence proved
    /// from a minidump that ima.dll loads and runs its thread anyway. It
    /// reduces what the middleware is asked to DO without changing whether it
    /// is RUNNING, and the fault is on the thread that keeps running.
    /// "DisableMusic 1" is the only lever on file that stops the DLL loading,
    /// and a full day of multiplayer under it produced no fault (BUGS.md,
    /// 2026-08-23).
    ///
    /// It has to be decided HERE rather than in the game because the driver
    /// loads once at startup, before any world exists - so "multiplayer only"
    /// can only be a launch-time decision unless music init is re-sequenced.
    /// The cost of doing it this way is stated rather than hidden: a session
    /// that starts in the campaign and then joins a server keeps the driver,
    /// because the process was already running when that choice was made.
    ///
    /// MusicInMultiplayer 1 OPTS OUT. Somebody who has asked for multiplayer
    /// music must not have the driver killed underneath them - they would get
    /// silence with nothing explaining why, and would reasonably conclude the
    /// variable was broken. One variable, one meaning: it is the answer to
    /// "do I want music in multiplayer", and this reads it rather than
    /// inventing a second switch beside it.
    /// </summary>
    public bool ShouldDisableMusicForMultiplayer()
    {
        try
        {
            // Both files matter. client-settings.cfg is what the launcher
            // owns and seeds; autoexec.cfg is what the game actually reads
            // and where a player who typed it themselves would have put it.
            // Either asking for multiplayer music is enough to opt out.

            foreach (var name in new[] { "autoexec.cfg", "client-settings.cfg" })
            {
                var path = Path.Combine(GameDir, name);
                if (!File.Exists(path)) continue;

                if (new ShogoConfigFile(path).GetFloat("MusicInMultiplayer", 0f) != 0f)
                    return false;
            }
        }
        catch (IOException)
        {
            // A config we cannot read is not a reason to refuse to launch.
            // Falling through to "disable" is the safe direction: the worst
            // case is silence in a multiplayer match, against a crash.
        }

        return true;
    }

    /// <param name="multiplayer">
    /// True when this launch is going straight into a multiplayer game - a
    /// join, or hosting a listen server. Not set for a plain launch into the
    /// menus, because that is not a multiplayer launch until the player makes
    /// it one, and guessing would take campaign music away from everybody.
    /// </param>
    public Process LaunchGame(string? joinAddress = null, string? extraArgs = null,
                             bool multiplayer = false)
    {
        var args = BuildRezArgs();

        if (multiplayer && ShouldDisableMusicForMultiplayer())
        {
            // Harmless if the prefs already added it - the console takes the
            // last value and both are 1.
            args += " +DisableMusic 1";
        }

        if (joinAddress is not null)
        {
            // Keep the in-game TCP/IP favorites list current...
            AddJoinAddress(joinAddress);
            // ...and hand the address to ShogoFRESH's one-click join, which
            // connects straight from the menu (CShell reads FreshConnect
            // once, then clears it).
            args += $" +FreshConnect \"{joinAddress}\"";
        }

        if (!string.IsNullOrWhiteSpace(extraArgs)) args += " " + extraArgs;

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(GameDir, "Client.exe"),
            WorkingDirectory = GameDir,
            UseShellExecute = true,
            Arguments = args,
        };
        return Process.Start(psi)!;
    }
}
