using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUBracerInjectorTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase("CMUYautjaStabilisingCrystal", "CMUMobYautja")]
    [TestCase("CMUYautjaHumanStabilisingCrystal", "CMMobHuman")]
    public async Task FabricatedInjectorTransfersItsMedicine(string injectorPrototype, string patientPrototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var injector = SEntMan.SpawnEntity(injectorPrototype, map.GridCoords);
            var patient = SEntMan.SpawnEntity(patientPrototype, map.GridCoords);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetInjectableSolution(patient, out _, out var bloodstream), Is.True);
            var before = bloodstream.Volume;
            var hypo = SEntMan.GetComponent<HyposprayComponent>(injector);
            Server.System<HypospraySystem>().TryDoInject((injector, hypo), patient, patient, doAfter: false);
            Assert.That(bloodstream.Volume, Is.EqualTo(before + hypo.TransferAmount),
                "using a fabricated injector must transfer medicine into the patient");
        });
    }
}
