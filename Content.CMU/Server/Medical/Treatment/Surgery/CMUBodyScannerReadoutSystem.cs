using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUBodyScannerReadoutSystem : EntitySystem
{
    [Dependency] private CMUMedicalChangeSystem _medicalChanges = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedRMCBloodstreamSystem _bloodstream = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;
    [Dependency] private IGameTiming _timing = default!;

    public List<CMUBodyScannerScanLine> BuildScanLines(EntityUid patient)
    {
        var lines = new List<CMUBodyScannerScanLine>();
        if (TryComp<MobStateComponent>(patient, out var mob))
        {
            var state = mob.CurrentState;
            var severity = state switch
            {
                MobState.Dead => CMUBodyScannerScanSeverity.Critical,
                MobState.Critical => CMUBodyScannerScanSeverity.Warning,
                _ => CMUBodyScannerScanSeverity.Stable,
            };
            lines.Add(ScanLine(
                CMUBodyScannerScanCategory.Vitals,
                CMUBodyScannerScanKind.State,
                severity,
                Loc.GetString("cmu-body-scanner-title-state"),
                state.ToString()));
        }

        if (TryComp<DamageableComponent>(patient, out var damageable))
        {
            var total = damageable.TotalDamage;
            var severity = total <= FixedPoint2.Zero
                ? CMUBodyScannerScanSeverity.Stable
                : total >= FixedPoint2.New(100)
                    ? CMUBodyScannerScanSeverity.Critical
                    : CMUBodyScannerScanSeverity.Warning;
            lines.Add(ScanLine(
                CMUBodyScannerScanCategory.Vitals,
                CMUBodyScannerScanKind.Damage,
                severity,
                Loc.GetString("cmu-body-scanner-title-damage"),
                Loc.GetString(
                    "cmu-body-scanner-detail-damage",
                    ("total", damageable.TotalDamage),
                    ("brute", damageable.DamagePerGroup.GetValueOrDefault("Brute")),
                    ("burn", damageable.DamagePerGroup.GetValueOrDefault("Burn")))));
        }

        if (_bloodstream.TryGetBloodSolution(patient, out var blood))
        {
            var ratio = blood.MaxVolume > FixedPoint2.Zero
                ? blood.Volume.Float() / blood.MaxVolume.Float()
                : 0f;
            var severity = ratio < 0.4f
                ? CMUBodyScannerScanSeverity.Critical
                : ratio < 0.75f
                    ? CMUBodyScannerScanSeverity.Warning
                    : CMUBodyScannerScanSeverity.Stable;
            lines.Add(ScanLine(
                CMUBodyScannerScanCategory.Vitals,
                CMUBodyScannerScanKind.Blood,
                severity,
                Loc.GetString("cmu-body-scanner-title-blood"),
                Loc.GetString("cmu-body-scanner-detail-blood", ("blood", blood.Volume), ("max", blood.MaxVolume)),
                hasRange: true,
                current: blood.Volume.Float(),
                maximum: blood.MaxVolume.Float()));
        }

        if (_medicalIndex.TryGetOrgan<HeartComponent>(patient, out var heartId) &&
            TryComp<HeartComponent>(heartId, out var heart))
        {
            var severity = heart.Stopped
                ? CMUBodyScannerScanSeverity.Critical
                : CMUBodyScannerScanSeverity.Stable;
            var detail = heart.Stopped
                ? Loc.GetString("cmu-body-scanner-detail-heart-stopped")
                : Loc.GetString("cmu-body-scanner-detail-heart-active", ("bpm", heart.BeatsPerMinute));
            lines.Add(ScanLine(
                CMUBodyScannerScanCategory.Vitals,
                CMUBodyScannerScanKind.Heart,
                severity,
                Loc.GetString("cmu-body-scanner-title-heart"),
                detail));
        }

        AddAnatomyLines(patient, lines);

        if (lines.Count == 0)
        {
            lines.Add(ScanLine(
                CMUBodyScannerScanCategory.Vitals,
                CMUBodyScannerScanKind.NoData,
                CMUBodyScannerScanSeverity.Stable,
                Loc.GetString("cmu-body-scanner-title-no-data"),
                Loc.GetString("cmu-body-scanner-detail-no-data")));
        }

        return lines;
    }

    private void AddAnatomyLines(EntityUid patient, List<CMUBodyScannerScanLine> lines)
    {
        var cache = EnsureComp<CMUBodyScannerAnatomyCacheComponent>(patient);
        var revision = _medicalChanges.GetRevision(patient);
        var tick = _timing.CurTick;
        if (!cache.Valid || cache.MedicalRevision != revision || cache.BuiltAt != tick)
        {
            cache.Lines.Clear();
            AddPartLines(patient, cache.Lines);
            AddOrganLines(patient, cache.Lines);
            cache.MedicalRevision = revision;
            cache.BuiltAt = tick;
            cache.Valid = true;
        }

        lines.AddRange(cache.Lines);
    }

    public List<(BodyPartType Type, BodyPartSymmetry Symmetry)> GetMissingLimbSlots(EntityUid patient)
    {
        var missing = new List<(BodyPartType Type, BodyPartSymmetry Symmetry)>();
        if (!_medicalIndex.TryGetRootPart(patient, out var root))
            return missing;

        foreach (var slot in _medicalIndex.GetBodyPartSlots(root.Owner))
        {
            if (slot.Type is not (BodyPartType.Arm or BodyPartType.Leg))
                continue;

            if (!CMUBodyPartSlots.TryGetSymmetry(slot.SlotId, BodyPartSymmetry.None, out var symmetry))
                continue;

            if (slot.Part is null)
                missing.Add((slot.Type, symmetry));
        }

        return missing;
    }

    public string OrganName(EntityUid organ)
    {
        var meta = MetaData(organ);
        if (meta.EntityPrototype?.ID is { } protoId && OrganDisplayName(protoId) is { } protoName)
            return protoName;

        var name = Name(organ);
        return string.IsNullOrWhiteSpace(name)
            ? CapitalizeFirst(meta.EntityPrototype?.ID ?? organ.ToString())
            : CapitalizeFirst(name);
    }

    public string OrganSlotName(string slotId)
    {
        return OrganDisplayName(slotId) ?? CapitalizeFirst(slotId);
    }

    public static string FormatOrganStage(OrganDamageStage stage)
    {
        return CapitalizeFirst(stage.ToString());
    }

    private void AddPartLines(EntityUid patient, List<CMUBodyScannerScanLine> lines)
    {
        foreach (var (part, partComp) in _medicalIndex.GetBodyParts(patient))
        {
            var details = new List<string>();
            var severity = CMUBodyScannerScanSeverity.Stable;
            var hasRange = false;
            var current = 0f;
            var maximum = 0f;
            if (TryComp<BodyPartHealthComponent>(part, out var health))
            {
                var partCurrent = health.Current;
                var partMaximum = health.Max;
                hasRange = true;
                current = partCurrent.Float();
                maximum = partMaximum.Float();
                severity = RangeSeverity(current, maximum);
            }

            if (TryComp<BodyPartWoundComponent>(part, out var wounds))
            {
                var untreated = _woundLedger.CountUntreatedWounds(wounds);
                if (untreated > 0)
                {
                    details.Add(Loc.GetString("cmu-body-scanner-part-wounds", ("count", untreated)));
                    severity = MaxSeverity(severity, CMUBodyScannerScanSeverity.Warning);
                }
            }

            if (TryComp<FractureComponent>(part, out var fracture) && fracture.Severity != FractureSeverity.None)
            {
                details.Add(Loc.GetString("cmu-body-scanner-part-fracture", ("severity", fracture.Severity)));
                severity = MaxSeverity(severity, CMUBodyScannerScanSeverity.Warning);
            }

            if (TryComp<InternalBleedingComponent>(part, out var bleed))
            {
                details.Add(Loc.GetString("cmu-body-scanner-part-bleed", ("rate", bleed.BloodlossPerSecond)));
                severity = CMUBodyScannerScanSeverity.Critical;
            }

            if (HasComp<CMUEscharComponent>(part))
            {
                details.Add(Loc.GetString("cmu-body-scanner-part-eschar"));
                severity = MaxSeverity(severity, CMUBodyScannerScanSeverity.Warning);
            }
            if (HasComp<CMUSplintedComponent>(part))
                details.Add(Loc.GetString("cmu-body-scanner-part-splinted"));
            if (HasComp<CMUCastComponent>(part))
                details.Add(Loc.GetString("cmu-body-scanner-part-cast"));
            if (HasComp<CMUTourniquetComponent>(part))
            {
                details.Add(Loc.GetString("cmu-body-scanner-part-tourniquet"));
                severity = MaxSeverity(severity, CMUBodyScannerScanSeverity.Warning);
            }

            if (details.Count == 0 && !hasRange)
                continue;

            lines.Add(new CMUBodyScannerScanLine(
                CMUBodyScannerScanCategory.Body,
                CMUBodyScannerScanKind.BodyPart,
                severity,
                SharedCMUSurgeryFlowSystem.FormatPartName(partComp.PartType, partComp.Symmetry),
                string.Join(", ", details),
                details,
                hasRange,
                current,
                maximum));
        }

        foreach (var (type, symmetry) in GetMissingLimbSlots(patient))
        {
            var missing = Loc.GetString("cmu-body-scanner-part-missing-limb");
            lines.Add(new CMUBodyScannerScanLine(
                CMUBodyScannerScanCategory.Body,
                CMUBodyScannerScanKind.MissingLimb,
                CMUBodyScannerScanSeverity.Critical,
                SharedCMUSurgeryFlowSystem.FormatPartName(type, symmetry),
                missing,
                [missing],
                false,
                0f,
                0f));
        }
    }

    private void AddOrganLines(EntityUid patient, List<CMUBodyScannerScanLine> lines)
    {
        foreach (var organ in _medicalIndex.GetOrgans(patient))
        {
            if (!TryComp<OrganHealthComponent>(organ.Owner, out var health))
                continue;

            var severity = health.Stage switch
            {
                OrganDamageStage.Dead or OrganDamageStage.Failing => CMUBodyScannerScanSeverity.Critical,
                OrganDamageStage.Damaged or OrganDamageStage.Bruised => CMUBodyScannerScanSeverity.Warning,
                _ => CMUBodyScannerScanSeverity.Stable,
            };
            var organCurrent = health.Current;
            var organMaximum = health.Max;
            lines.Add(ScanLine(
                CMUBodyScannerScanCategory.Organs,
                CMUBodyScannerScanKind.Organ,
                severity,
                OrganName(organ.Owner),
                Loc.GetString(
                "cmu-body-scanner-detail-organ",
                ("stage", FormatOrganStage(health.Stage)),
                ("current", health.Current),
                ("max", health.Max)),
                hasRange: true,
                current: organCurrent.Float(),
                maximum: organMaximum.Float()));
        }

        foreach (var (part, partComp) in _medicalIndex.GetBodyParts(patient))
        {
            foreach (var slot in _medicalIndex.GetOrganSlots(part))
            {
                if (slot.Organ is not null)
                    continue;

                lines.Add(ScanLine(
                    CMUBodyScannerScanCategory.Organs,
                    CMUBodyScannerScanKind.MissingOrgan,
                    CMUBodyScannerScanSeverity.Critical,
                    Loc.GetString("cmu-body-scanner-title-missing-organ", ("organ", OrganSlotName(slot.SlotId))),
                    Loc.GetString(
                        "cmu-body-scanner-detail-missing-organ",
                        ("part", SharedCMUSurgeryFlowSystem.FormatPartName(partComp.PartType, partComp.Symmetry)))));
            }
        }
    }

    private static CMUBodyScannerScanLine ScanLine(
        CMUBodyScannerScanCategory category,
        CMUBodyScannerScanKind kind,
        CMUBodyScannerScanSeverity severity,
        string title,
        string detail,
        List<string>? details = null,
        bool hasRange = false,
        float current = 0f,
        float maximum = 0f)
    {
        return new CMUBodyScannerScanLine(
            category,
            kind,
            severity,
            title,
            detail,
            details ?? [],
            hasRange,
            current,
            maximum);
    }

    private static CMUBodyScannerScanSeverity RangeSeverity(float current, float maximum)
    {
        if (maximum <= 0f || current <= 0f)
            return CMUBodyScannerScanSeverity.Critical;

        var ratio = current / maximum;
        if (ratio < 0.35f)
            return CMUBodyScannerScanSeverity.Critical;

        return ratio < 0.75f
            ? CMUBodyScannerScanSeverity.Warning
            : CMUBodyScannerScanSeverity.Stable;
    }

    private static CMUBodyScannerScanSeverity MaxSeverity(
        CMUBodyScannerScanSeverity left,
        CMUBodyScannerScanSeverity right)
    {
        return left >= right ? left : right;
    }

    private string? OrganDisplayName(string idOrSlot)
    {
        return idOrSlot switch
        {
            "CMUOrganHumanHeart" or "heart" => Loc.GetString("cmu-medical-scanner-organ-heart"),
            "CMUOrganHumanLungs" or "lungs" => Loc.GetString("cmu-medical-scanner-organ-lungs"),
            "CMUOrganHumanLiver" or "liver" => Loc.GetString("cmu-medical-scanner-organ-liver"),
            "CMUOrganHumanBrain" or "brain" => Loc.GetString("cmu-medical-scanner-organ-brain"),
            "CMUOrganHumanKidneys" or "kidneys" => Loc.GetString("cmu-medical-scanner-organ-kidneys"),
            "CMUOrganHumanStomach" or "stomach" => Loc.GetString("cmu-medical-scanner-organ-stomach"),
            "CMUOrganHumanEyes" or "eyes" => Loc.GetString("cmu-medical-scanner-organ-eyes"),
            _ => null,
        };
    }

    private static string CapitalizeFirst(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}
