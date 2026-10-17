using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines;
using Content.Shared.NPC.Systems;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Uses round sides, not platoon identities, for aircraft authentication.</summary>
public sealed partial class FighterIFFSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _factions = default!;

    public static string? Normalize(string? faction) =>
        string.IsNullOrWhiteSpace(faction) || string.Equals(faction.Trim(), Team.None, StringComparison.OrdinalIgnoreCase)
            ? null : faction.Trim().ToLowerInvariant();

    public static bool Same(string? left, string? right) =>
        Normalize(left) is { } known && known == Normalize(right);

    public string? GetOperatorFaction(EntityUid user) =>
        TryComp(user, out MarineComponent? marine) ? Normalize(marine.Faction) : null;

    public string? GetSiteFaction(EntityUid entity)
    {
        while (TryComp(entity, out TransformComponent? transform))
        {
            if (TryComp(entity, out ShipFactionComponent? ship) && Normalize(ship.Faction) is { } faction)
                return faction;
            entity = transform.ParentUid;
        }
        return null;
    }

    public bool Hostile(string? left, string? right)
    {
        if (Normalize(left) is not { } source || Normalize(right) is not { } target || source == target)
            return false;

        // Resolve the round's live relations so alliance changes also cancel pending locks.
        string? sourceId = null;
        string? targetId = null;
        var factions = _factions.GetFactions();
        foreach (var id in factions.Keys)
        {
            if (string.Equals(id, source, StringComparison.OrdinalIgnoreCase)) sourceId = id;
            if (string.Equals(id, target, StringComparison.OrdinalIgnoreCase)) targetId = id;
        }
        if (sourceId == null || targetId == null)
            return false; // Unidentified aircraft never become automatic targets.
        return !factions[sourceId].Friendly.Contains(targetId) && !factions[targetId].Friendly.Contains(sourceId) &&
               (factions[sourceId].Hostile.Contains(targetId) || factions[targetId].Hostile.Contains(sourceId));
    }
}
