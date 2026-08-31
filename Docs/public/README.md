# ShogoFRESH documentation

Everything here is written for someone outside the project: a player, a
server operator, or anyone curious about what the work actually changes.

Building FOR Shogo is documented separately: **ShogoMAKE** (github.com/KyodanCFG/ShogoMAKE)
carries MODDING, TEXTURE-MODDING, DTXFORMAT, REZFORMAT and LOCALIZATION,
beside the tools and examples they refer to. Those pages are maintained in
this tree and published with the kit — one copy, two front doors.

| Document | For |
|---|---|
| [BIBLE.md](BIBLE.md) | The reference: everything ShogoFRESH changes, adds or fixes, component by component, with the reasoning |
| [SERVER-GUIDE.md](SERVER-GUIDE.md) | Running a server. Everything an operator meets, in the order they meet it |

## What is not here

The development tree also carries working documents: the bug list, the
validation matrix, session handoffs, the decision log, design notes for things
not yet built, and the findings from reverse-engineering the closed engine.
None of it is published, and none of it is needed to use anything above.

**That boundary is mechanical, not remembered.** These documents live in a
directory that is published; the rest do not. `Tools/preflight.py` checks that
nothing here links to something you cannot open, which is the failure that
would actually cost you time.

## The two format documents are the authority

`DTXFORMAT.md` and `REZFORMAT.md` are not descriptions of the code — the code
is an implementation of them. Each has more than one implementation (a writer,
a validator, a reader in a different language) and preflight asserts they all
still agree with the document. If you find a case either document does not
cover, that is worth reporting: it means an implementation is guessing.

## Two things about the game itself

**`Client.exe` is closed and is never modified.** Monolith released the game
code in March 1999 but not the engine. Everything ShogoFRESH does happens in
the game DLLs and the launcher around them, which is why some things you might
expect to be fixable are not — the documents above say so where it matters
rather than leaving you to find out.

**Mods are additive.** They are `.rez` archives dropped in `Custom/`, and
renaming one to `.off` disables it. Nothing in the modding guides modifies a
game executable or an installed file.
