using Content.Shared.Atmos;
using Content.Shared.Camera;
using Content.Shared.Cuffs;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable;

namespace Content.Shared.Hands.EntitySystems;

public abstract partial class SharedHandsSystem
{
    private void InitializeRelay()
    {
        SubscribeLocalEvent<HandsComponent, GetEyePvsScaleRelayedEvent>(RelayEvent);
        SubscribeLocalEvent<HandsComponent, RefreshMovementSpeedModifiersEvent>(RelayEvent);

        // By-ref events.
        SubscribeLocalEvent<HandsComponent, GetEyeOffsetRelayedEvent>(RefRelayEvent);
        SubscribeLocalEvent<HandsComponent, ExtinguishEvent>(RefRelayEvent);
        SubscribeLocalEvent<HandsComponent, ProjectileReflectAttemptEvent>(RefRelayEvent);
        SubscribeLocalEvent<HandsComponent, HitScanReflectAttemptEvent>(RefRelayEvent);
        SubscribeLocalEvent<HandsComponent, WieldAttemptEvent>(RefRelayEvent);
        SubscribeLocalEvent<HandsComponent, UnwieldAttemptEvent>(RefRelayEvent);
        SubscribeLocalEvent<HandsComponent, TargetHandcuffedEvent>(RefRelayEvent);
        SubscribeLocalEvent<HandsComponent, RefreshWeightlessModifiersEvent>(RefRelayEvent);
    }

    private void RelayEvent<T>(Entity<HandsComponent> entity, ref T args) where T : EntityEventArgs
    {
        CoreRelayEvent(entity, ref args);
    }

    private void RefRelayEvent<T>(Entity<HandsComponent> entity, ref T args)
    {
        var ev = CoreRelayEvent(entity, ref args);
        if (ev != null)
            args = ev.Args;
    }

    private HeldRelayedEvent<T>? CoreRelayEvent<T>(Entity<HandsComponent> entity, ref T args)
    {
        HeldRelayedEvent<T>? ev = null;
        // Keep EnumerateHeld's active-hand-first order without allocating its yield iterator.
        // Camera offsets run during both prediction and rendering, often with both hands empty.
        if (TryGetActiveItem(entity.AsNullable(), out var active))
        {
            ev = new HeldRelayedEvent<T>(args);
            RaiseLocalEvent(active.Value, ref ev);
        }

        foreach (var name in entity.Comp.SortedHands)
        {
            if (name == entity.Comp.ActiveHandId || !TryGetHeldItem(entity.AsNullable(), name, out var held))
                continue;

            ev ??= new HeldRelayedEvent<T>(args);
            RaiseLocalEvent(held.Value, ref ev);
        }

        return ev;
    }
}
