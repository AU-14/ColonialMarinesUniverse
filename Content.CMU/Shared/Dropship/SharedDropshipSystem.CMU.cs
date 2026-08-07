using System.Diagnostics.CodeAnalysis;
using Content.Shared.AU14;
using Content.Shared.AU14.Round;

namespace Content.Shared._RMC14.Dropship;

public abstract partial class SharedDropshipSystem
{
    public bool TryGetGridFaction(EntityUid ent, [NotNullWhen(true)] out string? faction)
    {
        faction = null;
        if (!TryComp(ent, out TransformComponent? xform) || xform.GridUid is not { } grid)
            return false;

        if (TryComp<ShipFactionComponent>(grid, out var shipFaction) &&
            !string.IsNullOrWhiteSpace(shipFaction.Faction))
        {
            faction = shipFaction.Faction;
            return true;
        }

        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!TryComp<WhitelistedShuttleComponent>(child, out var shuttle) ||
                string.IsNullOrWhiteSpace(shuttle.Faction))
            {
                continue;
            }

            faction = shuttle.Faction;
            return true;
        }

        return false;
    }
}
