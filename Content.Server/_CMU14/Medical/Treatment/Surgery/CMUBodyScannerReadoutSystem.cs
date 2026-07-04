using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUBodyScannerReadoutSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedRMCBloodstreamSystem _bloodstream = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

    public List<CMUBodyScannerScanLine> BuildScanLines(EntityUid patient)
    {
        var lines = new List<CMUBodyScannerScanLine>();
        if (TryComp<MobStateComponent>(patient, out var mob))
            lines.Add(VitalsLine(Loc.GetString("cmu-body-scanner-line-state", ("state", mob.CurrentState))));

        if (TryComp<DamageableComponent>(patient, out var damageable))
        {
            lines.Add(VitalsLine(Loc.GetString(
                "cmu-body-scanner-line-damage",
                ("total", damageable.TotalDamage),
                ("brute", damageable.DamagePerGroup.GetValueOrDefault("Brute")),
                ("burn", damageable.DamagePerGroup.GetValueOrDefault("Burn")))));
        }

        if (_bloodstream.TryGetBloodSolution(patient, out var blood))
            lines.Add(VitalsLine(Loc.GetString("cmu-body-scanner-line-blood", ("blood", blood.Volume), ("max", blood.MaxVolume))));

        foreach (var organ in _body.GetBodyOrgans(patient))
        {
            if (TryComp<HeartComponent>(organ.Id, out var heart))
            {
                var state = heart.Stopped
                    ? Loc.GetString("cmu-body-scanner-heart-stopped")
                    : Loc.GetString("cmu-body-scanner-heart-active", ("bpm", heart.BeatsPerMinute));
                lines.Add(VitalsLine(state));
                break;
            }
        }

        AddPartLines(patient, lines);
        AddOrganLines(patient, lines);

        if (lines.Count == 0)
            lines.Add(VitalsLine(Loc.GetString("cmu-body-scanner-line-no-data")));

        return lines;
    }

    public List<(BodyPartType Type, BodyPartSymmetry Symmetry)> GetMissingLimbSlots(EntityUid patient)
    {
        var missing = new List<(BodyPartType Type, BodyPartSymmetry Symmetry)>();
        if (!TryComp<BodyComponent>(patient, out var bodyComp))
            return missing;
        if (_body.GetRootPartOrNull(patient, bodyComp) is not { } root)
            return missing;

        foreach (var (slotId, slot) in root.BodyPart.Children)
        {
            if (slot.Type is not (BodyPartType.Arm or BodyPartType.Leg))
                continue;

            if (!CMUBodyPartSlots.TryGetSymmetry(slotId, BodyPartSymmetry.None, out var symmetry))
                continue;

            var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);
            if (!_containers.TryGetContainer(root.Entity, containerId, out var container) ||
                container.ContainedEntities.Count == 0)
            {
                missing.Add((slot.Type, symmetry));
            }
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
        foreach (var (part, partComp) in _body.GetBodyChildren(patient))
        {
            var details = new List<string>();
            if (TryComp<BodyPartHealthComponent>(part, out var health))
                details.Add(Loc.GetString("cmu-body-scanner-part-health", ("current", health.Current), ("max", health.Max)));

            if (TryComp<BodyPartWoundComponent>(part, out var wounds))
            {
                var untreated = _woundLedger.CountUntreatedWounds(wounds);
                if (untreated > 0)
                    details.Add(Loc.GetString("cmu-body-scanner-part-wounds", ("count", untreated)));
            }

            if (TryComp<FractureComponent>(part, out var fracture) && fracture.Severity != FractureSeverity.None)
                details.Add(Loc.GetString("cmu-body-scanner-part-fracture", ("severity", fracture.Severity)));

            if (TryComp<InternalBleedingComponent>(part, out var bleed))
                details.Add(Loc.GetString("cmu-body-scanner-part-bleed", ("rate", bleed.BloodlossPerSecond)));

            if (HasComp<CMUEscharComponent>(part))
                details.Add(Loc.GetString("cmu-body-scanner-part-eschar"));
            if (HasComp<CMUSplintedComponent>(part))
                details.Add(Loc.GetString("cmu-body-scanner-part-splinted"));
            if (HasComp<CMUCastComponent>(part))
                details.Add(Loc.GetString("cmu-body-scanner-part-cast"));
            if (HasComp<CMUTourniquetComponent>(part))
                details.Add(Loc.GetString("cmu-body-scanner-part-tourniquet"));

            if (details.Count == 0)
                continue;

            lines.Add(BodyLine(Loc.GetString(
                "cmu-body-scanner-line-part",
                ("part", SharedCMUSurgeryFlowSystem.FormatPartName(partComp.PartType, partComp.Symmetry)),
                ("details", string.Join(", ", details)))));
        }

        foreach (var (type, symmetry) in GetMissingLimbSlots(patient))
        {
            lines.Add(BodyLine(Loc.GetString(
                "cmu-body-scanner-line-part",
                ("part", SharedCMUSurgeryFlowSystem.FormatPartName(type, symmetry)),
                ("details", Loc.GetString("cmu-body-scanner-part-missing-limb")))));
        }
    }

    private void AddOrganLines(EntityUid patient, List<CMUBodyScannerScanLine> lines)
    {
        foreach (var organ in _body.GetBodyOrgans(patient))
        {
            if (!TryComp<OrganHealthComponent>(organ.Id, out var health))
                continue;

            lines.Add(OrganLine(Loc.GetString(
                "cmu-body-scanner-line-organ",
                ("organ", OrganName(organ.Id)),
                ("stage", FormatOrganStage(health.Stage)),
                ("current", health.Current),
                ("max", health.Max))));
        }

        foreach (var (part, partComp) in _body.GetBodyChildren(patient))
        {
            foreach (var (slotId, _) in partComp.Organs)
            {
                var containerId = SharedBodySystem.OrganSlotContainerIdPrefix + slotId;
                if (!_containers.TryGetContainer(part, containerId, out var container))
                    continue;
                if (container.ContainedEntities.Count > 0)
                    continue;

                lines.Add(OrganLine(Loc.GetString(
                    "cmu-body-scanner-line-missing-organ",
                    ("organ", OrganSlotName(slotId)),
                    ("part", SharedCMUSurgeryFlowSystem.FormatPartName(partComp.PartType, partComp.Symmetry)))));
            }
        }
    }

    private static CMUBodyScannerScanLine VitalsLine(string text)
    {
        return new CMUBodyScannerScanLine(CMUBodyScannerScanCategory.Vitals, text);
    }

    private static CMUBodyScannerScanLine BodyLine(string text)
    {
        return new CMUBodyScannerScanLine(CMUBodyScannerScanCategory.Body, text);
    }

    private static CMUBodyScannerScanLine OrganLine(string text)
    {
        return new CMUBodyScannerScanLine(CMUBodyScannerScanCategory.Organs, text);
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
            "CMUOrganHumanEars" or "ears" => Loc.GetString("cmu-medical-scanner-organ-ears"),
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
