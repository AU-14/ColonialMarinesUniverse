using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Diagnostics.Examine;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.HealthExaminable;

public sealed partial class RMCHealthExaminableSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMUMedicalExamineProjectionSystem _woundProjection = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    private static readonly FixedPoint2[] Thresholds = new FixedPoint2[]
    {
        FixedPoint2.New(25),
        FixedPoint2.New(50),
        FixedPoint2.New(75),
        FixedPoint2.New(100),
        FixedPoint2.New(200),
        FixedPoint2.New(300),
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCHealthExaminableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<RMCHealthExaminableComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.SpeciesType == null)
            return;

        if (!TryComp(ent, out DamageableComponent? damageable))
            return;

        using (args.PushGroup(nameof(RMCHealthExaminableSystem), -1))
        {
            (bool Brute, bool Burn) suppress = HasComp<CMUHumanMedicalComponent>(ent)
                ? GetCmuLocalizedSuppressions(ent)
                : default;

            foreach (var group in ent.Comp.Groups)
            {
                if ((group == BruteGroup && suppress.Brute) || (group == BurnGroup && suppress.Burn))
                    continue;

                if (!damageable.DamagePerGroup.TryGetValue(group, out var groupDamage))
                    continue;

                for (var i = Thresholds.Length - 1; i >= 0; i--)
                {
                    var threshold = Thresholds[i];
                    if (groupDamage < threshold)
                        continue;

                    var id = $"rmc-health-examinable-{ent.Comp.SpeciesType}-{group}-{threshold.Int()}";
                    if (!Loc.TryGetString(id, out var msg, ("target", Identity.Entity(ent, EntityManager, args.Examiner))))
                        continue;

                    args.PushMarkup(msg);
                    break;
                }
            }
        }
    }

    private (bool Brute, bool Burn) GetCmuLocalizedSuppressions(EntityUid body)
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled))
            return default;

        var showBones = _cfg.GetCVar(CMUMedicalCCVars.BoneEnabled);
        var showWounds = _cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
        var brute = false;
        var burn = false;
        if (showWounds && TryComp<CMUMedicalExamineProjectionComponent>(body, out var projection))
        {
            brute = _woundProjection.GetRemainingDamage(projection, WoundType.Brute) > FixedPoint2.Zero;
            burn = _woundProjection.GetRemainingDamage(projection, WoundType.Burn) > FixedPoint2.Zero;
        }

        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(body))
        {
            if (showBones
                && TryComp<FractureComponent>(partUid, out var fracture)
                && fracture.Severity != FractureSeverity.None)
            {
                brute = true;
            }

            if (showWounds && HasComp<CMUEscharComponent>(partUid))
                burn = true;

            if (brute && burn)
                break;
        }

        return (brute, burn);
    }
}
