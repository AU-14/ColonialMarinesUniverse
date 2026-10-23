using System.Linq;
using Content.Shared.CMU14.Ghost.Roles;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Survivor;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Ghost.Roles;

/// <summary>
///     Works out which rail entry a ghost role belongs under, and where the entity offering it
///     actually is, for the two fields the ghost roles window needs and the role itself does not
///     carry.
/// </summary>
public sealed class CMUGhostRoleCategorySystem : EntitySystem
{
    [Dependency] private readonly AreaSystem _areas = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private const string Other = "Other";
    private const string Xenomorph = "Xenomorph";
    private const string Survivor = "Survivor";

    private static readonly Dictionary<string, string> FactionCategories = new()
    {
        ["govfor"] = "GovernmentForces",
        ["opfor"] = "OpposingForces",
        ["clf"] = "OpposingForces",
        ["weyu"] = "Corporate",
        ["corporate"] = "Corporate",
        ["colonist"] = Survivor,
        ["colony"] = Survivor,
    };

    public string GetCategory(EntityUid uid, string? declared, string? jobProto)
    {
        if (!string.IsNullOrEmpty(declared) && _prototypes.HasIndex<GhostRoleCategoryPrototype>(declared))
            return declared;

        if (HasComp<XenoComponent>(uid))
            return Xenomorph;

        if (HasComp<RMCSurvivorComponent>(uid))
            return Survivor;

        if (JobFaction(jobProto) is { } faction &&
            FactionCategories.TryGetValue(faction.ToLowerInvariant(), out var category))
        {
            return category;
        }

        return Other;
    }

    /// <summary>
    ///     Where <paramref name="uid"/> is, in the words the rest of the game uses for it - the area
    ///     name if it is standing in one, the grid's own name if it is somewhere without areas (a
    ///     dropship in transit, a shuttle), and nothing at all if it is off-grid.
    /// </summary>
    public string? GetLocation(EntityUid uid)
    {
        if (_areas.TryGetArea(uid, out _, out var areaProto))
            return areaProto.Name;

        if (Transform(uid).GridUid is { } grid && !TerminatingOrDeleted(grid))
            return Name(grid);

        return null;
    }

    /// <summary>
    ///     The faction of the first department listing <paramref name="jobProto"/>. Jobs do not name
    ///     their own faction, and a job in two departments of different factions is not a thing this
    ///     content has - so first is enough.
    /// </summary>
    private string? JobFaction(string? jobProto)
    {
        if (string.IsNullOrEmpty(jobProto))
            return null;

        foreach (var department in _prototypes.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (!string.IsNullOrEmpty(department.Faction) && department.Roles.Any(role => role.Id == jobProto))
                return department.Faction;
        }

        return null;
    }
}
