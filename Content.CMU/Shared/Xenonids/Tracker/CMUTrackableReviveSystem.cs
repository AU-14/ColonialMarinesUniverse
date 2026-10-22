using Content.Shared._RMC14.Tracker;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Xenonids.Tracker;

/// <summary>
/// The hive tracker drops a dead xeno's trackable marker, so a revived queen stayed untrackable.
/// Remember who lost it on death and hand it back when they get up again.
/// </summary>
public sealed class CMUTrackableReviveSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCTrackableComponent, ComponentRemove>(OnTrackableRemove);
        SubscribeLocalEvent<CMUTrackableOnReviveComponent, MobStateChangedEvent>(OnReviveMobStateChanged);
    }

    private void OnTrackableRemove(Entity<RMCTrackableComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent) || !HasComp<XenoComponent>(ent) || !_mobState.IsDead(ent))
            return;

        EnsureComp<CMUTrackableOnReviveComponent>(ent);
    }

    private void OnReviveMobStateChanged(Entity<CMUTrackableOnReviveComponent> ent, ref MobStateChangedEvent args)
    {
        if (_net.IsClient || args.NewMobState == MobState.Dead)
            return;

        RemCompDeferred<CMUTrackableOnReviveComponent>(ent);
        EnsureComp<RMCTrackableComponent>(ent);
    }
}
