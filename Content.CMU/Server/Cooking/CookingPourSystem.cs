// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.Cooking;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;

namespace Content.Server.CMU14.Cooking;

// A cup, a mug or a beaker held to a pot is poured into it, and not put in it as an ingredient (the pot is a storage, and the storage would take the cup first).
// This is a system of its own because the subscription must come before the storage, and the cooking has another subscription to the same event.
public sealed class CookingPourSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FoodCookerComponent, InteractUsingEvent>(OnPour, before: new[] { typeof(SharedStorageSystem) });
    }

    private void OnPour(Entity<FoodCookerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<SolutionTransferComponent>(args.Used))
            return;

        // The pouring is the work of the transfer of the game: it is given the click as if the vessel had been clicked after it.
        var click = new AfterInteractEvent(args.User, args.Used, ent, args.ClickLocation, true);
        RaiseLocalEvent(args.Used, click);
        args.Handled = true;
    }
}
