using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Examine;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;

namespace Content.Shared._CMU14.Medical.Examine;

public sealed partial class CMUMedicalExamineSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private const string UntreatedWoundColor = "#ff4d4d";
    private const string TreatedWoundColor = "#7bd88f";
    private const string FractureColor = "#dca94c";
    private const string SeveredColor = "#ff4d4d";
    private const string DetailedPartColor = "#9fc7ff";
    private const string DetailedInjurySiteColor = "#ff9f43";
    private const string DetailedWoundColor = "#ffb86c";
    private const string DetailedBurnColor = "#ff704d";
    private const string DetailedBleedColor = "#ff5f5f";
    private const string DetailedUntreatedColor = "#ffd166";
    private const string DetailedAdequateColor = "#f0c85a";
    private const string DetailedOptimalColor = "#7bd88f";
    private const string DetailedCleanupColor = "#d987ff";
    private const string DetailedHintColor = "#83c9ff";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUHumanMedicalComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<CMUHumanMedicalComponent> ent, ref ExaminedEvent args)
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled))
            return;

        using (args.PushGroup(nameof(CMUMedicalExamineSystem), -1))
        {
            AddBodyPartLines(
                ent,
                args,
                _cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled),
                _cfg.GetCVar(CMUMedicalCCVars.BoneEnabled),
                _cfg.GetCVar(CMUMedicalCCVars.BodyPartEnabled));
        }
    }

    private void AddBodyPartLines(
        EntityUid body,
        ExaminedEvent args,
        bool includeWounds,
        bool includeFractures,
        bool includeMissingParts)
    {
        var partSummaries = new List<BodyPartExamineSummary>();

        foreach (var (partUid, part) in _body.GetBodyChildren(body))
        {
            var sections = new List<string>();

            if (includeWounds)
            {
                var untreated = new List<string>();
                var adequate = new List<string>();
                var treated = new List<string>();
                if (TryComp<BodyPartWoundComponent>(partUid, out var wounds))
                {
                    for (var i = 0; i < wounds.Wounds.Count; i++)
                    {
                        if (GetTreatmentQuality(wounds, i) == WoundTreatmentQuality.Adequate)
                            adequate.Add(DescribeVisibleWound(wounds, i));
                        else if (IsWoundTreatedForExamine(wounds, i))
                            treated.Add(DescribeVisibleWound(wounds, i));
                        else
                            untreated.Add(DescribeVisibleWound(wounds, i));
                    }

                    if (wounds.ExternalBleeding != ExternalBleedTier.None)
                        untreated.Add("active bleeding");
                }

                if (HasComp<CMUEscharComponent>(partUid))
                    untreated.Add("charred burn tissue");

                if (untreated.Count > 0)
                    sections.Add($"[color={UntreatedWoundColor}]{ToSentence(untreated)}[/color]");

                if (adequate.Count > 0)
                    sections.Add($"[color={DetailedAdequateColor}]{ToSentence(adequate)}[/color]");

                if (treated.Count > 0)
                    sections.Add($"[color={TreatedWoundColor}]{ToSentence(treated)}[/color]");
            }

            if (includeFractures
                && TryComp<FractureComponent>(partUid, out var fracture)
                && fracture.Severity is FractureSeverity.Compound or FractureSeverity.Comminuted)
            {
                var stabilized = HasComp<CMUSplintedComponent>(partUid) || HasComp<CMUCastComponent>(partUid);
                sections.Add($"[color={FractureColor}]{DescribeVisibleFracture(fracture.Severity, stabilized)}[/color]");
            }

            if (sections.Count == 0)
                continue;

            partSummaries.Add(new BodyPartExamineSummary(
                BodyPartSortOrder(part.PartType, part.Symmetry),
                FormatPartName(part.PartType, part.Symmetry),
                ToSemicolonList(sections)));
        }

        if (includeMissingParts)
        {
            foreach (var (type, symmetry) in GetMissingPartSlots(body))
            {
                partSummaries.Add(new BodyPartExamineSummary(
                    BodyPartSortOrder(type, symmetry),
                    FormatPartName(type, symmetry),
                    $"[color={SeveredColor}]SEVERED[/color]"));
            }
        }

        partSummaries.Sort((a, b) => a.Order.CompareTo(b.Order));

        foreach (var summary in partSummaries)
        {
            args.PushMarkup(Loc.GetString(
                "cmu-medical-examine-body-part-line",
                ("part", summary.Part),
                ("conditions", summary.Conditions)));
        }
    }

    public string GetDetailedExamineText(EntityUid body)
    {
        var partSummaries = new List<BodyPartExamineSummary>();

        foreach (var (partUid, part) in _body.GetBodyChildren(body))
        {
            var sections = new List<string>();

            if (TryComp<BodyPartWoundComponent>(partUid, out var wounds))
            {
                for (var i = 0; i < wounds.Wounds.Count; i++)
                {
                    if (IsOptimallyTreatedForDetailedExamine(wounds, i))
                        continue;

                    sections.Add(DescribeDetailedWound(wounds, i));
                }

                if (wounds.ExternalBleeding != ExternalBleedTier.None)
                    sections.Add(Color($"external bleeding: {DescribeBleedTier(wounds.ExternalBleeding)}", DetailedBleedColor));
            }

            if (HasComp<CMUEscharComponent>(partUid))
                sections.Add(Color("burn eschar: charred tissue", DetailedBurnColor));

            if (sections.Count == 0)
                continue;

            partSummaries.Add(new BodyPartExamineSummary(
                BodyPartSortOrder(part.PartType, part.Symmetry),
                PartHeader(part.PartType, part.Symmetry),
                ToDetailedLines(sections)));
        }

        foreach (var (type, symmetry) in GetMissingPartSlots(body))
        {
            partSummaries.Add(new BodyPartExamineSummary(
                BodyPartSortOrder(type, symmetry),
                PartHeader(type, symmetry),
                Color("severed", SeveredColor)));
        }

        if (partSummaries.Count == 0)
            return Loc.GetString("cmu-medical-detailed-examine-none");

        partSummaries.Sort((a, b) => a.Order.CompareTo(b.Order));

        var lines = new List<string>(partSummaries.Count);
        foreach (var summary in partSummaries)
        {
            lines.Add($"{summary.Part}:\n  {summary.Conditions}");
        }

        return string.Join('\n', lines);
    }

    public string GetInspectInjuriesText(EntityUid body)
    {
        var groups = new Dictionary<string, InspectInjuryGroup>();

        foreach (var (partUid, part) in _body.GetBodyChildren(body))
        {
            var partName = FormatPartName(part.PartType, part.Symmetry);
            var partOrder = BodyPartSortOrder(part.PartType, part.Symmetry);

            if (TryComp<BodyPartWoundComponent>(partUid, out var wounds))
            {
                for (var i = 0; i < wounds.Wounds.Count; i++)
                {
                    if (IsOptimallyTreatedForDetailedExamine(wounds, i))
                        continue;

                    var wound = wounds.Wounds[i];
                    var size = i < wounds.Sizes.Count ? wounds.Sizes[i] : WoundSize.Deep;
                    var mechanism = i < wounds.Mechanisms.Count ? wounds.Mechanisms[i] : LegacyMechanismFor(wound.Type);
                    var quality = GetTreatmentQuality(wounds, i);
                    var cleanup = i < wounds.Cleanup.Count ? wounds.Cleanup[i] : WoundCleanupFlags.None;
                    var header = GetInspectWoundHeader(mechanism, wound.Type);
                    var key = header;

                    if (!groups.TryGetValue(key, out var group))
                    {
                        group = new InspectInjuryGroup(partOrder, header);
                        groups.Add(key, group);
                    }
                    else if (partOrder < group.Order)
                    {
                        group.Order = partOrder;
                    }

                    group.AddWound(partName, size, quality, wound.Treated, cleanup, DescribeOptimalHint(mechanism, wound.Type, cleanup));
                }

                if (wounds.ExternalBleeding == ExternalBleedTier.Arterial)
                    AddArterialBleedingSite(groups, partName, partOrder);
            }

            if (HasComp<CMUEscharComponent>(partUid))
            {
                const string header = "[color=#ff704d]Burn Eschar[/color]";
                const string key = header;

                if (!groups.TryGetValue(key, out var group))
                {
                    group = new InspectInjuryGroup(partOrder, header);
                    groups.Add(key, group);
                }
                else if (partOrder < group.Order)
                {
                    group.Order = partOrder;
                }

                group.AddCleanup("Charred Tissue Cleanup Required");
            }
        }

        foreach (var (type, symmetry) in GetMissingPartSlots(body))
        {
            var partName = FormatPartName(type, symmetry);
            var partOrder = BodyPartSortOrder(type, symmetry);
            var header = Color("severed", SeveredColor);
            var key = header;

            if (!groups.TryGetValue(key, out var group))
            {
                group = new InspectInjuryGroup(partOrder, header);
                groups.Add(key, group);
            }
            else if (partOrder < group.Order)
            {
                group.Order = partOrder;
            }

            group.AddSite(partName);
        }

        if (groups.Count == 0)
            return Loc.GetString("cmu-medical-detailed-examine-none");

        var ordered = new List<InspectInjuryGroup>(groups.Values);
        ordered.Sort((a, b) =>
        {
            var order = a.Order.CompareTo(b.Order);
            return order != 0
                ? order
                : string.Compare(a.Header, b.Header, StringComparison.Ordinal);
        });

        var lines = new List<string>(ordered.Count);
        foreach (var group in ordered)
        {
            lines.Add(group.Render());
        }

        return string.Join('\n', lines);
    }

    public ExternalBleedTier GetWorstExternalBleeding(EntityUid body)
    {
        var bleeding = ExternalBleedTier.None;

        foreach (var (partUid, _) in _body.GetBodyChildren(body))
        {
            if (!TryComp<BodyPartWoundComponent>(partUid, out var wounds) ||
                wounds.ExternalBleeding <= bleeding)
            {
                continue;
            }

            bleeding = wounds.ExternalBleeding;
        }

        return bleeding;
    }

    private static void AddArterialBleedingSite(Dictionary<string, InspectInjuryGroup> groups, string partName, int partOrder)
    {
        const string key = "arterial bleeding";
        var header = Color("Arterial Bleeding", DetailedBleedColor);

        if (!groups.TryGetValue(key, out var group))
        {
            group = new InspectInjuryGroup(partOrder, header, DetailedBleedColor);
            groups.Add(key, group);
        }
        else if (partOrder < group.Order)
        {
            group.Order = partOrder;
        }

        group.AddSite(partName);
    }

    private static bool IsOptimallyTreatedForDetailedExamine(BodyPartWoundComponent wounds, int index)
    {
        var cleanup = index < wounds.Cleanup.Count ? wounds.Cleanup[index] : WoundCleanupFlags.None;
        return GetTreatmentQuality(wounds, index) == WoundTreatmentQuality.Optimal &&
               cleanup == WoundCleanupFlags.None;
    }

    private List<(BodyPartType Type, BodyPartSymmetry Symmetry)> GetMissingPartSlots(EntityUid body)
    {
        var missing = new List<(BodyPartType Type, BodyPartSymmetry Symmetry)>();
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return missing;

        if (_body.GetRootPartOrNull(body, bodyComp) is not { } root)
            return missing;

        AddMissingChildSlots(root.Entity, root.BodyPart, missing);

        foreach (var (partUid, part) in _body.GetBodyChildren(body, bodyComp))
        {
            if (partUid == root.Entity)
                continue;

            AddMissingChildSlots(partUid, part, missing);
        }

        return missing;
    }

    private void AddMissingChildSlots(
        EntityUid parent,
        BodyPartComponent parentPart,
        List<(BodyPartType Type, BodyPartSymmetry Symmetry)> missing)
    {
        foreach (var (slotId, slot) in parentPart.Children)
        {
            if (!IsReportableMissingPart(slot.Type))
                continue;

            var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);
            if (_containers.TryGetContainer(parent, containerId, out var container) &&
                container.ContainedEntities.Count > 0)
            {
                continue;
            }

            if (TryGetPartSymmetry(slotId, parentPart.Symmetry, out var symmetry))
                missing.Add((slot.Type, symmetry));
        }
    }

    private static bool IsReportableMissingPart(BodyPartType type)
    {
        return type is BodyPartType.Arm
            or BodyPartType.Hand
            or BodyPartType.Leg
            or BodyPartType.Foot;
    }

    private static bool TryGetPartSymmetry(string slotId, BodyPartSymmetry parentSymmetry, out BodyPartSymmetry symmetry)
    {
        if (slotId.Contains("left", StringComparison.OrdinalIgnoreCase))
        {
            symmetry = BodyPartSymmetry.Left;
            return true;
        }

        if (slotId.Contains("right", StringComparison.OrdinalIgnoreCase))
        {
            symmetry = BodyPartSymmetry.Right;
            return true;
        }

        if (parentSymmetry is BodyPartSymmetry.Left or BodyPartSymmetry.Right)
        {
            symmetry = parentSymmetry;
            return true;
        }

        symmetry = BodyPartSymmetry.None;
        return false;
    }

    private static string DescribeVisibleWound(BodyPartWoundComponent wounds, int index)
    {
        var wound = wounds.Wounds[index];
        var size = index < wounds.Sizes.Count ? wounds.Sizes[index] : WoundSize.Deep;
        var sizeText = size switch
        {
            WoundSize.Small => "small",
            WoundSize.Deep => "moderate",
            WoundSize.Gaping => "large",
            WoundSize.Massive => "massive",
            _ => "moderate",
        };

        var kind = wound.Type switch
        {
            WoundType.Burn => "burn",
            WoundType.Surgery => "wound",
            _ => GetVisibleWoundKind(wounds, index),
        };

        var treated = DescribeVisibleTreatment(wounds, index);
        return $"a {treated}{sizeText} {kind}";
    }

    private static string DescribeVisibleTreatment(BodyPartWoundComponent wounds, int index)
    {
        var quality = GetTreatmentQuality(wounds, index);
        return quality switch
        {
            WoundTreatmentQuality.Adequate => "adequately treated ",
            WoundTreatmentQuality.Optimal => "treated ",
            _ => wounds.Wounds[index].Treated ? "treated " : string.Empty,
        };
    }

    private static string GetVisibleWoundKind(BodyPartWoundComponent wounds, int index)
    {
        if (index < wounds.Mechanisms.Count && wounds.Mechanisms[index] == WoundMechanism.Burn)
            return "burn";

        return "wound";
    }

    private static string DescribeVisibleFracture(FractureSeverity severity, bool stabilized)
    {
        var prefix = stabilized ? "stabilized " : string.Empty;
        return severity switch
        {
            FractureSeverity.Compound => $"a {prefix}compound fracture",
            FractureSeverity.Comminuted => $"a {prefix}shattered bone",
            _ => "a broken bone",
        };
    }

    private static string DescribeDetailedWound(BodyPartWoundComponent wounds, int index)
    {
        var details = GetDetailedWoundDetails(wounds, index);
        return ToDetailedLines(new List<string>
        {
            details.Header,
            details.Body,
        });
    }

    private static string GetInspectWoundHeader(WoundMechanism mechanism, WoundType type)
    {
        return Color(DescribeInspectWoundTitle(mechanism, type), WoundColorFor(mechanism, type));
    }

    private static string DescribeInspectWoundTitle(WoundMechanism mechanism, WoundType type) => mechanism switch
    {
        WoundMechanism.Bullet => "Bullet Wounds",
        WoundMechanism.Stab => "Stab Wounds",
        WoundMechanism.Slash => "Slash Wounds",
        WoundMechanism.Crush => "Crush Wounds",
        WoundMechanism.Burn => "Burns",
        WoundMechanism.Blast => "Blast Wounds",
        WoundMechanism.Fragment => "Fragment Wounds",
        WoundMechanism.Surgical => "Surgical Wounds",
        _ => type == WoundType.Burn ? "Burns" : "Wounds",
    };

    private static string InspectSeverity(WoundSize size) => size switch
    {
        WoundSize.Small => "Minor",
        WoundSize.Deep => "Moderate",
        WoundSize.Gaping => "Severe",
        WoundSize.Massive => "Massive",
        _ => "Moderate",
    };

    private static string DescribeInspectCleanupRequired(WoundCleanupFlags cleanup)
    {
        if (cleanup == WoundCleanupFlags.None)
            return "Cleanup Required";

        var entries = new List<string>(4);
        if ((cleanup & WoundCleanupFlags.RetainedFragment) != WoundCleanupFlags.None)
            entries.Add("Retained Fragments");
        if ((cleanup & WoundCleanupFlags.PoorClosure) != WoundCleanupFlags.None)
            entries.Add("Poor Closure");
        if ((cleanup & WoundCleanupFlags.CharredTissue) != WoundCleanupFlags.None)
            entries.Add("Charred Tissue");
        if ((cleanup & WoundCleanupFlags.CrushDebris) != WoundCleanupFlags.None)
            entries.Add("Crush Debris");
        if ((cleanup & WoundCleanupFlags.DirtyDressing) != WoundCleanupFlags.None)
            entries.Add("Dirty Dressing");

        return $"{ToSentence(entries)} Cleanup Required";
    }

    private static DetailedWoundDetails GetDetailedWoundDetails(BodyPartWoundComponent wounds, int index)
    {
        var wound = wounds.Wounds[index];
        var size = index < wounds.Sizes.Count ? wounds.Sizes[index] : WoundSize.Deep;
        var mechanism = index < wounds.Mechanisms.Count ? wounds.Mechanisms[index] : LegacyMechanismFor(wound.Type);
        var quality = GetTreatmentQuality(wounds, index);
        var cleanup = index < wounds.Cleanup.Count ? wounds.Cleanup[index] : WoundCleanupFlags.None;

        var header = Color($"{DescribeDetailedSize(size)} {DescribeMechanism(mechanism, wound.Type)}", WoundColorFor(mechanism, wound.Type));
        var details = new List<string>
        {
            Color(
                DescribeTreatment(quality, wound.Treated),
                TreatmentColorFor(quality, wound.Treated)),
        };

        var cleanupText = quality == WoundTreatmentQuality.Adequate
            ? DescribeCleanup(cleanup)
            : string.Empty;
        if (cleanupText.Length > 0)
            details.Add(Color(cleanupText, DetailedCleanupColor));

        var optimalHint = DescribeOptimalHint(mechanism, wound.Type, cleanup);
        if (quality != WoundTreatmentQuality.Optimal && optimalHint.Length > 0)
            details.Add(Color($"optimal: {optimalHint}", DetailedHintColor));

        return new DetailedWoundDetails(header, ToDetailedLines(details));
    }

    private static bool IsWoundTreatedForExamine(BodyPartWoundComponent wounds, int index)
    {
        return wounds.Wounds[index].Treated || GetTreatmentQuality(wounds, index) != WoundTreatmentQuality.Untreated;
    }

    private static WoundTreatmentQuality GetTreatmentQuality(BodyPartWoundComponent wounds, int index)
    {
        return index < wounds.TreatmentQualities.Count
            ? wounds.TreatmentQualities[index]
            : WoundTreatmentQuality.Untreated;
    }

    private static string ToDetailedLines(List<string> sections)
    {
        return string.Join("\n  ", sections);
    }

    private static string PartHeader(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return $"[bold]{Color(FormatPartName(type, symmetry), DetailedPartColor)}[/bold]";
    }

    private static string Color(string text, string color)
    {
        return $"[color={color}]{text}[/color]";
    }

    private static string WoundColorFor(WoundMechanism mechanism, WoundType type)
    {
        if (mechanism == WoundMechanism.Burn || type == WoundType.Burn)
            return DetailedBurnColor;

        return DetailedWoundColor;
    }

    private static string TreatmentColorFor(WoundTreatmentQuality quality, bool treated)
    {
        return quality switch
        {
            WoundTreatmentQuality.Optimal => DetailedOptimalColor,
            WoundTreatmentQuality.Adequate => DetailedAdequateColor,
            _ => treated ? TreatedWoundColor : DetailedUntreatedColor,
        };
    }

    private static string DescribeDetailedFracture(FractureSeverity severity, bool stabilized)
    {
        var prefix = stabilized ? "stabilized " : string.Empty;
        return severity switch
        {
            FractureSeverity.Hairline => $"{prefix}hairline fracture",
            FractureSeverity.Simple => $"{prefix}simple fracture",
            FractureSeverity.Compound => $"{prefix}compound fracture",
            FractureSeverity.Comminuted => $"{prefix}comminuted fracture",
            _ => "fracture",
        };
    }

    private static string DescribeDetailedSize(WoundSize size) => size switch
    {
        WoundSize.Small => "small",
        WoundSize.Deep => "deep",
        WoundSize.Gaping => "gaping",
        WoundSize.Massive => "massive",
        _ => "deep",
    };

    private static string DescribeMechanism(WoundMechanism mechanism, WoundType type) => mechanism switch
    {
        WoundMechanism.Bullet => "bullet wound",
        WoundMechanism.Stab => "stab wound",
        WoundMechanism.Slash => "slash wound",
        WoundMechanism.Crush => "crush wound",
        WoundMechanism.Burn => "burn",
        WoundMechanism.Blast => "blast wound",
        WoundMechanism.Fragment => "fragment wound",
        WoundMechanism.Surgical => "surgical wound",
        _ => type == WoundType.Burn ? "burn" : "wound",
    };

    private static string DescribeTreatment(WoundTreatmentQuality quality, bool treated) => quality switch
    {
        WoundTreatmentQuality.Optimal => "optimal treatment",
        WoundTreatmentQuality.Adequate => "adequate treatment",
        _ => treated ? "treated" : "untreated",
    };

    private static string DescribeCleanup(WoundCleanupFlags cleanup)
    {
        if (cleanup == WoundCleanupFlags.None)
            return string.Empty;

        var entries = new List<string>(4);
        if ((cleanup & WoundCleanupFlags.RetainedFragment) != WoundCleanupFlags.None)
            entries.Add("retained fragments");
        if ((cleanup & WoundCleanupFlags.PoorClosure) != WoundCleanupFlags.None)
            entries.Add("poor closure");
        if ((cleanup & WoundCleanupFlags.CharredTissue) != WoundCleanupFlags.None)
            entries.Add("charred tissue");
        if ((cleanup & WoundCleanupFlags.CrushDebris) != WoundCleanupFlags.None)
            entries.Add("crush debris");
        if ((cleanup & WoundCleanupFlags.DirtyDressing) != WoundCleanupFlags.None)
            entries.Add("dirty dressing");

        return $"cleanup needed: {ToSentence(entries)}";
    }

    private static string DescribeOptimalHint(WoundMechanism mechanism, WoundType type, WoundCleanupFlags cleanup)
    {
        if ((cleanup & WoundCleanupFlags.RetainedFragment) != WoundCleanupFlags.None)
            return "remove shrapnel";
        if ((cleanup & WoundCleanupFlags.PoorClosure) != WoundCleanupFlags.None)
            return "sealing dressing";
        if ((cleanup & WoundCleanupFlags.CharredTissue) != WoundCleanupFlags.None)
            return "burn gel dressing";
        if ((cleanup & WoundCleanupFlags.CrushDebris) != WoundCleanupFlags.None)
            return "compression dressing";

        return mechanism switch
        {
            WoundMechanism.Bullet or WoundMechanism.Stab or WoundMechanism.Fragment => "hemostatic dressing",
            WoundMechanism.Slash or WoundMechanism.Surgical => "sealing dressing",
            WoundMechanism.Crush or WoundMechanism.Blast => "compression dressing",
            WoundMechanism.Burn => "burn gel dressing",
            _ when type == WoundType.Burn => "burn gel dressing",
            _ when (cleanup & WoundCleanupFlags.DirtyDressing) != WoundCleanupFlags.None => "antiseptic dressing",
            _ => string.Empty,
        };
    }

    private static string DescribeBleedTier(ExternalBleedTier tier) => tier switch
    {
        ExternalBleedTier.Minor => "minor",
        ExternalBleedTier.Moderate => "moderate",
        ExternalBleedTier.Severe => "severe",
        ExternalBleedTier.Arterial => "arterial",
        _ => "none",
    };

    private static WoundMechanism LegacyMechanismFor(WoundType type) => type switch
    {
        WoundType.Burn => WoundMechanism.Burn,
        WoundType.Surgery => WoundMechanism.Surgical,
        _ => WoundMechanism.Generic,
    };

    private static string FormatPartName(BodyPartType type, BodyPartSymmetry symmetry)
    {
        var part = type.ToString().ToLowerInvariant();
        if (symmetry == BodyPartSymmetry.Left)
            return "Left " + part;

        if (symmetry == BodyPartSymmetry.Right)
            return "Right " + part;

        if (type == BodyPartType.Head)
            return "Head";

        if (type == BodyPartType.Torso)
            return "Torso";

        return type.ToString();
    }

    private static int BodyPartSortOrder(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return type switch
        {
            BodyPartType.Head => 0,
            BodyPartType.Torso => 10,
            BodyPartType.Arm when symmetry == BodyPartSymmetry.Left => 20,
            BodyPartType.Hand when symmetry == BodyPartSymmetry.Left => 21,
            BodyPartType.Arm when symmetry == BodyPartSymmetry.Right => 30,
            BodyPartType.Hand when symmetry == BodyPartSymmetry.Right => 31,
            BodyPartType.Leg when symmetry == BodyPartSymmetry.Left => 40,
            BodyPartType.Foot when symmetry == BodyPartSymmetry.Left => 41,
            BodyPartType.Leg when symmetry == BodyPartSymmetry.Right => 50,
            BodyPartType.Foot when symmetry == BodyPartSymmetry.Right => 51,
            _ => 100 + ((int) type * 10) + SymmetrySortOrder(symmetry),
        };
    }

    private static int SymmetrySortOrder(BodyPartSymmetry symmetry)
    {
        return symmetry switch
        {
            BodyPartSymmetry.Left => 0,
            BodyPartSymmetry.None => 1,
            BodyPartSymmetry.Right => 2,
            _ => 3,
        };
    }

    private static string ToSentence(List<string> parts)
    {
        return parts.Count switch
        {
            0 => string.Empty,
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => $"{string.Join(", ", parts.GetRange(0, parts.Count - 1))}, and {parts[parts.Count - 1]}",
        };
    }

    private static string ToSemicolonList(List<string> parts)
    {
        return string.Join("; ", parts);
    }

    private readonly record struct BodyPartExamineSummary(int Order, string Part, string Conditions);

    private readonly record struct DetailedWoundDetails(string Header, string Body);

    private sealed class InspectInjuryGroup
    {
        private readonly HashSet<string> _cleanupLines = new();
        private readonly HashSet<string> _optimalLines = new();
        private readonly HashSet<string> _siteLines = new();

        public int Order;
        public readonly string Header;
        public readonly List<string> CleanupLines = new();
        public readonly List<string> OptimalLines = new();
        public readonly List<string> SiteLines = new();
        private readonly string _siteColor;

        public InspectInjuryGroup(int order, string header, string siteColor = DetailedInjurySiteColor)
        {
            Order = order;
            Header = header;
            _siteColor = siteColor;
        }

        public void AddWound(
            string part,
            WoundSize size,
            WoundTreatmentQuality quality,
            bool treated,
            WoundCleanupFlags cleanup,
            string optimalTreatment)
        {
            if (quality == WoundTreatmentQuality.Adequate)
                AddCleanup(DescribeInspectCleanupRequired(cleanup));

            if (optimalTreatment.Length > 0)
                AddOptimal($"Optimal Treatment: {optimalTreatment}");

            if (quality == WoundTreatmentQuality.Untreated && !treated)
                AddSite($"{InspectSeverity(size)} {part}");
        }

        public void AddCleanup(string cleanup)
        {
            if (_cleanupLines.Add(cleanup))
                CleanupLines.Add(cleanup);
        }

        public void AddOptimal(string treatment)
        {
            if (_optimalLines.Add(treatment))
                OptimalLines.Add(treatment);
        }

        public void AddSite(string site)
        {
            if (_siteLines.Add(site))
                SiteLines.Add(site);
        }

        public string Render()
        {
            var lines = new List<string>
            {
                $"[bold]{Header}[/bold]",
            };

            foreach (var cleanup in CleanupLines)
                lines.Add($"  {Color(cleanup, DetailedCleanupColor)}");

            foreach (var treatment in OptimalLines)
                lines.Add($"  {Color(treatment, DetailedHintColor)}");

            if (SiteLines.Count > 0)
                lines.Add($"  {Color(string.Join(", ", SiteLines), _siteColor)}");

            return string.Join('\n', lines);
        }
    }
}
