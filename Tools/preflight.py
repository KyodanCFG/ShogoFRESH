"""ShogoFRESH pre-flight: everything checkable without launching the game.

Run from anywhere:   python Tools/preflight.py

Exists because three of the bugs this project has shipped were invisible to
the compiler and only findable by reading two files at once:

  * HostDoubleJump left in the dirty-tracking list after the property was
    removed (CS0103, caught by the build - the cheap version of this class).
  * HostCriticalHits never ADDED to it, so ticking the box alone silently
    failed to save. The build was perfectly happy.
  * A protocol field appended on one side and not read on the other, which
    does not fail - it silently mis-reads every field after it.

None of those are things a human notices twice. All of them are trivial to
check mechanically, so they are checked here instead.

Exit code is the number of failures, so this can gate a build if it ever
wants to.
"""

import io
import os
import re
import sys
import glob
import subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

fails = []
warns = []
oks = []


def read(rel):
    with io.open(os.path.join(ROOT, rel), encoding='utf-8', errors='replace') as f:
        return f.read()


def read_all(pattern):
    out = ''
    for f in glob.glob(os.path.join(ROOT, pattern)):
        with io.open(f, encoding='utf-8', errors='replace') as fh:
            out += fh.read()
    return out


# --------------------------------------------------------------------------
# 1. Launcher: every settable property must round-trip AND be tracked dirty
# --------------------------------------------------------------------------
#
# A property that is loaded but never saved silently reverts. A property that
# is saved but not in the dirty set leaves the Save button greyed out, so the
# user changes it, sees no prompt, and loses it. Both look like "the launcher
# forgot my setting" and neither is visible in review.

def launcher_settable_properties():
    """name -> setter text, for every property that assigns through Set(ref _x).

    One implementation because two checks need it, and because the regex has a
    trap worth writing down once: `public partial class MainViewModel\n{` also
    matches "public <words> <name> {", and being the first match in each file
    it CONSUMED the first real property of every partial - PlayerName and
    HostIsListen were invisible to this scan for as long as it has existed.
    Both happened to be tracked, so the hole cost nothing and announced
    nothing. Hence the type-keyword veto below.

    Note the veto has to be INSIDE the pattern rather than a skip after the
    match: finditer does not overlap, so a match that is thrown away has still
    eaten the text the next one needed. Filtering afterwards looks like it
    fixes this and does not.
    """
    vm = read_all(r'Launcher/ShogoLauncher/ViewModels/*.cs')

    # Properties declared with the Set(ref _x, value) pattern are the settable
    # ones; read-only computed properties are not our concern.
    out = {}
    for m in re.finditer(r'public\s+(?![\w\s]*\b(?:class|struct|record|interface|enum)\b)'
                         r'[\w<>\[\],\?\s\.]+?\s+(\w+)\s*\{([^}]*?)Set\(ref\s+_\w+', vm):
        out[m.group(1)] = m.group(2)
    return out


def launcher_tracked_sets():
    """set name -> the property names listed in it."""
    vm = read_all(r'Launcher/ShogoLauncher/ViewModels/*.cs')
    out = {}
    for name in ('HostProperties', 'SettingsProperties'):
        block = re.search(name + r'\s*=\s*new\(\)\s*\{(.*?)\};', vm, re.S)
        out[name] = set(re.findall(r'nameof\((\w+)\)', block.group(1))) if block else set()
    return out


def check_launcher_roundtrip():
    vm = read_all(r'Launcher/ShogoLauncher/ViewModels/*.cs')

    settable = set(launcher_settable_properties())

    tracked = set()
    for names in launcher_tracked_sets().values():
        tracked |= names

    # Which ones actually touch a config file
    persisted = set(re.findall(r'(\w+)\s*=\s*(?:autoexec|cfg|dgv)\.Get', vm))
    persisted |= set(re.findall(r'(\w+)\s*=\s*IndexToChoice\(', vm))

    # A property that is persisted but not dirty-tracked is the CriticalHits bug.
    untracked = sorted(p for p in persisted if p in settable and p not in tracked)

    # Deliberate exclusions: things intentionally saved elsewhere, or by their
    # own self-saving wrapper (see BACKLOG #7).
    allow = {'GameDir', 'SelectedServerProfile', 'NewProfileName',
             'FreshTakesPriority', 'CheckForUpdates', 'SelectedBotFilter',
             # UpdateRate is read from the config but written back as a
             # constant 20, and has no UI control - so it cannot be "changed
             # and silently lost", which is what this check is for. If it ever
             # gains a Host tab control (see the tick-rate discussion) it must
             # come off this list AND start saving from the property.
             'UpdateRate'}
    untracked = [u for u in untracked if u not in allow]

    if untracked:
        fails.append("launcher: persisted but NOT in a dirty-tracking set "
                     "(changing these will silently fail to save): " + ', '.join(untracked))
    else:
        oks.append("launcher: every persisted property is dirty-tracked (%d tracked)" % len(tracked))

    # And the reverse: tracked but never persisted = a control that pretends
    # to be a setting.
    # The reverse question - tracked but never loaded - was tried and produced
    # two dozen false positives, because loading happens through half a dozen
    # shapes (ternaries, ?? fallbacks, direct cfg objects, preset lookups). A
    # check nobody can trust is worse than no check, so it is not made.


# --------------------------------------------------------------------------
# 1b. Every control on a dirty-gated tab must reach that tab's dirty set
# --------------------------------------------------------------------------
#
# The check above asks "is this property persisted?", and infers that from the
# LOAD side - `X = cfg.Get...`. That inference is what let HostGameMode
# through for the whole life of the game-mode feature: it loads as
# `HostGameMode = GameModes[Math.Clamp(cfg.GetInt("GameMode", 0), ...)]`, so
# the assignment does not have a Get on its right-hand edge and the property
# was never considered persisted, never compared, never reported. Changing
# only the Game mode dropdown and closing the launcher discarded the choice
# with no prompt, because HostDirty gates both the prompt and the save-on-exit
# (MainWindow.xaml.cs). SelectedOnFootModel had the same hole and was only
# harmless because its list has one entry.
#
# So ask the question the user actually experiences instead, which needs no
# inference about load shapes: a control bound on a tab whose save is gated by
# a dirty flag must set that flag. Bound + settable + on the tab = tracked.

def check_dirty_tracking_covers_tabs():
    settable = launcher_settable_properties()
    tracked = launcher_tracked_sets()
    xaml = read(r'Launcher/ShogoLauncher/MainWindow.xaml')

    # TabItems are siblings, never nested, so splitting at each header is
    # enough - no brace matching needed.
    marks = [(m.group(1), m.start()) for m in re.finditer(r'<TabItem\s+Header="([^"]+)"', xaml)]
    slices = {}
    for i, (head, start) in enumerate(marks):
        end = marks[i + 1][1] if i + 1 < len(marks) else len(xaml)
        slices[head] = xaml[start:end]

    # Deliberate exclusions, and why each one is not a lost setting:
    #   SelectedServerProfile / NewProfileName - the profile picker's own state,
    #     saved by the profile buttons rather than by the tab's Save.
    #   GameDir - the install path, written to launcher prefs on the spot.
    #   MapScanSummary - a READOUT, not a setting. It reports what the Refresh
    #     button just found; nothing writes it to a config, so marking the tab
    #     dirty for it would offer to save a sentence.
    #   MapFilterText - also a readout-shaped control: it filters the view
    #     of the available list and is never persisted anywhere.
    allow = {'SelectedServerProfile', 'NewProfileName', 'GameDir',
             'MapScanSummary', 'MapFilterText'}

    bad = []
    checked = 0
    for tab, setname in (('Host', 'HostProperties'), ('Settings', 'SettingsProperties')):
        if tab not in slices:
            fails.append("preflight: no <TabItem Header=\"%s\"> in MainWindow.xaml" % tab)
            continue

        bound = set(re.findall(r'\{Binding\s+(?:Path=)?([A-Za-z_]\w*)', slices[tab]))

        for p in sorted(bound):
            if p in allow or p not in settable:
                continue
            # A non-public setter cannot be driven by a two-way binding, so it
            # is never a user edit that can be lost (DgVoodooPresent, and the
            # dirty flags themselves).
            if re.search(r'\b(private|protected|internal)\s+set', settable[p]):
                continue
            checked += 1
            if p not in tracked[setname]:
                bad.append("%s tab: %s bound but missing from %s" % (tab, p, setname))

        # And the reverse: a name in the set that is not a Set(ref) property
        # reads as tracking and does nothing. HostGravityOn sat here - a
        # derived checkbox whose setter assigns HostGravity, so Set() reports
        # the STORED name and the derived entry could never match.
        for p in sorted(tracked[setname]):
            if p not in settable:
                bad.append("%s: %s is listed but is not a Set(ref) property" % (setname, p))

    if bad:
        fails.append("launcher dirty tracking: " + '; '.join(bad))
    else:
        oks.append("launcher: every bound control on the Host and Settings tabs "
                   "reaches its dirty set (%d controls, no dead entries)" % checked)


# --------------------------------------------------------------------------
# 2. XAML bindings resolve
# --------------------------------------------------------------------------

def check_launcher_defaults_match_game():
    """The launcher's fallback for a console var must match the game's own.

    They are written ~1500 lines apart in two languages, so nothing connects
    them. HudNumberSize was 24 in the launcher and 18 in both the game and
    the shipped config: on an install whose autoexec.cfg lacked the key, the
    launcher showed 24 and SAVING wrote 24, silently overriding a default the
    game's own comment explains at length.

    Only vars whose game-side fallback is a plain literal are checked. The
    rest would need the game's logic reimplemented here, which is how a
    checker starts lying.
    """
    vm = read_all(r'Launcher/ShogoLauncher/ViewModels/*.cs')
    stats = read(r'ClientShellDLL/PlayerStats.cpp')
    cfg = read(r'Launcher/ShogoLauncher/Defaults/client-settings.cfg')

    # var -> (game-side literal, regex to find the launcher's fallback)
    known = {
        'HudNumberSize': (re.search(r'float fDesign = ([\d.]+)f;', stats),
                          r'GetFloat\("HudNumberSize",\s*([\d.]+)f\)'),
    }

    bad = []
    for var, (gm, pat) in known.items():
        if not gm:
            warns.append("could not read the game-side default for %s" % var)
            continue

        game = float(gm.group(1))

        for m in re.finditer(pat, vm):
            if float(m.group(1)) != game:
                bad.append("%s: launcher %s vs game %s" % (var, m.group(1), game))

        seed = re.search(r'"%s"\s+"([\d.]+)"' % var, cfg)
        if seed and float(seed.group(1)) != game:
            bad.append("%s: shipped cfg %s vs game %s" % (var, seed.group(1), game))

    if bad:
        fails.append("launcher default disagrees with the game: " + '; '.join(bad))
    else:
        oks.append("launcher fallbacks match the game's own defaults")


def check_config_key_symmetry():
    """Every key written should be read back, so the UI shows what will be used.

    Write-only keys are legitimate when they are deliberately managed rather
    than user-facing - but each one has to be a decision, not an oversight,
    so they are listed explicitly here.
    """
    # All the partials: the reads live in the core's LoadFromGameDirCore and
    # the writes in the Settings partial's SaveSettings, so scanning one file
    # would see half the round-trip and report everything as asymmetric.
    vm = read_all(r'Launcher/ShogoLauncher/ViewModels/MainViewModel*.cs')

    sets = set(re.findall(r'autoexec\.Set\("(\w+)"', vm))
    gets = set(re.findall(r'autoexec\.Get\w*\("(\w+)"', vm))

    # Managed on purpose: written to a fixed value, never surfaced.
    managed = {
        'CSendRate',        # pinned to 30; see the netcode discussion
        'screendepth',      # 16; the engine's own mode list is 16-bit
        'UpdateRateInitted',# migration flag - currently written and never READ,
                            # which is itself the bug to fix when UpdateRate
                            # gains a control. Listed so it is a decision.
        'UpdateRate',       # forced to 20 today; see the note in check_launcher_roundtrip
    }

    stray = sorted(sets - gets - managed)
    if stray:
        fails.append("config keys written but never read back (the UI cannot "
                     "show what the game will use): " + ', '.join(stray))
    else:
        oks.append("every user-facing config key round-trips (%d keys)" % len(sets & gets))

    orphan = sorted(gets - sets)
    if orphan:
        fails.append("config keys read but never written (changing them in the "
                     "UI does nothing): " + ', '.join(orphan))


def check_bindings():
    xaml = read(r'Launcher/ShogoLauncher/MainWindow.xaml')
    vm = read_all(r'Launcher/ShogoLauncher/ViewModels/*.cs')

    bindings = set(re.findall(r'\{Binding ([A-Za-z_]\w*)', xaml))
    props = set(re.findall(r'public\s+[\w<>\[\],\?\s\.]+?\s+([A-Za-z_]\w*)\s*(?:\{|=>)', vm))

    # Positional record members are properties too - `record BindingRow(string
    # Action, string Label, ...)`. Missing these is what made the checker
    # report Secondary as an unresolved binding on its first run.
    for params in re.findall(r'public\s+record\s+\w+\s*\(([^)]*)\)', vm, re.S):
        props |= set(re.findall(r'[\w<>\?\[\]]+\s+(\w+)\s*(?:,|$)', params))

    # bound on row/item types rather than the main viewmodel
    rowtypes = {'Name', 'SizeBytes', 'Selected', 'Enabled', 'ConflictNote', 'ConflictsWithFresh',
                'Swatch', 'Title', 'Description', 'StatusText', 'CanApply', 'CanUndo',
                'IsUpdateProne', 'DisplayAddress', 'Map', 'GameType', 'PingMs', 'SourceLabel',
                'IsFavorite', 'PlayerSummary', 'TotalPlayers', 'Source', 'HumanPlayers', 'Bots',
                'Converter'}

    missing = sorted(b for b in bindings if b not in props and b not in rowtypes)
    if missing:
        fails.append("XAML bindings with no matching property: " + ', '.join(missing))
    else:
        oks.append("all %d XAML bindings resolve" % len(bindings))


# --------------------------------------------------------------------------
# 3. String resources
# --------------------------------------------------------------------------

