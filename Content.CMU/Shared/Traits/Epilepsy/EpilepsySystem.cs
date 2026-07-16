using Content.Shared._RMC14.Xenonids.Screech;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Traits.Epilepsy;

public sealed partial class EpilepsySystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EpilepsyComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<EpilepsyComponent> ent, ref ComponentStartup args)
    {
        ScheduleNext(ent);
    }

    private void ScheduleNext(Entity<EpilepsyComponent> ent)
    {
        var interval = _random.NextFloat((float)ent.Comp.IntervalMin.TotalSeconds, (float)ent.Comp.IntervalMax.TotalSeconds);
        ent.Comp.NextSeizure = _timing.CurTime + TimeSpan.FromSeconds(interval);
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<EpilepsyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (time < comp.NextSeizure)
                continue;

            ScheduleNext((uid, comp));

            var duration = TimeSpan.FromSeconds(_random.NextFloat(comp.DurationMin, comp.DurationMax));

            _popup.PopupEntity(Loc.GetString("au14-epilepsy-seizure"), uid, uid, PopupType.LargeCaution);
            _jitter.DoJitter(uid, duration, true, 10, 6);
            _stun.TryParalyze(uid, duration, true);

            var blind = EnsureComp<ScreechBlindComponent>(uid);
            blind.Radius = comp.VisionDimRadius;
            blind.EndsAt = time + duration;
            Dirty(uid, blind);
        }
    }
}
