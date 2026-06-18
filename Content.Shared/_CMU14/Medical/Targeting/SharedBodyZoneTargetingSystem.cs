using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Targeting.Events;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.Body.Part;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Targeting;

public abstract partial class SharedBodyZoneTargetingSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;

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

    public TargetBodyZone? TryGetSelectedZone(Entity<BodyZoneTargetingComponent?> shooter)
    {
        if (!_medicalEnabled || !_hitLocationEnabled)
            return null;
        if (!Resolve(shooter.Owner, ref shooter.Comp, logMissing: false))
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
        TargetBodyZone.LeftHand => (BodyPartType.Hand, BodyPartSymmetry.Left),
        TargetBodyZone.RightHand => (BodyPartType.Hand, BodyPartSymmetry.Right),
        TargetBodyZone.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
        TargetBodyZone.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
        TargetBodyZone.LeftFoot => (BodyPartType.Foot, BodyPartSymmetry.Left),
        TargetBodyZone.RightFoot => (BodyPartType.Foot, BodyPartSymmetry.Right),
        _ => (BodyPartType.Torso, BodyPartSymmetry.None),
    };

    public static BodyRegion ToBodyRegion(TargetBodyZone zone) => zone switch
    {
        TargetBodyZone.Head => BodyRegion.Head,
        TargetBodyZone.Chest => BodyRegion.Chest,
        TargetBodyZone.GroinPelvis => BodyRegion.Groin,
        TargetBodyZone.LeftArm => BodyRegion.LeftArm,
        TargetBodyZone.RightArm => BodyRegion.RightArm,
        TargetBodyZone.LeftHand => BodyRegion.LeftHand,
        TargetBodyZone.RightHand => BodyRegion.RightHand,
        TargetBodyZone.LeftLeg => BodyRegion.LeftLeg,
        TargetBodyZone.RightLeg => BodyRegion.RightLeg,
        TargetBodyZone.LeftFoot => BodyRegion.LeftFoot,
        TargetBodyZone.RightFoot => BodyRegion.RightFoot,
        _ => BodyRegion.Chest,
    };
}