def check_strings():
    hdr = read(r'Shared/ClientRes.h')
    rc = read(r'ClientRes/ClientRes.rc')
    code = read_all(r'ClientShellDLL/*.cpp')

    defined = dict(re.findall(r'#define\s+(IDS_\w+)\s+(\d+)', hdr))

    byid = {}
    for name, num in defined.items():
        byid.setdefault(num, []).append(name)
    dupes = {n: v for n, v in byid.items() if len(v) > 1}
    if dupes:
        fails.append("duplicate string ids: " +
                     '; '.join("%s -> %s" % (k, ','.join(v)) for k, v in sorted(dupes.items())))
    else:
        oks.append("no duplicate IDS_ numbers (%d defined)" % len(defined))

    used = set(re.findall(r'\b(IDS_[A-Z0-9_]+)\b', code))
    undef = sorted(u for u in used if u not in defined)
    if undef:
        fails.append("IDS_ used in code but not defined: " + ', '.join(undef[:12]))
    else:
        oks.append("every IDS_ used by CShell is defined (%d referenced)" % len(used))

    # Strings ShogoFRESH added must have an entry in the .rc, or they render
    # empty with no warning of any kind.
    ours = ['IDS_KILLFEED_ENVIRONMENT', 'IDS_KILLFEED_RAMMED', 'IDS_CONTINUE']
    missing = [u for u in ours
               if u in defined and not re.search(r'^\s*' + u + r'\s+"', rc, re.M)]
    if missing:
        fails.append("ShogoFRESH IDS_ with no string in ClientRes.rc: " + ', '.join(missing))
    else:
        oks.append("ShogoFRESH-added strings all have .rc entries")


# --------------------------------------------------------------------------
# 4. Appended protocol fields are written and read in the same order
# --------------------------------------------------------------------------
#
# This is the scariest class of bug in the project, because getting it wrong
# does not fail - it mis-reads every field after the mistake, and the symptom
# turns up somewhere unrelated. Engine fact 9.

def check_protocol():
    srv = read(r'ObjectDLL/RiotServerShell.cpp')
    cli = read(r'ClientShellDLL/RiotClientShell.cpp')
    sfx_s = read(r'ObjectDLL/ClientWeaponSFX.cpp')
    sfx_c = read(r'ClientShellDLL/SFXMgr.cpp')

    if 'WriteToMessageByte(hWrite, nRules)' in srv and 'nInfMode = pClientDE->ReadFromMessageByte' in cli:
        oks.append("MID_SERVER_RULES: flags + infinite-ammo mode on both sides")
    else:
        fails.append("MID_SERVER_RULES: server sends the ammo mode but the client does not read it")

    if 'GetBestScore() + 1' in srv and 'fBest >= 1.0f' in cli:
        oks.append("MID_FRESH_MATCHINFO: best score sent as best+1, absent case guarded")
    else:
        fails.append("MID_FRESH_MATCHINFO: best-score round trip is wrong")

    if 'theStruct.bMachine' in sfx_s and 'w.bMachine' in sfx_c:
        s_tail = sfx_s[sfx_s.index('SFX_WEAPON_ID'):]
        c_tail = sfx_c[sfx_c.index('SFX_WEAPON_ID'):sfx_c.index('SFX_WEAPONSOUND_ID')]
        if s_tail.index('bMachine') > s_tail.index('rRot') and \
           c_tail.index('bMachine') > c_tail.index('rRot'):
            oks.append("SFX_WEAPON_ID: machine flag appended after rRot on both sides")
        else:
            fails.append("SFX_WEAPON_ID: machine flag is not last on one side - fields will desync")
    else:
        fails.append("SFX_WEAPON_ID: machine flag missing on one side")

    # bBreakable was appended AFTER bMachine and has to stay there on both
    # sides. A field read out of order does not fail - it silently mis-reads
    # every field after it, which is the whole reason engine fact 9 exists.

    if 'theStruct.bBreakable' in sfx_s and 'w.bBreakable' in sfx_c:
        if sfx_s.index('bBreakable') > sfx_s.index('bMachine'):
            oks.append("SFX_WEAPON_ID: breakable flag appended after the machine flag")
        else:
            fails.append("SFX_WEAPON_ID: bBreakable is written before bMachine")
    else:
        fails.append("SFX_WEAPON_ID: bBreakable is not present on both sides")

    # A float read past the end does NOT return zero (engine fact 9). Every
    # appended float must be range-checked where it is read.
    for name, guard in (('fBest', 'fBest >= 1.0f'),
                        ('fDuration', 'fDuration')):
        if name in cli and guard not in cli and name == 'fBest':
            fails.append("appended float %s is read without a sanity guard" % name)


# --------------------------------------------------------------------------
# 5. Shipped defaults match what the docs claim
# --------------------------------------------------------------------------

def check_defaults():
    cfg = read(r'Launcher/ShogoLauncher/Defaults/client-settings.cfg')
    seeded = dict(re.findall(r'"(\w+)"\s+"([^"]*)"', cfg))

    expect = {'Gore': '2', 'KillFeedStyle': '2', 'ProfanityFilter': '1',
              # 0 is the MODE - scale zoomed aim by the magnification -
              # not a sensitivity of zero. 1998's flat tenth was the
              # sniper rifle's correct value applied to every weapon.
              'ZoomSensitivity': '0',
              'StreamerMode': '0', 'ClassicCampaign': '0',
              # Prediction is the engine's interpolation of remote objects
              # toward their reported position. The engine's own default is
              # OFF - the backing int sits in zero-filled .data - and it is
              # on today only because Monolith's 1998 defaults.cfg says so.
              # Off means remote players visibly jump at the 7 Hz they
              # report at (engine fact 15), with nothing to explain why.
              # There is no case for off, so it is asserted rather than
              # offered as a choice.
              'Prediction': '1'}

    bad = []
    for k, v in expect.items():
        if k not in seeded:
            bad.append("%s missing" % k)
        elif seeded[k] != v:
            bad.append("%s is %s, expected %s" % (k, seeded[k], v))

    if bad:
        fails.append("client-settings.cfg: " + '; '.join(bad))
    else:
        oks.append("client-settings.cfg seeds match the documented defaults")


# --------------------------------------------------------------------------
# 5b. Nothing hands untrusted text to a printf as the FORMAT string
# --------------------------------------------------------------------------

def check_format_strings():
    """The bug this project has shipped three times.

    A console print whose first argument is a variable makes that variable
    the format string. If it holds anything a player typed - chat, a name -
    then "%s%s%s%s" walks the process off the end of its own stack and "%n"
    writes to it.

    It has been fixed three times and found again twice, because the fix was
    always to the SITE rather than to the class:

      0.8.2   the dedicated server's WriteConsoleString, and CSPrint
      0.8.15  chat reaching the engine's CPrint, which formats twice
      0.8.16  CSPrint's own console echo - CPrint(pMsg), four more latent

    BPrint is covered as well as CPrint, and it is the WORSE of the two.
    Both double-format; the difference is where the second pass goes. CPrint's
    lands on the local console, BPrint's is BROADCAST to every connected
    client. So the same mistake made through BPrint takes the whole server
    down instead of one machine.

    So it is a build check now rather than a thing to remember. Every call
    below must have a literal as its first argument; pass the data as an
    ARGUMENT instead, which is what the format string is for.
    """
    # Rule one, and the strong one: nothing reaches the engine's variadic
    # console except the wrapper. Grep-able, no false positives, and it does
    # not depend on parsing an argument list correctly.
    #
    # The 0.8.16 check parsed arguments instead, which worked but had to
    # join wrapped lines to avoid crying wolf. This says the same thing in a
    # way that cannot be got wrong.

    direct = []

    for d in ('ObjectDLL', 'ClientShellDLL'):
        for path in glob.glob(os.path.join(ROOT, d, '*.cpp')):
            if os.path.basename(path) == 'FreshPrint.cpp':
                continue        # the one place allowed to

            rel = os.path.relpath(path, ROOT).replace('\\', '/')

            with io.open(path, encoding='utf-8', errors='replace') as fh:
                for n, line in enumerate(fh.read().split('\n'), 1):
                    if line.strip().startswith('//'):
                        continue
                    if '->CPrint' in line or '->BPrint' in line:
                        direct.append('%s:%d' % (rel, n))

    if direct:
        fails.append('calls the engine CPrint/BPrint directly instead of '
                     'FreshPrint (see Shared/FreshPrint.h): '
                     + ', '.join(direct[:8]))
    else:
        oks.append('nothing calls the engine console directly (%d files checked)'
                   % sum(len(glob.glob(os.path.join(ROOT, d, '*.cpp')))
                         for d in ('ObjectDLL', 'ClientShellDLL')))

    funcs = ('CPrint', 'BPrint', 'CSPrint', 'WriteConsoleFormat',
             'FreshDebugPrint', 'AdminPrint', 'Announce', 'FreshCrashNote',
             'FreshEvidenceNote', 'FreshPrint')

    # FreshDebugPrint and FreshEvidenceNote take a channel/first arg before
    # the format, so their format is the SECOND argument.
    second = ('FreshDebugPrint',)

    bad = []

    sources = []
    for d in ('ObjectDLL', 'ClientShellDLL', 'ShogoServ', 'Shared'):
        sources += glob.glob(os.path.join(ROOT, d, '*.cpp'))
        sources += glob.glob(os.path.join(ROOT, d, '*.h'))

    for path in sources:
        rel = os.path.relpath(path, ROOT).replace('\\', '/')

        with io.open(path, encoding='utf-8', errors='replace') as fh:
            body = fh.read()

        lines = body.split('\n')

        for n, line in enumerate(lines, 1):
            stripped = line.strip()
            if stripped.startswith('//') or stripped.startswith('*'):
                continue

            for fn in funcs:
                m = re.search(r'\b%s\s*\(' % fn, line)
                if not m:
                    continue

                # These calls wrap, and the format is very often on the
                # NEXT line - the first version of this check reported six
                # of those as failures, which is exactly how a checker
                # nobody trusts gets switched off. Join forward until the
                # statement ends.

                rest = line[m.end():]

                k = n
                while ';' not in rest and k < len(lines) and k - n < 4:
                    rest += ' ' + lines[k].strip()
                    k += 1

                rest = rest.lstrip()

                # Skip the definitions and declarations themselves.
                if ('const char*' in rest or 'char*' in rest
                        or 'LPCTSTR' in rest or 'const char *' in rest):
                    continue

                if fn in second:
                    # channel first, then the format
                    parts = rest.split(',', 1)
                    if len(parts) < 2:
                        continue
                    rest = parts[1].lstrip()

                if not rest.startswith('"'):
                    bad.append('%s:%d  %s' % (rel, n, stripped[:70]))

    if bad:
        for b in bad[:12]:
            fails.append('format string is a variable - %s' % b)
    else:
        oks.append('no print call takes a variable as its format string')


# --------------------------------------------------------------------------
# 6. Version agrees everywhere
# --------------------------------------------------------------------------

def check_version():
    v1 = re.search(r'<Version>([\d.]+)</Version>',
                   read(r'Launcher/ShogoLauncher/ShogoLauncher.csproj')).group(1)
    v2 = re.search(r'#define FRESH_VERSION\s+"([\d.]+)"', read(r'Shared/FreshVersion.h')).group(1)
    v3 = re.search(r'Current version \*\*([\d.]+)\*\*', read('CLAUDE.md')).group(1)

    if v1 == v2 == v3:
        oks.append("version is %s in csproj, FreshVersion.h and CLAUDE.md" % v1)
    else:
        fails.append("version mismatch: csproj %s, FreshVersion.h %s, CLAUDE.md %s" % (v1, v2, v3))

    # The numeric triple beside it, which ShogoServ's VERSIONINFO resource is
    # built from. A string and three integers saying different things is how
    # FreshSrv.exe came to claim 1.0.0.1 in its file properties.
    #
    # Note this compares NUMBERS. 0.8.10 is a legal version - the patch
    # component is not a digit, and nothing in the toolchain ever required
    # rolling the minor over at 9.

    fresh = read(r'Shared/FreshVersion.h')

    parts = []
    for name in ('MAJOR', 'MINOR', 'PATCH'):
        m = re.search(r'#define FRESH_VERSION_%s\s+(\d+)' % name, fresh)
        if not m:
            fails.append("FreshVersion.h has no FRESH_VERSION_%s" % name)
            return
        parts.append(int(m.group(1)))

    want = [int(x) for x in v2.split('.')]

    if want == parts:
        oks.append("FRESH_VERSION_MAJOR/MINOR/PATCH match the version string")
    else:
        fails.append("FreshVersion.h: string is %s but MAJOR/MINOR/PATCH are %s"
                     % (v2, '.'.join(str(p) for p in parts)))


# --------------------------------------------------------------------------
# 7. Shared constants are not duplicated per side
# --------------------------------------------------------------------------
#
# The convention is that anything client and server must agree on lives in
# Shared/. A constant redefined on one side is how the two drift apart, and
# MAX_BOT_NAMES already did exactly that once.

def check_shared_constants():
    obj = read_all(r'ObjectDLL/*.h')
    cli = read_all(r'ClientShellDLL/*.h')
    shared = read_all(r'Shared/*.h')

    shared_defs = set(re.findall(r'#define\s+([A-Z][A-Z0-9_]{4,})\s+\S', shared))

    clashes = []
    for side, text, label in ((obj, obj, 'ObjectDLL'), (cli, cli, 'ClientShellDLL')):
        local = set(re.findall(r'#define\s+([A-Z][A-Z0-9_]{4,})\s+\S', text))
        both = sorted(local & shared_defs)
        for name in both:
            clashes.append("%s redefines %s" % (label, name))

    if clashes:
        warns.append("constants defined in Shared/ AND per-side: " + '; '.join(clashes[:8]))
    else:
        oks.append("no constant is defined both in Shared/ and per-side")


# --------------------------------------------------------------------------
# A message read must never be cast to a pointer
# --------------------------------------------------------------------------
#
# MID_PLAYDIALOG once carried a struct ADDRESS through the engine's queued
# message system, freed by the sender before the receiver ran. The wire form
# was converted to carry the contents - and two of the three receivers were
# converted with it. The third (CPlayerObj::ObjectMessageFn) kept casting the
# first DWORD to a pointer, so every line of dialogue spoken by the player
# dereferenced four bytes of the path string and crashed the campaign at
# world entry. The fix was found by crash report, not review, which is
# exactly why the pattern is banned mechanically: a message payload is data
# from a stream, never an address.

