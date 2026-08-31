namespace ShogoLauncher.Services;

/// <summary>
/// Petname/haikunator-style random player names, Shogo-flavored
/// (Adjective + Mech/Unit + Role), e.g. "CrimsonOrdogAce".
/// Used to replace the stock "Sanjuro" default so servers aren't full of
/// identical Sanjuros.
///
/// Names are kept to MaxNameLength so they fit the scoreboard and the kill
/// feed without wrapping. The generator picks a combination that already
/// fits rather than truncating one that doesn't - a chopped name reads like
/// a bug, and "CrimsonSparrowhawkJ" is worse than a shorter real name.
/// </summary>
public static class PilotNameGenerator
{
    private static readonly string[] Adjectives =
    {
        "Crimson", "Neon", "Silent", "Rusty", "Plasma", "Rogue", "Lucky",
        "Iron", "Shadow", "Blazing", "Chrome", "Stray", "Wired", "Static",
        "Feral", "Nova", "Grim", "Zero", "Turbo", "Vagrant", "Midnight",
        "Reckless", "Howling", "Jade", "Scarlet", "Phantom",
    };

    private static readonly string[] Units =
    {
        "Ordog", "Akuma", "Enforcer", "Predator", "Vandal", "Rascal",
        "Sparrowhawk", "Uhlan", "Andra", "Ruin", "Vigilance", "Hammerhead",
        "Dropship", "Raksha", "Tenma", "Bullgut", "Shredder", "Juggernaut",
    };

    private static readonly string[] Roles =
    {
        "Pilot", "Ace", "Ronin", "Runner", "Kid", "Vet", "Hunter", "Jockey",
    };

    /// <summary>Longest a player name may be, here and in the launcher field.</summary>
    public const int MaxNameLength = 20;

    public static string Generate()
    {
        var r = Random.Shared;

        // Try whole combinations until one fits. The pools are small enough
        // that this lands almost immediately, and the fallback below cannot
        // fail: the shortest word from each pool is comfortably inside 20.
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var name = Adjectives[r.Next(Adjectives.Length)]
                     + Units[r.Next(Units.Length)]
                     + Roles[r.Next(Roles.Length)];

            if (name.Length <= MaxNameLength) return name;
        }

        return ShortestOf(Adjectives) + ShortestOf(Units) + ShortestOf(Roles);
    }

    private static string ShortestOf(string[] pool)
    {
        var best = pool[0];
        foreach (var s in pool) if (s.Length < best.Length) best = s;
        return best;
    }
}
