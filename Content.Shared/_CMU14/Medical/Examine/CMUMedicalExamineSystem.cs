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
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Examine;

public sealed partial class CMUMedicalExamineSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private const string UntreatedWoundColor = "#ff4d4d";
    private const string TreatedWoundColor = "#7bd88f";
    private const string FractureColor = "#dca94c";
    private const string SeveredColor = "#ff4d4d";

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
        var now = _timing.CurTime;
        var partSummaries = new List<BodyPartExamineSummary>();

        foreach (var (partUid, part) in _body.GetBodyChildren(body))
        {
            var sections = new List<string>();

            if (includeWounds)
            {
                var untreated = new List<string>();
                var treated = new List<string>();
                if (TryComp<BodyPartWoundComponent>(partUid, out var wounds))
                {
                    for (var i = 0; i < wounds.Wounds.Count; i++)
                    {
                        var wound = wounds.Wounds[i];
                        var size = i < wounds.Sizes.Count ? wounds.Sizes[i] : WoundSize.Deep;
                        if (wound.Treated)
                            treated.Add(DescribeWound(wound, size, now));
                        else
                            untreated.Add(DescribeWound(wound, size, now));
                    }
                }

                if (HasComp<CMUEscharComponent>(partUid))
                    untreated.Add("charred burn tissue");

                if (untreated.Count > 0)
                    sections.Add($"[color={UntreatedWoundColor}]{ToSentence(untreated)}[/color]");

                if (treated.Count > 0)
                    sections.Add($"[color={TreatedWoundColor}]{ToSentence(treated)}[/color]");
            }

            if (includeFractures
                && TryComp<FractureComponent>(partUid, out var fracture)
                && fracture.Severity != FractureSeverity.None)
            {
                var stabilized = HasComp<CMUSplintedComponent>(partUid) || HasComp<CMUCastComponent>(partUid);
                sections.Add($"[color={FractureColor}]{DescribeFracture(fracture.Severity, stabilized)}[/color]");
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

    private static string DescribeWound(Wound wound, WoundSize size, TimeSpan now)
    {
        var sizeText = size switch
        {
            WoundSize.Small => "small",
            WoundSize.Deep => "deep",
            WoundSize.Gaping => "gaping",
            WoundSize.Massive => "massive",
            _ => "deep",
        };

        var kind = wound.Type switch
        {
            WoundType.Burn => "burn",
            WoundType.Surgery => "surgical wound",
            _ => "trauma wound",
        };

        var treated = wound.Treated ? "treated " : string.Empty;
        var bleeding = !wound.Treated
            && wound.Bloodloss > 0f
            && (wound.StopBleedAt is null || now < wound.StopBleedAt.Value)
                ? " (bleeding)"
                : string.Empty;

        return $"a {treated}{sizeText} {kind}{bleeding}";
    }

    private static string DescribeFracture(FractureSeverity severity, bool stabilized)
    {
        var prefix = stabilized ? "stabilized " : string.Empty;
        return severity switch
        {
            FractureSeverity.Hairline => $"a {prefix}hairline fracture",
            FractureSeverity.Simple => $"a {prefix}broken bone",
            FractureSeverity.Compound => $"a {prefix}compound fracture",
            FractureSeverity.Comminuted => $"a {prefix}shattered bone",
            _ => "a broken bone",
        };
    }

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
            BodyPartType.Arm when symmetry == BodyPartSymmetry.Left => 10,
            BodyPartType.Hand when symmetry == BodyPartSymmetry.Left => 11,
            BodyPartType.Torso => 20,
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
}