# --------------------------------------------------------------------------
# The squishie dims-trim detection gap stays open
# --------------------------------------------------------------------------
#
# The CLIENT decides "I am a squishie" as: on foot with a dims scale at or
# below 1.0 (CMoveMgr, the dims-trim gate). That is sound only while the
# non-squish on-foot dims scale (CPlayerMode::GetDimsScale, 1.1) stays
# STRICTLY ABOVE the SquishScale clamp ceiling (FRESHTUNE_SQUISH_MAX, 1.00).
# Three files each hold a third of the invariant; whoever edits one alone
# gets caught here rather than by a squishie whose feet float again.

def check_squish_trim_gap():
    mode = read_all(r'ObjectDLL/PlayerMode.cpp')
    m = re.search(r'case\s+PM_MODE_FOOT\s*:\s*fScale\s*=\s*([\d.]+)f', mode)
    if not m:
        fails.append("squish trim: cannot find PM_MODE_FOOT dims scale in PlayerMode.cpp")
        return
    foot = float(m.group(1))

    tuning = read_all(r'Shared/FreshTuning.h')
    m = re.search(r'#define\s+FRESHTUNE_SQUISH_MAX\s+([\d.]+)f', tuning)
    if not m:
        fails.append("squish trim: cannot find FRESHTUNE_SQUISH_MAX in FreshTuning.h")
        return
    squish_max = float(m.group(1))

    move = read_all(r'ClientShellDLL/CMoveMgr.cpp')
    m = re.search(r'IsOnFoot\(\)\s*&&\s*m_DimsScale\[MS_NORMAL\]\s*<=\s*([\d.]+)f', move)
    if not m:
        fails.append("squish trim: cannot find the client detection gate in CMoveMgr.cpp")
        return
    gate = float(m.group(1))

    if not (squish_max <= gate < foot):
        fails.append("squish trim gap broken: SquishScale max %.2f, client gate %.2f, "
                     "foot scale %.2f - need max <= gate < foot" % (squish_max, gate, foot))
    else:
        oks.append("squish dims trim: detection gap holds (max %.2f <= gate %.2f < foot %.2f)"
                   % (squish_max, gate, foot))


def check_no_pointer_from_wire():
    hits = []

    for pattern in (r'ObjectDLL/*.cpp', r'ClientShellDLL/*.cpp', r'Shared/*.cpp'):
        for path in glob.glob(os.path.join(ROOT, pattern)):
            with io.open(path, encoding='utf-8', errors='replace') as fh:
                text = fh.read()

            for m in re.finditer(r'\*\s*\)\s*\w[\w>:.-]*ReadFromMessage\w*', text):
                line = text.count('\n', 0, m.start()) + 1
                hits.append('%s:%d: %s' % (os.path.basename(path), line,
                                           m.group(0).replace('\n', ' ')))

    if hits:
        fails.append("message reads cast to pointers (a payload is data, "
                     "never an address): " + '; '.join(hits[:6]))
    else:
        oks.append("no message read is cast to a pointer")


# --------------------------------------------------------------------------
# Committed code may not depend on a file that is not committed
# --------------------------------------------------------------------------
#
# This one is about the WORKING COPY rather than the code, and it is here
# because the failure is invisible exactly where it happens. A commit that
# references a file which exists on your disk but is not in the repository
# builds perfectly for you and for nobody else - the break only shows up on a
# clean clone, which is to say in front of somebody else.
#
# It happened by staging a DIRECTORY: "git add ObjectDLL/" took three files
# belonging to unfinished work in another session, and a .dsp entry plus an
# #include went in while the header they name stayed untracked. Local builds
# kept passing, and a release shipped, before anybody could have noticed.
#
# Two dependency edges are checked, both cheap:
#   - every SOURCE= line in a tracked .dsp names a tracked file
#   - every #include "..." in tracked code that resolves to a file in this
#     tree names a tracked file
#
# Skipped without complaint outside a git checkout, so an exported copy of
# the source still passes preflight.

def _git_lines(args):
    try:
        out = subprocess.check_output(args, cwd=ROOT, stderr=subprocess.STDOUT)
    except Exception:
        return None

    if isinstance(out, bytes):
        out = out.decode('utf-8', 'replace')

    return set(p.replace('\\', '/').lower() for p in out.split('\n') if p.strip())


def check_no_untracked_dependencies():
    tracked = _git_lines(['git', 'ls-files'])

    if tracked is None:
        oks.append("dependency tracking check skipped (not a git checkout)")
        return

    modified = _git_lines(['git', 'diff', '--name-only', 'HEAD']) or set()

    # Two severities, because they are two different situations.
    #
    # If the referencing file matches HEAD, the dependency is COMMITTED and
    # the clone is broken right now - that is the mistake this exists for. If
    # the file is only modified in the working copy, the hazard belongs to
    # whoever commits it next, so unfinished work in another session is
    # visible without blocking unrelated commits.

    broken  = []
    pending = []

    def note(src, dep):
        rel = os.path.relpath(dep, ROOT).replace('\\', '/')
        if rel.lower() in tracked:
            return

        srcrel = os.path.relpath(src, ROOT).replace('\\', '/')
        line   = "%s needs %s" % (os.path.basename(src), rel)

        if srcrel.lower() in modified:
            pending.append(line)
        else:
            broken.append(line)

    # .dsp source lists - build.ps1 parses these, so an untracked entry is a
    # build failure on a clean clone rather than a subtle one.

    for dsp in glob.glob(os.path.join(ROOT, '*', '*.dsp')):
        if os.path.relpath(dsp, ROOT).replace('\\', '/').lower() not in tracked:
            continue

        with io.open(dsp, encoding='utf-8', errors='replace') as fh:
            for line in fh:
                m = re.match(r'(?i)^SOURCE=(.+?)\s*$', line)
                if not m:
                    continue

                path = os.path.normpath(os.path.join(os.path.dirname(dsp),
                                                     m.group(1).strip()))
                if os.path.exists(path):
                    note(dsp, path)

    # Project includes. Only quoted ones, and only when the named file is
    # actually present in this tree - an include that resolves into the
    # toolchain or the engine headers is none of our business.

    for pattern in ('ObjectDLL/*.cpp', 'ObjectDLL/*.h',
                    'ClientShellDLL/*.cpp', 'ClientShellDLL/*.h',
                    'ShogoServ/*.cpp', 'ShogoServ/*.h',
                    'Shared/*.cpp', 'Shared/*.h'):
        for src in glob.glob(os.path.join(ROOT, pattern)):
            if os.path.relpath(src, ROOT).replace('\\', '/').lower() not in tracked:
                continue

            with io.open(src, encoding='utf-8', errors='replace') as fh:
                text = fh.read()

            for inc in re.findall(r'^\s*#include\s+"([^"]+)"', text, re.M):
                for base in (os.path.dirname(src), os.path.join(ROOT, 'Shared')):
                    cand = os.path.normpath(os.path.join(base, inc))
                    if os.path.exists(cand):
                        note(src, cand)
                        break

    if broken:
        fails.append("COMMITTED code depends on untracked files - a clean clone "
                     "cannot build: " + '; '.join(sorted(set(broken))[:6]))

    if pending:
        warns.append("uncommitted work references untracked files - commit them "
                     "together or neither: " + '; '.join(sorted(set(pending))[:6]))

    if not broken and not pending:
        oks.append("no committed file depends on an untracked one")


# --------------------------------------------------------------------------

def check_manifest_allowlist():
    """The game and the launcher must agree on what a manifest may set.

    Two copies of a list is exactly the thing that drifts, and the symptom
    here is quiet and confusing: the launcher tells a mod author their setting
    is fine, the game refuses it, and nothing connects the two. See
    Shared/FreshManifest.cpp and Launcher/.../ModManifest.cs.
    """
    cpp = read(r'Shared/FreshManifest.cpp')
    cs = read(r'Launcher/ShogoLauncher/Services/ModManifest.cs')

    # Two lists, each duplicated across C++ and C#, each read by a different
    # DLL. Client presentation is applied by CShell.dll; gameplay rules are
    # applied by FreshSrv.exe and read by Object.lto.
    #
    # Docs/RENDERVARS.md is on the CLIENT globs because some allow-listed
    # variables are read by the RENDERER - d3d.ren reads FogEnable 31 times -
    # and a binary we neither compile nor ship cannot be grepped. The rule
    # below is "something actually reads this"; for renderer variables the
    # evidence is a measured read count recorded in that file, and a name
    # earns its place there by carrying one. MipmapDist and ModelDist1-3 are
    # registered by the renderer but read by nothing, so they are documented
    # as excluded rather than listed - which is the check still working.
    lists = (
        ('client', r's_szAllowed\[\]\s*=\s*\{(.*?)DNULL',
         r'\bAllowed\s*=\s*\{(.*?)\};',
         (r'ClientShellDLL/*.cpp', r'ClientShellDLL/*.h', r'Docs/RENDERVARS.md')),
        ('server', r's_szServerAllowed\[\]\s*=\s*\{(.*?)DNULL',
         r'ServerAllowed\s*=\s*\{(.*?)\};', (r'ObjectDLL/*.cpp', r'ObjectDLL/*.h')),
    )

    for label, cpp_re, cs_re, code_globs in lists:
        m = re.search(cpp_re, cpp, re.S)
        if not m:
            fails.append("could not find the %s allow-list in Shared/FreshManifest.cpp" % label)
            continue
        game = set(re.findall(r'"([A-Za-z_]\w*)"', m.group(1)))

        m = re.search(cs_re, cs, re.S)
        if not m:
            fails.append("could not find the %s allow-list in ModManifest.cs" % label)
            continue
        launcher = set(re.findall(r'"([A-Za-z_]\w*)"', m.group(1)))

        if game != launcher:
            detail = []
            if game - launcher:
                detail.append("game only: " + ", ".join(sorted(game - launcher)))
            if launcher - game:
                detail.append("launcher only: " + ", ".join(sorted(launcher - game)))
            fails.append("%s manifest allow-lists disagree - %s" % (label, "; ".join(detail)))
            continue

        # And every name on it has to be a variable something actually reads,
        # or it is a promise to mod authors that nothing keeps. This is how
        # GibScale was caught before it shipped.
        code = "".join(read_all(g) for g in code_globs)
        unread = [v for v in sorted(game) if ('"%s"' % v) not in code]

        if unread:
            fails.append("%s manifest allow-list names nothing reads: %s"
                         % (label, ", ".join(unread)))
        else:
            oks.append("%s manifest allow-list matches in game and launcher (%d vars, all read)"
                       % (label, len(game)))


# --------------------------------------------------------------------------

def check_player_models_agree():
    """The launcher's on-foot list and the game's must name the same bodies.

    Two copies of a list drifts, and this pair drifts QUIETLY: the launcher
    offers a body, the client sends the name, the server does not recognise it
    and falls back to Sanjuro. Nothing errors. The player simply is not who
    they picked, and the only clue is that they look like everyone else.

    The name is what travels, deliberately - an index would silently mean a
    DIFFERENT body the moment the two lists diverged, which is worse than not
    finding one.
    """
    hdr = read(r'Shared/FreshPlayerModels.h')
    cs = read(r'Launcher/ShogoLauncher/ViewModels/MainViewModel.Settings.cs')

    game = re.findall(r'\{\s*"([^"]+)"\s*,\s*"[^"]*\.abc"', hdr, re.I)
    m = re.search(r'OnFootModels\s*\{\s*get;\s*\}\s*=\s*\{([^}]*)\}', cs)
    if not game:
        return False, "no models found in Shared/FreshPlayerModels.h"
    if not m:
        return False, "no OnFootModels list found in the launcher"
    ui = re.findall(r'"([^"]+)"', m.group(1))

    if [g.lower() for g in game] != [u.lower() for u in ui]:
        return False, ("on-foot model lists disagree: game %s, launcher %s"
                       % (game, ui))
    if game and game[0].lower() != "sanjuro":
        return False, ("the first on-foot model must be Sanjuro - it is the "
                       "fallback for an unknown name and what a stock client "
                       "gets; found %r" % game[0])
    return True, "on-foot player models match in game and launcher (%d)" % len(game)


