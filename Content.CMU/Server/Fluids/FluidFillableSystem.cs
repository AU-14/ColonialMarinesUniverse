// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Fluids;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.Fluids;

// Pouring any liquid into a tank of the fluid system from a container in the hand, and drawing it out again.
public sealed class FluidFillableSystem : EntitySystem
{
    [Dependency] private readonly FluidSystem _fluids = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FluidFillableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FluidFillableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    // A container with something in it is poured into the tank, as much as there is room for.
    private void OnInteractUsing(Entity<FluidFillableComponent> tank, ref InteractUsingEvent args)
    {
        if (args.Handled || !_solutions.TryGetDrainableSolution(args.Used, out var container, out var solution))
            return;

        args.Handled = true;

        var poured = 0;
        foreach (var quantity in solution.Contents.ToArray())
        {
            var kind = quantity.Reagent.Prototype;
            var added = _fluids.Add(tank, kind, (float) quantity.Quantity);
            if (added <= 0f)
                continue;

            _solutions.RemoveReagent(container.Value, quantity.Reagent, FixedPoint2.New(added));
            poured += (int) added;
        }

        _popup.PopupEntity(Loc.GetString(poured > 0 ? "au14-fluid-poured-in" : "au14-fluid-nothing-poured", ("amount", poured)), tank, args.User);
    }

    private void OnGetVerbs(Entity<FluidFillableComponent> tank, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } held || !_solutions.TryGetRefillableSolution(held, out _, out _))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-fluid-verb-draw"),
            Priority = 1,
            Act = () => Draw(tank, user, held),
        });
    }

    // Fills the container in the hand with the liquid the tank has most of.
    private void Draw(Entity<FluidFillableComponent> tank, EntityUid user, EntityUid held)
    {
        if (!TryComp<FluidTankComponent>(tank, out var contents) || !_solutions.TryGetRefillableSolution(held, out var container, out var solution))
            return;

        string? kind = null;
        var most = 0f;
        foreach (var (reagent, amount) in contents.Contents)
        {
            if (amount > most)
            {
                kind = reagent;
                most = amount;
            }
        }

        if (kind == null)
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-barrel-empty"), tank, user);
            return;
        }

        var wanted = FixedPoint2.Min(FixedPoint2.New(most), solution.AvailableVolume);
        if (wanted <= FixedPoint2.Zero || !_solutions.TryAddReagent(container.Value, kind, wanted, out var accepted))
        {
            _popup.PopupEntity(Loc.GetString("au14-fluid-no-room"), tank, user);
            return;
        }

        _fluids.Remove(tank, kind, (float) accepted);
        _popup.PopupEntity(Loc.GetString("au14-fluid-drawn", ("amount", accepted.Int()), ("fluid", _fluids.Name(kind))), tank, user);
    }
}
