using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Construction.Prototypes;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Tools;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Construction;

[TestFixture]
public sealed class ToolsConstructionMergeRegressionTest : GameTest
{
    private static readonly string[] ToolQualities =
    [
        "Anchoring",
        "Brushing",
        "Cutting",
        "Honking",
        "Prying",
        "Pulsing",
        "Rolling",
        "Sawing",
        "Screwing",
        "Shearing",
        "Slicing",
        "VehicleServicing",
        "Welding",
    ];

    [Test]
    public async Task PrototypeInheritanceEventsAndToolQualitiesKeepBothContracts()
    {
        await Server.WaitAssertion(() =>
        {
            var constructionProperties = typeof(ConstructionPrototype)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(typeof(IInheritingPrototype).IsAssignableFrom(typeof(ConstructionPrototype)), Is.True);
                Assert.That(constructionProperties.Count(property => property.Name == nameof(ConstructionPrototype.Parents)),
                    Is.EqualTo(1), "ConstructionPrototype must have one authoritative upstream Parents field.");
                Assert.That(constructionProperties.Count(property => property.Name == nameof(ConstructionPrototype.Abstract)),
                    Is.EqualTo(1), "ConstructionPrototype must have one authoritative upstream Abstract field.");

                Assert.That(typeof(IInheritingPrototype).IsAssignableFrom(typeof(ConstructionGraphPrototype)), Is.True,
                    "ConstructionGraph still relies on the RMC partial for prototype inheritance.");
                Assert.That(typeof(InitialConstructionDoAfterEvent), Is.Not.Null);
                Assert.That(typeof(ToolRefineDoAfterEvent), Is.Not.Null);
                Assert.That(typeof(ConstructionPrototype).Assembly.GetType(
                        "Content.Shared.Construction.WelderRefineDoAfterEvent"),
                    Is.Null, "The zero-reference WelderRefine event must not survive the ToolRefine successor.");
            }

            var rmcConstruction = SProtoMan.Index<ConstructionPrototype>("CMBarricadeMetal");
            var inheritedConstruction = SProtoMan.Index<ConstructionPrototype>("CMChair");
            var inheritedGraph = SProtoMan.Index<ConstructionGraphPrototype>("CMSeat");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(rmcConstruction.Parents, Does.Contain("RMC"));
                Assert.That(rmcConstruction.IsCM, Is.True);
                Assert.That(rmcConstruction.RMCPrototype?.Id, Is.EqualTo("RMCMetalBarricadeBuild"));
                Assert.That(inheritedConstruction.IsCM, Is.True,
                    "The upstream-owned ConstructionPrototype inheritance fields must still propagate fork IsCM.");

                Assert.That(inheritedGraph.Parents, Does.Contain("RMC"));
                Assert.That(inheritedGraph.IsCM, Is.True,
                    "The RMC ConstructionGraph inheritance partial must remain authoritative.");
            }

            var actualQualities = SProtoMan.EnumeratePrototypes<ToolQualityPrototype>()
                .Select(prototype => prototype.ID)
                .Order()
                .ToArray();
            Assert.That(actualQualities, Is.EqualTo(ToolQualities),
                "Every typed Tool/component/construction-graph quality reference must resolve to the exact merged registry.");
        });
    }

    [Test]
    public async Task AnchorableSkipsSelfAndHonorsRmcTileFreeOverride()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var anchorable = SEntMan.System<AnchorableSystem>();
            var subject = SEntMan.SpawnEntity("Table", map.GridCoords);
            var blocker = SEntMan.SpawnEntity("Table", map.GridCoords);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(SComp<TransformComponent>(subject).Anchored, Is.True,
                    "Table must start anchored for the blocker setup to exercise the anchorability contract.");
                Assert.That(SComp<TransformComponent>(blocker).Anchored, Is.True,
                    "Table must start anchored for the blocker setup to exercise the anchorability contract.");
            }

            var physics = SComp<PhysicsComponent>(subject);
            Assert.That(anchorable.CanAnchorAt((subject, physics), map.GridCoords), Is.False,
                "The self exclusion must not hide another hard anchored blocker on the tile.");

            SEntMan.AddComponent<AnchorableTileFreeMergeProbeComponent>(subject);
            Assert.That(anchorable.CanAnchorAt((subject, physics), map.GridCoords), Is.True,
                "The fork RMCCheckTileFree override must be evaluated after the entity excludes itself.");
            var probe = SComp<AnchorableTileFreeMergeProbeComponent>(subject);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(probe.Checks, Is.EqualTo(1));
                Assert.That(probe.SawSelf, Is.False,
                    "The subject must be skipped before the RMC override is raised for other blockers.");
            }
        });
    }

    [Test]
    public async Task MachinePartMaterialCostUsesTheSystemPrototypeManager()
    {
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity("StationAnchorCircuitboard", MapCoordinates.Nullspace);
            var board = SComp<MachineBoardComponent>(uid);
            var machineParts = SEntMan.System<MachinePartSystem>();

            Assert.That(machineParts.TryGetMachineBoardMaterialCost((uid, board), out var single), Is.True);
            Assert.That(machineParts.TryGetMachineBoardMaterialCost((uid, board), out var doubled, 2), Is.True);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(single, Is.Not.Empty);
                Assert.That(doubled.Keys, Is.EquivalentTo(single.Keys));
                foreach (var (material, amount) in single)
                    Assert.That(doubled[material], Is.EqualTo(amount * 2), material);
            }
        });
    }
}

[RegisterComponent]
public sealed partial class AnchorableTileFreeMergeProbeComponent : Component
{
    public int Checks;
    public bool SawSelf;
}

public sealed class AnchorableTileFreeMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnchorableTileFreeMergeProbeComponent, RMCCheckTileFreeEvent>(OnCheckTileFree);
    }

    private static void OnCheckTileFree(
        Entity<AnchorableTileFreeMergeProbeComponent> ent,
        ref RMCCheckTileFreeEvent args)
    {
        ent.Comp.Checks++;
        ent.Comp.SawSelf |= args.AnchoredEntity == ent.Owner;
        args.IsTileFree = args.AnchoredEntity != ent.Owner;
    }
}