def check_rez_readers():
    """The two .rez readers must agree, and both must cite the one spec.

    RezArchive.cs and Shared/FreshRez.cpp implement the same format in two
    languages because the server cannot call C#. Neither is the authority -
    Docs/public/REZFORMAT.md is - and the realistic drift is somebody fixing one
    reader and not the other. These are the structural constants that decide
    whether a file is readable at all; if they stop matching, one reader
    starts rejecting archives the other accepts.
    """
    cs = read(r'Launcher/ShogoLauncher/Services/RezArchive.cs')
    cpp = read(r'Shared/FreshRez.cpp')
    spec = r'Docs/public/REZFORMAT.md'

    try:
        read(spec)
    except Exception:
        fails.append("Docs/public/REZFORMAT.md is missing - it is the spec both readers cite")
        return

    for name, text in (("RezArchive.cs", cs), ("FreshRez.h", read(r'Shared/FreshRez.h'))):
        if "REZFORMAT" not in text:
            fails.append("%s does not point at Docs/public/REZFORMAT.md" % name)

    # Banner length, the CR LF EOF terminator, and the walk depth cap.
    facts = [
        ("banner length 127", "BannerLength = 127" in cs, "FRESHREZ_BANNER		127" in cpp),
        ("terminator 0x0D",   "0x0D" in cs,               "0x0D" in cpp),
        ("terminator 0x0A",   "0x0A" in cs,               "0x0A" in cpp),
        ("terminator 0x1A",   "0x1A" in cs,               "0x1A" in cpp),
        ("depth cap 16",      "depth > 16" in cs,         "FRESHREZ_MAXDEPTH	16" in cpp),
    ]

    bad = [n for n, a, b in facts if not (a and b)]

    if bad:
        fails.append("rez readers disagree on: " + ", ".join(bad))
        return

    # The entry-size cap that silently dropped a 5MB map. Neither reader may
    # reintroduce one - the bound is the end of the archive, nothing else.
    if "FRESHREZ_MAXENTRY" in cpp and "#define FRESHREZ_MAXENTRY" in cpp:
        fails.append("FreshRez.cpp has an entry-size cap again - see Docs/public/REZFORMAT.md")
        return

    # ---- The WRITER, which is the third implementation of the same spec. ---
    #
    # It has to agree with the readers on the structural constants, and it has
    # two facts of its own that no reader can catch. The banner must be exact:
    # lithrez lists nothing at all from an archive whose banner is anything
    # else, so a "tidy-up" of that string would produce archives Monolith's own
    # tool cannot read - and nothing else here would notice. And the first data
    # block sits at 168, not at the 139 where the documented header ends.

    try:
        mk = read(r'Tools/mkrez.py')
    except Exception:
        fails.append("Tools/mkrez.py is missing - it is the .rez writer")
        return

    if "REZFORMAT" not in mk:
        fails.append("Tools/mkrez.py does not point at Docs/public/REZFORMAT.md")

    writer_facts = [
        ("banner length 127",   "REZ_BANNER_LEN   = 127" in mk),
        ("header/data at 168",  "REZ_HEADER_LEN   = 168" in mk),
        ("terminator CR LF EOF", r"\r\n\x1a" in mk),
        ("banner names RezMgr", "RezMgr Version 1 Copyright (C) 1995 MONOLITH INC." in mk),
        ("banner second line",  "LithTech Resource File" in mk),
    ]

    bad = [n for n, ok in writer_facts if not ok]

    if bad:
        fails.append("Tools/mkrez.py disagrees with the spec on: " + ", ".join(bad))
        return

    # The type-code encoding, checked FUNCTIONALLY - by calling the writer's
    # own function - because this is the arm that would have caught 0.10.9.
    # The value must be right-aligned (last character in the low byte, "DAT"
    # = 0x00444154); the shifted form decodes identically in every viewer,
    # including lithrez's, and only the ENGINE's exact-DWORD lookup notices.
    # That shipped: CSHELL.DLL sat in the archive unfindable and the whole
    # mod silently fell back to stock. See REZFORMAT.md, "right-aligned".
    try:
        import mkrez
        measured = {"DAT": 0x00444154, "DLL": 0x00444C4C, "LTO": 0x004C544F, "": 0}
        wrong = ["%s -> 0x%08x (want 0x%08x)" % (k, mkrez.encode_ext(k), v)
                 for k, v in measured.items() if mkrez.encode_ext(k) != v]
        if wrong:
            fails.append("mkrez.encode_ext is misaligned - the engine will not "
                         "find files by type and the mod will silently not load: "
                         + ", ".join(wrong))
            return
    except ImportError as e:
        fails.append("could not import mkrez to check its encoding: %s" % e)
        return

    # The banner is 127 bytes including the terminator, and lithrez rejects
    # anything else. Checked by length rather than by eye because the padding
    # is trailing spaces, which no reviewer will ever count.
    import re as _re
    m = _re.search(r'REZ_BANNER = \(\n(.*?)\n\)', mk, _re.S)
    if m:
        try:
            literal = eval("(" + m.group(1) + ")", {"__builtins__": {}}, {})
            if len(literal) != 127:
                fails.append("Tools/mkrez.py banner is %d bytes, must be exactly 127 - "
                             "lithrez reads nothing from an archive with any other banner"
                             % len(literal))
                return
        except Exception as e:
            fails.append("could not evaluate the mkrez banner literal: %s" % e)
            return
    else:
        fails.append("could not find REZ_BANNER in Tools/mkrez.py")
        return

    oks.append("rez readers and the writer agree on the format and cite the spec")


def check_dtx_implementations():
    """The DTX writer and the DTX validator must agree, and both must cite the spec.

    Tools/png2dtx.py writes textures and DtxValidator.cs checks them, so a
    disagreement is the worst possible shape: the writer produces files the
    launcher then reports as broken, or - far worse - the validator blesses a
    file the engine will not draw.

    The mipmap ceiling is the one that has to hold. Nine levels renders the
    model WHITE in game, with no crash, no fallback and nothing in any log, so
    the build-time check is the only place a mod author can be told. Docs/
    DTXFORMAT.md is the authority for both.
    """
    spec = r'Docs/public/DTXFORMAT.md'

    try:
        read(spec)
    except Exception:
        fails.append("Docs/public/DTXFORMAT.md is missing - it is the spec both DTX tools cite")
        return

    py = read(r'Tools/png2dtx.py')
    cs = read(r'Launcher/ShogoLauncher/Services/DtxValidator.cs')

    for name, text in (("png2dtx.py", py), ("DtxValidator.cs", cs)):
        if "DTXFORMAT" not in text:
            fails.append("%s does not point at Docs/public/DTXFORMAT.md" % name)

    facts = [
        ("max 8 mipmaps",  "MAX_MIPMAPS = 8" in py,      "MaxMipmaps = 8" in cs),
        ("44-byte header", "DTX_HEADER = 44" in py,      "HeaderSize  = 44" in cs),
        ("1024 palette",   "DTX_PALETTE = 1024" in py,   "PaletteSize = 1024" in cs),
        ("version -2",     "DTX_VERSION = -2" in py,     "Version     = -2" in cs),
        ("alpha flag 0x2", "DTX_FLAG_ALPHA = 0x2" in py, "FlagAlpha   = 0x2" in cs),
        # Both now walk the trailing section chain - png2dtx to carry it across
        # a conversion, the validator to check it lands on EOF - so the two
        # numbers that describe a section header are implemented twice.
        ("32-byte section header",
         "DTX_SECTION_HEADER = 32" in py,    "SectionHeaderSize = 32" in cs),
        ("section length at +28",
         "DTX_SECTION_LENGTH_AT = 28" in py, "SectionLengthAt   = 28" in cs),
    ]

    bad = [n for n, a, b in facts if not (a and b)]

    if bad:
        fails.append("DTX writer and validator disagree on: " + ", ".join(bad))
        return

    # The alpha plane rounds UP per level. Rounding down is wrong by one byte
    # for any texture reaching a 1x1 level, and it reported SKINS\MULTIPLAY.DTX
    # - a file Monolith shipped - as malformed.
    if "(w * h + 1) / 2" not in cs:
        fails.append("DtxValidator.cs no longer rounds the alpha plane up - "
                     "see Docs/public/DTXFORMAT.md, it will reject stock textures")
        return

    # The same hazard this check has always guarded, guarding a better fix.
    #
    # It used to require "sections == 0" - the validator skipped its size check
    # whenever nSections was set, because the trailing layout was unknown. The
    # layout is known now, so the skip is gone and the guard moves to what
    # actually keeps Monolith's files out of the findings list: nSections IS
    # NOT EVIDENCE. Two dozen shipped textures carry rubbish in that field
    # (17023, 58137, 6422) and no sections at all, the file ending exactly
    # where the pixels do. Walking that many blocks reports them as broken.
    #
    # So the validator must decide from the SIZE and walk to EOF, never from
    # the count.
    if "SectionsEndAtEntryEnd" not in cs:
        fails.append("DtxValidator.cs no longer walks the section chain to EOF - "
                     "see Docs/public/DTXFORMAT.md")
        return

    if "ReadU16(h, 14)" in cs:
        fails.append("DtxValidator.cs reads nSections again - two dozen shipped "
                     "textures carry rubbish in that field and no sections, so "
                     "trusting it reports Monolith's own files as errors")
        return

    oks.append("DTX writer and validator agree on the format constants and cite the spec")


def check_weapon_key_roundtrip():
    """There must be exactly ONE weapon/key table, and GetCommandId must
    derive from it.

    This check used to compare two switch statements - GetWeaponId mapping a
    key to a weapon, GetCommandId mapping a weapon back to a key - and assert
    that every weapon's key led home. It was written because the sniper rifle
    shipped a round trip that did not close: it answered "key 9", key 9
    answered "nothing" in a mech, and the result was a model hovering above
    the head, ammunition read off the end of a table, and a fire that played
    its animation and its sound and produced no bullet.

    0.10.48 removed the second switch instead. GetCommandId now takes the
    player mode and SEARCHES GetWeaponId for the key that answers, so the
    round trip is true by construction and cannot be broken by editing one
    table and not the other - there is only one table.

    So the thing worth checking changed shape. It is no longer "do the two
    agree" but "is there still only one", which is the invariant the fix
    bought and exactly the sort of thing that gets undone by a well-meaning
    optimisation six months from now.
    """
    src = read(r'Shared/WeaponDefs.h')

    nl = "\r\n" if "\r\n" in src else "\n"

    i = src.find('inline int GetCommandId(')

    if i < 0:
        fails.append("weapon key round-trip: GetCommandId is gone")
        return

    j = src.find(nl + '}', i)

    if j < i:
        fails.append("weapon key round-trip: could not read GetCommandId")
        return

    body = src[i:j]

    # A parallel table, back again.

    if re.search(r'case\s+GUN_\w+', body):
        fails.append("weapon key round-trip: GetCommandId names weapons "
                     "directly again - that is a second copy of GetWeaponId's "
                     "table and the two will drift. Derive it instead")
        return

    # Derived, and from the right thing.

    if 'GetWeaponId(' not in body:
        fails.append("weapon key round-trip: GetCommandId no longer searches "
                     "GetWeaponId, so nothing keeps the two in step")
        return

    # And it has to be TOLD which tier, or a both-tier weapon is guesswork
    # again - which is the whole bug this shape exists to make impossible.

    sig = src[i:src.find(')', i) + 1]

    if 'dwPlayerMode' not in sig:
        fails.append("weapon key round-trip: GetCommandId does not take a "
                     "player mode, so a weapon in both tiers has one key for "
                     "both - see the sniper rifle")
        return

    # Count what the forward table actually maps, so the ok line says
    # something rather than merely appearing.

    fwd = src.find('inline int GetWeaponId(')
    fwd_body = src[fwd:src.find(nl + '}', fwd)] if fwd > 0 else ''

    weapons = set(re.findall(r'nWeaponId = (GUN_\w+)', fwd_body))

    oks.append("one weapon/key table, GetCommandId derived from it (%d weapons)"
               % len(weapons))


# --------------------------------------------------------------------------
# The two NetDefs.h copies must agree about NetGame's shape
# --------------------------------------------------------------------------
#
# ShogoServ carries its own partial copy of NetDefs.h (it is MFC and pulls a
# different header set, so the duplication is deliberate - see the note in
# Shared/NetDefs.h). NetGame travels between the two through the engine as an
# opaque blob, so ObjectDLL/ClientShellDLL and FreshSrv.exe must lay it out
# identically or one of them reads the other's rotation as garbage.
#
# This is exactly the "one fact implemented twice" case CLAUDE.md says to
# pin with a check. It nearly cost us already: a 78-map rotation overflowed
# the 50-entry array and took the server down, and the fix changed both
# copies - nothing but habit would have kept them together next time.

def check_netdefs_copies_agree():
    pairs = (('Shared/NetDefs.h', 'ShogoServ/NetDefs.h'),)
    names = ('MAX_GAME_LEVELS', 'NML_ROTATION', 'NML_LEVEL', 'MAX_PLAYER_NAME')

    for a, b in pairs:
        ta, tb = read(a), read(b)
        bad = []

        for n in names:
            ma = re.search(r'#define\s+%s\s+(\d+)' % n, ta)
            mb = re.search(r'#define\s+%s\s+(\d+)' % n, tb)

            if not ma or not mb:
                bad.append("%s missing from %s" % (n, a if not ma else b))
            elif ma.group(1) != mb.group(1):
                bad.append("%s is %s in %s but %s in %s" % (n, ma.group(1), a, mb.group(1), b))

        # The rotation row must be declared with the rotation constant in
        # both, or the sizes agree by accident and drift on the next edit.
        for f in (a, b):
            if not re.search(r'm_sLevels\[MAX_GAME_LEVELS\]\[NML_ROTATION\]', read(f)):
                bad.append("%s does not declare m_sLevels with NML_ROTATION" % f)

        # Every NST_ token the two sides SHARE must carry the same string.
        #
        # These are the wire names the game DLL writes with Sparam_Add and the
        # server app reads with Sparam_Get. A token whose value drifts is
        # silent in the worst way: Sparam_Get simply returns false and the
        # field keeps its previous value, so the display looks stale rather
        # than wrong and nothing is logged.
        #
        # The server app spreads its copies over TWO headers - NetDefs.h and
        # NetStart.h, which redefines most of the same tokens - so the
        # comparison is against the union. Presence is deliberately NOT
        # required in both directions: Shared/ carries tokens the app has no
        # use for, and demanding symmetry would mean adding dead defines to
        # keep a check quiet.
        def nst(*paths):
            out = {}
            for p in paths:
                out.update(dict(re.findall(r'#define\s+(NST_\w+)\s+"([^"]*)"', read(p))))
            return out

        toks_a = nst(a)
        toks_b = nst(b, 'ShogoServ/NetStart.h')

        for n in sorted(set(toks_a) & set(toks_b)):
            if toks_a[n] != toks_b[n]:
                bad.append("%s is \"%s\" in %s but \"%s\" on the server side"
                           % (n, toks_a[n], a, toks_b[n]))

        if bad:
            fails.append("NetGame layout differs between the NetDefs.h copies: " + '; '.join(bad))
        else:
            oks.append("both NetDefs.h copies agree on NetGame's layout (%d levels x %d bytes)" %
                       (int(re.search(r'#define\s+MAX_GAME_LEVELS\s+(\d+)', ta).group(1)),
                        int(re.search(r'#define\s+NML_ROTATION\s+(\d+)', ta).group(1))))


# --------------------------------------------------------------------------

