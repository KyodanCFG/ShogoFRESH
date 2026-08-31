using System;
using System.Collections.Generic;
using System.Linq;

namespace ShogoLauncher.Services;

/// <summary>
/// The pickups a host can ban from spawning, and the parsing for the two
/// server vars that carry them: "BlockWeapons" (a list of weapon ids, e.g.
/// "5 8") and "BlockItems" (a list of pickup class names, e.g.
/// "FirstAid_50 ArmorRepair_500").
///
/// This is a mirror of exactly one file: Shared\PickupCatalog.h, the game
/// side's single catalog. When that table changes, change this one; nothing
/// else needs to stay in sync.
///
/// Melee weapons and the two unused weapon slots aren't listed: the game
/// code never removes or substitutes them, so banning one would do nothing.
/// Unlock keys aren't either - blocking one would strand a level.
///
/// Ultra powerups and upgrades CAN be blocked but are never substituted in
/// anywhere, so blocking one simply removes it.
/// </summary>
public static class BlockablePickups
{
    /// <summary>Weapon entries carry an Id; item entries carry a ClassName.</summary>
    public sealed record Entry(string Name, bool Mech, bool IsWeapon, string Note, int Id = -1, string ClassName = "");

    public static readonly IReadOnlyList<Entry> All = new List<Entry>
    {
        // --- Mech (MCA) weapons ---
        new("Pulse Rifle",     true,  true,  "Starting mech weapon - hitscan, low damage.",  Id: 0),
        new("Laser Cannon",    true,  true,  "Continuous beam, heavy ammo drain.",           Id: 1),
        new("Spider",          true,  true,  "Homing swarm missiles.",                       Id: 2),
        new("Bullgut",         true,  true,  "Short range shotgun blast.",                   Id: 3),
        // "DMR" under the FRESH ruleset, still the Sniper Rifle under Classic.
        // Named for FRESH because that is what a server runs unless it opts out.
        new("DMR",             true,  true,  "Semi-auto marksman rifle; accurate scoped, spread at the hip.", Id: 4),
        new("Juggernaut",      true,  true,  "Rocket launcher - the big splash weapon.",     Id: 5),
        new("Shredder",        true,  true,  "Rapid fire flak.",                             Id: 6),
        new("Red Riot",        true,  true,  "Charged energy cannon.",                       Id: 8),

        // --- Mech (MCA) items ---
        new("Power Surge 50",   true, false, "Repairs 50 mech health.",     ClassName: "PowerSurge_50"),
        new("Power Surge 100",  true, false, "Repairs 100 mech health.",    ClassName: "PowerSurge_100"),
        new("Power Surge 150",  true, false, "Repairs 150 mech health.",    ClassName: "PowerSurge_150"),
        new("Power Surge 250",  true, false, "Repairs 250 mech health.",    ClassName: "PowerSurge_250"),
        new("Armor Repair 100", true, false, "Restores 100 mech armor.",    ClassName: "ArmorRepair_100"),
        new("Armor Repair 250", true, false, "Restores 250 mech armor.",    ClassName: "ArmorRepair_250"),
        new("Armor Repair 500", true, false, "Restores 500 mech armor.",    ClassName: "ArmorRepair_500"),

        // --- On-foot weapons ---
        new("Colt .45",        false, true,  "Starting sidearm.",                            Id: 13),
        new("Shotgun",         false, true,  "Close range spread.",                          Id: 14),
        new("Assault Rifle",   false, true,  "Automatic mid-range rifle.",                   Id: 15),
        new("Energy Grenade",  false, true,  "Thrown energy burst.",                         Id: 16),
        new("Kato Grenade",    false, true,  "Thrown fragmentation grenade.",                Id: 17),
        new("MAC-10",          false, true,  "Fast, inaccurate SMG.",                        Id: 18),
        new("TOW Launcher",    false, true,  "On-foot guided missile.",                      Id: 19),
        new("Squeaky Toy",     false, true,  "Joke weapon - harmless.",                      Id: 21),

        // --- On-foot items ---
        new("First Aid 10",    false, false, "Heals 10 health on foot.",    ClassName: "FirstAid_10"),
        new("First Aid 15",    false, false, "Heals 15 health on foot.",    ClassName: "FirstAid_15"),
        new("First Aid 25",    false, false, "Heals 25 health on foot.",    ClassName: "FirstAid_25"),
        new("First Aid 50",    false, false, "Heals 50 health on foot.",    ClassName: "FirstAid_50"),
        new("Body Armor 50",   false, false, "Restores 50 armor on foot.",  ClassName: "BodyArmor_50"),
        new("Body Armor 100",  false, false, "Restores 100 armor on foot.", ClassName: "BodyArmor_100"),
        new("Body Armor 200",  false, false, "Restores 200 armor on foot.", ClassName: "BodyArmor_200"),

        // --- Ultra powerups and upgrades ---
        //
        // These can be banned but are never substituted IN anywhere: they
        // are map-authored pacing, and dropping one where a medkit used to
        // be would change a level's shape. Blocking one removes it.
        //
        // They are listed as on-foot only because the tier split above is
        // about amounts, and these have none - they are the same pickup
        // whichever mode you are in.

        new("Ultra Damage",      false, false, "Temporary damage boost.",        ClassName: "UltraDamage"),
        new("Ultra Health",      false, false, "Large instant heal.",            ClassName: "UltraHealth"),
        new("Ultra Power Surge", false, false, "Large instant mech repair.",     ClassName: "UltraPowerSurge"),
        new("Ultra Shield",      false, false, "Temporary damage resistance.",   ClassName: "UltraShield"),
        new("Ultra Stealth",     false, false, "Temporary invisibility.",        ClassName: "UltraStealth"),
        new("Ultra Reflect",     false, false, "Reflects damage back.",          ClassName: "UltraReflect"),
        new("Ultra Night Vision",false, false, "Temporary night vision.",        ClassName: "UltraNightVision"),
        new("Ultra Infrared",    false, false, "Temporary infrared vision.",     ClassName: "UltraInfrared"),
        new("Ultra Silencer",    false, false, "Temporarily silences weapons.",  ClassName: "UltraSilencer"),
        new("Ultra Restore",     false, false, "Restores health and armor.",     ClassName: "UltraRestore"),

        new("Damage Upgrade",    false, false, "Permanent damage increase.",     ClassName: "DamageUpgrade"),
        new("Protection Upgrade",false, false, "Permanent damage reduction.",    ClassName: "ProtectionUpgrade"),
        new("Regen Upgrade",     false, false, "Permanent health regeneration.", ClassName: "RegenUpgrade"),
        new("Health Upgrade",    false, false, "Permanent maximum health.",      ClassName: "HealthUpgrade"),
        new("Armor Upgrade",     false, false, "Permanent maximum armor.",       ClassName: "ArmorUpgrade"),
        new("Targeting Upgrade", false, false, "Permanent targeting aid.",       ClassName: "TargetingUpgrade"),
    };

