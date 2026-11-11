using System.Collections.Generic;
using System.Numerics;
using Content.Shared._RMC14.Effects;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.Grenades;

public sealed partial class CMUBoundingGrenadeSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUBoundingGrenadeComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<CMUBoundingGrenadeComponent, ProjectileFixedDistanceStopEvent>(OnFixedDistanceStop);
    }

    private void OnLand(Entity<CMUBoundingGrenadeComponent> ent, ref LandEvent args)
    {
        // Only a primed grenade bounds; a dropped or thrown one with the pin still in just lands.
        if (!HasComp<ActiveTimerTriggerComponent>(ent))
            return;

        Bound(ent, args.User);
    }

    private void OnFixedDistanceStop(Entity<CMUBoundingGrenadeComponent> ent, ref ProjectileFixedDistanceStopEvent args)
    {
        // Fired from a launcher, it bounds as soon as it reaches the spot it was aimed at.
        Bound(ent, null);
    }

    private void Bound(Entity<CMUBoundingGrenadeComponent> ent, EntityUid? user)
    {
        if (_net.IsClient || ent.Comp.DetonateAt != null || _container.IsEntityInContainer(ent))
            return;

        // The bound replaces the fuse, so stop the timer from going off mid-jump.
        RemComp<ActiveTimerTriggerComponent>(ent);

        ent.Comp.DetonateAt = _timing.CurTime + ent.Comp.BoundTime;
        ent.Comp.User = user ?? CompOrNull<TimerTriggerComponent>(ent)?.User;
        Dirty(ent);

        var height = ent.Comp.BoundHeight;
        var anim = new RMCSpriteOffsetAnimationComponent
        {
            StartingOffset = Vector2.Zero,
            PathOffsets = new List<Vector2>
            {
                Vector2.Zero,
                new(0, height * 0.75f),
                new(0, height),
            },
            Length = (float) ent.Comp.BoundTime.TotalSeconds,
        };
        AddComp(ent, anim, true);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUBoundingGrenadeComponent>();
        while (query.MoveNext(out var uid, out var bounding))
        {
            if (bounding.DetonateAt is not { } at || time < at)
                continue;

            bounding.DetonateAt = null;
            _trigger.Trigger(uid, bounding.User, TriggerSystem.DefaultTriggerKey);
        }
    }
}
