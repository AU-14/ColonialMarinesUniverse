using Content.Shared.CMU14.Xenomorphs;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Xenomorphs;

public sealed class CMUXenoCloudVisionSystem : EntitySystem
{
    private static readonly TimeSpan RecoveryDuration = TimeSpan.FromSeconds(30);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedNightVisionSystem _nightVision = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SmokeComponent, StartCollideEvent>(OnSmokeStartCollide);
        SubscribeLocalEvent<SmokeComponent, EndCollideEvent>(OnSmokeEndCollide);
        SubscribeLocalEvent<SmokeComponent, ComponentShutdown>(OnSmokeShutdown);
        SubscribeLocalEvent<CMUXenoVisionObscuringCloudComponent, StartCollideEvent>(OnMarkedCloudStartCollide);
        SubscribeLocalEvent<CMUXenoVisionObscuringCloudComponent, EndCollideEvent>(OnMarkedCloudEndCollide);
        SubscribeLocalEvent<CMUXenoVisionObscuringCloudComponent, ComponentShutdown>(OnMarkedCloudShutdown);
        SubscribeLocalEvent<XenoComponent, DamageChangedEvent>(OnXenoDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CMUXenoCloudVisionObscuredComponent>();
        var now = _timing.CurTime;
        while (query.MoveNext(out var uid, out var obscured))
        {
            if (obscured.Clouds.Count > 0 || obscured.RestoreAt == TimeSpan.Zero || now < obscured.RestoreAt)
                continue;

            RestoreVision(uid, obscured);
        }
    }

    private void OnSmokeStartCollide(Entity<SmokeComponent> cloud, ref StartCollideEvent args)
    {
        StartCloudExposure(args.OtherEntity, cloud.Owner);
    }

    private void OnSmokeEndCollide(Entity<SmokeComponent> cloud, ref EndCollideEvent args)
    {
        EndCloudExposure(args.OtherEntity, cloud.Owner);
    }

    private void OnSmokeShutdown(Entity<SmokeComponent> cloud, ref ComponentShutdown args)
    {
        EndCloudForNearbyXenos(cloud.Owner);
    }

    private void OnMarkedCloudStartCollide(Entity<CMUXenoVisionObscuringCloudComponent> cloud, ref StartCollideEvent args)
    {
        StartCloudExposure(args.OtherEntity, cloud.Owner);
    }

    private void OnMarkedCloudEndCollide(Entity<CMUXenoVisionObscuringCloudComponent> cloud, ref EndCollideEvent args)
    {
        EndCloudExposure(args.OtherEntity, cloud.Owner);
    }

    private void OnMarkedCloudShutdown(Entity<CMUXenoVisionObscuringCloudComponent> cloud, ref ComponentShutdown args)
    {
        EndCloudForNearbyXenos(cloud.Owner);
    }

    private void OnXenoDamageChanged(Entity<XenoComponent> xeno, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;

        foreach (var damage in args.DamageDelta.DamageDict.Values)
        {
            if (damage >= 0)
                continue;

            if (TryComp<CMUXenoCloudVisionObscuredComponent>(xeno, out var obscured))
                RestoreVision(xeno, obscured);
            return;
        }
    }

    private void StartCloudExposure(EntityUid target, EntityUid cloud)
    {
        if (!HasComp<XenoComponent>(target) ||
            !TryComp<NightVisionComponent>(target, out var nightVision))
        {
            return;
        }

        var now = _timing.CurTime;
        if (TryComp<CMUXenoCloudVisionObscuredComponent>(target, out var existing) &&
            existing.Clouds.Count == 0 &&
            existing.RestoreAt != TimeSpan.Zero &&
            now >= existing.RestoreAt)
        {
            RestoreVision(target, existing);
        }

        if (!TryComp<CMUXenoCloudVisionObscuredComponent>(target, out var obscured))
        {
            obscured = AddComp<CMUXenoCloudVisionObscuredComponent>(target);
            obscured.DrawVisibleMobs = nightVision.DrawVisibleMobs;
        }

        obscured.Clouds.Add(cloud);
        obscured.RestoreAt = TimeSpan.Zero;
        _nightVision.SetDrawVisibleMobs((target, nightVision), false);
    }

    private void EndCloudExposure(EntityUid target, EntityUid cloud)
    {
        if (!TryComp<CMUXenoCloudVisionObscuredComponent>(target, out var obscured) ||
            !obscured.Clouds.Remove(cloud))
        {
            return;
        }

        if (obscured.Clouds.Count == 0)
            obscured.RestoreAt = _timing.CurTime + RecoveryDuration;
    }

    private void EndCloudForNearbyXenos(EntityUid cloud)
    {
        var query = EntityQueryEnumerator<CMUXenoCloudVisionObscuredComponent>();
        while (query.MoveNext(out var uid, out var obscured))
            EndCloudExposure(uid, cloud);
    }

    private void RestoreVision(EntityUid xeno, CMUXenoCloudVisionObscuredComponent obscured)
    {
        _nightVision.SetDrawVisibleMobs((xeno, null), obscured.DrawVisibleMobs);
        RemComp(xeno, obscured);
    }
}

[RegisterComponent]
[Access(typeof(CMUXenoCloudVisionSystem))]
public sealed partial class CMUXenoCloudVisionObscuredComponent : Component
{
    public readonly HashSet<EntityUid> Clouds = new();

    public TimeSpan RestoreAt;

    public bool DrawVisibleMobs = true;
}
