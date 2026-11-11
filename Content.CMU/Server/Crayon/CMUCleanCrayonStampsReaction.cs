using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Crayon;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Crayon;

[DataDefinition]
public sealed partial class CMUCleanCrayonStampsReaction : ITileReaction
{
    [DataField]
    public FixedPoint2 CleanCost { get; private set; } = FixedPoint2.New(0.25f);

    public FixedPoint2 TileReact(TileRef tile, ReagentPrototype reagent, FixedPoint2 reactVolume, IEntityManager entityManager, List<ReagentData>? data)
    {
        var lookup = entityManager.System<EntityLookupSystem>();
        var amount = FixedPoint2.Zero;
        foreach (var uid in lookup.GetLocalEntitiesIntersecting(tile, 0.5f))
        {
            if (amount + CleanCost > reactVolume)
                break;

            if (!entityManager.HasComponent<CMUCrayonStampComponent>(uid))
                continue;

            entityManager.QueueDeleteEntity(uid);
            amount += CleanCost;
        }

        return amount;
    }
}
