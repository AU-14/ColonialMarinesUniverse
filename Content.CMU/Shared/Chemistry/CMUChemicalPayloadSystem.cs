using Content.Shared.Containers.ItemSlots;
using Content.Shared.Payload.Components;

namespace Content.Shared.CMU14.Chemistry;

/// <summary>
/// Upstream payload slots take anything that fits a dispenser, so jugs, buckets, minitanks and canisters went in.
/// Only vials, beakers, large beakers and high-capacity beakers are allowed: they carry
/// <see cref="CMUChemicalPayloadContainerComponent"/>.
/// </summary>
public sealed class CMUChemicalPayloadSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChemicalPayloadComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    private void OnInsertAttempt(Entity<ChemicalPayloadComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<CMUChemicalPayloadContainerComponent>(args.Item))
            args.Cancelled = true;
    }
}
