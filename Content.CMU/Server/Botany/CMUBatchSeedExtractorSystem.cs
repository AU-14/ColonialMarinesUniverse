using Content.Server.Power.EntitySystems;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.CMU14.Botany;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Botany;

/// <summary>
/// CMU14: runs the batch seed extractor. Copies the GrinderSystem tick: a batch starts when the machine has power, extractable
/// produce and room in its output slots, pauses (keeping its time left) when power is lost, and on completion turns each produce
/// into seed packets the same way the upstream seed extractor does, putting the packets into the output slots.
/// </summary>
public sealed class CMUBatchSeedExtractorSystem : EntitySystem
{
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerStateSystem _powerState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUBatchSeedExtractorComponent, InteractUsingEvent>(OnInteractUsing);
        // The seedless insert refusal is in CMUBatchSeedExtractorSharedSystem so the client predicts it too.
        SubscribeLocalEvent<CMUBatchSeedExtractorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUBatchSeedExtractorComponent, CMUBatchSeedExtractorStartMessage>(OnStartMessage);
        SubscribeLocalEvent<CMUBatchSeedExtractorComponent, CMUBatchSeedExtractorEjectAllMessage>(OnEjectAllMessage);
        SubscribeLocalEvent<CMUBatchSeedExtractorComponent, CMUBatchSeedExtractorToggleAutoMessage>(OnToggleAutoMessage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUBatchSeedExtractorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var extractor = new Entity<CMUBatchSeedExtractorComponent>(uid, comp);

            if (!this.IsPowered(uid, EntityManager))
            {
                if (comp.EndTime != null)
                    PauseBatch(extractor);

                _powerState.TrySetWorkingState(uid, false);
                continue;
            }

            if (comp.EndTime == null && (comp.AutoStart || comp.Remaining != null))
                TryStartBatch(extractor);

            if (comp.EndTime is { } endTime && now >= endTime)
                CompleteBatch(extractor);

            _powerState.TrySetWorkingState(uid, comp.EndTime != null);
        }
    }

    /// <summary>
    /// Starts a batch, or resumes a paused one, when there is extractable produce and room for at least one produce's packets.
    /// A paused batch whose produce was taken out is dropped.
    /// </summary>
    public bool TryStartBatch(Entity<CMUBatchSeedExtractorComponent> extractor)
    {
        var comp = extractor.Comp;
        if (comp.EndTime != null)
            return false;

        if (!HasExtractableInput(extractor) || CountFreeOutputSlots(extractor) < CMUSeedYield.MaxPacketCount(comp.BaseSeeds))
        {
            if (comp.Remaining != null || comp.Active)
            {
                comp.Remaining = null;
                comp.Duration = null;
                comp.Active = false;
                Dirty(extractor);
            }

            return false;
        }

        var full = comp.WorkTime * Math.Max(0f, comp.WorkTimeMultiplier);
        var duration = comp.Remaining ?? full;
        comp.Remaining = null;
        comp.Duration = full;
        comp.EndTime = _timing.CurTime + duration;
        comp.Active = true;
        Dirty(extractor);
        return true;
    }

    private void PauseBatch(Entity<CMUBatchSeedExtractorComponent> extractor)
    {
        var comp = extractor.Comp;
        if (comp.EndTime is not { } endTime)
            return;

        var left = endTime - _timing.CurTime;
        comp.Remaining = left > TimeSpan.Zero ? left : TimeSpan.Zero;
        comp.EndTime = null;
        Dirty(extractor);
    }

    private void CompleteBatch(Entity<CMUBatchSeedExtractorComponent> extractor)
    {
        var comp = extractor.Comp;
        comp.EndTime = null;
        comp.Remaining = null;
        comp.Duration = null;
        comp.Active = false;

        var coords = Transform(extractor).Coordinates;
        var maxPerProduce = CMUSeedYield.MaxPacketCount(comp.BaseSeeds);

        for (var i = 0; i < comp.InputSlotCount; i++)
        {
            if (_itemSlots.GetItemOrNull(extractor.Owner, comp.InputSlotId(i)) is not { } item ||
                TerminatingOrDeleted(item) ||
                EntityManager.IsQueuedForDeletion(item) ||
                !TryComp<ProduceComponent>(item, out var produce) ||
                !CMUSeedYield.TryGetSeedSource(_botany, produce, out var plantProtoId, out var plantData))
            {
                continue;
            }

            // Full outputs block the rest of the batch; the remaining produce waits for the next one.
            if (CountFreeOutputSlots(extractor) < maxPerProduce)
                break;

            var amount = CMUSeedYield.RollPacketCount(comp.BaseSeeds, _random);
            for (var n = 0; n < amount; n++)
            {
                // The machine is the "user": it has no hands, so the packet stays at its coordinates until slotted.
                var packet = _botany.SpawnSeedPacket(plantData, plantProtoId, produce.PlantData, coords, extractor.Owner);
                TryInsertOutput(extractor, packet);
            }

            // The produce owns its snapshot; SpawnSeedPacket cloned it, so the produce can go now.
            QueueDel(item);
        }

        Dirty(extractor);
    }

    private bool HasExtractableInput(Entity<CMUBatchSeedExtractorComponent> extractor)
    {
        var comp = extractor.Comp;
        for (var i = 0; i < comp.InputSlotCount; i++)
        {
            if (_itemSlots.GetItemOrNull(extractor.Owner, comp.InputSlotId(i)) is not { } item ||
                TerminatingOrDeleted(item) ||
                EntityManager.IsQueuedForDeletion(item) ||
                !TryComp<ProduceComponent>(item, out var produce))
            {
                continue;
            }

            if (CMUSeedYield.TryGetSeedSource(_botany, produce, out _, out _))
                return true;
        }

        return false;
    }

    private int CountFreeOutputSlots(Entity<CMUBatchSeedExtractorComponent> extractor)
    {
        var comp = extractor.Comp;
        var free = 0;
        for (var i = 0; i < comp.OutputSlotCount; i++)
        {
            if (_itemSlots.TryGetSlot(extractor.Owner, comp.OutputSlotId(i), out var slot) && !slot.HasItem)
                free++;
        }

        return free;
    }

    private bool TryInsertOutput(Entity<CMUBatchSeedExtractorComponent> extractor, EntityUid packet)
    {
        var comp = extractor.Comp;
        for (var i = 0; i < comp.OutputSlotCount; i++)
        {
            if (!_itemSlots.TryGetSlot(extractor.Owner, comp.OutputSlotId(i), out var slot) || slot.HasItem)
                continue;

            // No user: WindowMachine only keeps players out of the output slots.
            if (_itemSlots.TryInsert(extractor.Owner, slot, packet, null))
                return true;
        }

        return false;
    }

    // Clicking the machine with produce puts it into the first free input slot, so hand use still works.
    private void OnInteractUsing(Entity<CMUBatchSeedExtractorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<ProduceComponent>(args.Used, out var produce))
            return;

        args.Handled = true;

        if (produce.PlantProtoId == null)
            return;

        if (CMUSeedYield.IsSeedless(_botany, produce))
        {
            _popup.PopupEntity(Loc.GetString("seed-extractor-component-no-seeds", ("name", args.Used)),
                ent,
                args.User,
                PopupType.MediumCaution);
            return;
        }

        ItemSlot? free = null;
        for (var i = 0; i < ent.Comp.InputSlotCount; i++)
        {
            if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.InputSlotId(i), out var slot) ||
                slot.HasItem ||
                !_itemSlots.CanInsert(ent.Owner, slot, args.Used, args.User))
            {
                continue;
            }

            free = slot;
            break;
        }

        if (free == null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-batch-seed-extractor-full", ("machine", ent.Owner)),
                ent,
                args.User,
                PopupType.MediumCaution);
            return;
        }

        if (_hands.IsHolding(args.User, args.Used) && !_hands.TryDrop(args.User, args.Used))
            return;

        if (!_itemSlots.TryInsert(ent.Owner, free, args.Used, args.User))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-batch-seed-extractor-inserted", ("name", args.Used), ("machine", ent.Owner)),
            ent,
            args.User);
    }

    private void OnStartMessage(Entity<CMUBatchSeedExtractorComponent> ent, ref CMUBatchSeedExtractorStartMessage args)
    {
        if (ent.Comp.EndTime != null)
            return;

        if (!this.IsPowered(ent.Owner, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("cmu-batch-seed-extractor-unpowered"), ent, args.Actor);
            return;
        }

        if (!TryStartBatch(ent))
            _popup.PopupEntity(Loc.GetString("cmu-batch-seed-extractor-cannot-start"), ent, args.Actor);
    }

    private void OnEjectAllMessage(Entity<CMUBatchSeedExtractorComponent> ent, ref CMUBatchSeedExtractorEjectAllMessage args)
    {
        for (var i = 0; i < ent.Comp.OutputSlotCount; i++)
        {
            if (_itemSlots.TryGetSlot(ent.Owner, ent.Comp.OutputSlotId(i), out var slot) && slot.HasItem)
                _itemSlots.TryEjectToHands(ent.Owner, slot, args.Actor, true);
        }
    }

    private void OnToggleAutoMessage(Entity<CMUBatchSeedExtractorComponent> ent, ref CMUBatchSeedExtractorToggleAutoMessage args)
    {
        ent.Comp.AutoStart = !ent.Comp.AutoStart;
        Dirty(ent);
    }

    private void OnExamined(Entity<CMUBatchSeedExtractorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var comp = ent.Comp;
        using var group = args.PushGroup(nameof(CMUBatchSeedExtractorComponent));

        if (!this.IsPowered(ent.Owner, EntityManager))
        {
            args.PushMarkup(Loc.GetString("cmu-batch-seed-extractor-examine-unpowered"));
        }
        else if (comp.EndTime is { } endTime && comp.Duration is { } duration && duration > TimeSpan.Zero)
        {
            var left = endTime - _timing.CurTime;
            var percent = (int) Math.Clamp((1 - left.TotalSeconds / duration.TotalSeconds) * 100, 0, 100);
            args.PushMarkup(Loc.GetString("cmu-batch-seed-extractor-examine-progress", ("percent", percent)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("cmu-batch-seed-extractor-examine-idle"));
        }
    }
}
