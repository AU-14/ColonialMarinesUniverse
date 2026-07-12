using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;

namespace Content.Shared._CMU14.Medical.Diagnostics.Examine;

public sealed partial class CMUMedicalExamineSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMUMedicalExamineProjectionSystem _woundProjection = default!;

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
    private const string RoboticLimbColor = "#8fd8ff";
    private const string RoboticDamageColor = "#b9ecff";

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
        TryComp<CMUMedicalExamineProjectionComponent>(body, out var woundProjection);

        foreach (var (partUid, part) in _medicalIndex.GetBodyParts(body))
        {
            var sections = new List<string>();

            AddRoboticVisibleSections(partUid, sections);

            if (includeWounds)
            {
                var untreated = new List<string>();
                var treatedWounds = 0;
                if (woundProjection != null &&
                    _woundProjection.TryGetPart(woundProjection, part.PartType, part.Symmetry, out var projectedPart))
                {
                    foreach (var wound in projectedPart.Wounds)
                    {
                        if (wound.Treated)
                        {
                            treatedWounds++;
                            continue;
                        }

                        untreated.Add(DescribeVisibleWound(wound));
                    }

                    if (projectedPart.ExternalBleeding != ExternalBleedTier.None)
                        untreated.Add("active bleeding");
                }

                if (HasComp<CMUEscharComponent>(partUid))
                    untreated.Add("charred burn tissue");

                if (untreated.Count > 0)
                    sections.Add($"[color={UntreatedWoundColor}]{ToSentence(untreated)}[/color]");

                if (treatedWounds > 0)
                    sections.Add($"[color={TreatedWoundColor}]{DescribeVisibleTreatedWounds(treatedWounds, "treated")}[/color]");
            }

            if (includeFractures
                && TryComp<FractureComponent>(partUid, out var fracture)
                && fracture.Severity.IsAtLeast(FractureSeverity.Simple))
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
        TryComp<CMUMedicalExamineProjectionComponent>(body, out var woundProjection);

        foreach (var (partUid, part) in _medicalIndex.GetBodyParts(body))
        {
            var sections = new List<string>();

            AddRoboticDetailedSections(partUid, sections);

            if (woundProjection != null &&
                _woundProjection.TryGetPart(woundProjection, part.PartType, part.Symmetry, out var projectedPart))
            {
                foreach (var wound in projectedPart.Wounds)
                    sections.Add(DescribeDetailedWound(wound));

                if (projectedPart.ExternalBleeding != ExternalBleedTier.None)
                    sections.Add(Color($"external bleeding: {DescribeBleedTier(projectedPart.ExternalBleeding)}", DetailedBleedColor));
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
        TryComp<CMUMedicalExamineProjectionComponent>(body, out var woundProjection);

        foreach (var (partUid, part) in _medicalIndex.GetBodyParts(body))
        {
            var partName = FormatPartName(part.PartType, part.Symmetry);
            var partOrder = BodyPartSortOrder(part.PartType, part.Symmetry);

            AddRoboticInspectSite(groups, partUid, partName, partOrder);

            if (woundProjection != null &&
                _woundProjection.TryGetPart(woundProjection, part.PartType, part.Symmetry, out var projectedPart))
            {
                foreach (var wound in projectedPart.Wounds)
                {
                    if (wound.Treated)
                        continue;

                    var header = GetInspectWoundHeader(wound.Mechanism, wound.Type);
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

                    group.AddWound(partName, wound.Size, wound.Damage.Float());
                }

                if (projectedPart.ExternalBleeding == ExternalBleedTier.Arterial)
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

                group.AddSite("charred tissue");
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
        return TryComp<CMUMedicalExamineProjectionComponent>(body, out var projection)
            ? _woundProjection.GetWorstExternalBleeding(projection)
            : ExternalBleedTier.None;
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

    private void AddRoboticVisibleSections(EntityUid part, List<string> sections)
    {
        if (!TryComp<CMURoboticLimbComponent>(part, out var robotic))
            return;

        var details = GetRoboticDetails(robotic);
        sections.Add(ToSentence(details));
    }

    private void AddRoboticDetailedSections(EntityUid part, List<string> sections)
    {
        if (!TryComp<CMURoboticLimbComponent>(part, out var robotic))
            return;

        sections.Add(Color(Loc.GetString("cmu-robotic-limb-detailed-state"), RoboticLimbColor));

        if (robotic.BruteDamage > FixedPoint2.Zero)
            sections.Add(Color(Loc.GetString("cmu-robotic-limb-detailed-brute"), RoboticDamageColor));

        if (robotic.BurnDamage > FixedPoint2.Zero)
            sections.Add(Color(Loc.GetString("cmu-robotic-limb-detailed-burn"), RoboticDamageColor));
    }

    private void AddRoboticInspectSite(
        Dictionary<string, InspectInjuryGroup> groups,
        EntityUid part,
        string partName,
        int partOrder)
    {
        if (!TryComp<CMURoboticLimbComponent>(part, out var robotic))
            return;

        var details = GetRoboticDamageDetails(robotic);
        if (details.Count == 0)
            return;

        const string key = "robotic limb damage";
        var header = Color(Loc.GetString("cmu-robotic-limb-inspect-header"), RoboticLimbColor);
        if (!groups.TryGetValue(key, out var group))
        {
            group = new InspectInjuryGroup(partOrder, header, RoboticDamageColor);
            groups.Add(key, group);
        }
        else if (partOrder < group.Order)
        {
            group.Order = partOrder;
        }

        group.AddSite($"{partName} ({ToSentence(details)})");
    }

    private List<string> GetRoboticDetails(CMURoboticLimbComponent robotic)
    {
        var details = new List<string>
        {
            Color(Loc.GetString("cmu-robotic-limb-examine-state"), RoboticLimbColor),
        };

        AddRoboticDamageDetails(robotic, details, true);
        return details;
    }

    private List<string> GetRoboticDamageDetails(CMURoboticLimbComponent robotic)
    {
        var details = new List<string>();
        AddRoboticDamageDetails(robotic, details);
        return details;
    }

    private void AddRoboticDamageDetails(
        CMURoboticLimbComponent robotic,
        List<string> details,
        bool colorDamage = false)
    {
        if (robotic.BruteDamage > FixedPoint2.Zero)
            AddRoboticDamageDetail(details, Loc.GetString("cmu-robotic-limb-examine-brute"), colorDamage);

        if (robotic.BurnDamage > FixedPoint2.Zero)
            AddRoboticDamageDetail(details, Loc.GetString("cmu-robotic-limb-examine-burn"), colorDamage);
    }

    private static void AddRoboticDamageDetail(List<string> details, string detail, bool colorDamage)
    {
        details.Add(colorDamage ? Color(detail, RoboticDamageColor) : detail);
    }

    private List<(BodyPartType Type, BodyPartSymmetry Symmetry)> GetMissingPartSlots(EntityUid body)
    {
        var missing = new List<(BodyPartType Type, BodyPartSymmetry Symmetry)>();
        foreach (var (partUid, part) in _medicalIndex.GetBodyParts(body))
            AddMissingChildSlots(partUid, part.Symmetry, missing);

        return missing;
    }

    private void AddMissingChildSlots(
        EntityUid parent,
        BodyPartSymmetry parentSymmetry,
        List<(BodyPartType Type, BodyPartSymmetry Symmetry)> missing)
    {
        foreach (var slot in _medicalIndex.GetBodyPartSlots(parent))
        {
            if (!CMUBodyPartSlots.IsReportableMissingPart(slot.Type))
                continue;
            if (slot.Part is not null)
                continue;

            if (CMUBodyPartSlots.TryGetSymmetry(slot.SlotId, parentSymmetry, out var symmetry))
                missing.Add((slot.Type, symmetry));
        }
    }

    private static string DescribeVisibleWound(CMUMedicalVisibleWound wound)
    {
        return $"a {WoundSizeProfile.StageName(wound.Size, wound.Damage.Float())}";
    }

    private static string DescribeVisibleTreatedWounds(int count, string treatment)
    {
        var noun = count == 1 ? "wound" : "wounds";
        return $"{noun} {treatment}";
    }

    private static string DescribeVisibleFracture(FractureSeverity severity, bool stabilized)
    {
        var prefix = stabilized ? "stabilized " : string.Empty;
        return severity switch
        {
            FractureSeverity.Simple => $"a {prefix}simple fracture",
            FractureSeverity.Compound => $"a {prefix}compound fracture",
            FractureSeverity.Shattered => $"a {prefix}shattered bone",
            _ => "a broken bone",
        };
    }

    private static string DescribeDetailedWound(CMUMedicalVisibleWound wound)
    {
        var details = GetDetailedWoundDetails(wound);
        return ToDetailedLines(details.Header, details.Body);
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

    private static string InspectSeverity(WoundSize size, float damage) => WoundSizeProfile.SeverityRank(size, damage) switch
    {
        0 => "Minor",
        1 => "Moderate",
        2 => "Severe",
        3 => "Massive",
        _ => "Moderate",
    };

    private static DetailedWoundDetails GetDetailedWoundDetails(CMUMedicalVisibleWound wound)
    {
        var header = Color(
            WoundSizeProfile.StageName(wound.Size, wound.Damage.Float()),
            WoundColorFor(wound.Mechanism, wound.Type));
        var details = Color(
            DescribeTreatment(wound.Treated),
            TreatmentColorFor(wound.Treated));

        return new DetailedWoundDetails(header, details);
    }

    private static string ToDetailedLines(List<string> sections)
    {
        return string.Join("\n  ", sections);
    }

    private static string ToDetailedLines(string first, string second)
    {
        return $"{first}\n  {second}";
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

    private static string TreatmentColorFor(bool treated)
    {
        return treated ? TreatedWoundColor : DetailedUntreatedColor;
    }

    private static string DescribeDetailedFracture(FractureSeverity severity, bool stabilized)
    {
        var prefix = stabilized ? "stabilized " : string.Empty;
        return severity switch
        {
            FractureSeverity.Hairline => $"{prefix}hairline fracture",
            FractureSeverity.Simple => $"{prefix}simple fracture",
            FractureSeverity.Compound => $"{prefix}compound fracture",
            FractureSeverity.Shattered => $"{prefix}shattered fracture",
            _ => "fracture",
        };
    }

    private static string DescribeTreatment(bool treated) => treated ? "treated" : "untreated";

    private static string DescribeBleedTier(ExternalBleedTier tier) => tier switch
    {
        ExternalBleedTier.Minor => "minor",
        ExternalBleedTier.Moderate => "moderate",
        ExternalBleedTier.Severe => "severe",
        ExternalBleedTier.Arterial => "arterial",
        _ => "none",
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
        private readonly HashSet<string> _siteLines = new();

        public int Order;
        public readonly string Header;
        public readonly List<string> SiteLines = new();
        private readonly string _siteColor;

        public InspectInjuryGroup(int order, string header, string siteColor = DetailedInjurySiteColor)
        {
            Order = order;
            Header = header;
            _siteColor = siteColor;
        }

        public void AddWound(string part, WoundSize size, float damage)
        {
            AddSite($"{InspectSeverity(size, damage)} {part}");
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

            if (SiteLines.Count > 0)
                lines.Add($"  {Color(string.Join(", ", SiteLines), _siteColor)}");

            return string.Join('\n', lines);
        }
    }
}
