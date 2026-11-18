using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands.EntitySystems;

namespace Content.IntegrationTests.CMU14.Medical;

public sealed class CMUPouchInjectionTest : GameTest
{
    [Test]
    public async Task OtherPatientReceivesDoseOnlyAfterWindup()
    {
        var map = await Pair.CreateTestMap();
        EntityUid injector = default;
        float startVolume = default;
        float Remaining()
        {
            Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(injector, "pen", out _, out var solution), Is.True);
            return solution!.Volume.Float();
        }

        await Server.WaitAssertion(() =>
        {
            var user = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var patient = SSpawnAtPosition("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(0.75f, 0f)));
            injector = SSpawnAtPosition("RMCMedicAutoInjectorBicaridine15", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, injector), Is.True);
            startVolume = Remaining();
            Assert.That(Server.System<HypospraySystem>().TryDoInject((injector, SComp<HyposprayComponent>(injector)), patient, user), Is.True);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() => Assert.That(Remaining(), Is.EqualTo(startVolume)));
        await Pair.RunSeconds(2f);
        await Server.WaitAssertion(() => Assert.That(Remaining(), Is.LessThan(startVolume)));
    }
}
