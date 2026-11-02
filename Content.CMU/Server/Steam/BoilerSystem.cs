// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Steam;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Stacks;

namespace Content.Server.CMU14.Steam;

// The boiler: it lights a piece of fuel when there is water to boil and the pipes it is joined to are under pressure, and while
// the fire burns it turns water into water vapor, the gas of the atmosphere, in those pipes.
public sealed class BoilerSystem : EntitySystem
{
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly SteamGasSystem _steam = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoilerComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<BoilerComponent, ExaminedEvent>(OnExamined);
    }

    // Only what burns goes into the fuel slot.
    private void OnInsertAttempt(Entity<BoilerComponent> boiler, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != BoilerComponent.FuelSlotId)
            return;

        if (!TryComp<StackComponent>(args.Item, out var stack) || !boiler.Comp.Fuels.ContainsKey(stack.StackTypeId))
            args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BoilerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateBoiler((uid, comp), frameTime);
        }
    }

    private void UpdateBoiler(Entity<BoilerComponent> boiler, float frameTime)
    {
        var comp = boiler.Comp;

        var water = _fluids.GetAmount(boiler.Owner, FluidKinds.Water);
        var wanted = water > 0f && _steam.GetPressure(boiler.Owner) < comp.MaxPressure;

        // A fire is lit only when it has work to do; one that is burning goes on until the piece is gone.
        if (comp.BurnLeft <= 0f && wanted)
            TryLight(boiler);

        if (comp.BurnLeft > 0f)
        {
            comp.BurnLeft -= frameTime;

            if (wanted)
            {
                var used = Math.Min(water, comp.WaterPerSecond * frameTime);
                if (_steam.Add(boiler.Owner, used * comp.SteamPerWater, comp.MaxPressure) > 0f)
                    _fluids.Remove(boiler.Owner, FluidKinds.Water, used);
            }
        }

        SetBurning(boiler, comp.BurnLeft > 0f);
    }

    // Burns the next piece of the fuel slot. Returns false when there is none.
    private bool TryLight(Entity<BoilerComponent> boiler)
    {
        if (_itemSlots.GetItemOrNull(boiler.Owner, BoilerComponent.FuelSlotId) is not { } fuel ||
            !TryComp<StackComponent>(fuel, out var stack) ||
            stack.Count <= 0 ||
            !boiler.Comp.Fuels.TryGetValue(stack.StackTypeId, out var seconds))
        {
            return false;
        }

        _stack.SetCount((fuel, stack), stack.Count - 1);
        boiler.Comp.BurnLeft = seconds;
        return true;
    }

    private void SetBurning(Entity<BoilerComponent> boiler, bool burning)
    {
        if (boiler.Comp.Burning == burning)
            return;

        boiler.Comp.Burning = burning;
        Dirty(boiler);

        _appearance.SetData(boiler.Owner, BoilerVisuals.Burning, burning);
        if (_light.TryGetLight(boiler.Owner, out var light))
            _light.SetEnabled(boiler.Owner, burning, light);
    }

    private void OnExamined(Entity<BoilerComponent> boiler, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(boiler.Comp.Burning ? "au14-boiler-examine-burning" : "au14-boiler-examine-cold"));
    }
}
