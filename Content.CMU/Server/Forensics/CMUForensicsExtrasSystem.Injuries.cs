using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Forensics;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server.CMU14.Forensics;

public sealed partial class CMUForensicsExtrasSystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

    /// <summary>Below this share of normal blood volume, blood loss is the cause of death.</summary>
    private const float FatalBloodLevel = 0.45f;

    private static readonly (BodyPartType Type, BodyPartSymmetry Symmetry)[] ExpectedParts =
    {
        (BodyPartType.Head, BodyPartSymmetry.None),
        (BodyPartType.Arm, BodyPartSymmetry.Left),
        (BodyPartType.Arm, BodyPartSymmetry.Right),
        (BodyPartType.Hand, BodyPartSymmetry.Left),
        (BodyPartType.Hand, BodyPartSymmetry.Right),
        (BodyPartType.Leg, BodyPartSymmetry.Left),
        (BodyPartType.Leg, BodyPartSymmetry.Right),
        (BodyPartType.Foot, BodyPartSymmetry.Left),
        (BodyPartType.Foot, BodyPartSymmetry.Right),
    };

    private string GetCauseOfDeath(EntityUid target)
    {
        if (!_mobState.IsDead(target))
            return string.Empty;

        if (TryComp(target, out CMUTimeOfDeathComponent? death) && !string.IsNullOrEmpty(death.Cause))
            return death.Cause;

        return DetermineCauseOfDeath(target);
    }

    private string DetermineCauseOfDeath(EntityUid body)
    {
        var parts = _medicalIndex.GetBodyParts(body).ToList();
        if (parts.Any(p => p.Comp.PartType == BodyPartType.Torso) &&
            parts.All(p => p.Comp.PartType != BodyPartType.Head))
        {
            return Loc.GetString("cmu-forensics-cause-decapitation");
        }

        if (TryComp(body, out BloodstreamComponent? blood) &&
            _bloodstream.GetBloodLevel((body, blood)) < FatalBloodLevel)
        {
            return Loc.GetString("cmu-forensics-cause-blood-loss");
        }

        if (!TryComp(body, out DamageableComponent? damageable))
            return Loc.GetString("cmu-forensics-cause-unknown");

        var damage = _damageable.GetAllDamage((body, damageable));
        var worst = damage.DamageDict
            .Where(d => d.Value > FixedPoint2.Zero)
            .OrderByDescending(d => d.Value)
            .Select(d => (string?) d.Key.Id)
            .FirstOrDefault();

        if (worst == null)
            return Loc.GetString("cmu-forensics-cause-unknown");

        // Physical trauma: the wounds say what did it better than the damage type does
        if (worst is "Blunt" or "Slash" or "Piercing" && GetDominantMechanism(parts) is { } mechanism)
        {
            var byMechanism = mechanism switch
            {
                WoundMechanism.Bullet => "cmu-forensics-cause-gunshot",
                WoundMechanism.Blast or WoundMechanism.Fragment => "cmu-forensics-cause-explosive",
                WoundMechanism.Stab => "cmu-forensics-cause-stab",
                WoundMechanism.Slash => "cmu-forensics-cause-laceration",
                WoundMechanism.Crush => "cmu-forensics-cause-blunt",
                WoundMechanism.Burn => "cmu-forensics-cause-burns",
                _ => null,
            };

            if (byMechanism != null)
                return Loc.GetString(byMechanism);
        }

        return Loc.GetString(worst switch
        {
            "Blunt" => "cmu-forensics-cause-blunt",
            "Slash" => "cmu-forensics-cause-laceration",
            "Piercing" => "cmu-forensics-cause-piercing",
            "Heat" => "cmu-forensics-cause-burns",
            "Cold" => "cmu-forensics-cause-hypothermia",
            "Shock" => "cmu-forensics-cause-electrocution",
            "Caustic" => "cmu-forensics-cause-chemical-burns",
            "Asphyxiation" => "cmu-forensics-cause-asphyxiation",
            "Bloodloss" => "cmu-forensics-cause-blood-loss",
            "Poison" => "cmu-forensics-cause-poisoning",
            "Radiation" => "cmu-forensics-cause-radiation",
            "Cellular" or "Genetic" => "cmu-forensics-cause-cellular",
            _ => "cmu-forensics-cause-unknown",
        });
    }

    private WoundMechanism? GetDominantMechanism(List<Entity<BodyPartComponent>> parts)
    {
        var totals = new Dictionary<WoundMechanism, float>();
        foreach (var part in parts)
        {
            if (!TryComp(part, out BodyPartWoundComponent? wounds))
                continue;

            foreach (var entry in _woundLedger.GetEntries(wounds))
            {
                if (entry.Mechanism is WoundMechanism.Generic or WoundMechanism.Surgical)
                    continue;

                totals[entry.Mechanism] = totals.GetValueOrDefault(entry.Mechanism) + entry.Wound.Damage.Float();
            }
        }

        return totals.Count == 0 ? null : totals.MaxBy(t => t.Value).Key;
    }

    private List<CMUForensicInjury> BuildInjuryReport(EntityUid body)
    {
        var report = new List<CMUForensicInjury>();
        var parts = _medicalIndex.GetBodyParts(body).ToList();
        if (parts.Count == 0)
            return report;

        // Head to feet, left before right
        foreach (var part in parts
                     .OrderBy(p => PartOrder(p.Comp.PartType))
                     .ThenBy(p => p.Comp.Symmetry))
        {
            var location = PartName(part.Comp.PartType, part.Comp.Symmetry);

            if (TryComp(part, out BodyPartWoundComponent? wounds))
            {
                // Group identical wounds so ten bullet holes read as one line
                var groups = _woundLedger.GetEntries(wounds)
                    .GroupBy(e => (e.Mechanism, Category: WoundSizeProfile.Category(e.Size)));

                foreach (var group in groups)
                {
                    var worst = group.MaxBy(e => WoundSizeProfile.SeverityRank(e.Size, e.Wound.Damage.Float()));
                    var rank = WoundSizeProfile.SeverityRank(worst.Size, worst.Wound.Damage.Float());
                    var treated = group.All(e => e.Bandages > 0 || e.Wound.Treated);

                    var description = Loc.GetString("cmu-forensics-injury-wound",
                        ("count", group.Count()),
                        ("kind", WoundKind(group.Key.Mechanism, worst.Size)),
                        ("worst", WoundSizeProfile.TierName(worst.Size)),
                        ("treated", treated ? "yes" : "no"));

                    report.Add(new CMUForensicInjury(location, description, RankToSeverity(rank)));
                }

                var bleedTier = wounds.ExternalBleeding;
                if (bleedTier != ExternalBleedTier.None)
                {
                    report.Add(new CMUForensicInjury(location,
                        Loc.GetString("cmu-forensics-injury-bleeding",
                            ("tier", bleedTier.ToString().ToLowerInvariant())),
                        bleedTier >= ExternalBleedTier.Severe
                            ? CMUForensicInjurySeverity.Critical
                            : CMUForensicInjurySeverity.Moderate));
                }
            }

            if (TryComp(part, out InternalBleedingComponent? _))
            {
                report.Add(new CMUForensicInjury(location,
                    Loc.GetString("cmu-forensics-injury-internal-bleeding"),
                    CMUForensicInjurySeverity.Critical));
            }

            var fractureSeverity = CompOrNull<FractureComponent>(part)?.Severity ?? FractureSeverity.None;
            if (fractureSeverity != FractureSeverity.None)
            {
                report.Add(new CMUForensicInjury(location,
                    Loc.GetString("cmu-forensics-injury-fracture",
                        ("severity", fractureSeverity.ToString().ToLowerInvariant())),
                    fractureSeverity switch
                    {
                        FractureSeverity.Hairline => CMUForensicInjurySeverity.Minor,
                        FractureSeverity.Simple => CMUForensicInjurySeverity.Moderate,
                        FractureSeverity.Compound => CMUForensicInjurySeverity.Severe,
                        _ => CMUForensicInjurySeverity.Critical,
                    }));
            }

            if (TryComp(part, out CMUShrapnelComponent? shrapnel) && shrapnel.Fragments > 0)
            {
                report.Add(new CMUForensicInjury(location,
                    Loc.GetString("cmu-forensics-injury-shrapnel", ("count", shrapnel.Fragments)),
                    shrapnel.Fragments >= 5 ? CMUForensicInjurySeverity.Severe : CMUForensicInjurySeverity.Moderate));
            }
        }

        // Missing limbs, only for bodies laid out like a person
        if (parts.Any(p => p.Comp.PartType == BodyPartType.Torso))
        {
            foreach (var (type, symmetry) in ExpectedParts)
            {
                if (parts.Any(p => p.Comp.PartType == type && p.Comp.Symmetry == symmetry))
                    continue;

                report.Add(new CMUForensicInjury(PartName(type, symmetry),
                    Loc.GetString("cmu-forensics-injury-missing"),
                    CMUForensicInjurySeverity.Missing));
            }
        }

        return report;
    }

    private string WoundKind(WoundMechanism mechanism, WoundSize size)
    {
        var key = mechanism switch
        {
            WoundMechanism.Bullet => "cmu-forensics-wound-gunshot",
            WoundMechanism.Stab => "cmu-forensics-wound-stab",
            WoundMechanism.Slash => "cmu-forensics-wound-laceration",
            WoundMechanism.Crush => "cmu-forensics-wound-blunt",
            WoundMechanism.Burn => "cmu-forensics-wound-burn",
            WoundMechanism.Blast => "cmu-forensics-wound-blast",
            WoundMechanism.Fragment => "cmu-forensics-wound-shrapnel",
            WoundMechanism.Surgical => "cmu-forensics-wound-surgical",
            _ => WoundSizeProfile.Category(size) switch
            {
                WoundCategory.Burn => "cmu-forensics-wound-burn",
                WoundCategory.Bruise => "cmu-forensics-wound-bruise",
                WoundCategory.LostLimb => "cmu-forensics-wound-stump",
                _ => "cmu-forensics-wound-generic",
            },
        };

        return Loc.GetString(key);
    }

    private static CMUForensicInjurySeverity RankToSeverity(int rank) => rank switch
    {
        <= 0 => CMUForensicInjurySeverity.Minor,
        1 => CMUForensicInjurySeverity.Moderate,
        2 => CMUForensicInjurySeverity.Severe,
        _ => CMUForensicInjurySeverity.Critical,
    };

    private static int PartOrder(BodyPartType type) => type switch
    {
        BodyPartType.Head => 0,
        BodyPartType.Torso => 1,
        BodyPartType.Arm => 2,
        BodyPartType.Hand => 3,
        BodyPartType.Leg => 4,
        BodyPartType.Foot => 5,
        _ => 6,
    };

    private string PartName(BodyPartType type, BodyPartSymmetry symmetry)
    {
        var typeName = Loc.GetString(type switch
        {
            BodyPartType.Torso => "cmu-medical-body-part-type-torso",
            BodyPartType.Head => "cmu-medical-body-part-type-head",
            BodyPartType.Arm => "cmu-medical-body-part-type-arm",
            BodyPartType.Hand => "cmu-medical-body-part-type-hand",
            BodyPartType.Leg => "cmu-medical-body-part-type-leg",
            BodyPartType.Foot => "cmu-medical-body-part-type-foot",
            BodyPartType.Tail => "cmu-medical-body-part-type-tail",
            _ => "cmu-medical-body-part-type-other",
        });

        var side = symmetry switch
        {
            BodyPartSymmetry.Left => "cmu-medical-body-part-side-left",
            BodyPartSymmetry.Right => "cmu-medical-body-part-side-right",
            _ => null,
        };

        return side == null
            ? typeName
            : Loc.GetString("cmu-medical-body-part-sided", ("side", Loc.GetString(side)), ("type", typeName));
    }
}
