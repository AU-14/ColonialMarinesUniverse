// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Server.CMU14.Fluids;
using Content.Server.Power.EntitySystems;
using Content.Shared.CMU14.Mining;
using Content.Shared.CMU14.Portable;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Mining;

// The drills. A drill needs steam (or power), a head with something left of it, a deposit of ore on its own tile and room for what
// it takes. Every turn it takes from one to ten pieces of ore out of the deposit, picked among the kinds the deposit holds, and
// wears the head down by one. The deposit is gone when it is empty, and the head when it is worn out.
public sealed class OreDrillSystem : EntitySystem
{
    [Dependency] private readonly SteamGasSystem _steam = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerStateSystem _powerState = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private static readonly SoundSpecifier BreakSound = new SoundCollectionSpecifier("MetalBreak");

    private const float DirtyInterval = 0.2f;

    private readonly HashSet<Entity<OreDepositComponent>> _found = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OreDrillComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<OreDrillComponent, PortablePlaceableOverEvent>(OnPlaceableOver);
        SubscribeLocalEvent<DrillHeadComponent, ExaminedEvent>(OnHeadExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<OreDrillComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateDrill((uid, comp), frameTime);
        }
    }

    // The drill is made to stand on a deposit, so the deposit on the tile is not in its way.
    private void OnPlaceableOver(Entity<OreDrillComponent> drill, ref PortablePlaceableOverEvent args)
    {
        if (HasComp<OreDepositComponent>(args.Other))
            args.Allowed = true;
    }

    private void UpdateDrill(Entity<OreDrillComponent> drill, float frameTime)
    {
        var comp = drill.Comp;
        var steamMode = comp.SteamPerSecond > 0f;

        var deposit = FindDeposit(drill);
        var onDeposit = deposit != null;
        if (comp.OnDeposit != onDeposit)
        {
            comp.OnDeposit = onDeposit;
            Dirty(drill);
        }

        var head = GetHead(drill);
        var ready = deposit != null && head != null && HasRoom(drill);

        // Without steam or power, or with nothing to do, the drill stands.
        if (!ready || !HasEnergy(drill, steamMode, frameTime))
        {
            Stop(drill, !ready);
            if (!steamMode)
                _powerState.TrySetWorkingState(drill.Owner, false);

            return;
        }

        SetActive(drill, true);
        if (!steamMode)
            _powerState.TrySetWorkingState(drill.Owner, true);

        comp.Progress += frameTime / Math.Max(comp.CycleSeconds, 0.1f);
        if (comp.Progress >= 1f)
        {
            comp.Progress = 0f;
            Complete(drill, deposit!.Value, head!.Value);
            return;
        }

        comp.NextDirty -= frameTime;
        if (comp.NextDirty <= 0f)
        {
            comp.NextDirty = DirtyInterval;
            Dirty(drill);
        }
    }

    // Steam is used up while the drill works; the electric drill only has to be powered.
    private bool HasEnergy(Entity<OreDrillComponent> drill, bool steamMode, float frameTime)
    {
        return steamMode
            ? _steam.TryUse(drill.Owner, drill.Comp.SteamPerSecond * frameTime)
            : this.IsPowered(drill.Owner, EntityManager);
    }

    private void SetActive(Entity<OreDrillComponent> drill, bool active)
    {
        if (drill.Comp.Active == active)
            return;

        drill.Comp.Active = active;
        Dirty(drill);
        _appearance.SetData(drill.Owner, DrillVisuals.Working, active);
    }

    // The turn in progress is kept while the drill only lacks steam or power, and lost when the deposit or the head is gone.
    private void Stop(Entity<OreDrillComponent> drill, bool forget)
    {
        var comp = drill.Comp;
        if (!comp.Active && (!forget || comp.Progress == 0f))
            return;

        if (forget)
            comp.Progress = 0f;

        SetActive(drill, false);
        Dirty(drill);
    }

    private Entity<DrillHeadComponent>? GetHead(Entity<OreDrillComponent> drill)
    {
        if (_itemSlots.GetItemOrNull(drill.Owner, OreDrillComponent.HeadSlotId) is not { } head ||
            !TryComp<DrillHeadComponent>(head, out var comp) || comp.Durability <= 0)
        {
            return null;
        }

        return (head, comp);
    }

    // Some output slot is empty, or holds a stack that has room for more.
    private bool HasRoom(Entity<OreDrillComponent> drill)
    {
        foreach (var slotId in OreDrillComponent.OutputSlotIds)
        {
            if (_itemSlots.GetItemOrNull(drill.Owner, slotId) is not { } held)
                return true;

            if (TryComp<StackComponent>(held, out var stack) && _stack.GetAvailableSpace(stack) > 0)
                return true;
        }

        return false;
    }

    // The deposit on the same tile as the drill.
    private Entity<OreDepositComponent>? FindDeposit(Entity<OreDrillComponent> drill)
    {
        _found.Clear();
        _lookup.GetEntitiesInRange(Transform(drill).Coordinates, 0.4f, _found);

        foreach (var deposit in _found)
        {
            if (!TerminatingOrDeleted(deposit) && deposit.Comp.Ores.Count > 0)
                return deposit;
        }

        return null;
    }

    private void Complete(Entity<OreDrillComponent> drill, Entity<OreDepositComponent> deposit, Entity<DrillHeadComponent> head)
    {
        var comp = drill.Comp;
        var left = 0;
        foreach (var count in deposit.Comp.Ores.Values)
            left += count;

        // One to ten pieces, never more than the deposit has.
        var yield = Math.Min(_random.Next(comp.MinPerCycle, comp.MaxPerCycle + 1), left);
        var taken = new Dictionary<ProtoId<StackPrototype>, int>();

        for (var i = 0; i < yield; i++)
        {
            // Each piece is one of the pieces left in the deposit, the kinds with more left being the likelier.
            var roll = _random.Next(left);
            foreach (var (ore, count) in deposit.Comp.Ores.ToList())
            {
                if (roll < count)
                {
                    taken[ore] = taken.GetValueOrDefault(ore) + 1;
                    if (count <= 1)
                        deposit.Comp.Ores.Remove(ore);
                    else
                        deposit.Comp.Ores[ore] = count - 1;

                    left--;
                    break;
                }

                roll -= count;
            }
        }

        foreach (var (ore, count) in taken)
            Output(drill, ore, count);

        // The deposit is used up when nothing is left of it.
        if (deposit.Comp.Ores.Count == 0)
            QueueDel(deposit);

        WearHead(drill, head);
        Dirty(drill);
    }

    // Puts the ore into the output slots, onto the stacks that are there or into an empty slot; what does not fit lies in front.
    private void Output(Entity<OreDrillComponent> drill, ProtoId<StackPrototype> oreId, int count)
    {
        if (!_proto.TryIndex(oreId, out var stackProto) || string.IsNullOrEmpty(stackProto.Spawn.Id))
            return;

        var coords = Transform(drill).Coordinates;
        var made = Spawn(stackProto.Spawn, coords);
        if (!TryComp<StackComponent>(made, out var madeStack))
        {
            Del(made);
            return;
        }

        _stack.SetCount((made, madeStack), count);

        // Onto the stacks of the same ore already lying in the slots.
        foreach (var slotId in OreDrillComponent.OutputSlotIds)
        {
            if (_itemSlots.GetItemOrNull(drill.Owner, slotId) is not { } held || !TryComp<StackComponent>(held, out var heldStack) ||
                heldStack.StackTypeId != oreId)
            {
                continue;
            }

            _stack.TryMergeStacks((made, madeStack), (held, heldStack), out _);
            if (TerminatingOrDeleted(made))
                return;
        }

        // What is left goes into an empty slot.
        foreach (var slotId in OreDrillComponent.OutputSlotIds)
        {
            if (_itemSlots.GetItemOrNull(drill.Owner, slotId) == null && _itemSlots.TryInsert(drill.Owner, slotId, made, null))
                return;
        }

        // No slot took it: it lies in front of the drill, which is no loss.
    }

    private void WearHead(Entity<OreDrillComponent> drill, Entity<DrillHeadComponent> head)
    {
        head.Comp.Durability--;
        Dirty(head);

        if (head.Comp.Durability > 0)
            return;

        _popup.PopupEntity(Loc.GetString("au14-drill-head-broke"), drill);
        _audio.PlayPvs(BreakSound, drill);
        PredictedQueueDel(head);
    }

    private void OnExamined(Entity<OreDrillComponent> drill, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using var block = args.PushGroup(nameof(OreDrillComponent));
        args.PushMarkup(Loc.GetString(drill.Comp.OnDeposit ? "au14-drill-examine-deposit" : "au14-drill-examine-no-deposit"));
        args.PushMarkup(Loc.GetString(drill.Comp.Active ? "au14-drill-examine-working" : "au14-drill-examine-idle"));
    }

    private void OnHeadExamined(Entity<DrillHeadComponent> head, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("au14-drill-head-examine", ("left", head.Comp.Durability), ("max", head.Comp.MaxDurability)));
    }
}
