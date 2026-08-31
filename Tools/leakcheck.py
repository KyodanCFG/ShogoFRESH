"""Does this repository contain anything it must not?

    python Tools/leakcheck.py

Run it before every push. Exit code is the number of blocking problems, so it
can gate a script.

--------------------------------------------------------------------------
Why this exists
--------------------------------------------------------------------------

ShogoFRESH is built partly from Monolith's Shogo v2.2 source release, whose
licence says the source "is NOT public domain" and "may not be freely
distributed to any BBS, CD, floppy or any other media". A public repository
is other media.

So the split is: the launcher and tools are original work and live here; the
modified game DLLs are built from that source and ship only as compiled
binaries in Releases.

That split is only worth anything if it holds. It is held by hand - somebody
copies files from a private tree into this one - and hand-held boundaries
fail quietly. One stray file, once, and it is in the git history for good.
Deleting it later does not remove it; it only makes it slightly harder to
find.

This checks the boundary mechanically, every time, in about a second.

It also checks for things that are not licence problems but are still
mistakes to publish: a personal email address, a local filesystem path with
a username in it, anything password-shaped.

--------------------------------------------------------------------------
What a failure means
--------------------------------------------------------------------------

BLOCKING  do not push. Remove the file or the line, then run it again.
REVIEW    look at it and decide. Prose that NAMES an engine class is fine -
          describing code is not distributing it. A file containing that
          class is not fine.
"""

import io
import os
import re
import sys

# The tree this script lives in, so it needs no configuring and cannot be
# pointed at the wrong directory by accident.
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

SKIP_DIRS = {'.git', 'bin', 'obj', '.vs', '__pycache__', 'node_modules'}

# Directories that are Monolith's source in the private tree. None of these
# may exist here under any circumstances.
FORBIDDEN_DIRS = ['ObjectDLL', 'ClientShellDLL', 'Shared', 'ShogoServ',
                  'AppHeaders', 'ClientRes', 'ServerRes', 'Misc', 'Compat',
                  'Dist']

# Build artefacts of the game code. A .h could legitimately be ours one day,
# so it is flagged for review rather than blocked.
FORBIDDEN_EXT = {'.cpp', '.c', '.lto', '.dsp', '.dsw', '.rc', '.rez', '.lta'}
REVIEW_EXT = {'.h', '.hpp'}

# Identifiers that only ever appear in engine or game source. Finding one in
# code means source leaked; finding one in prose usually just means the docs
# are explaining something.
ENGINE_TOKENS = [
    'cpp_client_de.h', 'cpp_server_de.h', 'client_de.h', 'server_de.h',
    'CRiotClientShell', 'CRiotServerShell', 'CBaseCharacter', 'CPlayerObj',
    'CWeaponFX', 'CDestructable', 'ObjectCreateStruct', 'HMESSAGEWRITE',
    'HMESSAGEREAD', 'LPBASECLASS', 'HOBJECT', 'DVector', 'DRotation',
    'MID_PLAYER_', 'MID_FRESH_', 'GUN_PULSERIFLE_ID', 'BEGIN_CLASS',
]

# How the closed engine came to be understood is a need-to-know matter, and
# the public tree is not who needs to know. Claims here should stand on
# Monolith's released source, the SDK headers, or plainly observable
# behaviour - never on "we took the binary apart", which is a different
# posture and an invitation to argue about something other than the work.
#
# This is a REVIEW list, not a blocking one. Several of these have innocent
# uses: a "byte scan" of a .rez archive is our own file-format work, and
# naming Client.exe is unavoidable when saying which files are ours and
# which are not. The check exists to put a human's eye on each one rather
# than to decide for them.
METHOD_WORDS = [
    (r'(?i)reverse[ -]engineer',        'how the engine was studied'),
    (r'(?i)\bdecompil',                 'how the engine was studied'),
    (r'(?i)\bdisassembl',               'how the engine was studied'),
    (r'(?i)\bghidra\b',                 'the tool used'),
    (r'(?i)\bsymbol dump',              'the method'),
    (r'(?i)\binstruction stream\b',     'the method'),
    (r'(?i)\b(?:d3d|soft)\.ren\b',      'naming a closed binary'),
    (r'(?i)\bLaunch\.dll\b',            'naming a closed binary'),
    (r'(?i)\bserver\.dll\b',            'naming a closed binary'),
    (r'0x00[0-9A-Fa-f]{6}',             'an engine address'),
    (r'\+0x[0-9A-Fa-f]{4,}',            'an engine offset'),
    (r'(?i)\bzero-filled\b',            'binary-level detail'),
    (r'(?i)\bdead store\b',             'binary-level detail'),
    (r'(?i)\bbacking (?:int|float)\b',  'binary-level detail'),
]

