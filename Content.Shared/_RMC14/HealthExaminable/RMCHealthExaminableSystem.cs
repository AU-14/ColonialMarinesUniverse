using System.Collections.Immutable;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.HealthExaminable;

public sealed class RMCHealthExaminableSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    private readonly ImmutableArray<FixedPoint2> _thresholds = ImmutableArray.Create<FixedPoint2>(25, 50, 75);

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCHealthExaminableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<RMCHealthExaminableComponent> ent, ref ExaminedEvent args)
    {
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

                for (var i = _thresholds.Length - 1; i >= 0; i--)
                {
                    var threshold = _thresholds[i];
                    if (groupDamage < threshold)
                        continue;

                    var id = $"rmc-health-examinable-{group}-{threshold.Int()}";
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

        foreach (var (partUid, _) in _body.GetBodyChildren(body))
        {
            if (showBones
                && TryComp<FractureComponent>(partUid, out var fracture)
                && fracture.Severity != FractureSeverity.None)
            {
                brute = true;
            }

            if (showWounds
                && TryComp<BodyPartWoundComponent>(partUid, out var wounds)
                && wounds.Wounds.Count > 0)
            {
                foreach (var wound in wounds.Wounds)
                {
                    if (wound.Type == WoundType.Burn)
                        burn = true;
                    else
                        brute = true;
                }
            }

            if (showWounds && HasComp<CMUEscharComponent>(partUid))
                burn = true;

            if (brute && burn)
                break;
        }

        return (brute, burn);
    }
}