def check_resource_ids():
    """No ShogoFRESH-added control id may be reused, and none defined twice.

    IDC_FRESH_GAMEMODE was #defined twice, both 1082, and IDC_COMMANDS_SHUTDOWN
    was 1082 as well. It routed correctly because the two controls live in
    different dialogs and MFC dispatches per dialog - so nothing misbehaved,
    and nothing would have, right up until one of them moved dialogs or a
    handler was added by id in a dialog that held both. Silent until it isn't.

    The root cause is mechanical: _APS_NEXT_CONTROL_VALUE, the number the
    resource editor hands out next, sat at 1070 while ids had been written by
    hand up to 1083. Anything the editor added would have collided. So the
    check is really two:

      - no symbol defined twice, whatever the values
      - _APS_NEXT_CONTROL_VALUE ahead of every IDC_ in the file

    Value reuse is only enforced from 1070 up. Below that is Monolith's, and
    the VC6 wizard shared ids freely across dialogs - IDC_SHUTDOWNSERVER and
    IDC_SERVICELIST are both 1002 in the 1999 baseline. Failing on those would
    mean either rewriting stock resources or a permanently-red check.
    """
    FIRST_FRESH_ID = 1070      # stock stops at IDC_COLORPICKER 1069

    src = read(r'ShogoServ/resource.h')
    before = len(fails)

    seen = {}
    values = {}
    apsnext = None

    for line in src.replace('\r\n', '\n').split('\n'):
        m = re.match(r'\s*#define\s+(\w+)\s+(0x[0-9A-Fa-f]+|\d+)\s*$', line)
        if not m:
            continue

        name, raw = m.group(1), m.group(2)
        val = int(raw, 16) if raw.lower().startswith('0x') else int(raw)

        if name == '_APS_NEXT_CONTROL_VALUE':
            apsnext = val
            continue

        if name in seen:
            fails.append("resource.h: %s is #defined twice (%d, then %d)"
                         % (name, seen[name], val))
        seen[name] = val

        if name.startswith('IDC_'):
            values.setdefault(val, []).append(name)

    ours = [(v, n) for v, n in sorted(values.items()) if v >= FIRST_FRESH_ID]

    for val, names in ours:
        if len(set(names)) > 1:
            fails.append("resource.h: control id %d is shared by %s - both are "
                         "ShogoFRESH's, so one of them can just move"
                         % (val, ' and '.join(sorted(set(names)))))

    highest = max(values) if values else 0

    if apsnext is None:
        fails.append("resource.h: _APS_NEXT_CONTROL_VALUE is missing")
    elif apsnext <= highest:
        fails.append("resource.h: _APS_NEXT_CONTROL_VALUE is %d but ids reach "
                     "%d - the next control the editor adds would collide"
                     % (apsnext, highest))

    if len(fails) == before:
        oks.append("no resource id defined twice or shared among ours "
                   "(%d ShogoFRESH controls)" % len(ours))

# --------------------------------------------------------------------------

def check_keybind_rows():
    """Every bindable action must appear in the launcher's keybind layout.

    The controls list exists twice: g_CommandArray in ClientUtilities.cpp
    drives the in-game menu, and Defaults/keybind-layout.json drives the
    launcher's Keybinds tab. They are the same fact, and nothing makes them
    agree.

    The failure is quiet and was reported rather than caught: two actions
    added under Fire in the game showed up at the BOTTOM of the launcher's
    list, because KeybindLayout.Arrange puts anything not in Order last. No
    error, no missing row - just the wrong order, in one of the two places.

    Only presence is checked, not position. The two lists are allowed to
    differ on purpose (the launcher hides legacy actions and groups a few
    differently); what is never intentional is an action the launcher has
    never heard of.

    To break this on purpose: add a row to g_CommandArray and not to
    Defaults/keybind-layout.json.
    """
    import json

    src = read(r'ClientShellDLL/ClientUtilities.cpp')

    block = re.search(r'g_CommandArray\[NUM_COMMANDS\]\s*=\s*\{(.*?)\n\};', src, re.S)
    if not block:
        fails.append("could not find g_CommandArray in ClientUtilities.cpp")
        return

    actions = re.findall(r'IDS_ACTIONSTRING_(\w+)\s*\}', block.group(1))
    if not actions:
        fails.append("g_CommandArray parsed but no action strings found")
        return

    rc = read(r'ClientRes/ClientRes.rc')

    # IDS_ACTIONSTRING_X -> the engine action NAME it holds, which is what
    # the layout file keys on.
    names = []
    for a in actions:
        if a == 'UNASSIGNED':
            continue
        m = re.search(r'IDS_ACTIONSTRING_%s\s+"([^"]+)"' % re.escape(a), rc)
        if m:
            names.append(m.group(1))

    with io.open(os.path.join(ROOT, 'Launcher/ShogoLauncher/Defaults/keybind-layout.json'),
                 encoding='utf-8') as f:
        layout = json.load(f)

    known = set(x.lower() for x in layout.get('Order', []))
    known |= set(x.lower() for x in layout.get('Hidden', []))

    missing = [n for n in names if n.lower() not in known]

    if missing:
        fails.append("keybind-layout.json does not list %s - the launcher will "
                     "show %s at the bottom of the list instead of in order"
                     % (', '.join(missing), 'them' if len(missing) > 1 else 'it'))
    else:
        oks.append("every bindable action appears in the launcher's keybind layout (%d)"
                   % len(names))


def check_localisation():
    """The English JSON must still describe the English resources.

    Localisation/strings.en.json is generated from ClientRes.rc, ServerRes.rc
    and the five TEXT resources in ClientRes/. Nothing forces anyone to
    regenerate it: Visual Studio rewrites .rc files on its own, and a string
    edited in the editor would leave the JSON describing a game that no
    longer exists. A translator would then translate a line nobody sees.

    Two things are checked, and they fail differently:

      - content drift: the committed JSON does not match what the sources
        say right now. Fix by running Tools/loc_export.py.
      - writer disagreement: feeding the English JSON back through the
        writer does not reproduce the sources byte for byte. That means the
        reader and writer have drifted apart, which is the bug that would
        silently corrupt every localised build.

    To break this on purpose: change any string in ClientRes.rc without
    re-running loc_export, or change an escape rule in only one of
    rc_escape/rc_unescape.
    """
    sys.path.insert(0, os.path.join(ROOT, 'Tools'))
    import json
    import locstrings
    import loc_export
    import loc_build

    before = len(fails)

    committed_path = os.path.join(ROOT, 'Localisation', 'strings.en.json')
    if not os.path.isfile(committed_path):
        fails.append("Localisation/strings.en.json is missing - "
                     "run python Tools/loc_export.py")
        return

    with io.open(committed_path, encoding='utf-8') as f:
        committed = json.load(f)

    current = loc_export.build()

    for path, table in current['stringTables'].items():
        was = committed.get('stringTables', {}).get(path, {})
        now_all = {k: v for b in table.values() for k, v in b.items()}
        was_all = {k: v for b in was.values() for k, v in b.items()}
        for name in sorted(set(now_all) - set(was_all)):
            fails.append("%s: %s is in the resources but not in "
                         "strings.en.json - run Tools/loc_export.py"
                         % (path, name))
        for name in sorted(set(was_all) - set(now_all)):
            fails.append("%s: %s is in strings.en.json but no longer in the "
                         "resources - run Tools/loc_export.py" % (path, name))
        changed = [n for n in sorted(set(now_all) & set(was_all))
                   if now_all[n] != was_all[n]]
        for name in changed[:5]:
            fails.append("%s: %s changed in the resources and not in "
                         "strings.en.json - run Tools/loc_export.py"
                         % (path, name))
        if len(changed) > 5:
            fails.append("%s: and %d more strings differ from "
                         "strings.en.json" % (path, len(changed) - 5))

    if current['documents'] != committed.get('documents'):
        fails.append("Localisation/strings.en.json documents section is stale "
                     "- run Tools/loc_export.py")

    # Only meaningful once the content agrees. Run against stale content it
    # reports a writer fault for what is really an un-regenerated JSON, which
    # sends the reader looking in the wrong file.
    if len(fails) == before:
        for path, n in loc_build.verify(committed):
            fails.append("localisation writer does not reproduce %s (%d lines "
                         "differ) - the .rc reader and writer have drifted"
                         % (path, n))

    if len(fails) == before:
        total = sum(len(b) for t in current['stringTables'].values()
                    for b in t.values())
        pages = sum(len(d['pages']) for d in current['documents'].values())
        oks.append("localisation: %d strings and %d document pages match "
                   "strings.en.json, and round-trip byte-exact" % (total, pages))


def check_public_docs():
    """Docs/public/ is what gets published. Three ways that goes wrong.

    ONE: the directory and the sync-public.ps1 allow-list disagree. The
    allow-list names files one by one on purpose - a wholesale tree sync
    would publish anything dropped into the directory - so the two are the
    same fact written twice, and two copies of a fact drift. A doc in the
    directory but not the list is written and never shipped; a doc in the
    list but not the directory makes the sync print "skip (absent)" and
    carry on, which nobody reads.

    TWO: a public doc links to an internal one. The reader cannot open it
    and has no way to find out why. Four docs did this before the split -
    SERVER-GUIDE pointed at the moderation design twice, MODDING at a design
    note, REFACTORING at the test plan.

    THREE: a public doc cites shogo-re/, the unpublished reverse-engineering
    tree. DTXFORMAT went as far as printing a command the reader was told to
    run against a checkout they do not have.
    """
    import io as _io

    pub = os.path.join(ROOT, 'Docs', 'public')
    if not os.path.isdir(pub):
        fails.append("Docs/public/ is missing - it is the publication boundary")
        return

    on_disk = set(f for f in os.listdir(pub) if f.lower().endswith('.md'))

    sync = os.path.join(ROOT, 'Tools', 'sync-public.ps1')
    text = _io.open(sync, encoding='utf-8', errors='replace').read()
    listed = set(re.findall(r"Docs\\public\\([A-Za-z0-9_.-]+\.md)", text))

    # The publication boundary has TWO front doors since 2026-08-30: the
    # ShogoFRESH repo (sync-public.ps1) carries the player-facing docs, and
    # the ShogoMAKE (shogo-re's makepackage, GAME_DOCS) stages the
    # creator-facing ones straight from this directory at build time. A doc
    # in neither list is still the failure this check exists for; a doc in
    # the kit's list is published, just through the other door. Read from
    # makepackage itself rather than duplicated here, so adding a doc to the
    # kit cannot silently disagree with this check.
    kit_pkg = os.path.join(ROOT, '..', 'shogo-re', 'tools', 'makepackage.py')
    kit_listed = set()
    if os.path.exists(kit_pkg):
        ktext = _io.open(kit_pkg, encoding='utf-8', errors='replace').read()
        kit_listed = set(re.findall(r'"Docs/public/([A-Za-z0-9_.-]+\.md)"', ktext))
    else:
        warns.append("shogo-re/tools/makepackage.py not found - kit-published "
                     "docs cannot be verified from here")

    for name in sorted(on_disk - listed - kit_listed):
        fails.append("Docs/public/%s is in neither sync-public.ps1 nor the "
                     "kit's GAME_DOCS - it will never be published" % name)
    for name in sorted(listed - on_disk):
        fails.append("sync-public.ps1 lists Docs/public/%s, which does not "
                     "exist - the sync skips it silently" % name)

    # Internal docs, by name. Anything under Docs/ that is not in public/.
    docs = os.path.join(ROOT, 'Docs')
    internal = set(f for f in os.listdir(docs)
                   if f.lower().endswith('.md') and
                   os.path.isfile(os.path.join(docs, f)))

    for name in sorted(on_disk):
        body = _io.open(os.path.join(pub, name), encoding='utf-8',
                        errors='replace').read()

        for other in sorted(internal):
            # Bare filename is enough - these are all distinctive, and a
            # match in prose is as dangling as a match in a link.
            if other in body:
                fails.append("Docs/public/%s references %s, which is not "
                             "published - the reader cannot open it"
                             % (name, other))

        if 'shogo-re' in body:
            fails.append("Docs/public/%s cites shogo-re/, which is not "
                         "published" % name)

    if not fails:
        oks.append("public docs: %d published, allow-list agrees, no dangling "
                   "internal references" % len(on_disk))


def check_debug_channels():
    """The debug-channel table must be bigger than the channel list.

    Both FreshDebug.cpp copies register channels LAZILY on first use into a
    fixed array, and return null once it is full - silently. A table sized
    equal to the channel count therefore works for every channel except
    whichever one is queried last, which reads as "that diagnostic is
    broken" with nothing anywhere to explain it. CamDebug was the ninth
    channel against a table of eight and would have raced for a slot.

    Also checks the two copies agree, per the rule that one fact
    implemented twice earns a check.
    """
    header = read(r'Shared/FreshDebug.h')

    channels = re.findall(r'^#define\s+FRESHDBG_[A-Z]+\s+"', header, re.M)
    n = len(channels)

    sizes = {}
    for name in ('ClientShellDLL/FreshDebug.cpp', 'ObjectDLL/FreshDebug.cpp'):
        text = read(name.replace('/', os.sep))
        m = re.search(r'#define\s+MAX_DEBUG_CHANNELS\s+(\d+)', text)
        if not m:
            fails.append("%s has no MAX_DEBUG_CHANNELS" % name)
            return
        sizes[name] = int(m.group(1))

    if len(set(sizes.values())) != 1:
        fails.append("the two FreshDebug.cpp copies disagree on "
                     "MAX_DEBUG_CHANNELS: %s" % sizes)
        return

    size = list(sizes.values())[0]

    if size < n:
        fails.append("MAX_DEBUG_CHANNELS is %d but Shared/FreshDebug.h "
                     "declares %d channels - the table registers lazily and "
                     "returns null once full, so the last channel queried "
                     "would silently never print" % (size, n))
        return

    oks.append("debug channels: %d declared, table holds %d, both copies agree"
               % (n, size))


