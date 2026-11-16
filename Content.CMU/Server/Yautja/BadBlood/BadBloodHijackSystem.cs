using Content.Server.Administration.Logs;
using Content.Shared._RMC14.AntiAir;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

// CMU14: the shipside counter to the Vessel Strike (docs/GraphAudit-BadBlood-ObjectivesPlan.md ECS-503, STATE-503).
// The clan's Leader can hijack a dropship the xeno way; a Bad Blood hijack is tracked and logged. Nothing here touches
// the hijack flow or the strike.
public sealed partial class BadBloodHijackSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        // ECS-503: only this system subscribes to the Leader component's startup and shutdown
        SubscribeLocalEvent<BadBloodLeaderComponent, ComponentStartup>(OnLeaderStartup);
        SubscribeLocalEvent<BadBloodLeaderComponent, ComponentShutdown>(OnLeaderShutdown);

        // STATE-503
        SubscribeLocalEvent<RMCDropshipHijackAntiAirResolvedEvent>(OnHijackResolved);
        SubscribeLocalEvent<DropshipHijackLandedEvent>(OnHijackLanded);
    }

    // DropshipHijackerComponent is Access-locked to the dropship system, so its fields keep their defaults:
    // a xeno-style hijacker with the xeno lockout and no intel cost
    private void OnLeaderStartup(Entity<BadBloodLeaderComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<DropshipHijackerComponent>(ent.Owner);
    }

    private void OnLeaderShutdown(Entity<BadBloodLeaderComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        // the event mob CMUMobYautjaBadBlood can hijack on its own, it keeps the component when it stops leading
        if (MetaData(ent.Owner).EntityPrototype is { } proto && proto.HasComp<DropshipHijackerComponent>(Factory))
            return;

        RemComp<DropshipHijackerComponent>(ent.Owner);
    }

    private void OnHijackResolved(ref RMCDropshipHijackAntiAirResolvedEvent ev)
    {
        if (TerminatingOrDeleted(ev.Dropship) || TerminatingOrDeleted(ev.Actor) || !_badBlood.IsBadBlood(ev.Actor))
            return;

        var marked = EnsureComp<BadBloodHijackedDropshipComponent>(ev.Dropship);
        marked.Hijacker = ev.Actor;
        marked.StartedAt = _timing.CurTime;
        marked.CrashLogged = false;

        var diverted = ev.DivertedZone != null ? $", diverted from {ev.OriginalZone} to {ev.DivertedZone}" : string.Empty;
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"BadBlood obj: {ToPrettyString(ev.Actor):player} hijacked dropship {ToPrettyString(ev.Dropship):dropship} towards {ToPrettyString(ev.Destination):destination}{diverted}");

        var stage = new BadBloodObjectiveStageEvent(
            BadBloodObjectiveStage.Hijack,
            $"{Name(ev.Actor)} hijacked {Name(ev.Dropship)}",
            ev.Dropship);
        RaiseLocalEvent(ref stage);
    }

    private void OnHijackLanded(ref DropshipHijackLandedEvent ev)
    {
        var query = EntityQueryEnumerator<BadBloodHijackedDropshipComponent>();
        while (query.MoveNext(out var uid, out var marked))
        {
            if (marked.CrashLogged)
                continue;

            marked.CrashLogged = true;
            var flightSeconds = (int) (_timing.CurTime - marked.StartedAt).TotalSeconds;
            _adminLog.Add(LogType.Action, LogImpact.High,
                $"BadBlood obj: dropship {ToPrettyString(uid):dropship} hijacked by {ToPrettyString(marked.Hijacker):player} crashed on map {ToPrettyString(ev.Map):map} after {flightSeconds}s");
        }
    }
}
