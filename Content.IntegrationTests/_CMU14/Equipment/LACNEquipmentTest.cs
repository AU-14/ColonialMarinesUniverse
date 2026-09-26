using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Equipment;

[TestFixture]
public sealed class LACNEquipmentTest : GameTest
{
    [Test]
    public async Task EngineeringBackpackCanRefillAnEmptyWelder()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var pack = SEntMan.SpawnEntity("CMULACNBackpackEngineer", map.GridCoords);
            var welder = SEntMan.SpawnEntity("RMCWelderEmpty", map.GridCoords);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetDrainableSolution(pack, out var tank, out var fuel), Is.True);
            Assert.That(fuel!.GetReagentQuantity(new ReagentId("WeldingFuel", null)).Float(), Is.EqualTo(650));
            Assert.That(solutions.TryGetRefillableSolution(welder, out var destination, out var contents), Is.True);
            Assert.That(Server.System<SolutionTransferSystem>().Transfer(null, pack, tank!.Value, welder, destination!.Value, 50).Float(), Is.EqualTo(50));
            Assert.That(contents!.GetReagentQuantity(new ReagentId("WeldingFuel", null)).Float(), Is.EqualTo(50));
            Assert.That(fuel.Volume.Float(), Is.EqualTo(600));
        });
    }

    [TestCase("CMULACNBeltMagazine", "RMCMagazineSniperXM43E1AntiMateriel")]
    [TestCase("CMULACNBeltMagazine", "CMMagazineSniperM96S")]
    [TestCase("CMULACNBeltStandard", "RMCMagazineSniperXM43E1AntiMateriel")]
    public async Task MagazineRigsAcceptSniperMagazines(string rig, string magazine)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var belt = SEntMan.SpawnEntity(rig, map.GridCoords);
            var ammo = SEntMan.SpawnEntity(magazine, map.GridCoords);
            Assert.That(Server.System<SharedStorageSystem>().Insert(belt, ammo, out _), Is.True);
            // The magazine-only rig must not become general purpose storage.
            if (rig == "CMULACNBeltMagazine")
            {
                var tool = SEntMan.SpawnEntity("CMWrench", map.GridCoords);
                Assert.That(Server.System<SharedStorageSystem>().Insert(belt, tool, out _), Is.False);
            }
        });
    }
}
