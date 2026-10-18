// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

// Filling a pit back in with a shovel: a 30 second do-after that removes the pit.
public sealed class PitFillingSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly TimeSpan FillDelay = TimeSpan.FromSeconds(30);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PitComponent, InteractUsingEvent>(OnPitInteractUsing);
        SubscribeLocalEvent<PitComponent, PitFillDoAfterEvent>(OnFillPit);
    }

    private void OnPitInteractUsing(Entity<PitComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<PitDiggingToolComponent>(args.Used))
            return;

        args.Handled = true;
        StartFill(ent.Owner, args.User);
    }

    private void StartFill(EntityUid target, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, FillDelay, new PitFillDoAfterEvent(), target, target)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("au14-pit-tool-fill-started"), target, user);
    }

    private void OnFillPit(Entity<PitComponent> ent, ref PitFillDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        QueueDel(ent.Owner);
    }
}
