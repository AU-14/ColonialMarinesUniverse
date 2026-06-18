using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Surgery;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Human.Diagnostics.Examine;

public sealed partial class CMUMedicalExamineSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private const int SurgerySkillNovice = 1;

    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";

    private const string UntreatedWoundColor = "#ff4d4d";
    private const string TreatedWoundColor = "#7bd88f";
    private const string FractureColor = "#dca94c";
    private const string SeveredColor = "#ff4d4d";
    private const string ProstheticColor = "#7ac7ff";
    private const string DetailedPartColor = "#9fc7ff";
    private const string DetailedInjurySiteColor = "#ff9f43";
    private const string DetailedWoundColor = "#ffb86c";
    private const string DetailedBurnColor = "#ff704d";
    private const string DetailedBleedColor = "#ff5f5f";
    private const string DetailedUntreatedColor = "#ffd166";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<HumanMedicalComponent> ent, ref ExaminedEvent args)
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled))
            return;

        using (args.PushGroup(nameof(CMUMedicalExamineSystem), -1))
        {
            AddBodyPartLines(
                ent.Owner,
                ent.Comp,
                args,
                _cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled),
                _cfg.GetCVar(CMUMedicalCCVars.BoneEnabled),
                _cfg.GetCVar(CMUMedicalCCVars.BodyPartEnabled),
                HasSurgeryExamineSkill(args.Examiner));
        }
    }

    private void AddBodyPartLines(
        EntityUid body,
        HumanMedicalComponent medical,
        ExaminedEvent args,
        bool includeWounds,
        bool includeFractures,
        bool includeMissingParts,
        bool includeSurgeryDetails)
    {
        var partSummaries = new List<BodyPartExamineSummary>();

        foreach (var region in medical.Regions)
        {
            if (region.Region == BodyRegion.None)
                continue;

            var sections = new List<string>();
            if (includeWounds)
                AddVisibleWoundSections(body, medical, region.Region, sections);

            if (includeFractures && region.Skeletal.Broken)
            {
                var text = region.Skeletal.Splinted
                    ? "a stabilized fracture"
                    : "a broken bone";
                sections.Add(Color(text, FractureColor));
            }

            if (includeMissingParts && region.Presence != LimbPresence.Present)
            {
                sections.Add(Color(DescribePresence(region.Presence), PresenceColor(region.Presence)));
                if (region.Presence != LimbPresence.Prosthetic)
                    AddVisibleLinkedStumpSections(medical, region.Region, sections);
            }

            AddVisibleSurgerySections(body, medical, region, sections, includeSurgeryDetails);

            if (sections.Count == 0)
                continue;

            var conditions = ToVisibleConditionList(sections, out var multiline);
            partSummaries.Add(new BodyPartExamineSummary(
                BodyRegionSortOrder(region.Region),
                FormatRegionName(region.Region),
                conditions,
                multiline));
        }

        partSummaries.Sort((a, b) => a.Order.CompareTo(b.Order));

        foreach (var summary in partSummaries)
        {
            if (summary.Multiline)
            {
                args.PushMarkup($"{summary.Part}:\n  {summary.Conditions}");
                continue;
            }

            args.PushMarkup(Loc.GetString(
                "cmu-medical-examine-body-part-line",
                ("part", summary.Part),
                ("conditions", summary.Conditions)));
        }
    }

    public string GetDetailedExamineText(EntityUid body)
    {
        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return Loc.GetString("cmu-medical-detailed-examine-none");

        var partSummaries = new List<BodyPartExamineSummary>();

        foreach (var region in medical.Regions)
        {
            if (region.Region == BodyRegion.None)
                continue;

            var sections = new List<string>();
            AddDetailedRegionSections(body, medical, region, sections);
            if (sections.Count == 0)
                continue;

            partSummaries.Add(new BodyPartExamineSummary(
                BodyRegionSortOrder(region.Region),
                PartHeader(region.Region),
                ToDetailedLines(sections),
                Multiline: true));
        }

        if (partSummaries.Count == 0)
            return Loc.GetString("cmu-medical-detailed-examine-none");

        partSummaries.Sort((a, b) => a.Order.CompareTo(b.Order));

        var lines = new List<string>(partSummaries.Count);
        foreach (var summary in partSummaries)
            lines.Add($"{summary.Part}:\n  {summary.Conditions}");

        return string.Join('\n', lines);
    }

    public string GetInspectInjuriesText(EntityUid body)
    {
        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return Loc.GetString("cmu-medical-detailed-examine-none");

        var groups = new Dictionary<string, InspectInjuryGroup>();

        foreach (var injury in medical.Injuries)
        {
            if (IsInjuryClosed(injury))
                continue;

            var partName = FormatRegionName(injury.Region);
            var partOrder = BodyRegionSortOrder(injury.Region);
            var header = InspectHeader(injury);

            if (!groups.TryGetValue(header, out var group))
            {
                group = new InspectInjuryGroup(partOrder, header, InjurySiteColor(injury));
                groups.Add(header, group);
            }
            else if (partOrder < group.Order)
            {
                group.Order = partOrder;
            }

            group.AddSite($"{DescribeStage(injury.Stage, titleCase: true)} {partName}");
        }

        foreach (var bleed in medical.BleedSources)
        {
            if (!bleed.Active || bleed.Kind == BleedKind.Internal)
                continue;

            var partName = FormatRegionName(bleed.Region);
            var partOrder = BodyRegionSortOrder(bleed.Region);
            const string header = "[color=#ff5f5f]Active Bleeding[/color]";

            if (!groups.TryGetValue(header, out var group))
            {
                group = new InspectInjuryGroup(partOrder, header, DetailedBleedColor);
                groups.Add(header, group);
            }
            else if (partOrder < group.Order)
            {
                group.Order = partOrder;
            }

            group.AddSite($"{DescribeBleedSeverity(BleedingRules.GetSeverity(bleed.Rate))} {partName}");
        }

        foreach (var region in medical.Regions)
        {
            if (region.Region == BodyRegion.None ||
                !TryGetShrapnelSummary(medical, region.Region, out var shrapnel))
            {
                continue;
            }

            var partName = FormatRegionName(region.Region);
            var partOrder = BodyRegionSortOrder(region.Region);
            const string header = "[color=#ff5f5f]Embedded Shrapnel[/color]";

            if (!groups.TryGetValue(header, out var group))
            {
                group = new InspectInjuryGroup(partOrder, header, DetailedBleedColor);
                groups.Add(header, group);
            }
            else if (partOrder < group.Order)
            {
                group.Order = partOrder;
            }

            group.AddSite(DescribeInspectShrapnelSite(shrapnel, partName));
        }

        foreach (var region in medical.Regions)
        {
            if (region.Region == BodyRegion.None || region.Presence == LimbPresence.Present)
                continue;

            var partName = FormatRegionName(region.Region);
            var partOrder = BodyRegionSortOrder(region.Region);
            var header = Color(DescribePresence(region.Presence), PresenceColor(region.Presence));

            if (!groups.TryGetValue(header, out var group))
            {
                group = new InspectInjuryGroup(partOrder, header);
                groups.Add(header, group);
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
            lines.Add(group.Render());

        return string.Join('\n', lines);
    }

    public BleedSeverity GetWorstExternalBleeding(EntityUid body)
    {
        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return BleedSeverity.None;

        var worst = BleedSeverity.None;
        foreach (var bleed in medical.BleedSources)
        {
            if (!bleed.Active || bleed.Kind == BleedKind.Internal)
                continue;

            var severity = BleedingRules.GetSeverity(bleed.Rate);
            if (severity > worst)
                worst = severity;
        }

        return worst;
    }

    public static string DescribeVisibleBleedingForExamine(BleedSeverity severity, bool surgical)
    {
        var source = surgical
            ? " surgical"
            : string.Empty;

        return $"{DescribeBleedSeverity(severity)}{source} bleeding";
    }

    private void AddVisibleWoundSections(
        EntityUid body,
        HumanMedicalComponent medical,
        BodyRegion region,
        List<string> sections)
    {
        var untreated = new List<VisibleInjuryBucket>();
        var activeBleeds = new List<VisibleBleedBucket>();
        var suppressedBleeds = new List<SuppressedBleedBucket>();
        var treatedWounds = 0;

        foreach (var injury in medical.Injuries)
        {
            if (injury.Region != region || IsInjuryClosed(injury))
                continue;

            if (IsInjuryTreated(injury))
                treatedWounds++;
            else
                AddVisibleInjuryBucket(untreated, injury);
        }

        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Region == region && bleed.Active && bleed.Kind != BleedKind.Internal)
                AddVisibleBleedBucket(activeBleeds, bleed);
            else if (bleed.Region == region && IsBleedSuppressedButNeedsSurgery(bleed))
                AddSuppressedBleedBucket(suppressedBleeds, bleed);
        }

        if (untreated.Count > 0)
            sections.Add(Color(ToSentence(DescribeVisibleInjuries(untreated)), UntreatedWoundColor));

        if (activeBleeds.Count > 0)
            sections.Add(Color(ToSentence(DescribeVisibleBleeds(activeBleeds)), UntreatedWoundColor));

        if (suppressedBleeds.Count > 0)
            sections.Add(Color(ToSentence(DescribeSuppressedBleeds(suppressedBleeds)), UntreatedWoundColor));

        if (TryGetShrapnelSummary(medical, region, out var shrapnel))
            sections.Add(Color(DescribeVisibleShrapnel(shrapnel), UntreatedWoundColor));

        if (treatedWounds > 0)
            sections.Add(Color(DescribeVisibleTreatedWounds(treatedWounds), TreatedWoundColor));
    }

    private static void AddVisibleLinkedStumpSections(
        HumanMedicalComponent medical,
        BodyRegion missingRegion,
        List<string> sections)
    {
        var stumpRegion = LimbLossRules.GetStumpAnchorRegion(missingRegion);
        if (stumpRegion == BodyRegion.None ||
            stumpRegion == missingRegion)
        {
            return;
        }

        var untreated = new List<VisibleInjuryBucket>();
        var activeBleeds = new List<VisibleBleedBucket>();

        foreach (var injury in medical.Injuries)
        {
            if (injury.Region != stumpRegion ||
                !injury.IsOpenStump ||
                IsInjuryClosed(injury))
            {
                continue;
            }

            AddVisibleInjuryBucket(untreated, injury);
        }

        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Region == stumpRegion &&
                bleed.Active &&
                bleed.Kind == BleedKind.Stump)
            {
                AddVisibleBleedBucket(activeBleeds, bleed);
            }
        }

        if (untreated.Count > 0)
            sections.Add(Color(ToSentence(DescribeVisibleInjuries(untreated)), UntreatedWoundColor));

        if (activeBleeds.Count > 0)
            sections.Add(Color(ToSentence(DescribeVisibleBleeds(activeBleeds)), UntreatedWoundColor));
    }

    private void AddVisibleSurgerySections(
        EntityUid body,
        HumanMedicalComponent medical,
        RegionState region,
        List<string> sections,
        bool includeSurgeryDetails)
    {
        SurgeryOperationState operation = default;
        var hasOperation = includeSurgeryDetails && TryGetSurgeryOperation(body, region.Region, out operation);
        if (region.Incision == IncisionDepth.Closed && !hasOperation)
            return;

        if (!includeSurgeryDetails)
        {
            if (region.Incision != IncisionDepth.Closed)
                sections.Add(Color(DescribeUnskilledIncision(region.Incision), DetailedWoundColor));
            return;
        }

        var details = DescribeVisibleSurgeryDetails(medical, region, hasOperation, operation);
        if (!string.IsNullOrEmpty(details))
            sections.Add(details);
    }

    private static string DescribeVisibleSurgeryDetails(
        HumanMedicalComponent medical,
        RegionState region,
        bool hasOperation,
        SurgeryOperationState operation)
    {
        var lines = new List<string>();
        if (region.Incision != IncisionDepth.Closed)
            lines.Add(Color($"incision: {DescribeSurgeryIncisionState(region.Incision)}", DetailedWoundColor));

        if (hasOperation)
        {
            lines.Add(Color(
                $"procedure: {DescribeSurgeryProcedure(operation.ProcedureId)} (step {operation.StepIndex + 1})",
                DetailedWoundColor));

            var nextStep = DescribeNextSurgeryStep(medical, region, operation);
            if (!string.IsNullOrEmpty(nextStep))
                lines.Add(Color($"next: {nextStep}", DetailedUntreatedColor));

            return $"{Color("surgery", DetailedWoundColor)}\n    {string.Join("\n    ", lines)}";
        }

        if (HumanSurgeryProcedureRules.TryGetRequiredProcedureForRegion(
                medical,
                region.Region,
                out var requiredProcedure) &&
            requiredProcedure != SurgeryProcedureId.None)
        {
            lines.Add(Color($"procedure: {DescribeSurgeryProcedure(requiredProcedure)}", DetailedWoundColor));

            var nextStep = DescribeRequiredSurgeryStep(medical, region, requiredProcedure);
            if (!string.IsNullOrEmpty(nextStep))
                lines.Add(Color($"next: {nextStep}", DetailedUntreatedColor));

            return $"{Color("surgery", DetailedWoundColor)}\n    {string.Join("\n    ", lines)}";
        }

        var possibleSteps = DescribePossibleSurgerySteps(region);
        if (!string.IsNullOrEmpty(possibleSteps))
            lines.Add(Color($"next: {possibleSteps}", DetailedUntreatedColor));

        return lines.Count == 0
            ? string.Empty
            : $"{Color("surgery", DetailedWoundColor)}\n    {string.Join("\n    ", lines)}";
    }

    private bool HasSurgeryExamineSkill(EntityUid examiner)
    {
        return _skills.HasSkill(examiner, SurgerySkill, SurgerySkillNovice);
    }

    private bool TryGetSurgeryOperation(
        EntityUid body,
        BodyRegion region,
        out SurgeryOperationState operation)
    {
        if (TryComp<ActiveHumanSurgeryOperationComponent>(body, out var active))
        {
            foreach (var candidate in active.Operations)
            {
                if (candidate.Region != region)
                    continue;

                operation = candidate;
                return true;
            }
        }

        operation = default;
        return false;
    }

    private void AddDetailedRegionSections(
        EntityUid body,
        HumanMedicalComponent medical,
        RegionState region,
        List<string> sections)
    {
        if (region.Presence != LimbPresence.Present)
            sections.Add(Color(DescribePresence(region.Presence), PresenceColor(region.Presence)));

        if (region.BruteDamage > FixedPoint2.Zero || region.BurnDamage > FixedPoint2.Zero)
        {
            sections.Add(Color(
                $"trauma: {region.BruteDamage} brute, {region.BurnDamage} burn",
                DetailedInjurySiteColor));
        }

        if (region.Skeletal.Broken)
        {
            sections.Add(Color(
                region.Skeletal.Splinted ? "stabilized fracture" : "unstabilized fracture",
                FractureColor));
        }

        if (region.Incision != IncisionDepth.Closed)
            sections.Add(Color($"incision: {region.Incision}", DetailedWoundColor));

        if (TryGetShrapnelSummary(medical, region.Region, out var shrapnel))
            sections.Add(Color($"foreign object: {DescribeVisibleShrapnel(shrapnel)}", DetailedBleedColor));

        foreach (var injury in medical.Injuries)
        {
            if (injury.Region != region.Region || IsInjuryClosed(injury))
                continue;

            sections.Add(DescribeDetailedInjury(injury));
        }

        var activeBleeds = new List<VisibleBleedBucket>();
        var suppressedBleeds = new List<SuppressedBleedBucket>();
        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Region != region.Region)
                continue;

            if (bleed.Active)
            {
                AddVisibleBleedBucket(activeBleeds, bleed);
                continue;
            }

            if (IsBleedSuppressedButNeedsSurgery(bleed))
                AddSuppressedBleedBucket(suppressedBleeds, bleed);
        }

        foreach (var bleed in activeBleeds)
            sections.Add(Color(DescribeDetailedBleed(bleed), DetailedBleedColor));

        foreach (var bleed in suppressedBleeds)
            sections.Add(Color(DescribeDetailedSuppressedBleed(bleed), DetailedUntreatedColor));
    }

    private static void AddVisibleInjuryBucket(List<VisibleInjuryBucket> buckets, InjuryRecord injury)
    {
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];
            if (bucket.Kind != injury.Kind || bucket.Stage != injury.Stage)
                continue;

            bucket.Count++;
            return;
        }

        buckets.Add(new VisibleInjuryBucket(injury.Kind, injury.Stage));
    }

    private static void AddVisibleBleedBucket(List<VisibleBleedBucket> buckets, BleedSource bleed)
    {
        var surgical = bleed.Flags.HasFlag(BleedFlags.Surgical);
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];
            if (bucket.Surgical != surgical)
                continue;

            bucket.Rate += bleed.Rate;
            return;
        }

        buckets.Add(new VisibleBleedBucket(surgical, bleed.Rate));
    }

    private static void AddSuppressedBleedBucket(List<SuppressedBleedBucket> buckets, BleedSource bleed)
    {
        var surgical = bleed.Flags.HasFlag(BleedFlags.Surgical);
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];
            if (bucket.Surgical != surgical)
                continue;

            return;
        }

        buckets.Add(new SuppressedBleedBucket(surgical));
    }

    private static List<string> DescribeVisibleInjuries(List<VisibleInjuryBucket> buckets)
    {
        var descriptions = new List<string>(buckets.Count);
        foreach (var bucket in buckets)
            descriptions.Add(DescribeVisibleInjury(bucket));

        return descriptions;
    }

    private static List<string> DescribeVisibleBleeds(List<VisibleBleedBucket> buckets)
    {
        var descriptions = new List<string>(buckets.Count);
        foreach (var bucket in buckets)
            descriptions.Add(DescribeVisibleBleed(bucket));

        return descriptions;
    }

    private static List<string> DescribeSuppressedBleeds(List<SuppressedBleedBucket> buckets)
    {
        var descriptions = new List<string>(buckets.Count);
        foreach (var bucket in buckets)
            descriptions.Add(DescribeSuppressedBleed(bucket));

        return descriptions;
    }

    private static string DescribeVisibleInjury(VisibleInjuryBucket bucket)
    {
        var stage = DescribeStage(bucket.Stage);
        var noun = DescribeInjuryKind(bucket.Kind, bucket.Count);
        return bucket.Count == 1
            ? $"{ArticleFor(stage)} {stage} {noun}"
            : $"{bucket.Count} {stage} {noun}";
    }

    private static string DescribeVisibleBleed(VisibleBleedBucket bucket)
    {
        return DescribeVisibleBleedingForExamine(
            BleedingRules.GetSeverity(bucket.Rate),
            bucket.Surgical);
    }

    private static string DescribeSuppressedBleed(SuppressedBleedBucket bucket)
    {
        return bucket.Surgical
            ? "surgical bleeding suppressed, surgery needed"
            : "bleeding suppressed, surgery needed";
    }

    private static string DescribeDetailedBleed(VisibleBleedBucket bucket)
    {
        return DescribeVisibleBleedingForExamine(
            BleedingRules.GetSeverity(bucket.Rate),
            bucket.Surgical);
    }

    private static string DescribeDetailedSuppressedBleed(SuppressedBleedBucket bucket)
    {
        return global::Robust.Shared.Localization.Loc.GetString(
            "cmu-medical-detailed-examine-suppressed-bleed-needs-surgery",
            ("kind", bucket.Surgical ? "surgical" : "active"));
    }

    private static string DescribeSurgeryIncisionState(IncisionDepth incision) => incision switch
    {
        IncisionDepth.OpenSkin => "open",
        IncisionDepth.Retracted => "retracted",
        IncisionDepth.DeepAccess => "deep access",
        _ => "closed",
    };

    private static string DescribeUnskilledIncision(IncisionDepth incision)
    {
        return incision == IncisionDepth.Closed
            ? string.Empty
            : "an open incision";
    }

    private static string DescribeSurgeryProcedure(SurgeryProcedureId procedure) => procedure switch
    {
        SurgeryProcedureId.SurgicalAccess => "surgical access",
        SurgeryProcedureId.SutureWound => "wound suturing",
        SurgeryProcedureId.RemoveForeignObject => "foreign object removal",
        SurgeryProcedureId.SealStump => "stump repair",
        SurgeryProcedureId.RepairInternalBleeding => "internal bleeding repair",
        SurgeryProcedureId.RemoveEschar => "eschar removal",
        SurgeryProcedureId.RepairOrgan => "organ repair",
        SurgeryProcedureId.RepairFracture => "fracture repair",
        SurgeryProcedureId.CloseIncision => "incision closure",
        SurgeryProcedureId.AlienEmbryoRemoval => "alien embryo removal",
        SurgeryProcedureId.EyeSurgery => "eye repair",
        SurgeryProcedureId.BrainDamageSurgery => "brain damage repair",
        SurgeryProcedureId.Amputation => "amputation",
        SurgeryProcedureId.ReattachLimb => "limb reattachment",
        SurgeryProcedureId.FitProsthetic => "prosthetic fitting",
        SurgeryProcedureId.RemoveProsthetic => "prosthetic removal",
        _ => "surgery",
    };

    private static string DescribeNextSurgeryStep(
        HumanMedicalComponent medical,
        RegionState region,
        SurgeryOperationState operation)
    {
        return operation.ProcedureId switch
        {
            SurgeryProcedureId.SurgicalAccess => DescribePossibleSurgerySteps(region),
            SurgeryProcedureId.SutureWound => "suture the wound",
            SurgeryProcedureId.RemoveForeignObject => "remove the foreign object",
            SurgeryProcedureId.SealStump => "seal the stump",
            SurgeryProcedureId.RepairInternalBleeding => HasShallowAccess(medical, region.Region)
                ? "repair the internal bleed"
                : DescribePossibleSurgerySteps(region),
            SurgeryProcedureId.RemoveEschar => "remove the eschar",
            SurgeryProcedureId.RepairOrgan => HasRequiredRepairAccess(medical, region.Region)
                ? "repair the organ damage"
                : DescribePossibleSurgerySteps(region),
            SurgeryProcedureId.RepairFracture => HasRequiredRepairAccess(medical, region.Region)
                ? "set and mend the fracture"
                : DescribePossibleSurgerySteps(region),
            SurgeryProcedureId.CloseIncision => "close the incision",
            SurgeryProcedureId.AlienEmbryoRemoval => operation.StepIndex <= 0
                ? "cut the embryo roots"
                : "remove the embryo",
            SurgeryProcedureId.EyeSurgery => "repair the eyes",
            SurgeryProcedureId.BrainDamageSurgery => operation.StepIndex <= 0
                ? "remove damaged tissue or fragments"
                : "repair the brain damage",
            SurgeryProcedureId.Amputation => operation.StepIndex <= 0
                ? "sever the supporting tissue"
                : "amputate the limb",
            SurgeryProcedureId.ReattachLimb => "attach the limb",
            SurgeryProcedureId.FitProsthetic => "fit the prosthetic",
            SurgeryProcedureId.RemoveProsthetic => "remove the prosthetic",
            _ => string.Empty,
        };
    }

    private static string DescribePossibleSurgerySteps(RegionState region)
    {
        return region.Incision switch
        {
            IncisionDepth.OpenSkin => "clamp surgical bleeders, then retract the incision",
            IncisionDepth.Retracted when IsEncasedRegion(region.Region) && !HasFracturedBoneAccess(region) =>
                "cut bone for deep access, or close the incision if no repair remains",
            IncisionDepth.Retracted =>
                "repair exposed damage, or close the incision if no repair remains",
            IncisionDepth.DeepAccess =>
                "repair deep damage, then mend bone access",
            _ => string.Empty,
        };
    }

    private static string DescribeRequiredSurgeryStep(
        HumanMedicalComponent medical,
        RegionState region,
        SurgeryProcedureId procedure)
    {
        return procedure switch
        {
            SurgeryProcedureId.SealStump => HasShallowAccess(medical, region.Region)
                ? "seal the stump"
                : DescribePossibleSurgerySteps(region),
            SurgeryProcedureId.SutureWound => "suture the wound",
            SurgeryProcedureId.RepairInternalBleeding => HasShallowAccess(medical, region.Region)
                ? "repair the internal bleed"
                : DescribePossibleSurgerySteps(region),
            SurgeryProcedureId.RemoveEschar => HasShallowAccess(medical, region.Region)
                ? "remove the eschar"
                : DescribePossibleSurgerySteps(region),
            SurgeryProcedureId.RepairOrgan => HasRequiredRepairAccess(medical, region.Region)
                ? "repair the organ damage"
                : DescribePossibleSurgerySteps(region),
            SurgeryProcedureId.RepairFracture => HasRequiredRepairAccess(medical, region.Region)
                ? "set and mend the fracture"
                : DescribePossibleSurgerySteps(region),
            _ => DescribePossibleSurgerySteps(region),
        };
    }

    private static bool HasRequiredRepairAccess(HumanMedicalComponent medical, BodyRegion region)
    {
        return IsEncasedRegion(region)
            ? HasDeepAccess(medical, region)
            : HasShallowAccess(medical, region);
    }

    private static bool HasShallowAccess(HumanMedicalComponent medical, BodyRegion region)
    {
        return HumanMedicalLedger.GetRegion(medical, region).Incision >= IncisionDepth.Retracted;
    }

    private static bool HasDeepAccess(HumanMedicalComponent medical, BodyRegion region)
    {
        var state = HumanMedicalLedger.GetRegion(medical, region);
        return state.Incision == IncisionDepth.DeepAccess ||
            HasFracturedBoneAccess(state);
    }

    private static bool HasFracturedBoneAccess(RegionState region)
    {
        return IsEncasedRegion(region.Region) &&
            region.Incision >= IncisionDepth.Retracted &&
            region.Skeletal.Broken &&
            region.Skeletal.Severity.IsAtLeast(FractureSeverity.Compound);
    }

    private static bool IsEncasedRegion(BodyRegion region)
    {
        return region is BodyRegion.Head or BodyRegion.Chest;
    }

    private static bool TryGetShrapnelSummary(
        HumanMedicalComponent medical,
        BodyRegion region,
        out ShrapnelExamineSummary summary)
    {
        var fragments = 0;
        var severity = 0f;
        var depth = ForeignObjectDepth.Surface;

        foreach (var shrapnel in medical.ForeignObjects)
        {
            if (shrapnel.Region != region ||
                shrapnel.Kind != ForeignObjectKind.Shrapnel ||
                shrapnel.Fragments <= 0)
            {
                continue;
            }

            fragments += shrapnel.Fragments;
            severity = MathF.Max(severity, shrapnel.Severity);
            if (shrapnel.Depth > depth)
                depth = shrapnel.Depth;
        }

        summary = new ShrapnelExamineSummary(fragments, severity, depth);
        return fragments > 0;
    }

    private static string DescribeVisibleShrapnel(ShrapnelExamineSummary summary)
    {
        return summary.Depth switch
        {
            ForeignObjectDepth.Surgical => "surgically embedded shrapnel",
            ForeignObjectDepth.Deep => "deep embedded shrapnel",
            _ => "embedded shrapnel",
        };
    }

    private static string DescribeInspectShrapnelSite(ShrapnelExamineSummary summary, string partName)
    {
        return summary.Depth switch
        {
            ForeignObjectDepth.Surgical => $"Surgical {partName}",
            ForeignObjectDepth.Deep => $"Deep {partName}",
            _ => partName,
        };
    }

    private static string DescribeVisibleTreatedWounds(int count)
    {
        var noun = count == 1 ? "wound" : "wounds";
        return $"{noun} treated";
    }

    private static string DescribeDetailedInjury(InjuryRecord injury)
    {
        var treatment = IsInjuryTreated(injury) ? "treated" : "untreated";
        return ToDetailedLines(new List<string>
        {
            Color(
                $"{DescribeStage(injury.Stage)} {DescribeInjuryKind(injury.Kind)}",
                InjuryColor(injury)),
            Color(treatment, IsInjuryTreated(injury) ? TreatedWoundColor : DetailedUntreatedColor),
        });
    }

    private static string InspectHeader(InjuryRecord injury)
    {
        return Color(DescribeInspectTitle(injury), InjuryColor(injury));
    }

    private static string DescribeInspectTitle(InjuryRecord injury) => injury.Kind switch
    {
        InjuryKind.Burn => "Burns",
        InjuryKind.Bruise => "Bruises",
        InjuryKind.Puncture => "Punctures",
        InjuryKind.InternalBleed => "Internal Bleeding",
        InjuryKind.Stump => "Open Stumps",
        InjuryKind.SurgicalIncision => "Surgical Incisions",
        _ => "Wounds",
    };

    private static string DescribeInjuryKind(InjuryKind kind, int count = 1) => kind switch
    {
        InjuryKind.Cut => count == 1 ? "cut" : "cuts",
        InjuryKind.Puncture => count == 1 ? "hole" : "holes",
        InjuryKind.Bruise => count == 1 ? "bruise" : "bruises",
        InjuryKind.Burn => count == 1 ? "burn" : "burns",
        InjuryKind.InternalBleed => count == 1 ? "internal bleed" : "internal bleeds",
        InjuryKind.Stump => count == 1 ? "stump" : "stumps",
        InjuryKind.SurgicalIncision => count == 1 ? "surgical incision" : "surgical incisions",
        _ => count == 1 ? "wound" : "wounds",
    };

    private static string DescribeStage(InjuryStage stage, bool titleCase = false)
    {
        var text = stage switch
        {
            InjuryStage.None => "minor",
            InjuryStage.Tiny => "tiny",
            InjuryStage.Small => "small",
            InjuryStage.Moderate => "moderate",
            InjuryStage.Large => "large",
            InjuryStage.Deep => "deep",
            InjuryStage.Flesh => "flesh",
            InjuryStage.Gaping => "gaping",
            InjuryStage.GapingBig => "large gaping",
            InjuryStage.Massive => "massive",
            InjuryStage.Huge => "huge",
            InjuryStage.Monumental => "monumental",
            InjuryStage.Severe => "severe",
            InjuryStage.Carbonised => "carbonised",
            InjuryStage.InternalBleed => "active",
            InjuryStage.Stump => "open",
            _ => "moderate",
        };

        return titleCase ? CapitalizeFirst(text) : text;
    }

    private static string DescribePresence(LimbPresence presence) => presence switch
    {
        LimbPresence.Missing => "missing",
        LimbPresence.Detached => "severed",
        LimbPresence.Prosthetic => "prosthetic",
        _ => "present",
    };

    private static string PresenceColor(LimbPresence presence)
    {
        return presence == LimbPresence.Prosthetic
            ? ProstheticColor
            : SeveredColor;
    }

    private static string DescribeBleedSeverity(BleedSeverity severity) => severity switch
    {
        BleedSeverity.Trace => "trace",
        BleedSeverity.Light => "light",
        BleedSeverity.Moderate => "moderate",
        BleedSeverity.Heavy => "heavy",
        BleedSeverity.Critical => "critical",
        _ => "none",
    };

    private static string DescribeBleedKind(BleedKind kind) => kind switch
    {
        BleedKind.External => "external",
        BleedKind.Internal => "internal",
        BleedKind.Stump => "stump",
        _ => "unknown",
    };

    private static string ArticleFor(string nextWord)
    {
        if (string.IsNullOrEmpty(nextWord))
            return "a";

        return nextWord[0] is 'a' or 'e' or 'i' or 'o' or 'u'
            ? "an"
            : "a";
    }

    private static string InjuryColor(InjuryRecord injury)
    {
        return injury.Kind == InjuryKind.Burn
            ? DetailedBurnColor
            : DetailedWoundColor;
    }

    private static string InjurySiteColor(InjuryRecord injury)
    {
        return injury.Kind == InjuryKind.Burn
            ? DetailedBurnColor
            : DetailedInjurySiteColor;
    }

    private static bool IsInjuryClosed(InjuryRecord injury)
    {
        return injury.Flags.HasFlag(InjuryFlags.Closed) ||
               injury.Flags.HasFlag(InjuryFlags.Sutured);
    }

    private static bool IsInjuryTreated(InjuryRecord injury)
    {
        return injury.Flags.HasFlag(InjuryFlags.Bandaged) ||
               injury.Flags.HasFlag(InjuryFlags.Salved) ||
               injury.Flags.HasFlag(InjuryFlags.Clamped) ||
               injury.Flags.HasFlag(InjuryFlags.Sutured) ||
               injury.Flags.HasFlag(InjuryFlags.Closed);
    }

    private static bool IsBleedSuppressedButNeedsSurgery(BleedSource bleed)
    {
        if (bleed.Treatment.HasFlag(TreatmentFlags.Closed) ||
            bleed.Treatment.HasFlag(TreatmentFlags.Sutured))
        {
            return false;
        }

        return bleed.Treatment.HasFlag(TreatmentFlags.Clamped) ||
            bleed.Treatment.HasFlag(TreatmentFlags.TemporarilySuppressed) ||
            bleed.Treatment.HasFlag(TreatmentFlags.Tourniquetted);
    }

    private string FormatRegionName(BodyRegion region)
    {
        return Loc.GetString(HumanMedicalScannerBuiSystem.GetRegionLocKey(region));
    }

    private string PartHeader(BodyRegion region)
    {
        return $"[bold]{Color(FormatRegionName(region), DetailedPartColor)}[/bold]";
    }

    private static string Color(string text, string color)
    {
        return $"[color={color}]{text}[/color]";
    }

    private static string ToDetailedLines(List<string> sections)
    {
        return string.Join("\n  ", sections);
    }

    private static int BodyRegionSortOrder(BodyRegion region)
    {
        return (int) region;
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

    private static string ToVisibleConditionList(List<string> parts, out bool multiline)
    {
        multiline = false;
        foreach (var part in parts)
        {
            if (!part.Contains('\n'))
                continue;

            multiline = true;
            break;
        }

        if (multiline)
            return string.Join("\n  ", parts);

        return string.Join(", ", parts);
    }

    private static string CapitalizeFirst(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var chars = value.ToCharArray();
        chars[0] = char.ToUpperInvariant(chars[0]);
        return new string(chars);
    }

    private readonly record struct BodyPartExamineSummary(
        int Order,
        string Part,
        string Conditions,
        bool Multiline);

    private readonly record struct ShrapnelExamineSummary(
        int Fragments,
        float Severity,
        ForeignObjectDepth Depth);

    private sealed class VisibleInjuryBucket
    {
        public readonly InjuryKind Kind;
        public readonly InjuryStage Stage;
        public int Count = 1;

        public VisibleInjuryBucket(InjuryKind kind, InjuryStage stage)
        {
            Kind = kind;
            Stage = stage;
        }
    }

    private sealed class VisibleBleedBucket
    {
        public readonly bool Surgical;
        public FixedPoint2 Rate;

        public VisibleBleedBucket(bool surgical, FixedPoint2 rate)
        {
            Surgical = surgical;
            Rate = rate;
        }
    }

    private sealed class SuppressedBleedBucket
    {
        public readonly bool Surgical;

        public SuppressedBleedBucket(bool surgical)
        {
            Surgical = surgical;
        }
    }

    private sealed class InspectInjuryGroup
    {
        private readonly HashSet<string> _siteLines = new();
        private readonly string _siteColor;

        public int Order;
        public readonly string Header;
        public readonly List<string> SiteLines = new();

        public InspectInjuryGroup(int order, string header, string siteColor = DetailedInjurySiteColor)
        {
            Order = order;
            Header = header;
            _siteColor = siteColor;
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
