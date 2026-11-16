using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Xenonids.Heal;
using Content.Shared.CMU14.Medical.Core;

namespace Content.Server.CMU14.Xenomorphs;

public sealed class CMUXenoHealerSystem : EntitySystem
{
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMUWoundsSystem _wounds = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoHumanSalveAppliedEvent>(OnHumanSalveApplied);
    }

    private void OnHumanSalveApplied(ref XenoHumanSalveAppliedEvent args)
    {
        foreach (var part in _medicalIndex.GetBodyParts(args.Patient))
            _wounds.StopSurfaceBleedingOnPart(part.Owner);
    }
}
