using Content.Shared._RMC14.Atmos;
using Content.Shared.CMU14.Atmos;
using Content.Shared.CMU14.Chemistry.Fuel;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// FLOW-22 spread rule of a fire spec. A fire lit from a Flowing solution creeps (<see cref="CMUSpreadingFireSystem"/>)
/// to <see cref="CMUFireSpec.SpreadDepth"/> generations even where the map's <c>cmu.fire_spread_depth</c> is 0; a
/// fire from a solution whose Viscous level matches or beats its Flowing level never creeps, even from a fire
/// prototype or map that would otherwise spread. Other fires follow the map default.
/// </summary>
public sealed partial class CMUFireSpecSpreadSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TileFireComponent, CMUFireSpecAppliedEvent>(OnFireSpecApplied);
    }

    private void OnFireSpecApplied(Entity<TileFireComponent> ent, ref CMUFireSpecAppliedEvent args)
    {
        var depth = args.Spec.SpreadDepth;
        if (depth < 0)
        {
            RemComp<CMUAllowFireSpreadComponent>(ent);
            RemComp<CMUSpreadingFireComponent>(ent);
            return;
        }

        if (depth == 0)
            return;

        EnsureComp<CMUAllowFireSpreadComponent>(ent);

        // A spread child gets its remaining depth from the parent right after this.
        if (args.SpreadChild)
            return;

        var spread = EnsureComp<CMUSpreadingFireComponent>(ent);
        spread.Depth = depth;
        spread.NextSpread = _timing.CurTime + spread.SpreadEvery;
    }
}
