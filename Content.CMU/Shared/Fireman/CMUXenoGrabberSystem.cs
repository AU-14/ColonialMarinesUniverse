using Content.Shared._RMC14.Fireman;

namespace Content.Shared.CMU14.Fireman;

/// <summary>
/// FLOW-02: a <see cref="CMUXenoGrabberComponent"/> holder is not punished by RMC's pull-paralyze for pulling a caste it
/// is allowed to grab (T3s such as the praetorian, boiler and despoiler, never the heavies), so the pull that starts
/// the grab can happen at all. Shared, so the client predicts the same pull.
/// </summary>
public sealed class CMUXenoGrabberSystem : EntitySystem
{
    [Dependency] private FiremanCarrySystem _fireman = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUXenoGrabberComponent, CMUPullParalyzeImmuneEvent>(OnPullParalyzeImmune);
    }

    private void OnPullParalyzeImmune(Entity<CMUXenoGrabberComponent> ent, ref CMUPullParalyzeImmuneEvent args)
    {
        if (_fireman.IsGrabbableXenoBody(ent.Comp, args.Target))
            args.Immune = true;
    }
}
