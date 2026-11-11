using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Explosion.Components; // CMU14
using Content.Shared.Throwing;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Trigger;

public sealed partial class RMCTriggerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OnShootTriggerAmmoTimerComponent, AmmoShotEvent>(OnTriggerTimerAmmoShot);
        SubscribeLocalEvent<TriggerOnThrowEndComponent, StopThrowEvent>(OnThrownAmmoStops);
        SubscribeLocalEvent<TriggerOnThrowEndComponent, TriggerEvent>(OnTrigger);

        SubscribeLocalEvent<TriggerOnFixedDistanceStopComponent, ProjectileFixedDistanceStopEvent>(OnTriggerOnFixedDistanceStop);
    }

    private void OnTriggerTimerAmmoShot(Entity<OnShootTriggerAmmoTimerComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            switch (ent.Comp.TimerStart)
            {
                case TimerStartMode.OnShoot:
                    _trigger.HandleTimerTrigger(projectile, null, ent.Comp.Delay, ent.Comp.BeepInterval, ent.Comp.InitialBeepDelay, ent.Comp.BeepSound);
                    break;
                case TimerStartMode.OnHitGround:
                    var primedAmmoComp = EnsureComp<TriggerOnThrowEndComponent>(projectile);
                    primedAmmoComp.Delay = TimeSpan.FromSeconds(ent.Comp.Delay);
                    primedAmmoComp.BeepInterval = ent.Comp.BeepInterval;
                    primedAmmoComp.InitialBeepDelay = ent.Comp.InitialBeepDelay;
                    primedAmmoComp.BeepSound = ent.Comp.BeepSound;
                    break;

            }
        }
    }

    private void OnThrownAmmoStops(Entity<TriggerOnThrowEndComponent> ent, ref StopThrowEvent args)
    {
        _trigger.HandleTimerTrigger(ent, null, ((float)ent.Comp.Delay.TotalSeconds), ent.Comp.BeepInterval, ent.Comp.InitialBeepDelay, ent.Comp.BeepSound);
    }

    private void OnTriggerOnFixedDistanceStop(Entity<TriggerOnFixedDistanceStopComponent> ent, ref ProjectileFixedDistanceStopEvent args)
    {
        var active = EnsureComp<ActiveTriggerOnThrowEndComponent>(ent);
        active.TriggerAt = _timing.CurTime + ent.Comp.Delay;
    }

    private void OnTrigger(Entity<TriggerOnThrowEndComponent> ent, ref TriggerEvent args)
    {
        RemCompDeferred<TriggerOnThrowEndComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<ActiveTriggerOnThrowEndComponent>();
        while (query.MoveNext(out var uid, out var active))
        {
            if (time < active.TriggerAt)
                continue;

            _trigger.Trigger(uid);

            // CMU14 Begin
            // scatter payloads (flares) spawn their contents on the scatter system's next update and Del themselves.
            // queueing it here could delete it before that update ever saw it
            if (TryComp<ScatteringGrenadeComponent>(uid, out var scatter) && scatter.IsTriggered && scatter.Count > 0)
            {
                RemCompDeferred<ActiveTriggerOnThrowEndComponent>(uid);
                continue;
            }
            // CMU14 End

            if (!EntityManager.IsQueuedForDeletion(uid) && !TerminatingOrDeleted(uid))
                QueueDel(uid);
        }
    }
}
