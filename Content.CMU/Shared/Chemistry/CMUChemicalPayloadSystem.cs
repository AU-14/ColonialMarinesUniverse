using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Payload.Components;

namespace Content.Shared.CMU14.Chemistry;

/// <summary>
/// Upstream payload slots take anything that fits a dispenser, so jugs, buckets and canisters went in.
/// Cap it at a large beaker like CM grenades do.
/// </summary>
public sealed class CMUChemicalPayloadSystem : EntitySystem
{
    public static readonly FixedPoint2 MaxContainerVolume = 120;

    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChemicalPayloadComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    private void OnInsertAttempt(Entity<ChemicalPayloadComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var total = FixedPoint2.Zero;
        foreach (var (_, solution) in _solution.EnumerateSolutions(args.Item))
        {
            total += solution.Comp.Solution.MaxVolume;
        }

        if (total > MaxContainerVolume)
            args.Cancelled = true;
    }
}
