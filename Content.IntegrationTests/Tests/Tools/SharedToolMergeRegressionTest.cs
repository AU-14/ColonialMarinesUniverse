#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Reflection;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Tools;
using Content.Shared.DoAfter;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.Tests.Tools;

public sealed class SharedToolMergeRegressionTest : InteractionTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseItem
  id: SharedToolMergeRegressionTool
  components:
  - type: Tool
    speedModifier: 2
    qualities:
    - Anchoring
  - type: ToolMergeProbe

- type: entity
  parent: BaseItem
  id: SharedToolMergeRegressionTarget
  components:
  - type: ToolMergeProbe
";

    [SidedDependency(Side.Server)] private readonly SharedToolSystem _tools = default!;

    [Test]
    public async Task RmcDelayDuplicatePredictionAndExamineUnionIsRetained()
    {
        var firstTargetNet = await SpawnTarget("SharedToolMergeRegressionTarget");
        var secondTargetNet = await Spawn("SharedToolMergeRegressionTarget");
        var toolNet = await PlaceInHands("SharedToolMergeRegressionTool");
        var firstTarget = ToServer(firstTargetNet);
        var secondTarget = ToServer(secondTargetNet);
        var tool = ToServer(toolNet);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_tools.UseTool(
                tool,
                SPlayer,
                firstTarget,
                TimeSpan.FromSeconds(8),
                new ProtoId<ToolQualityPrototype>[] { "Anchoring" },
                new SharedToolMergeMutableDoAfterEvent(1),
                out var firstId,
                duplicateCondition: DuplicateConditions.SameEvent |
                                    DuplicateConditions.SameTarget |
                                    DuplicateConditions.SameTool,
                predicted: false),
                Is.True);
            Assert.That(firstId, Is.Not.Null);
        });
        await RunTicks(1);

        var first = ActiveDoAfters.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(2)),
                "The RMC delay interception must run before Tool.SpeedModifier is applied.");
            Assert.That(first.Args.DuplicateCondition,
                Is.EqualTo(DuplicateConditions.SameEvent |
                           DuplicateConditions.SameTarget |
                           DuplicateConditions.SameTool));
            Assert.That(first.Args.ExamineText, Is.Not.Null.And.Not.Empty,
                "The upstream tool-use examine text must survive the fork do-after wrapper.");
            Assert.That(ReadPredicted(first.Args.Event), Is.False);
        }

        var clone = first.Args.Event.Clone();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(clone, Is.Not.SameAs(first.Args.Event),
                "A mutable wrapped event must force the tool wrapper to clone.");
            Assert.That(ReadPredicted(clone), Is.False,
                "The server/PVS prediction choice must survive ToolDoAfterEvent.Clone().");
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(_tools.UseTool(
                tool,
                SPlayer,
                firstTarget,
                8,
                new ProtoId<ToolQualityPrototype>("Anchoring"),
                new SharedToolMergeMutableDoAfterEvent(1),
                duplicateCondition: DuplicateConditions.SameEvent |
                                    DuplicateConditions.SameTarget |
                                    DuplicateConditions.SameTool),
                Is.True);
        });
        await RunTicks(1);

        Assert.That(ActiveDoAfters, Is.Empty,
            "The single-quality overload must forward the duplicate conditions so the matching do-after is cancelled without replacement.");
        Assert.That(SComp<SharedToolMergeProbeComponent>(firstTarget).RmcDoAfterCancellations, Is.EqualTo(1));

        await Server.WaitAssertion(() =>
        {
            Assert.That(_tools.UseTool(
                tool,
                SPlayer,
                secondTarget,
                TimeSpan.FromSeconds(8),
                new ProtoId<ToolQualityPrototype>[] { "Anchoring" },
                new SharedToolMergeMutableDoAfterEvent(2),
                out var changedTargetId,
                duplicateCondition: DuplicateConditions.SameEvent |
                                    DuplicateConditions.SameTarget |
                                    DuplicateConditions.SameTool,
                predicted: false),
                Is.True);
            Assert.That(changedTargetId, Is.Not.Null,
                "Changing a selected duplicate dimension must allow a second tool use.");
        });
        await RunTicks(1);

        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));
        await CancelDoAfters();

        var firstProbe = SComp<SharedToolMergeProbeComponent>(firstTarget);
        var secondProbe = SComp<SharedToolMergeProbeComponent>(secondTarget);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SComp<SharedToolMergeProbeComponent>(tool).ToolUseEvents, Is.EqualTo(3));
            Assert.That(firstProbe.RmcDoAfterCancellations, Is.EqualTo(1));
            Assert.That(firstProbe.WrappedDoAfterCancellations, Is.EqualTo(1));
            Assert.That(secondProbe.RmcDoAfterCancellations, Is.EqualTo(1));
            Assert.That(secondProbe.WrappedDoAfterCancellations, Is.EqualTo(1));
        }
    }

    private static bool ReadPredicted(DoAfterEvent ev)
    {
        var field = ev.GetType().GetField("Predicted", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "The tool wrapper must retain its prediction field.");
        return (bool) field!.GetValue(ev)!;
    }
}

[RegisterComponent]
public sealed partial class SharedToolMergeProbeComponent : Component
{
    public int ToolUseEvents;
    public int RmcDoAfterCancellations;
    public int WrappedDoAfterCancellations;
}

public sealed class SharedToolMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharedToolMergeProbeComponent, RMCToolUseEvent>(OnToolUse);
        SubscribeLocalEvent<SharedToolMergeProbeComponent, RMCToolDoAfterEvent>(OnRmcDoAfter);
        SubscribeLocalEvent<SharedToolMergeProbeComponent, SharedToolMergeMutableDoAfterEvent>(OnWrappedDoAfter);
    }

    private static void OnToolUse(Entity<SharedToolMergeProbeComponent> ent, ref RMCToolUseEvent args)
    {
        ent.Comp.ToolUseEvents++;
        args.Delay = TimeSpan.FromSeconds(4);
        args.Handled = true;
    }

    private static void OnRmcDoAfter(Entity<SharedToolMergeProbeComponent> ent, ref RMCToolDoAfterEvent args)
    {
        if (args.Cancelled)
            ent.Comp.RmcDoAfterCancellations++;
    }

    private static void OnWrappedDoAfter(
        Entity<SharedToolMergeProbeComponent> ent,
        ref SharedToolMergeMutableDoAfterEvent args)
    {
        if (args.Cancelled)
            ent.Comp.WrappedDoAfterCancellations++;
    }
}

[Serializable, NetSerializable]
public sealed partial class SharedToolMergeMutableDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Value;

    private SharedToolMergeMutableDoAfterEvent()
    {
    }

    public SharedToolMergeMutableDoAfterEvent(int value)
    {
        Value = value;
    }

    public override DoAfterEvent Clone() => new SharedToolMergeMutableDoAfterEvent(Value);
}

#pragma warning restore RA0002