def check_fall_tuning_exposed():
    """The Host tab's fall dials must agree with FreshTuning.h.

    FallDamage and FallThreshold are the only FreshTuning.h variables with a
    launcher UI, which means their bounds and defaults are now written twice
    in two languages about two thousand lines apart. The game clamps
    SILENTLY, so a launcher whose ceiling was higher than the game's would
    write a number the host chose, show it back, and have it quietly ignored.

    Only exposed variables are checked. The rest of FreshTuning.h is
    deliberately console-only, so there is nothing to disagree with.
    """
    tune = read(r'Shared/FreshTuning.h')
    vm   = read(r'Launcher/ShogoLauncher/ViewModels/MainViewModel.Host.cs')
    svc  = read(r'Launcher/ShogoLauncher/Services/HostService.cs')

    def game(macro):
        m = re.search(r'#define\s+%s\s+([-\d.]+)f' % macro, tune)
        return float(m.group(1)) if m else None

    wanted = {
        'FRESHTUNE_FALL_SCALE_MIN':      None,
        'FRESHTUNE_FALL_SCALE_MAX':      None,
        'FRESHTUNE_FALL_THRESH_MIN':     None,
        'FRESHTUNE_FALL_THRESH_MAX':     None,
        'FRESHTUNE_FALL_SCALE_DEFAULT':  None,
        'FRESHTUNE_FALL_THRESH_DEFAULT': None,
    }

    for k in wanted:
        wanted[k] = game(k)
        if wanted[k] is None:
            fails.append("could not read %s from FreshTuning.h" % k)
            return

    # The launcher's clamp calls, and the two places a default is written:
    # the property initialiser and the cfg fallback it is loaded back with.
    bad = []

    def launcher(pat, where, label):
        m = re.search(pat, where)
        if not m:
            bad.append("could not find the launcher's %s" % label)
            return None
        return [float(g) for g in m.groups()]

    clamp_scale = launcher(
        r'HostFallDamage\s*=\s*ClampReport\(HostFallDamage,\s*([\d.]+),\s*([\d.]+)',
        vm, 'fall damage clamp')
    clamp_thresh = launcher(
        r'HostFallThreshold\s*=\s*ClampReport\(HostFallThreshold,\s*([\d.]+),\s*([\d.]+)',
        vm, 'fall threshold clamp')

    if clamp_scale and clamp_scale != [wanted['FRESHTUNE_FALL_SCALE_MIN'],
                                       wanted['FRESHTUNE_FALL_SCALE_MAX']]:
        bad.append("fall damage range: launcher %s vs game %s"
                   % (clamp_scale, [wanted['FRESHTUNE_FALL_SCALE_MIN'],
                                    wanted['FRESHTUNE_FALL_SCALE_MAX']]))

    if clamp_thresh and clamp_thresh != [wanted['FRESHTUNE_FALL_THRESH_MIN'],
                                         wanted['FRESHTUNE_FALL_THRESH_MAX']]:
        bad.append("fall threshold range: launcher %s vs game %s"
                   % (clamp_thresh, [wanted['FRESHTUNE_FALL_THRESH_MIN'],
                                     wanted['FRESHTUNE_FALL_THRESH_MAX']]))

    # Defaults: three copies each - the record parameter, the property
    # initialiser, and the cfg read-back fallback. All three must be the
    # game's own, or a host who never touches the box still changes it.
    defaults = [
        ('fall damage', wanted['FRESHTUNE_FALL_SCALE_DEFAULT'], [
            (svc, r'double FallDamage = ([\d.]+)'),
            (vm,  r'_hostFallDamage = ([\d.]+)'),
            (vm,  r'GetFloat\("FallDamage",\s*([\d.]+)f\)'),
        ]),
        ('fall threshold', wanted['FRESHTUNE_FALL_THRESH_DEFAULT'], [
            (svc, r'double FallThreshold = ([\d.]+)'),
            (vm,  r'_hostFallThreshold = ([\d.]+)'),
            (vm,  r'GetFloat\("FallThreshold",\s*([\d.]+)f\)'),
        ]),
    ]

    for label, expect, sites in defaults:
        for text, pat in sites:
            m = re.search(pat, text)
            if not m:
                bad.append("could not find a %s default matching %s" % (label, pat))
            elif float(m.group(1)) != expect:
                bad.append("%s default: launcher %s vs game %s"
                           % (label, m.group(1), expect))

    if bad:
        fails.append("host fall dials disagree with FreshTuning.h: " + '; '.join(bad))
    else:
        oks.append("host fall dials match FreshTuning.h (ranges and defaults)")


def check_sfx_id_table():
    """Adding a special-FX id must add a row to the per-type ceiling table.

    SFXMgr sizes m_dynSFXLists and s_nDynArrayMaxNums by DYN_ARRAY_SIZE,
    which is SFX_TOTAL_NUMBER + 1, and indexes both by the raw FX id. So a
    new id whose ceiling row is missing gets a maximum of ZERO objects: the
    effect compiles, links, is dispatched, and never appears. Nothing says
    why, and CreateSFX still hands back a pointer.

    Hit while adding SFX_BLASTSWEEP_ID on 2026-08-23 - the row was only
    remembered because the table's fixed-size initialiser was being read for
    another reason. The header's instruction ("adjust SFX_TOTAL_NUMBER") does
    not mention the second table at all.

    Also asserts SFX_TOTAL_NUMBER really is the highest id, which is what
    the header says it must be and what makes the id-to-index identity safe.
    """
    ids  = read(r'Shared/SFXMsgIds.h')
    mgr  = read(r'ClientShellDLL/SFXMgr.cpp')
    mgrh = read(r'ClientShellDLL/SFXMgr.h')

    defined = {}
    for m in re.finditer(r'#define\s+(SFX_\w+_ID)\s+(\d+)', ids):
        defined[m.group(1)] = int(m.group(2))

    total = re.search(r'#define\s+SFX_TOTAL_NUMBER\s+(\d+)', ids)

    if not defined or not total:
        fails.append("could not read the SFX id table from SFXMsgIds.h")
        return

    total = int(total.group(1))
    highest = max(defined.values())

    if total != highest:
        fails.append("SFX_TOTAL_NUMBER is %d but the highest id is %d - "
                     "DYN_ARRAY_SIZE would be too small and GetDynArrayIndex "
                     "silently folds the overflow onto the general list (0)"
                     % (total, highest))
        return

    # DYN_ARRAY_SIZE is only ever total + 1; check rather than assume, since
    # the whole point here is that the two files drift.
    dyn = re.search(r'#define\s+DYN_ARRAY_SIZE\s+\(SFX_TOTAL_NUMBER\s*\+\s*1\)', mgrh)
    if not dyn:
        warns.append("DYN_ARRAY_SIZE is no longer SFX_TOTAL_NUMBER + 1 - "
                     "check_sfx_id_table's arithmetic needs revisiting")
        return

    size = total + 1

    # The fixed-size initialiser. Count its comma-separated entries.
    tbl = re.search(r's_nDynArrayMaxNums\[DYN_ARRAY_SIZE\]\s*=\s*\{(.*?)\};',
                    mgr, re.S)

    if not tbl:
        fails.append("could not find s_nDynArrayMaxNums in SFXMgr.cpp")
        return

    body = re.sub(r'//[^\n]*', '', tbl.group(1))
    rows = [e for e in (x.strip() for x in body.split(',')) if e]

    if len(rows) != size:
        fails.append("s_nDynArrayMaxNums has %d entries but DYN_ARRAY_SIZE is "
                     "%d - id %d would get a ceiling of 0 and never draw"
                     % (len(rows), size, len(rows)))
        return

    # Every id above the stock set needs a case in CreateSFX, or it is
    # dispatched to nothing and returns null.
    missing = [n for n, v in defined.items()
               if v > 31 and ('case %s' % n) not in mgr]

    if missing:
        fails.append("SFX ids with no CreateSFX case: " + ', '.join(sorted(missing)))
        return

    oks.append("SFX id table: %d ids, highest %d, %d ceiling rows, all dispatched"
               % (len(defined), highest, len(rows)))


def check_weapon_powerup_ids():
    """A pickup's weapon id is written twice per class; they must agree.

    Each WeaponPowerup subclass declares ADD_LONGINTPROP_FLAG(WeaponType,
    GUN_X_ID, PF_HIDDEN) and then sets m_iWeaponType = GUN_X_ID in its
    constructor. Two copies of one fact, sixteen times over, and a mismatch
    is invisible: the constructor decides what the pickup IS and the property
    decides what a map stores, so the two disagreeing produces a pickup that
    changes identity depending on whether it came from a map or from a
    respawn (engine fact 8 - CreateObject skips the property path).

    Also asserts the non-zero GUARD on the WeaponType read is still there.
    Without it a stored 0 - which is what every WORLDS\\MULTI map and every
    newly built map carries - overwrites the constructor's id, and since
    GUN_PULSERIFLE_ID is 0 every pickup in single player becomes a pulse
    rifle. That was BUGS.md C8, live from 1998 until 2026-08-24, and the
    guard is a single line that reads like a style choice rather than the
    fix it is.
    """
    src = read(r'ObjectDLL/WeaponPowerups.cpp')

    # The guard first - it is the whole bug.
    read_block = re.search(
        r'GetPropGeneric\(\s*"WeaponType",\s*&genProp\s*\)\s*==\s*DE_OK\s*\)'
        r'(.{0,200}?)m_iWeaponType\s*=\s*\(\s*DBYTE\s*\)genProp\.m_Long',
        src, re.S)

    if not read_block:
        fails.append("could not find the WeaponType property read in "
                     "WeaponPowerup::ReadProp")
        return

    if 'genProp.m_Long != 0' not in read_block.group(1):
        fails.append("the WeaponType read has lost its non-zero guard - a map "
                     "storing 0 will overwrite every subclass id and, because "
                     "GUN_PULSERIFLE_ID is 0, turn every SP pickup into a "
                     "pulse rifle again (BUGS.md C8)")
        return

    # Now the per-class pairing, in document order.
    tokens = re.findall(
        r'ADD_LONGINTPROP_FLAG\(\s*WeaponType\s*,\s*(GUN_\w+)\s*,'
        r'|m_iWeaponType\s*=\s*(GUN_\w+)\s*;',
        src)

    pending = None
    pairs   = []
    bad     = []

    for declared, assigned in tokens:
        if declared:
            if pending:
                bad.append("%s declares a WeaponType default but its "
                           "constructor never assigns one" % pending)
            pending = declared
        elif assigned:
            if pending is None:
                bad.append("constructor sets m_iWeaponType = %s with no "
                           "matching ADD_LONGINTPROP_FLAG above it" % assigned)
            else:
                if pending != assigned:
                    bad.append("property default %s vs constructor %s"
                               % (pending, assigned))
                pairs.append(pending)
                pending = None

    if pending:
        bad.append("%s declares a WeaponType default with no constructor "
                   "assignment after it" % pending)

    if not pairs:
        fails.append("found no WeaponType property/constructor pairs - the "
                     "check has stopped looking at anything")
        return

    if bad:
        fails.append("weapon powerup ids disagree: " + '; '.join(bad))
    else:
        oks.append("weapon powerup ids: %d classes, property default matches "
                   "constructor, stored-zero guard in place" % len(pairs))


def check_character_mgr_pairing():
    """Every character added to CCharacterMgr must be let go of again.

    The manager holds raw CBaseCharacter* and hands their m_hObject to the
    AI as targets. Nothing removes a character when the engine destroys it -
    CBaseCharacter::RemoveObject() is the only Remove, and a player never
    reaches it (the object is reused across respawns, and a disconnecting
    client's is destroyed by the engine). A player who left therefore stayed
    on m_playerList as a pointer to a dead object, and the next AI sense tick
    crashed the dedicated server inside CreateInterObjectLink.

    The destructor is the one path every character takes, so that is where
    the release has to be. This asserts it is still there, because the bug
    was invisible for months and cost nothing to reintroduce.
    """
    dtor = read(r'ObjectDLL/BaseCharacter.cpp')
    mgr  = read(r'ObjectDLL/CharacterMgr.cpp')

    m = re.search(r'CBaseCharacter::~CBaseCharacter\s*\(\s*\)\s*\{(.*?)\n\}',
                  dtor, re.S)

    if not m:
        fails.append("cannot find ~CBaseCharacter to check its "
                     "CCharacterMgr release")
        return

    if 'RemoveFromAllLists' not in m.group(1):
        fails.append("~CBaseCharacter does not call "
                     "CCharacterMgr::RemoveFromAllLists - a destroyed "
                     "character stays on the manager's lists as a dangling "
                     "pointer, which is the dedicated-server crash on "
                     "disconnect")
        return

    # And the sweep has to cover every list, or it half-works in a way
    # nothing would notice until an AI targeted the wrong faction's corpse.

    lists = set(re.findall(r'CTList<CBaseCharacter\*>\s+(m_\w+)',
                           read(r'ObjectDLL/CharacterMgr.h')))

    sweep = re.search(r'RemoveFromAllLists\s*\(\s*CBaseCharacter\s*\*\s*\w+\s*\)'
                      r'\s*\{(.*?)\n\}', mgr, re.S)

    if not sweep:
        fails.append("cannot find CCharacterMgr::RemoveFromAllLists body")
        return

    missed = sorted(l for l in lists if l not in sweep.group(1))

    if missed:
        fails.append("RemoveFromAllLists does not sweep %s - a character on "
                     "one of those outlives its object" % ", ".join(missed))
        return

    oks.append("every CCharacterMgr list is released in ~CBaseCharacter (%d)"
               % len(lists))


def check_docs_are_text():
    """No control bytes in the docs, because grep cannot see the file.

    CLAUDE.md warns that this environment mangles escapes written through a
    shell heredoc - "'\\0' becomes a literal NUL". On 2026-08-13 that was
    found to have actually happened: Docs/TESTPLAN.md described the GameSpy
    \\status query, meant to write `bots\\4` and `bots\\0` literally, and
    carried the CONTROL BYTES 0x04 and 0x00 instead.

    The NUL is what makes this worth a check rather than a fix. grep treats
    a file containing one as BINARY and skips it silently - so every
    doc-wide search had been blind to TESTPLAN.md for as long as it was
    there, including the searches that maintain these very checks. A
    mangled escape inside a code span also renders as nothing, so reading
    the page does not reveal it either. It is invisible from both
    directions, which is the definition of something a machine should look
    for.

    Tabs, newlines and carriage returns are text; everything below 0x20
    other than those is not.
    """
    allowed = {9, 10, 13}
    bad = []

    # Docs/ AND the top-level markdown. CLAUDE.md was outside the first
    # version of this check and had the same fault - the server log path,
    # mangled identically to the one in the runbook it links to.

    targets = [(ROOT, [f for f in os.listdir(ROOT)
                       if f.lower().endswith('.md')])]

    for root, dirs, files in os.walk(os.path.join(ROOT, "Docs")):
        dirs[:] = [d for d in dirs if d not in ('.git', '__pycache__')]
        targets.append((root, files))

    for root, files in targets:
        for f in files:
            if not f.lower().endswith(('.md', '.txt')):
                continue
            path = os.path.join(root, f)
            data = io.open(path, 'rb').read()

            # EVERY occurrence, not the first. Reporting one per file turns
            # a cleanup into a fix-run-fix loop, which is how the first
            # pass of this check missed six of the nine it eventually
            # found - each repair simply revealed the next.

            rel = os.path.relpath(path, ROOT)

            for i, b in enumerate(data):
                if b < 0x20 and b not in allowed:
                    bad.append("%s: byte 0x%02X at offset %d (%r)"
                               % (rel, b, i,
                                  data[max(0, i - 30):i + 10]))

            # A LONE CR, which is what a mangled backslash-r leaves behind.
            #
            # The check above cannot see it: 0x0D is legitimate text and
            # these files are CRLF, so banning it outright would fire on
            # every line. But a CR that is NOT followed by LF is not a line
            # ending, and in a CRLF document there is no other honest reason
            # for one.
            #
            # Found the hard way, in HANDOFF.md, hours after this check was
            # written to catch exactly this class - a heredoc turned the
            # literal text backslash-r-c-o-n into CR + "con" and the doc
            # rendered as "con" with the CR invisible. The first version of
            # this check would have passed it forever.

            for i, b in enumerate(data):
                if b == 0x0D and (i + 1 >= len(data) or data[i + 1] != 0x0A):
                    bad.append("%s: LONE CR at offset %d - a mangled "
                               "escape, not a line ending (%r)"
                               % (rel, i, data[max(0, i - 30):i + 10]))

    if bad:
        for b in bad:
            fails.append("control byte in a doc - a heredoc-mangled escape, "
                         "and grep skips the whole file as binary: %s" % b)
        return

    oks.append("docs are text: no control bytes to hide a file from grep")



