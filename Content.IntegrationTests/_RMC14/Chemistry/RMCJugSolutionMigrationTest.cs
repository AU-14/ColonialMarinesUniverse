using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Nutrition.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Chemistry;

[TestFixture]
[TestOf(typeof(SolutionComponent))]
public sealed class RMCJugSolutionMigrationTest : GameTest
{
    private const string SolutionName = "beaker";
    private const int Capacity = 200;

    private static readonly IReadOnlyDictionary<string, string?> Jugs =
        new Dictionary<string, string?>
        {
            ["RMCJug"] = null,
            ["RMCJugCarbon"] = "RMCCarbon",
            ["RMCJugFluorine"] = "RMCFluorine",
            ["RMCJugChlorine"] = "RMCChlorine",
            ["RMCJugAluminum"] = "RMCAluminum",
            ["RMCJugPhosphorus"] = "RMCPhosphorus",
            ["RMCJugSulfur"] = "RMCSulfur",
            ["RMCJugSilicon"] = "RMCSilicon",
            ["RMCJugHydrogen"] = "RMCHydrogen",
            ["RMCJugLithium"] = "RMCLithium",
            ["RMCJugSodium"] = "RMCSodium",
            ["RMCJugPotassium"] = "RMCPotassium",
            ["RMCJugRadium"] = "RMCRadium",
            ["RMCJugIron"] = "RMCIron",
            ["RMCJugCopper"] = "RMCCopper",
            ["RMCJugGold"] = "RMCGold",
            ["RMCJugUranium"] = "RMCUranium",
            ["RMCJugPlatinum"] = "RMCPlatinum",
            ["RMCJugMercury"] = "RMCMercury",
            ["RMCJugSilver"] = "RMCSilver",
            ["RMCJugEthanol"] = "RMCEthanol",
            ["RMCJugSugar"] = "RMCSugar",
            ["RMCJugNitrogen"] = "RMCNitrogen",
            ["RMCJugOxygen"] = "RMCOxygen",
            ["RMCJugWater"] = "Water",
            ["RMCJugSulphuricAcid"] = "RMCSulphuricAcid",
        };

    [SidedDependency(Side.Server)]
    private SharedSolutionContainerSystem _solutions = default!;

