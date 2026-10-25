using System;
using System.Collections.Generic;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
// alias for static methods below: EntitySystem.Loc (instance) shadows the static Loc facade here
using StaticLoc = Robust.Shared.Localization.Loc;

namespace Content.Shared.CMU14.Medical.Diagnostics.Examine;

public sealed partial class CMUMedicalExamineSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMUMedicalExamineProjectionSystem _woundProjection = default!;
    [Dependency] private SharedCMUOpenStumpSystem _stumps = default!;

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
            foreach (var line in GetBodyPartLines(ent))
                args.PushMarkup(line);
        }
    }

    /// <summary>
    /// The per-body-part wound, fracture and missing-limb lines shown when examining someone, as markup.
    /// </summary>
    public List<string> GetBodyPartLines(EntityUid body)
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled))
            return new List<string>();

        return BuildBodyPartLines(
            body,
            _cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled),
            _cfg.GetCVar(CMUMedicalCCVars.BoneEnabled),
            _cfg.GetCVar(CMUMedicalCCVars.BodyPartEnabled));
    }

    private List<string> BuildBodyPartLines(
        EntityUid body,
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
                        untreated.Add(Loc.GetString("cmu-medical-examine-active-bleeding"));
                }

                if (HasComp<CMUEscharComponent>(partUid))
                    untreated.Add(Loc.GetString("cmu-medical-examine-charred-burn-tissue"));

                if (untreated.Count > 0)
                    sections.Add($"[color={UntreatedWoundColor}]{ToSentence(untreated)}[/color]");

                if (treatedWounds > 0)
                    sections.Add($"[color={TreatedWoundColor}]{DescribeVisibleTreatedWounds(treatedWounds)}[/color]");
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
                    $"[color={SeveredColor}]{Loc.GetString("cmu-medical-examine-severed")}[/color]{StumpSuffix(body, type, symmetry)}"));
            }
        }

        partSummaries.Sort((a, b) => a.Order.CompareTo(b.Order));

        var lines = new List<string>(partSummaries.Count);
        foreach (var summary in partSummaries)
        {
            lines.Add(Loc.GetString(
                "cmu-medical-examine-body-part-line",
                ("part", summary.Part),
                ("conditions", summary.Conditions)));
        }

        return lines;
    }

    private string StumpSuffix(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (!_stumps.TryFindStump(body, type, symmetry, out _, out var stump))
            return string.Empty;

        return stump.Clamped
            ? $"; [color={TreatedWoundColor}]{Loc.GetString("cmu-medical-examine-stump-tourniquet")}[/color]"
            : $"; [color={UntreatedWoundColor}]{Loc.GetString("cmu-medical-examine-stump-bleeding")}[/color]";
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
                    sections.Add(Color(Loc.GetString("cmu-medical-examine-external-bleeding-detailed", ("tier", DescribeBleedTier(projectedPart.ExternalBleeding))), DetailedBleedColor));
            }

            if (HasComp<CMUEscharComponent>(partUid))
                sections.Add(Color(Loc.GetString("cmu-medical-examine-burn-eschar-detailed"), DetailedBurnColor));

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
                Color(Loc.GetString("cmu-medical-detailed-examine-severed"), SeveredColor)));
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
                var header = Color(Loc.GetString("cmu-medical-examine-burn-eschar-header"), DetailedBurnColor);
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

                group.AddSite(Loc.GetString("cmu-medical-examine-charred-tissue"));
            }
        }

        foreach (var (type, symmetry) in GetMissingPartSlots(body))
        {
            var partName = FormatPartName(type, symmetry);
            var partOrder = BodyPartSortOrder(type, symmetry);
            var header = Color(Loc.GetString("cmu-medical-detailed-examine-severed"), SeveredColor);
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
        var header = Color(StaticLoc.GetString("cmu-medical-examine-arterial-bleeding-header"), DetailedBleedColor);

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
        return StaticLoc.GetString("cmu-medical-examine-wound-indef", ("stage", WoundSizeProfile.StageName(wound.Size, wound.Damage.Float())));
    }

    private static string DescribeVisibleTreatedWounds(int count)
    {
        return StaticLoc.GetString("cmu-medical-examine-treated-wounds", ("count", count));
    }

    private static string DescribeVisibleFracture(FractureSeverity severity, bool stabilized)
    {
        return severity switch
        {
            FractureSeverity.Simple => StaticLoc.GetString(stabilized ? "cmu-medical-examine-fracture-simple-stabilized" : "cmu-medical-examine-fracture-simple"),
            FractureSeverity.Compound => StaticLoc.GetString(stabilized ? "cmu-medical-examine-fracture-compound-stabilized" : "cmu-medical-examine-fracture-compound"),
            FractureSeverity.Shattered => StaticLoc.GetString(stabilized ? "cmu-medical-examine-fracture-shattered-stabilized" : "cmu-medical-examine-fracture-shattered"),
            _ => StaticLoc.GetString("cmu-medical-examine-fracture-default"),
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

    private static string DescribeInspectWoundTitle(WoundMechanism mechanism, WoundType type) => StaticLoc.GetString(mechanism switch
    {
        WoundMechanism.Bullet => "cmu-medical-examine-wound-title-bullet",
        WoundMechanism.Stab => "cmu-medical-examine-wound-title-stab",
        WoundMechanism.Slash => "cmu-medical-examine-wound-title-slash",
        WoundMechanism.Crush => "cmu-medical-examine-wound-title-crush",
        WoundMechanism.Burn => "cmu-medical-examine-wound-title-burn",
        WoundMechanism.Blast => "cmu-medical-examine-wound-title-blast",
        WoundMechanism.Fragment => "cmu-medical-examine-wound-title-fragment",
        WoundMechanism.Surgical => "cmu-medical-examine-wound-title-surgical",
        _ => type == WoundType.Burn ? "cmu-medical-examine-wound-title-burn" : "cmu-medical-examine-wound-title-default",
    });

    private static string InspectSeverity(WoundSize size, float damage) => StaticLoc.GetString(WoundSizeProfile.SeverityRank(size, damage) switch
    {
        0 => "cmu-medical-examine-severity-minor",
        1 => "cmu-medical-examine-severity-moderate",
        2 => "cmu-medical-examine-severity-severe",
        3 => "cmu-medical-examine-severity-massive",
        _ => "cmu-medical-examine-severity-moderate",
    });

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

    private static string DescribeDetailedFracture(FractureSeverity severity, bool stabilized) => severity switch
    {
        FractureSeverity.Hairline => StaticLoc.GetString(stabilized ? "cmu-medical-examine-fracture-hairline-stabilized" : "cmu-medical-examine-fracture-hairline"),
        FractureSeverity.Simple => StaticLoc.GetString(stabilized ? "cmu-medical-examine-fracture-simple-stabilized" : "cmu-medical-examine-fracture-simple"),
        FractureSeverity.Compound => StaticLoc.GetString(stabilized ? "cmu-medical-examine-fracture-compound-stabilized" : "cmu-medical-examine-fracture-compound"),
        FractureSeverity.Shattered => StaticLoc.GetString(stabilized ? "cmu-medical-examine-fracture-shattered-stabilized" : "cmu-medical-examine-fracture-shattered"),
        _ => StaticLoc.GetString("cmu-medical-examine-fracture-default"),
    };

    private static string DescribeTreatment(bool treated) => StaticLoc.GetString(treated ? "cmu-medical-examine-treated" : "cmu-medical-examine-untreated");

    private static string DescribeBleedTier(ExternalBleedTier tier) => StaticLoc.GetString(tier switch
    {
        ExternalBleedTier.Minor => "cmu-medical-scanner-bleed-tier-minor",
        ExternalBleedTier.Moderate => "cmu-medical-scanner-bleed-tier-moderate",
        ExternalBleedTier.Severe => "cmu-medical-scanner-bleed-tier-severe",
        ExternalBleedTier.Arterial => "cmu-medical-scanner-bleed-tier-arterial",
        _ => "cmu-medical-examine-bleed-tier-none",
    });

    private static string FormatPartName(BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (symmetry == BodyPartSymmetry.Left)
            return StaticLoc.GetString(PartLocId(type, "left"));

        if (symmetry == BodyPartSymmetry.Right)
            return StaticLoc.GetString(PartLocId(type, "right"));

        return StaticLoc.GetString(PartLocId(type, null));
    }

    private static string PartLocId(BodyPartType type, string? side)
    {
        var typeKey = type switch
        {
            BodyPartType.Head => "head",
            BodyPartType.Torso => "torso",
            BodyPartType.Arm => "arm",
            BodyPartType.Hand => "hand",
            BodyPartType.Leg => "leg",
            BodyPartType.Foot => "foot",
            BodyPartType.Tail => "tail",
            _ => "other",
        };

        return side is null
            ? $"cmu-medical-examine-part-{typeKey}"
            : $"cmu-medical-examine-part-{typeKey}-{side}";
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
        var and = StaticLoc.GetString("cmu-medical-examine-and");
        return parts.Count switch
        {
            0 => string.Empty,
            1 => parts[0],
            2 => $"{parts[0]} {and} {parts[1]}",
            _ => $"{string.Join(", ", parts.GetRange(0, parts.Count - 1))}, {and} {parts[parts.Count - 1]}",
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
