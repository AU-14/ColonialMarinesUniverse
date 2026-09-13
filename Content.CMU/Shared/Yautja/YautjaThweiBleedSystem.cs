using Content.Shared._RMC14.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// Thwei adds clotting only while it remains in the bloodstream.
/// </summary>
public sealed partial class YautjaThweiBleedSystem : EntitySystem
{
    [Dependency] private SharedRMCBloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BloodstreamComponent, BleedModifierEvent>(OnBleed);
    }

    private void OnBleed(Entity<BloodstreamComponent> ent, ref BleedModifierEvent args)
    {
        if (_bloodstream.TryGetChemicalSolution(ent.Owner, out _, out var solution) &&
            solution.GetTotalPrototypeQuantity("thwei") > FixedPoint2.Zero)
        {
            args.BleedReductionAmount += 0.75f;
        }
    }
}
