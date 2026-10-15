// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Portable;
using Content.Shared.Examine;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Electric;

// Crude oil and the fuel: the deposit is filled when it appears, the pump takes the oil out of it into its tank, and the generator
// turns the fuel of its tank into power.
public sealed class OilSystem : EntitySystem
{
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPowerStateSystem _powerState = default!;

    private readonly HashSet<Entity<OilDepositComponent>> _deposits = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OilDepositComponent, MapInitEvent>(OnDepositInit);
        SubscribeLocalEvent<OilDepositComponent, ExaminedEvent>(OnDepositExamined);
        SubscribeLocalEvent<OilPumpComponent, ExaminedEvent>(OnPumpExamined);
        SubscribeLocalEvent<OilPumpComponent, PortablePlaceableOverEvent>(OnPumpPlaceableOver);
        SubscribeLocalEvent<LiquidFuelGeneratorComponent, ExaminedEvent>(OnGeneratorExamined);
    }

    private void OnDepositInit(Entity<OilDepositComponent> deposit, ref MapInitEvent args)
    {
        Generate(deposit);
    }

    public void Generate(Entity<OilDepositComponent> deposit)
    {
        if (deposit.Comp.Generated)
            return;

        deposit.Comp.Generated = true;
        deposit.Comp.Remaining = _random.Next(deposit.Comp.MinTotal, deposit.Comp.MaxTotal + 1);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var pumps = EntityQueryEnumerator<OilPumpComponent>();
        while (pumps.MoveNext(out var uid, out var pump))
            UpdatePump((uid, pump), frameTime);

        var generators = EntityQueryEnumerator<LiquidFuelGeneratorComponent, PowerSupplierComponent>();
        while (generators.MoveNext(out var uid, out var gen, out var supplier))
        {
            var running = false;
            if (gen.Fuels.Count == 0)
            {
                running = _fluids.TryUse(uid, gen.Kind, gen.PerSecond * frameTime);
            }
            else
            {
                foreach (var (fuel, perSecond) in gen.Fuels)
                {
                    if (_fluids.TryUse(uid, fuel, perSecond * frameTime))
                    {
                        running = true;
                        break;
                    }
                }
            }

            if (supplier.Enabled != running)
                supplier.Enabled = running;

            if (gen.Running != running)
            {
                gen.Running = running;
                _appearance.SetData(uid, RecipeMachineVisuals.Working, running);
            }
        }
    }

    private void UpdatePump(Entity<OilPumpComponent> pump, float frameTime)
    {
        var working = false;

        if (this.IsPowered(pump.Owner, EntityManager) && FindDeposit(pump) is { } deposit)
        {
            var wanted = Math.Min(pump.Comp.PerSecond * frameTime, deposit.Comp.Remaining);
            var added = _fluids.Add(pump.Owner, pump.Comp.Kind, wanted);
            if (added > 0f)
            {
                working = true;
                deposit.Comp.Remaining -= (int) Math.Ceiling(added);
                if (deposit.Comp.Remaining <= 0)
                    QueueDel(deposit);
            }
        }

        _powerState.TrySetWorkingState(pump.Owner, working);
        _appearance.SetData(pump.Owner, RecipeMachineVisuals.Working, working);
    }

    private Entity<OilDepositComponent>? FindDeposit(Entity<OilPumpComponent> pump)
    {
        _deposits.Clear();
        _lookup.GetEntitiesInRange(Transform(pump).Coordinates, pump.Comp.Range, _deposits);

        foreach (var deposit in _deposits)
        {
            if (deposit.Comp.Remaining > 0)
                return deposit;
        }

        return null;
    }

    // The oil pump is made to stand on a deposit of oil, so the deposit on the tile is not in its way.
    private void OnPumpPlaceableOver(Entity<OilPumpComponent> pump, ref PortablePlaceableOverEvent args)
    {
        if (HasComp<OilDepositComponent>(args.Other))
            args.Allowed = true;
    }

    private void OnDepositExamined(Entity<OilDepositComponent> deposit, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("au14-oil-deposit-examine", ("amount", deposit.Comp.Remaining)));
    }

    private void OnPumpExamined(Entity<OilPumpComponent> pump, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(FindDeposit(pump) != null ? "au14-oil-pump-examine-deposit" : "au14-oil-pump-examine-none"));
    }

    private void OnGeneratorExamined(Entity<LiquidFuelGeneratorComponent> gen, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(gen.Comp.Running ? "au14-generator-examine-running" : "au14-generator-examine-idle"));
    }
}
