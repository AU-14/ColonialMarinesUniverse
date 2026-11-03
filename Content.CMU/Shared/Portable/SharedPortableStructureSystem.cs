// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Throwing;

namespace Content.Shared.CMU14.Portable;

// The slowing down has to be known to both sides so the carrier's movement is predicted right.
public abstract class SharedPortableStructureSystem : EntitySystem
{
    [Dependency] private readonly HeldSpeedModifierSystem _heldSpeed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PortableStructureComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PortableStructureComponent, ThrowItemAttemptEvent>(OnThrowAttempt);
    }

    private void OnStartup(Entity<PortableStructureComponent> ent, ref ComponentStartup args)
    {
        // The existing held-speed system also handles pulling and prediction after the machine is dropped.
        _heldSpeed.CMUSetPortableSpeed(ent, ent.Comp.SpeedModifier);
    }

    private void OnThrowAttempt(Entity<PortableStructureComponent> ent, ref ThrowItemAttemptEvent args)
    {
        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("cmu-portable-too-heavy-to-throw"), ent, args.User);
    }
}
