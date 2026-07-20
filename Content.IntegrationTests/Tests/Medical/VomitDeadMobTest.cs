using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
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
        var mobState = server.System<MobStateSystem>();
        var solutions = server.System<SharedSolutionContainerSystem>();
        var vomit = server.System<VomitSystem>();

        await server.WaitAssertion(() =>
        {
            var subject = entMan.SpawnEntity(SubjectPrototype, map.GridCoords);
            var body = entMan.GetComponent<BodyComponent>(subject);
            var stomachUid = body.Organs!.ContainedEntities.Single(entMan.HasComponent<StomachComponent>);
            var stomach = entMan.GetComponent<StomachComponent>(stomachUid);

            Assert.That(solutions.ResolveSolution(
                stomachUid,
                StomachSystem.DefaultSolutionName,
                ref stomach.Solution,
                out var stomachSolution));

            var water = FixedPoint2.New(5);
            var solution = stomachSolution!;
            Assert.That(solutions.TryAddReagent(stomach.Solution!.Value, "Water", water));
            mobState.ChangeMobState(subject, MobState.Dead);

            vomit.Vomit(subject);
            Assert.That(solution.GetTotalPrototypeQuantity("Water"), Is.EqualTo(water));

            vomit.Vomit(subject, force: true);
            Assert.That(solution.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }
}