def check_net_update_rate():
    """UpdateRate is written in three places and must be one number.

    It is the CLIENT's send rate - Client.exe reads it, clamps it to [2,60]
    and writes it into the outgoing packet; the server's variable of the same
    name has no readers at all. Docs/NETRATES.md carries the xrefs.

    Three copies of that number exist, and they are the usual shape of a
    silent drift: Defaults/client-settings.cfg seeds it into autoexec.cfg
    when the fix is applied, MainViewModel.Settings.cs writes it on every
    Save, and RestoreSettingsDefaults reads it back with a hardcoded
    fallback. Change one and a player gets whichever ran last - the seed on
    a fresh install, the Save path the first time they touch the Settings
    tab.

    Also checks the engine's clamp, because a value outside [2,60] is not
    refused anywhere: Client.exe silently pulls it back into range, so the
    cfg would say one thing and the wire another.
    """
    import io as _io

    cfg = os.path.join(ROOT, 'Launcher', 'ShogoLauncher', 'Defaults',
                       'client-settings.cfg')
    vm = os.path.join(ROOT, 'Launcher', 'ShogoLauncher', 'ViewModels',
                      'MainViewModel.Settings.cs')
    for path in (cfg, vm):
        if not os.path.isfile(path):
            fails.append("%s is missing - cannot check the update rate"
                         % os.path.relpath(path, ROOT))
            return

    text = _io.open(cfg, encoding='utf-8', errors='replace').read()
    m = re.search(r'"UpdateRate"\s+"(\d+)"', text)
    if not m:
        fails.append("client-settings.cfg does not seed UpdateRate - the "
                     "game would fall back to the 1998 modem default of 6")
        return
    seeded = int(m.group(1))

    text = _io.open(vm, encoding='utf-8', errors='replace').read()
    written = re.findall(r'autoexec\.Set\("UpdateRate",\s*(\d+)\)', text)
    restored = re.findall(r'GetInt\("UpdateRate",\s*(\d+)\)', text)
    if not written:
        fails.append("MainViewModel.Settings.cs no longer writes UpdateRate - "
                     "the Settings tab would leave whatever was there")
        return

    values = set([seeded] + [int(v) for v in written] + [int(v) for v in restored])
    if len(values) != 1:
        fails.append("UpdateRate disagrees across its copies: seed %d, Save "
                     "%s, RestoreDefaults %s - a player gets whichever wrote "
                     "last" % (seeded, ",".join(written),
                               ",".join(restored) or "none"))
        return

    rate = values.pop()
    if not 2 <= rate <= 60:
        fails.append("UpdateRate is %d, outside the engine's clamp of [2,60] "
                     "- Client.exe pulls it back into range silently, so the "
                     "cfg and the wire would disagree" % rate)
        return

    oks.append("net update rate: %d in all %d places, inside the engine's "
               "[2,60] clamp" % (rate, 1 + len(written) + len(restored)))

def check_server_driven_projectile():
    """No hand-coded {SPIDER, KATO} pair - it must go through the predicate.

    Three client sites decide a projectile's FX is server-driven rather than
    drawn locally: WeaponModel::DoProjectile, CProjectileFX::CreateObject and
    CWeaponFX::CreateObject. They used to list GUN_SPIDER_ID and
    GUN_KATOGRENADE_ID by hand. When the energy grenade was reworked into a
    sticky mine (the spider's class) it was added to FreshIsThrownGrenade but
    to NONE of these three - so a mine you fired flashed on landing and its
    real explosion was suppressed as a duplicate (BUGS.md M1/M2).

    The fix was FreshIsServerDrivenProjectile, one predicate the three now
    share. This check stops the pattern coming back: any single source line
    that names both GUN_SPIDER_ID and GUN_KATOGRENADE_ID is a hand-coded pair
    that has bypassed the predicate, and the next weapon added to the concept
    will drift from it exactly as the energy grenade did.
    """
    import glob as _glob

    offenders = []
    for d in ('ClientShellDLL', 'ObjectDLL'):
        for path in _glob.glob(os.path.join(ROOT, d, '*.cpp')):
            for n, line in enumerate(io.open(path, encoding='utf-8',
                                             errors='replace'), 1):
                if 'GUN_SPIDER_ID' in line and 'GUN_KATOGRENADE_ID' in line:
                    rel = os.path.relpath(path, ROOT)
                    offenders.append("%s:%d" % (rel, n))

    if offenders:
        for o in offenders:
            fails.append("hand-coded SPIDER/KATO pair at %s - use "
                         "FreshIsServerDrivenProjectile so a new weapon in "
                         "the set cannot drift from it (BUGS.md M1/M2)" % o)
        return

    oks.append("server-driven projectiles go through one predicate, no "
               "hand-coded SPIDER/KATO pair")



def check_arena_pass_reaches_pickups():
    """An arena mode must be consulted at all THREE gates or it does nothing.

    "Every weapon pickup becomes a rocket launcher" passes three independent
    yes/no gates on its way to the map, in two files:

      1. CRiotServerShell decides whether to RUN the weapon pass at all.
      2. ApplyWeaponRules' early return decides whether there is work to do.
      3. The collect loop's bTakeAll decides whether to take every pickup or
         only the blocked ones.

    Drop the arena test from any one of them and TOWs Out silently leaves the
    map untouched - no error, no log line, and the mode simply has no effect.
    That shipped: gate 2 never knew about arena modes at all (it predates
    them), so a server with no BlockWeapons and no RandomPickups ran TOWs Out
    with every original pickup still standing. Reported from play on 0.10.59.

    Infinite ammo survives the same three gates only by accident - it marks
    weapons blocked, which carries it through gate 2 as a side effect. So the
    accident is not a pattern to rely on, and arena is not the only fact that
    could fall down this hole.
    """
    shell = read(r'ObjectDLL/RiotServerShell.cpp')
    block = read(r'ObjectDLL/WeaponBlocklist.cpp')

    bad = []

    # Gate 1: the caller weighs arena alongside its other reasons.
    if not re.search(r'DBOOL\s+bArena\s*=\s*FreshRules\(\)->IsArena\(\)', shell):
        bad.append("CRiotServerShell no longer asks IsArena() before running "
                   "the weapon pass")
    elif not re.search(r'if\s*\([^)]*\bbArena\b[^)]*\)', shell):
        bad.append("CRiotServerShell computes bArena but no longer tests it")

    # Gate 2: the early return. This is the one that was missing.
    m = re.search(r'if\s*\(\s*nBlocked\s*<=\s*0[^;]*?\)\s*return\s*0\s*;', block, re.S)

    if not m:
        bad.append("could not find ApplyWeaponRules' early return - if it was "
                   "removed that is fine, but this check needs rewriting")
    elif 'IsArena' not in m.group(0):
        bad.append("ApplyWeaponRules' early return does not consult IsArena(), "
                   "so an arena mode with no blocklist and no shuffle returns "
                   "before touching a single pickup")

    # Gate 3: the collect loop takes every pickup, not just blocked ones.
    if not re.search(r'bTakeAll\s*=\s*bRandomizeAll\s*\|\|\s*FreshRules\(\)->IsArena\(\)', block):
        bad.append("the collect loop's bTakeAll no longer consults IsArena(), "
                   "so an arena mode would only replace BLOCKED pickups")

    if bad:
        fails.append("arena pickups: " + '; '.join(bad))
    else:
        oks.append("arena pickup pass: all three gates consult IsArena()")


def check_game_mode_extraction():
    """A mode is a class, not a global somebody tests against in six places.

    Before the rules extraction, `g_nGameMode ==` appeared across PlayerObj,
    RiotServerShell, FreshBot and WeaponBlocklist, so adding a mode meant
    visiting all of them and the modes themselves were unreadable - "what does
    Squishie do" was a grep, not a file. The interface (ObjectDLL/FreshRules.h)
    turns those six questions round, and the whole value of that is lost the
    first time somebody reaches past it.

    So this asserts the global is GONE rather than merely tidied: the name may
    appear in prose explaining why it is not there, but nothing may declare or
    read it. It also checks the hooks stay Lua-shaped - every one returning a
    scalar or a string - because that is the property that lets a Lua mode
    implement the same interface later, and it is easy to break by adding one
    convenient hook that hands back a CPlayerObj*.
    """
    import io as _io

    rules_h = os.path.join(ROOT, 'ObjectDLL', 'FreshRules.h')
    rules_c = os.path.join(ROOT, 'ObjectDLL', 'FreshRules.cpp')

    for path in (rules_h, rules_c):
        if not os.path.isfile(path):
            fails.append("%s is missing - the game-mode rules layer is the "
                         "thing this check exists to protect"
                         % os.path.relpath(path, ROOT))
            return

    # 1. The global must not be declared or read anywhere. Comments may name it.
    offenders = []
    for sub in ('ObjectDLL', 'ClientShellDLL', 'Shared', 'ShogoServ'):
        base = os.path.join(ROOT, sub)
        if not os.path.isdir(base):
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if d not in ('.git', 'Debug', 'Release')]
            for name in filenames:
                if not name.endswith(('.cpp', '.h')):
                    continue
                full = os.path.join(dirpath, name)
                for n, line in enumerate(_io.open(full, encoding='utf-8',
                                                  errors='replace'), 1):
                    if 'g_nGameMode' not in line:
                        continue
                    stripped = line.strip()
                    if stripped.startswith('//') or stripped.startswith('*'):
                        continue
                    offenders.append("%s:%d" % (os.path.relpath(full, ROOT), n))

    if offenders:
        fails.append("g_nGameMode is back in live code (%s) - a mode is a class "
                     "in FreshRules.cpp answering hooks, not a global tested "
                     "against. Add a hook instead."
                     % ", ".join(offenders[:4]))

    # 2. Every hook must return something a Lua implementation could return.
    text = _io.open(rules_h, encoding='utf-8', errors='replace').read()
    hooks = re.findall(r'virtual\s+([A-Za-z_][A-Za-z0-9_ \*]*?)\s+(\w+)\s*\(',
                       text)
    allowed = ('int', 'DBOOL', 'DFLOAT', 'const char*', 'void')
    bad = []
    for rettype, fn in hooks:
        norm = ' '.join(rettype.split()).replace(' *', '*')
        if norm == '~CFreshRules' or fn.startswith('~'):
            continue
        if norm not in allowed:
            bad.append("%s %s()" % (norm, fn))

    if bad:
        fails.append("FreshRules hooks must return a scalar or a string so a "
                     "Lua mode can implement the same interface - these do not: "
                     "%s" % ", ".join(bad))

    # 3. Rules objects must hold NO STATE. This is what makes a mid-match mode
    #    swap a pointer assignment - the "party" conductor direction depends on
    #    it, and one convenient member variable would quietly turn a swap into
    #    a thing needing enter/exit callbacks.
    text_c = _io.open(rules_c, encoding='utf-8', errors='replace').read()
    stateful = []
    depth = 0
    cls = None
    for line in text_c.splitlines():
        bare = line.split('//')[0].strip()
        if cls is None:
            m = re.match(r'class\s+(\w+)\s*:\s*public\s+CFreshRules', bare)
            if m:
                cls, depth = m.group(1), 0
            continue
        depth += bare.count('{') - bare.count('}')
        if depth <= 0 and '}' in bare:
            cls = None
            continue
        # a data member is a declaration with no call parentheses in it,
        # AT CLASS SCOPE (depth 1) - a local variable inside a method body
        # sits deeper and is not state, it dies with the call. The first
        # hook with a local (CRulesSquishieWar::MechSlots) false-positived
        # here the day after this guard was written.
        if (depth == 1
                and bare.endswith(';') and '(' not in bare and ')' not in bare
                and not bare.startswith('public') and not bare.startswith('return')
                and not bare.startswith('#')):
            stateful.append("%s: %s" % (cls, bare))

    if stateful:
        fails.append("game-mode rules must hold no state so a mid-match swap "
                     "stays a pointer assignment - these declare members: %s"
                     % "; ".join(stateful[:3]))

    if not offenders and not bad and not stateful:
        oks.append("game modes: %d hooks, all scalar-returning, rules stateless, "
                   "g_nGameMode gone" % len(hooks))


