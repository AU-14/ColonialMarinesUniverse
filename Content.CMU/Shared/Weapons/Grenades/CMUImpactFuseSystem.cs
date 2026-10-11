using System.Collections.Generic;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.Grenades;

public sealed class CMUImpactFuseSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUImpactFuseComponent, ProjectileFixedDistanceStopEvent>(OnStop);
        SubscribeLocalEvent<CMUImpactFuseComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStop(Entity<CMUImpactFuseComponent> ent, ref ProjectileFixedDistanceStopEvent args)
    {
        Detonate(ent);
    }

    private void OnStartCollide(Entity<CMUImpactFuseComponent> ent, ref StartCollideEvent args)
    {
        // Only while it's flying out of a launcher; a thrown or dropped one doesn't care.
        if (!args.OtherFixture.Hard || !HasComp<ProjectileFixedDistanceComponent>(ent))
            return;

        Detonate(ent);
    }

    private void Detonate(Entity<CMUImpactFuseComponent> ent)
    {
        if (_net.IsClient || ent.Comp.Detonated || TerminatingOrDeleted(ent))
            return;

        ent.Comp.Detonated = true;
        _trigger.Trigger(ent, null, TriggerSystem.DefaultTriggerKey);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUImpactFuseComponent, ProjectileFixedDistanceComponent>();
        while (query.MoveNext(out var uid, out var fuse, out _))
        {
            if (fuse.Detonated)
                continue;

            fuse.LaunchedAt ??= time;
            if (time < fuse.LaunchedAt + fuse.ArmDelay)
                continue;

            _mobs.Clear();
            _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(uid), fuse.HitRadius, _mobs);
            foreach (var mob in _mobs)
            {
                if (_mobState.IsDead(mob))
                    continue;

                Detonate((uid, fuse));
                break;
            }
        }
    }
}
