using System.Collections.Generic;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Body.Systems;
using Content.Shared.Examine;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Synthetic;

public sealed partial class CMUSynthLimbSurgeryExamineSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private const int SurgerySkillNovice = 1;
    private const string DetailColor = "#ffb86c";
    private const string HeaderColor = "#7ac7ff";
    private const string NextColor = "#ffd166";

    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUSynthLimbSurgeryComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<CMUSynthLimbSurgeryComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Slots.Count == 0)
            return;

        using (args.PushGroup(nameof(CMUSynthLimbSurgeryExamineSystem), -1))
        {
            var detailed = _skills.HasSkill(args.Examiner, SurgerySkill, SurgerySkillNovice);
            var slots = new List<CMUSynthLimbSurgeryState>(ent.Comp.Slots);
            slots.Sort((a, b) => SlotSortOrder(a.SlotId).CompareTo(SlotSortOrder(b.SlotId)));

            foreach (var state in slots)
            {
                var part = FormatPartName(state.SlotId);
                if (!detailed)
                {
                    args.PushMarkup(Loc.GetString(
                        "cmu-synth-surgery-examine-unskilled",
                        ("part", part)));
                    continue;
                }

                args.PushMarkup(FormatDetailedExamine(part, state));
            }
        }
    }

    private string FormatDetailedExamine(string part, CMUSynthLimbSurgeryState state)
    {
        var stage = Loc.GetString(StageLoc(state.Stage));
        var next = Loc.GetString(NextLoc(state));

        return Loc.GetString(
            "cmu-synth-surgery-examine-skilled-header",
            ("part", part)) +
            "\n  " +
            Color(Loc.GetString("cmu-synth-surgery-examine-title"), HeaderColor) +
            "\n    " +
            Color(Loc.GetString("cmu-synth-surgery-examine-chassis", ("stage", stage)), DetailColor) +
            "\n    " +
            Color(Loc.GetString("cmu-synth-surgery-examine-procedure"), DetailColor) +
            "\n    " +
            Color(Loc.GetString("cmu-synth-surgery-examine-next", ("next", next)), NextColor);
    }

    private string NextLoc(CMUSynthLimbSurgeryState state)
    {
        return state.Stage switch
        {
            CMUSynthLimbSurgeryStage.ChassisOpen => "cmu-synth-surgery-examine-next-wire",
            CMUSynthLimbSurgeryStage.WiringPrepped when IsSlotOccupied(state) => "cmu-synth-surgery-examine-next-pry",
            CMUSynthLimbSurgeryStage.WiringPrepped => "cmu-synth-surgery-examine-next-limb",
            CMUSynthLimbSurgeryStage.LimbAttached => "cmu-synth-surgery-examine-next-weld",
            _ => "cmu-synth-surgery-examine-next-weld",
        };
    }

    private bool IsSlotOccupied(CMUSynthLimbSurgeryState state)
    {
        return _containers.TryGetContainer(
                state.Parent,
                SharedBodySystem.GetPartSlotContainerId(state.SlotId),
                out var container) &&
            container.ContainedEntities.Count > 0;
    }

    private static string StageLoc(CMUSynthLimbSurgeryStage stage)
    {
        return stage switch
        {
            CMUSynthLimbSurgeryStage.ChassisOpen => "cmu-synth-surgery-examine-stage-open",
            CMUSynthLimbSurgeryStage.WiringPrepped => "cmu-synth-surgery-examine-stage-wiring",
            CMUSynthLimbSurgeryStage.LimbAttached => "cmu-synth-surgery-examine-stage-limb",
            _ => "cmu-synth-surgery-examine-stage-open",
        };
    }

    private static int SlotSortOrder(string slotId)
    {
        return slotId switch
        {
            "head" => 0,
            "left_arm" => 10,
            "left_hand" => 11,
            "right_arm" => 20,
            "right_hand" => 21,
            "left_leg" => 30,
            "left_foot" => 31,
            "right_leg" => 40,
            "right_foot" => 41,
            _ => 100,
        };
    }

    private static string FormatPartName(string slotId)
    {
        var text = slotId.Replace('_', ' ');
        return text.Length == 0
            ? text
            : string.Concat(char.ToUpperInvariant(text[0]), text[1..]);
    }

    private static string Color(string text, string color)
    {
        return $"[color={color}]{text}[/color]";
    }
}
