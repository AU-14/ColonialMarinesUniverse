using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Storage.Components;

namespace Content.Shared.CMU14.Botany;

/// <summary>
/// CMU14: the parts of the batch seed extractor that both sides must agree on. The window's slot buttons are predicted, so the
/// seedless refusal lives here and runs on the client too; otherwise seedless produce would flicker into a slot.
/// Only this system subscribes (CMUBatchSeedExtractorComponent, ItemSlotInsertAttemptEvent), and the dump events: dumping a
/// plant bag is predicted too, so its slot inserts must be.
/// </summary>
public sealed class CMUBatchSeedExtractorSharedSystem : EntitySystem
{
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUBatchSeedExtractorComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<CMUBatchSeedExtractorComponent, GetDumpableVerbEvent>(OnGetDumpableVerb);
        SubscribeLocalEvent<CMUBatchSeedExtractorComponent, DumpEvent>(OnDump);
    }

    public static bool IsInputSlot(CMUBatchSeedExtractorComponent comp, string? slotId)
    {
        if (slotId == null)
            return false;

        for (var i = 0; i < comp.InputSlotCount; i++)
        {
            if (comp.InputSlotId(i) == slotId)
                return true;
        }

        return false;
    }

    /// <summary>The first empty input slot, or null when every input slot is full.</summary>
    public string? FreeInputSlot(Entity<CMUBatchSeedExtractorComponent> ent)
    {
        for (var i = 0; i < ent.Comp.InputSlotCount; i++)
        {
            var id = ent.Comp.InputSlotId(i);
            if (_itemSlots.TryGetSlot(ent.Owner, id, out var slot) && !slot.HasItem)
                return id;
        }

        return null;
    }

    // CMU14 (FLOW-03): a plant bag (anything dumpable) can be dumped into the extractor while it has a free input slot.
    private void OnGetDumpableVerb(Entity<CMUBatchSeedExtractorComponent> ent, ref GetDumpableVerbEvent args)
    {
        if (FreeInputSlot(ent) != null)
            args.Verb = Loc.GetString("cmu-batch-seed-extractor-dump", ("machine", ent.Owner));
    }

    // Each extractable produce goes into the next free input slot; the insert gate below turns away seedless produce,
    // seed packets and anything else, and whatever is not taken stays in the bag.
    private void OnDump(Entity<CMUBatchSeedExtractorComponent> ent, ref DumpEvent args)
    {
        if (args.Handled)
            return;

        var inserted = false;
        while (args.DumpQueue.TryDequeue(out var item))
        {
            if (FreeInputSlot(ent) is not { } slot)
                break;

            // The dump's own sound plays once; the slot's insert sound would play once per produce, twice for the dumper.
            if (_itemSlots.TryInsert(ent.Owner, slot, item, args.User, excludeUserAudio: true))
                inserted = true;
        }

        args.Handled = true;
        args.PlaySound |= inserted;
    }

    // Seedless produce (or produce of no plant) never enters an input slot, whether by hand, window or inserter.
    // On the client a mutated snapshot may not be present, so the check falls back to the plant prototype there.
    private void OnInsertAttempt(Entity<CMUBatchSeedExtractorComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || !IsInputSlot(ent.Comp, args.Slot.ID))
            return;

        if (!TryComp<ProduceComponent>(args.Item, out var produce) ||
            !CMUSeedYield.TryGetSeedSource(_botany, produce, out _, out _))
        {
            args.Cancelled = true;
        }
    }
}