def check_game_mode_lists():
    """The launcher's GameModes dropdown mirrors FreshGameModeName().

    Two copies of the mode list exist on purpose: the shared header's switch
    (what the game calls each mode) and the launcher's array (what the Host
    tab offers, where the INDEX is the number written to ShogoSrv.cfg). Two
    copies of anything drift, and this drift would be silent: a mode missing
    from the launcher is merely unhostable from the UI, and a misordered one
    quietly hosts the wrong mode. Standard rule - one fact twice gets a check.
    """
    header = os.path.join(ROOT, 'Shared', 'FreshGameModes.h')
    host = os.path.join(ROOT, 'Launcher', 'ShogoLauncher', 'ViewModels',
                        'MainViewModel.Host.cs')

    for path in (header, host):
        if not os.path.isfile(path):
            fails.append("%s is missing - cannot check the game mode lists"
                         % os.path.relpath(path, ROOT))
            return

    htext = open(header, encoding='utf-8', errors='replace').read()

    consts = dict(re.findall(r'#define\s+(GAMEMODE_\w+)\s+(\d+)', htext))
    consts.pop('GAMEMODE_FIRST', None)
    consts.pop('GAMEMODE_LAST', None)

    body = re.search(r'FreshGameModeName[^{]*\{(.*?)\n\}', htext, re.S)
    if not body:
        fails.append("FreshGameModeName() not found in FreshGameModes.h")
        return

    names_by_value = {0: 'Deathmatch'}     # the default arm
    for const, name in re.findall(r'case\s+(GAMEMODE_\w+)\s*:\s*return\s+"([^"]+)"',
                                  body.group(1)):
        if const not in consts:
            fails.append("game modes: %s has a name but no #define" % const)
            return
        names_by_value[int(consts[const])] = name

    ctext = open(host, encoding='utf-8', errors='replace').read()
    arr = re.search(r'GameModes\s*\{\s*get;\s*\}\s*=\s*\r?\n?\s*\{([^}]*)\}', ctext)
    if not arr:
        fails.append("GameModes array not found in MainViewModel.Host.cs")
        return

    launcher_list = re.findall(r'"([^"]+)"', arr.group(1))

    # THE THING THAT MATTERS IS THE INDEX, not contiguity. The launcher
    # writes the dropdown's INDEX into ShogoSrv.cfg as the mode number, so
    # entry i must be mode i - and ids get RESERVED here (4 and 5 were
    # spoken for by Duel and Headcount while Squad Deathmatch built at 6),
    # which an earlier "must be contiguous from 0" rule refused outright.
    # A reserved id is legitimate as long as the launcher placeholds it
    # rather than closing the gap, because closing it would silently host
    # the wrong mode. So: every NAMED mode must sit at its own index, and
    # every gap must be a visible placeholder.
    top = max(names_by_value)

    if len(launcher_list) != top + 1:
        fails.append("game modes: header's highest id is %d so the launcher "
                     "needs %d entries, has %d - the index IS the mode number"
                     % (top, top + 1, len(launcher_list)))
        return

    for nMode in range(top + 1):
        entry = launcher_list[nMode]

        if nMode in names_by_value:
            if entry != names_by_value[nMode]:
                fails.append("game mode %d: header says %r, launcher says %r"
                             % (nMode, names_by_value[nMode], entry))
                return
        elif not entry.lower().startswith("(reserved"):
            fails.append("game mode %d has no name in the header, so the "
                         "launcher entry must be a '(reserved: ...)' "
                         "placeholder - it says %r. Closing the gap would "
                         "host the wrong mode." % (nMode, entry))
            return

    nReserved = (top + 1) - len(names_by_value)

    oks.append("game mode lists: launcher indices match FreshGameModeName "
               "(%d modes, %d reserved)" % (len(names_by_value), nReserved))


def check_fresh_ammo_doctrine():
    """Every FRESH weapon holds four magazines, and a pickup is two of them.

    The relationship used to be pickup == clip and was stated in four comments
    rather than checked anywhere. As of 0.10.59 the whole table is uniform:

        pickup == 2 x clip          carry == 4 x clip

    and both are checked EXACTLY rather than as a band, because the table now
    says something exact. A band would have accepted the intermediate state
    this arrived through - eight magazines everywhere, which was more ammunition
    than anyone asked for and looked perfectly reasonable in a range check.

    The tightness is the point. At one magazine per pickup the rule was visible
    in the numbers and needed no check; at two it is not, and a single weapon
    tuned in isolation would silently leave the doctrine behind. If the doctrine
    genuinely changes, this check is the thing that has to change with it - which
    is the intended cost, not an obstacle.

    THE RED RIOT IS THE ONE EXCEPTION, named here rather than tolerated: one
    magazine per pickup, because its magazine is a single round and a two-round
    pickup would drop half its entire reserve on the floor. Its carry still
    obeys the four-magazine rule.
    """
    import io as _io

    path = os.path.join(ROOT, 'Shared', 'WeaponDefs.h')
    if not os.path.isfile(path):
        fails.append("Shared/WeaponDefs.h is missing")
        return

    text = _io.open(path, encoding='utf-8', errors='replace').read()

    try:
        start = text.index('static const WeaponAmmoStats s_Fresh[GUN_MAX_NUMBER]')
        end = text.index('static const WeaponAmmoStats s_Invalid')
    except ValueError:
        fails.append("cannot find the s_Fresh ammo table - the FRESH pickup "
                     "doctrine is unchecked")
        return

    block = text[start:end]

    # Rows carrying a real magazine. clip -1 is the clipless melee/toy rows,
    # clip 0 the unused slots; neither has a magazine to be two of.
    rows = re.findall(
        r'/\*(GUN_\w+)\*/\s*\{\s*(-?\d+),\s*(\d+),\s*(\d+),\s*\d+,\s*(\d+)\s*\}',
        block)

    if len(rows) < 15:
        fails.append("only %d rows parsed out of s_Fresh - the ammo doctrine "
                     "check is not seeing the table it thinks it is" % len(rows))
        return

    # One magazine per pickup, because its magazine is one round - see the table.
    PICKUP_EXEMPT = {'GUN_REDRIOT_ID': 1}

    bad_pickup, bad_carry, bad_pair = [], [], []
    checked = 0

    for name, clip, pmin, pmax, carry in rows:
        clip, pmin, pmax, carry = int(clip), int(pmin), int(pmax), int(carry)
        if clip <= 0:
            continue
        checked += 1

        want_pickup = clip * PICKUP_EXEMPT.get(name, 2)

        if pmin != pmax:
            bad_pair.append("%s (%d..%d)" % (name, pmin, pmax))
        if pmin != want_pickup:
            bad_pickup.append("%s: clip %d, pickup %d (want %d)"
                              % (name, clip, pmin, want_pickup))
        if carry != clip * 4:
            bad_carry.append("%s: clip %d, carry %d (want %d)"
                             % (name, clip, carry, clip * 4))

    if bad_pair:
        fails.append("FRESH pickups are FLAT - min and max must match, so a "
                     "pickup is a known quantity rather than a roll: %s"
                     % ", ".join(bad_pair))
    if bad_pickup:
        fails.append("a FRESH pickup is two magazines (pickup == 2 x clip) - "
                     "these disagree: %s. A weapon that genuinely wants a "
                     "different size goes in PICKUP_EXEMPT here with its reason, "
                     "the way the Red Riot does."
                     % "; ".join(bad_pickup[:4]))
    if bad_carry:
        fails.append("every FRESH weapon carries four magazines "
                     "(carry == 4 x clip) - these disagree: %s. If the doctrine "
                     "changed, change it here too rather than leaving the table "
                     "and the rule disagreeing."
                     % "; ".join(bad_carry[:4]))

    if not (bad_pair or bad_pickup or bad_carry):
        oks.append("FRESH ammo: %d weapons, four magazines carried, two per "
                   "pickup (Red Riot one, by name)" % checked)


def check_mouse_smoothness():
    """`inputrate` has a floor, and the floor is written in three places.

    The engine calls it "smoothness" - IDS_MOUSE_INPUTRATE in ClientRes.rc is
    literally that word - and it floors the measured gap between DirectInput
    events. That gap is the DIVISOR when movement becomes a turn rate, so 0
    means no floor at all and a 1000 Hz mouse hands the engine ~1 ms to divide
    by. This launcher shipped 0 for years and called it "raw mouse input",
    which is not what 0 does.

    The floor now lives in the seed file, in a C# constant, and in the XAML
    slider's Minimum. Three copies of one number, which is the shape this
    project has a rule about: they are checked to agree here, and the seeded
    value is checked to sit inside the range Monolith's own slider offered
    (0-40, of which we refuse the bottom).
    """
    import io as _io

    cfg = os.path.join(ROOT, 'Launcher', 'ShogoLauncher', 'Defaults',
                       'client-settings.cfg')
    vm = os.path.join(ROOT, 'Launcher', 'ShogoLauncher', 'ViewModels',
                      'MainViewModel.Settings.cs')
    xaml = os.path.join(ROOT, 'Launcher', 'ShogoLauncher', 'MainWindow.xaml')
    for path in (cfg, vm, xaml):
        if not os.path.isfile(path):
            fails.append("%s is missing - cannot check the mouse smoothness floor"
                         % os.path.relpath(path, ROOT))
            return

    text = _io.open(cfg, encoding='utf-8', errors='replace').read()
    m = re.search(r'"inputrate"\s+"([0-9.]+)"', text)
    if not m:
        fails.append("client-settings.cfg does not seed inputrate - an install "
                     "would keep whatever it had, including 0")
        return
    seeded = float(m.group(1))

    text = _io.open(vm, encoding='utf-8', errors='replace').read()
    lo = re.search(r'MOUSE_SMOOTHNESS_MIN\s*=\s*([0-9.]+)f', text)
    hi = re.search(r'MOUSE_SMOOTHNESS_MAX\s*=\s*([0-9.]+)f', text)
    if not lo or not hi:
        fails.append("MainViewModel.Settings.cs no longer declares "
                     "MOUSE_SMOOTHNESS_MIN/MAX - the floor is what stops "
                     "inputrate going back to 0")
        return
    lo, hi = float(lo.group(1)), float(hi.group(1))

    text = _io.open(xaml, encoding='utf-8', errors='replace').read()
    m = re.search(r'Binding MouseSmoothness\}"\s+Minimum="([0-9.]+)"\s+Maximum="([0-9.]+)"',
                  text)
    if not m:
        fails.append("MainWindow.xaml has no MouseSmoothness slider with an "
                     "explicit Minimum/Maximum - the UI could offer 0 again")
        return
    ui_lo, ui_hi = float(m.group(1)), float(m.group(2))

    if (ui_lo, ui_hi) != (lo, hi):
        fails.append("mouse smoothness range disagrees: C# says [%g, %g], the "
                     "slider says [%g, %g]" % (lo, hi, ui_lo, ui_hi))
        return
    if not lo <= seeded <= hi:
        fails.append("client-settings.cfg seeds inputrate %g, outside the "
                     "launcher's own [%g, %g]" % (seeded, lo, hi))
        return
    if lo <= 0:
        fails.append("the mouse smoothness floor is %g - at 0 there is no floor "
                     "on the input interval at all, which is the bug this "
                     "range exists to prevent" % lo)
        return
    if hi > 40:
        fails.append("mouse smoothness maximum is %g; Monolith's own slider "
                     "stopped at 40 and nothing is known about values past it"
                     % hi)
        return

    oks.append("mouse smoothness: seed %g inside [%g, %g], C# and slider agree"
               % (seeded, lo, hi))

def check_string_overlay():
    """Display text goes through the overlay, or the feature silently rots.

    FreshStrings gives every id-drawn string a file override
    (Strings\override.txt). Its choke point is TextHelper.cpp: all of
    CTextHelper's id-taking entry points resolve through FreshFormatString,
    which consults the overlay and falls back to FormatString. A new call
    site written as pClientDE->FormatString(strID) would compile, run, and
    simply never show a mapper's text - the same silent-bypass shape as a
    print that skips FreshPrint, and the same fix: assert the class, not
    the sites.

    The two font-name lookups (IDS_INGAMEFONT / IDS_REPLACEMENTFONT) are
    exempt BY DESIGN: they resolve fonts, not display text, and routing
    them through the overlay would let a text file change the font and
    break the bitmap-font fallback (engine fact 23). The check therefore
    keys on strID, which only the display-text entry points take.
    """
    path = os.path.join(ROOT, 'ClientShellDLL', 'TextHelper.cpp')
    text = open(path, encoding='latin-1').read()

    direct = len(re.findall(r'FormatString\s*\(\s*strID', text))
    routed = len(re.findall(r'FreshFormatString\s*\(\s*pClientDE\s*,\s*strID', text))

    impl = open(os.path.join(ROOT, 'ClientShellDLL', 'FreshStrings.cpp'),
                encoding='latin-1').read()
    fallback = len(re.findall(r'->FormatString\s*\(', impl))

    if direct:
        fails.append("string overlay: %d direct FormatString(strID) call(s) in "
                     "TextHelper.cpp bypass the overlay - use FreshFormatString" % direct)
    elif routed < 5:
        fails.append("string overlay: only %d FreshFormatString site(s) in "
                     "TextHelper.cpp - the id entry points have changed shape, "
                     "re-check the hook" % routed)
    elif fallback != 1:
        fails.append("string overlay: FreshStrings.cpp holds %d FormatString "
                     "fallback(s), expected exactly 1 - the class has grown a "
                     "second door" % fallback)
    else:
        oks.append("string overlay: %d TextHelper sites routed, 0 direct, "
                   "one fallback in FreshStrings" % routed)


def main():
    for fn in (check_launcher_roundtrip, check_dirty_tracking_covers_tabs,
               check_launcher_defaults_match_game,
               check_config_key_symmetry, check_bindings, check_strings,
               check_protocol, check_defaults, check_format_strings, check_version,
               check_shared_constants, check_manifest_allowlist,
               check_player_models_agree,
               check_no_pointer_from_wire, check_no_untracked_dependencies,
               check_rez_readers, check_dtx_implementations,
               check_weapon_key_roundtrip, check_netdefs_copies_agree,
               check_resource_ids, check_localisation, check_keybind_rows,
               check_public_docs, check_debug_channels,
               check_character_mgr_pairing, check_fall_tuning_exposed,
               check_sfx_id_table,
               check_arena_pass_reaches_pickups,
               check_weapon_powerup_ids,
               check_docs_are_text, check_net_update_rate,
               check_server_driven_projectile, check_mouse_smoothness,
               check_game_mode_extraction, check_game_mode_lists,
               check_fresh_ammo_doctrine, check_squish_trim_gap,
               check_string_overlay):
        try:
            fn()
        except Exception as e:
            fails.append("%s blew up: %s" % (fn.__name__, e))

    print("PASS (%d)" % len(oks))
    for o in oks:
        print("  ok    " + o)

    if warns:
        print("\nWARN (%d)" % len(warns))
        for w in warns:
            print("  warn  " + w)

    print("\nFAIL (%d)" % len(fails))
    for f in fails:
        print("  FAIL  " + f)

    return len(fails)


if __name__ == '__main__':
    sys.exit(main())