# Never publish these, licence or no licence.
SECRETS = [
    (r'[\w.+-]+@gmail\.com',                'a personal email address'),
    (r'[Cc]:\\+Users\\+[A-Za-z0-9_.-]+',    'a local path with a username in it'),
    (r'(?i)\b(api[_-]?key|secret|token)\s*[:=]\s*["\'][^"\']{8,}',
                                            'something credential-shaped'),
    (r'ghp_[A-Za-z0-9]{20,}',               'a GitHub token'),

]

# Named identity strings load from a file that is deliberately NOT in the
# repository, because the first version put them right here - which published
# the exact strings this check exists to keep out. The needles cannot live in
# the haystack detector.
#
# Tools/leakcheck.private.re sits beside this script on the maintainer's disk
# (gitignored), one `regex<TAB>label` per line. Absent file = the named
# patterns simply do not run, which is correct for an outside contributor:
# the generic patterns above still hold, and the maintainer's sync runs with
# the file present. Also remember the one thing no file scan can see: commit
# AUTHORSHIP. Check `git log --format='%ae'` by hand before a first push.
import os as _os
_private = _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), 'leakcheck.private.re')
if _os.path.exists(_private):
    for _line in open(_private, encoding='utf-8'):
        _line = _line.rstrip('\n')
        if not _line or _line.startswith('#') or '\t' not in _line:
            continue
        _pat, _label = _line.split('\t', 1)
        SECRETS.append((_pat, _label))

# Files where a match is expected and fine. Deliberately named individually
# rather than exempting Tools/ wholesale - a directory-wide exemption is
# exactly where a leaked file would end up hiding.
#
# Both of these are checkers. They name symbols on both sides of the boundary
# BECAUSE that is what they check for.
SECRET_ALLOW = {'Tools/leakcheck.py',       # this file lists the patterns
                # The maintainer-local needle file. Allowed to EXIST on disk
                # (the walk sees it) because it is gitignored and can never
                # publish - it is the one place the named strings must live.
                'Tools/leakcheck.private.re'}
METHOD_ALLOW = {'Tools/leakcheck.py'}       # and this list too
TOKEN_ALLOW  = {'Tools/leakcheck.py',       # the token list itself
                'Tools/preflight.py'}       # checks protocol field ordering

blocking = []
review = []


def walk():
    for root, dirs, files in os.walk(ROOT):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for f in files:
            full = os.path.join(root, f)
            yield full, os.path.relpath(full, ROOT).replace('\\', '/')


def readable(path):
    if os.path.splitext(path)[1].lower() in {'.pdf', '.png', '.jpg', '.ico',
                                             '.dll', '.exe', '.zip', '.rez'}:
        return None
    try:
        return io.open(path, encoding='utf-8', errors='replace').read()
    except Exception:
        return None


def main():
    # 1. no forbidden directory exists
    for d in FORBIDDEN_DIRS:
        if os.path.isdir(os.path.join(ROOT, d)):
            blocking.append("directory `%s/` is Monolith source and must not be here" % d)

    # 2. no game-code artefacts
    for full, rel in walk():
        ext = os.path.splitext(rel)[1].lower()
        if ext in FORBIDDEN_EXT:
            blocking.append("%s  (game-code artefact)" % rel)
        elif ext in REVIEW_EXT:
            review.append("%s  (a header - ours, or leaked?)" % rel)

    # 3. no engine identifiers in anything that is not prose
    for full, rel in walk():
        s = readable(full)
        if s is None:
            continue
        if rel in TOKEN_ALLOW:
            continue
        is_prose = rel.endswith('.md') or rel.endswith('.txt')
        for t in ENGINE_TOKENS:
            if t in s:
                msg = "%s  mentions `%s`" % (rel, t)
                (review if is_prose else blocking).append(msg)
                break

    # 4. nothing describing HOW the closed engine was studied
    for full, rel in walk():
        if rel in METHOD_ALLOW:
            continue
        body = readable(full)
        if body is None:
            continue
        for pat, what in METHOD_WORDS:
            m = re.search(pat, body)
            if m:
                review.append("%s  reads as %s: %s"
                              % (rel, what, m.group(0)[:48]))
                break

    # 5. nothing private
    for full, rel in walk():
        if rel in SECRET_ALLOW:
            continue
        s = readable(full)
        if s is None:
            continue
        for pat, what in SECRETS:
            m = re.search(pat, s)
            if m:
                blocking.append("%s  contains %s: %s" % (rel, what, m.group(0)[:48]))

    # ------------------------------------------------------------------
    print("ShogoFRESH leak check")
    print("  %s" % ROOT)
    print("=" * 70)

    if blocking:
        print("\nBLOCKING (%d) - do not push" % len(blocking))
        for x in sorted(set(blocking)):
            print("   " + x)
    else:
        print("\nBLOCKING (0)")

    if review:
        print("\nREVIEW (%d) - look, then decide" % len(review))
        for x in sorted(set(review)):
            print("   " + x)
        print("\n   Prose naming an engine class is fine - describing code is")
        print("   not distributing it. A FILE containing that class is not.")

    if not blocking:
        print("\nClean. Safe to push.")

    return len(blocking)


if __name__ == '__main__':
    sys.exit(main())
