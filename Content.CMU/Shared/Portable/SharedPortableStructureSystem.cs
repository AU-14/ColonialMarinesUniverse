// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Hands;
using Content.Shared.Movement.Systems;

namespace Content.Shared.CMU14.Portable;

// The slowing down has to be known to both sides so the carrier's movement is predicted right.
public abstract class SharedPortableStructureSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PortableStructureComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<PortableStructureComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<PortableStructureComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshSpeed);
    }

    private void OnEquipped(Entity<PortableStructureComponent> ent, ref GotEquippedHandEvent args)
    {
        _movement.RefreshMovementModifiers(args.User);
    }

    private void OnUnequipped(Entity<PortableStructureComponent> ent, ref GotUnequippedHandEvent args)
    {
        _movement.RefreshMovementModifiers(args.User);
    }

    private void OnRefreshSpeed(Entity<PortableStructureComponent> ent, ref HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        args.Args.ModifySpeed(ent.Comp.SpeedModifier);
    }
}