    /// <summary>Ultras and upgrades: blockable, but never rolled into a spot.</summary>
    public static bool IsSpecial(Entry e) =>
        !e.IsWeapon &&
        (e.ClassName.StartsWith("Ultra", StringComparison.Ordinal) ||
         e.ClassName.EndsWith("Upgrade", StringComparison.Ordinal));

    public static IEnumerable<Entry> Weapons => All.Where(e => e.IsWeapon);
    public static IEnumerable<Entry> Items => All.Where(e => !e.IsWeapon);

    // ----- "BlockWeapons": numeric ids -----

    /// <summary>Ids in a "BlockWeapons" string, deduplicated, unknown ids dropped.</summary>
    public static List<int> ParseWeaponIds(string? list)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(list)) return result;

        foreach (var token in list.Split(new[] { ' ', ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(token, out var id)) continue;
            if (result.Contains(id)) continue;
            if (!Weapons.Any(w => w.Id == id)) continue;

            result.Add(id);
        }

        return result;
    }

    public static string FormatWeapons(IEnumerable<int> ids)
    {
        var set = ids.ToHashSet();
        return string.Join(" ", Weapons.Where(w => set.Contains(w.Id)).Select(w => w.Id));
    }

    // ----- "BlockItems": pickup class names -----

    /// <summary>Class names in a "BlockItems" string, deduplicated, unknown names dropped.</summary>
    public static List<string> ParseItemClasses(string? list)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(list)) return result;

        foreach (var token in list.Split(new[] { ' ', ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Items.FirstOrDefault(e => string.Equals(e.ClassName, token, StringComparison.OrdinalIgnoreCase));
            if (match is null) continue;
            if (result.Contains(match.ClassName)) continue;

            result.Add(match.ClassName);
        }

        return result;
    }

    public static string FormatItems(IEnumerable<string> classNames)
    {
        var set = classNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return string.Join(" ", Items.Where(e => set.Contains(e.ClassName)).Select(e => e.ClassName));
    }

    // ----- Shared -----

    /// <summary>Round-trip stored lists so they always come back in catalog order.</summary>
    public static string NormalizeWeapons(string? list) => FormatWeapons(ParseWeaponIds(list));
    public static string NormalizeItems(string? list) => FormatItems(ParseItemClasses(list));

    /// <summary>How many pickups the two lists block between them.</summary>
    public static int CountBlocked(string? weapons, string? items) =>
        ParseWeaponIds(weapons).Count + ParseItemClasses(items).Count;
}