    [Test]
    public async Task AllJugSolutionsAreDirectAndPreserveContentsAndCapabilities()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Jugs, Has.Count.EqualTo(26));
            var factory = SEntMan.ComponentFactory;

            foreach (var (prototypeId, reagentId) in Jugs)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
                var enumerated = _solutions.EnumerateSolutions(prototype).ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(prototype.TryComp<SolutionComponent>(out _, factory), Is.True, prototypeId);
                    Assert.That(prototype.TryComp<SolutionContainerManagerComponent>(out _, factory), Is.False,
                        prototypeId);
                    Assert.That(prototype.TryComp<SolutionManagerComponent>(out _, factory), Is.False, prototypeId);
                    Assert.That(_solutions.TryGetSolution(prototype, SolutionName, out var prototypeSolution), Is.True,
                        prototypeId);
                    AssertSolution(prototypeSolution!, reagentId, prototypeId);
                    Assert.That(enumerated, Has.Length.EqualTo(1),
                        $"{prototypeId} must enumerate its direct solution exactly once");
                    Assert.That(enumerated[0].Id, Is.EqualTo(SolutionName), prototypeId);
                    AssertSolution(enumerated[0].Solution, reagentId, $"{prototypeId} enumerated");
                });

                var jug = SEntMan.SpawnEntity(prototypeId, map.GridCoords);
                Assert.That(_solutions.TryGetSolution(jug, SolutionName, out var solutionEntity, out var solution),
                    Is.True, prototypeId);
                Assert.Multiple(() =>
                {
                    Assert.That(solutionEntity!.Value.Owner, Is.EqualTo(jug),
                        $"{prototypeId} must own its sole solution directly");
                    Assert.That(SEntMan.HasComponent<SolutionContainerManagerComponent>(jug), Is.False, prototypeId);
                    Assert.That(SEntMan.HasComponent<SolutionManagerComponent>(jug), Is.False, prototypeId);
                    AssertSolution(solution!, reagentId, prototypeId);
                });
            }

            var basePrototype = SProtoMan.Index<EntityPrototype>("RMCJug");
            AssertSolutionName<MixableSolutionComponent>(basePrototype, component => component.Solution);
            AssertSolutionName<RefillableSolutionComponent>(basePrototype, component => component.Solution);
            AssertSolutionName<DrainableSolutionComponent>(basePrototype, component => component.Solution);
            AssertSolutionName<ExaminableSolutionComponent>(basePrototype, component => component.Solution);
            AssertSolutionName<DrawableSolutionComponent>(basePrototype, component => component.Solution);
            AssertSolutionName<InjectableSolutionComponent>(basePrototype, component => component.Solution);
            AssertSolutionName<EdibleComponent>(basePrototype, component => component.Solution);
            AssertSolutionName<SpillableComponent>(basePrototype, component => component.SolutionName);

            void AssertSolutionName<T>(EntityPrototype prototype, Func<T, string> getName)
                where T : Component, new()
            {
                Assert.That(prototype.TryComp<T>(out var component, factory), Is.True, typeof(T).Name);
                Assert.That(getName(component!), Is.EqualTo(SolutionName), typeof(T).Name);
            }
        });
    }

    [Test]
    public async Task WaterJugReplicatesFullHalfAndEmptyFillVisuals()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        NetEntity jugNet = default;
        Entity<SolutionComponent> solutionEntity = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var jug = SEntMan.SpawnEntity("RMCJugWater", map.GridCoords);
                jugNet = SEntMan.GetNetEntity(jug);
                Assert.That(_solutions.TryGetSolution(jug, SolutionName, out var solution, out _), Is.True);
                solutionEntity = solution!.Value;
                Assert.That(solutionEntity.Owner, Is.EqualTo(jug));
                AssertServerState(Capacity, 1f);
                Server.PlayerMan.SetAttachedEntity(session, jug);
            });
            await Pair.RunUntilSynced();

            await AssertClientFill(Capacity, "fill-6", visible: true);

            await Server.WaitPost(() =>
            {
                var removed = _solutions.RemoveReagent(solutionEntity, "Water", FixedPoint2.New(100));
                Assert.That(removed, Is.EqualTo(FixedPoint2.New(100)));
                AssertServerState(100, 0.5f);
            });
            await Pair.RunUntilSynced();

            await AssertClientFill(100, "fill-3", visible: true);

            await Server.WaitPost(() =>
            {
                _solutions.RemoveAllSolution(solutionEntity);
                AssertServerState(0, 0f);
            });
            await Pair.RunUntilSynced();

            await AssertClientFill(0, "fill-3", visible: false);
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
        }

        void AssertServerState(int volume, float fillFraction)
        {
            var solution = SEntMan.GetComponent<SolutionComponent>(solutionEntity).Solution;
            var appearance = Server.System<SharedAppearanceSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(volume)));
                Assert.That(appearance.TryGetData<float>(
                    solutionEntity.Owner,
                    SolutionContainerVisuals.FillFraction,
                    out var actualFraction), Is.True);
                Assert.That(actualFraction, Is.EqualTo(fillFraction));
            });
        }

        async Task AssertClientFill(int volume, string state, bool visible)
        {
            await Client.WaitAssertion(() =>
            {
                var jug = CEntMan.GetEntity(jugNet);
                var sprite = CEntMan.GetComponent<SpriteComponent>(jug);
                var solution = CEntMan.GetComponent<SolutionComponent>(jug).Solution;
                var sprites = Client.System<SpriteSystem>();
                Assert.That(sprites.LayerMapTryGet(
                    (jug, sprite),
                    SolutionContainerLayers.Fill,
                    out var layer,
                    false), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(volume)));
                    Assert.That(sprite[layer].RsiState.Name, Is.EqualTo(state));
                    Assert.That(sprite[layer].Visible, Is.EqualTo(visible));
                });
            });
        }
    }

    private static void AssertSolution(Solution solution, string? reagentId, string prototypeId)
    {
        Assert.Multiple(() =>
        {
            Assert.That(solution.MaxVolume, Is.EqualTo(FixedPoint2.New(Capacity)), prototypeId);
            Assert.That(solution.Temperature, Is.EqualTo(293.15f), prototypeId);
            Assert.That(solution.CanReact, Is.True, prototypeId);
            Assert.That(solution.Contents, Has.Count.EqualTo(reagentId == null ? 0 : 1), prototypeId);
            Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(reagentId == null ? 0 : Capacity)), prototypeId);
            if (reagentId != null)
            {
                Assert.That(solution.GetTotalPrototypeQuantity(reagentId),
                    Is.EqualTo(FixedPoint2.New(Capacity)), prototypeId);
            }
        });
    }
}
