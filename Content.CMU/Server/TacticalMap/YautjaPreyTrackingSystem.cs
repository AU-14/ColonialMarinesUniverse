using Content.Server._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.TacticalMap;

public sealed partial class YautjaPreyTrackingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TacticalMapSystem _tacticalMap = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaTrackedPreyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<YautjaTrackedPreyComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<YautjaTrackedPreyComponent, MoveEvent>(OnMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaTrackedPreyComponent>();
        while (query.MoveNext(out var uid, out var tracking))
        {
            if (tracking.ExpiresAt > now)
                continue;

            _tacticalMap.RemoveYautjaPreyBlip(uid, tracking.Map);
            RemCompDeferred<YautjaTrackedPreyComponent>(uid);
        }
    }

    public void Track(EntityUid target, TimeSpan duration)
    {
        var tracking = EnsureComp<YautjaTrackedPreyComponent>(target);
        tracking.ExpiresAt = _timing.CurTime + duration;
        UpdateBlip((target, tracking));
        Dirty(target, tracking);
    }

    private void OnStartup(Entity<YautjaTrackedPreyComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.ExpiresAt > _timing.CurTime)
            UpdateBlip(ent);
    }

    private void OnShutdown(Entity<YautjaTrackedPreyComponent> ent, ref ComponentShutdown args)
    {
        _tacticalMap.RemoveYautjaPreyBlip(ent.Owner, ent.Comp.Map);
    }

    private void OnMove(Entity<YautjaTrackedPreyComponent> ent, ref MoveEvent args)
    {
        UpdateBlip(ent);
    }

    private void UpdateBlip(Entity<YautjaTrackedPreyComponent> ent)
    {
        var oldMap = ent.Comp.Map;
        if (!_tacticalMap.TrySetYautjaPreyBlip(ent.Owner, out var newMap))
        {
            _tacticalMap.RemoveYautjaPreyBlip(ent.Owner, oldMap);
            ent.Comp.Map = null;
            Dirty(ent);
            return;
        }

        if (oldMap is { } oldMapUid && oldMapUid != newMap)
            _tacticalMap.RemoveYautjaPreyBlip(ent.Owner, oldMapUid);

        ent.Comp.Map = newMap;
        Dirty(ent);
    }
}
