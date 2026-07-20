using System.Linq;
using Content.Server.Body.Systems;
using Content.Server.Medical;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(VomitSystem))]
public sealed class VomitDeadMobTest
{
    private const string SubjectPrototype = "VomitDeadMobTestSubject";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: VomitDeadMobTestSubject
  components:
  - type: MobState
  - type: Body
    prototype: Human
";

    [Test]
    public async Task DeadMobOnlyVomitsWhenForced()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var body = server.System<BodySystem>();
        var mobState = server.System<MobStateSystem>();
        var solutions = server.System<SharedSolutionContainerSystem>();
        var vomit = server.System<VomitSystem>();

        await server.WaitAssertion(() =>
        {
            var subject = entMan.SpawnEntity(SubjectPrototype, map.GridCoords);
            var stomach = body.GetBodyOrganEntityComps<StomachComponent>(subject).Single();

            Assert.That(solutions.ResolveSolution(
                stomach.Owner,
                StomachSystem.DefaultSolutionName,
                ref stomach.Comp1.Solution,
                out var stomachSolution));

            var water = FixedPoint2.New(5);
            var solution = stomachSolution!;
            Assert.That(solutions.TryAddReagent(stomach.Comp1.Solution!.Value, "Water", water));
            mobState.ChangeMobState(subject, MobState.Dead);

            vomit.Vomit(subject);
            Assert.That(solution.GetTotalPrototypeQuantity("Water"), Is.EqualTo(water));

            vomit.Vomit(subject, force: true);
            Assert.That(solution.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }
}
