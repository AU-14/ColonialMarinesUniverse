using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._CMU14.Xenomorphs.Pathogen.SporeCloud;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.SporeSac;

public sealed partial class CMUPathogenSporeSacSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPathogenSporeSacComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<CMUPathogenSporeSacComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnStartCollide(Entity<CMUPathogenSporeSacComponent> sac, ref StartCollideEvent args)
    {
        if (sac.Comp.Status != SporeSacStatus.Waiting)
            return;

        if (!CanTrigger(sac, args.OtherEntity))
            return;

        Burst(sac, kill: false);
    }

    private void OnDamageChanged(Entity<CMUPathogenSporeSacComponent> sac, ref DamageChangedEvent args)
    {
        // Swap for whatever your actual damageable→health bridge looks like;
        // if you're not using DamageableComponent for this, drive Health
        // directly from wherever attack damage lands and call HealthCheck from there.
        HealthCheck(sac);
    }

    private bool CanTrigger(Entity<CMUPathogenSporeSacComponent> sac, EntityUid crosser)
    {
        // Mirror can_hug(crosser, XENO_HIVE_PATHOGEN) && !isyautja && !issynth.
        if (HasComp<Content.Shared._RMC14.Synth.SynthComponent>(crosser))
            return false;

        if (!HasComp<Content.Shared.Mobs.Components.MobStateComponent>(crosser))
            return false;

        // Don't trigger on members of the same hive as whoever placed the sac.
        if (sac.Comp.Placer is { } placer && _hive.FromSameHive(placer, crosser))
            return false;

        // TODO: predator (yautja) exclusion, matching isyautja() in DM.
        return true;
    }

    private void HealthCheck(Entity<CMUPathogenSporeSacComponent> sac)
    {
        if (sac.Comp.Health > 0)
            return;

        Burst(sac, kill: true);
    }

    public void Burst(Entity<CMUPathogenSporeSacComponent> sac, bool kill)
    {
        if (kill)
            QueueDel(sac); // DM used a 3s delayed qdel; swap to a timer if you want that grace period.

        if (sac.Comp.Status != SporeSacStatus.Waiting)
            return;

        sac.Comp.Status = SporeSacStatus.Deploying;
        sac.Comp.BurstAt = _timing.CurTime + sac.Comp.BurstToReleaseDelay;
        _appearance.SetData(sac, SporeSacVisuals.State, sac.Comp.Status);
        Dirty(sac);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var query = EntityQueryEnumerator<CMUPathogenSporeSacComponent>();
        while (query.MoveNext(out var uid, out var sac))
        {
            if (sac.Status == SporeSacStatus.Deploying &&
                sac.BurstAt is { } burstAt && time >= burstAt)
            {
                ReleaseCloud((uid, sac));
            }

            if (sac.Status == SporeSacStatus.Deployed &&
                sac.RegenerateAt is { } regenAt && time >= regenAt)
            {
                ResetSpores((uid, sac));
            }
        }
    }

    private void ReleaseCloud(Entity<CMUPathogenSporeSacComponent> sac)
    {
        if (Deleted(sac) || sac.Comp.Status == SporeSacStatus.Deployed)
            return;

        if (!sac.Comp.SilentRelease)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-xeno-spore-sac-release"),
                sac, PopupType.LargeCaution);
        }

        var cloud = SpawnAtPosition(sac.Comp.CloudPrototype, Transform(sac).Coordinates);
        if (sac.Comp.Placer is { } placer)
            _hive.SetSameHive(placer, cloud);

        if (TryComp(cloud, out CMUPathogenSporeCloudComponent? cloudComp))
            cloudComp.SilentInhale = sac.Comp.SilentRelease;

        sac.Comp.Status = SporeSacStatus.Deployed;
        sac.Comp.RegenerateAt = _timing.CurTime + sac.Comp.RegenerationTime;
        Dirty(sac);
    }

    private void ResetSpores(Entity<CMUPathogenSporeSacComponent> sac)
    {
        sac.Comp.Status = SporeSacStatus.Waiting;
        sac.Comp.RegenerateAt = null;
        _appearance.SetData(sac, SporeSacVisuals.State, sac.Comp.Status);

        if (sac.Comp.MaxBatches != 0 && sac.Comp.SporeBatch >= sac.Comp.MaxBatches)
        {
            QueueDel(sac);
            return;
        }

        sac.Comp.SporeBatch++;
        Dirty(sac);
    }
}