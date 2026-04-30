using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared.Body.Part;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.BodyPart;

public abstract class SharedBodyZoneTargetingSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    private bool _medicalEnabled;
    private bool _hitLocationEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BodyZoneTargetSelectedMessage>(OnZoneSelected);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.HitLocationEnabled, v => _hitLocationEnabled = v, true);
    }

    private void OnZoneSelected(BodyZoneTargetSelectedMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } shooter)
            return;

        if (!TryComp<BodyZoneTargetingComponent>(shooter, out var aim))
            return;

        aim.Selected = msg.Zone;
        aim.LastSelectedAt = Timing.CurTime;
        Dirty(shooter, aim);
    }

    public TargetBodyZone? TryGetFreshSelection(Entity<BodyZoneTargetingComponent?> shooter)
    {
        if (!_medicalEnabled || !_hitLocationEnabled)
            return null;
        if (!Resolve(shooter.Owner, ref shooter.Comp, logMissing: false))
            return null;

        if (shooter.Comp.LastSelectedAt == TimeSpan.Zero)
            return null;

        return shooter.Comp.Selected;
    }

    public void SelectZone(Entity<BodyZoneTargetingComponent?> shooter, TargetBodyZone zone)
    {
        if (!Resolve(shooter.Owner, ref shooter.Comp, logMissing: false))
            return;

        shooter.Comp.Selected = zone;
        shooter.Comp.LastSelectedAt = Timing.CurTime;
        Dirty(shooter.Owner, shooter.Comp);
    }

    public static (BodyPartType Type, BodyPartSymmetry Symmetry) ToBodyPart(TargetBodyZone zone) => zone switch
    {
        TargetBodyZone.Head => (BodyPartType.Head, BodyPartSymmetry.None),
        TargetBodyZone.Chest => (BodyPartType.Torso, BodyPartSymmetry.None),
        TargetBodyZone.GroinPelvis => (BodyPartType.Torso, BodyPartSymmetry.None),
        TargetBodyZone.LeftArm => (BodyPartType.Arm, BodyPartSymmetry.Left),
        TargetBodyZone.RightArm => (BodyPartType.Arm, BodyPartSymmetry.Right),
        TargetBodyZone.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
        TargetBodyZone.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
        _ => (BodyPartType.Torso, BodyPartSymmetry.None),
    };
}
