using System.Globalization;
using System.Linq;
using Content.Shared._RMC14.Dropship.Fabricator;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Dropship.Fabricator;

public enum CMUDropshipFabricatorFilterKind
{
    All,
    Weapon,
    Support,
}

/// <summary>
///     A filter selection in the dropship fabricator sidebar. <see cref="Weapon"/> is only set for
///     <see cref="CMUDropshipFabricatorFilterKind.Weapon"/>.
/// </summary>
public readonly record struct CMUDropshipFabricatorFilter(CMUDropshipFabricatorFilterKind Kind, string? Weapon = null)
{
    public static readonly CMUDropshipFabricatorFilter All = new(CMUDropshipFabricatorFilterKind.All);
    public static readonly CMUDropshipFabricatorFilter Support = new(CMUDropshipFabricatorFilterKind.Support);

    public static CMUDropshipFabricatorFilter ForWeapon(string weapon)
    {
        return new CMUDropshipFabricatorFilter(CMUDropshipFabricatorFilterKind.Weapon, weapon);
    }
}

/// <summary>
///     One printable in the dropship fabricator catalog.
/// </summary>
/// <param name="Weapon">
///     Display name of the dropship weapon this entry is, or loads into. Null for support gear that
///     belongs to no weapon. Grouping by name merges re-parented copies of the same weapon.
/// </param>
public sealed record CMUDropshipFabricatorEntry(
    EntProtoId<DropshipFabricatorPrintableComponent> Id,
    string Name,
    int Cost,
    DropshipFabricatorPrintableComponent.CategoryType Category,
    string? Weapon);

public static class CMUDropshipFabricatorCatalog
{
    private static readonly StringComparer NameComparer = StringComparer.CurrentCultureIgnoreCase;

    public static List<string> GetWeapons(IEnumerable<CMUDropshipFabricatorEntry> entries)
    {
        return entries
            .Select(e => e.Weapon)
            .OfType<string>()
            .Distinct(NameComparer)
            .OrderBy(w => w, NameComparer)
            .ToList();
    }

    public static bool IsVisible(CMUDropshipFabricatorEntry entry, CMUDropshipFabricatorFilter filter, string? search)
    {
        return MatchesFilter(entry, filter) && MatchesSearch(entry, search);
    }

    public static bool MatchesFilter(CMUDropshipFabricatorEntry entry, CMUDropshipFabricatorFilter filter)
    {
        return filter.Kind switch
        {
            CMUDropshipFabricatorFilterKind.Weapon => entry.Weapon != null && NameComparer.Equals(entry.Weapon, filter.Weapon),
            CMUDropshipFabricatorFilterKind.Support => entry.Weapon == null,
            _ => true,
        };
    }

    /// <summary>
    ///     Matches the item's own name or the name of the weapon it belongs to, so searching a weapon
    ///     also lists all of its ammo.
    /// </summary>
    public static bool MatchesSearch(CMUDropshipFabricatorEntry entry, string? search)
    {
        search = search?.Trim();
        if (string.IsNullOrEmpty(search))
            return true;

        return Contains(entry.Name, search)
               || entry.Weapon != null && Contains(entry.Weapon, search);
    }

    public static int Count(IEnumerable<CMUDropshipFabricatorEntry> entries, CMUDropshipFabricatorFilter filter, string? search)
    {
        return entries.Count(e => IsVisible(e, filter, search));
    }

    private static bool Contains(string source, string value)
    {
        return CultureInfo.CurrentCulture.CompareInfo.IndexOf(source, value, CompareOptions.IgnoreCase) >= 0;
    }
}
