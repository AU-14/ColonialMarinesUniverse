// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Ghost.Components;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

// A rope hung in a pit: a verb takes it down and gives the coil back.
public sealed class PitRopeSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PitRopeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<PitRopeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        // Ghosts are let through ladders, but they do not take ropes down.
        if (!args.CanAccess || !args.CanInteract || HasComp<GhostComponent>(args.User))
            return;

        var target = ent;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-pit-rope-remove"),
            Act = () => RemoveRope(target, user),
            Priority = 30,
        });
    }

    private void RemoveRope(Entity<PitRopeComponent> ent, EntityUid user)
    {
        var coords = Transform(ent).Coordinates;

        if (ent.Comp.Other is { } other && !TerminatingOrDeleted(other))
            QueueDel(other);

        QueueDel(ent.Owner);
        Spawn("RUMCPit", coords);

        var coil = Spawn("RUMCRopeCoil", Transform(user).Coordinates);
        _hands.TryPickupAnyHand(user, coil);
    }
}
