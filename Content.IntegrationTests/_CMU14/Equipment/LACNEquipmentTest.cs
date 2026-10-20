using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Equipment;

[TestFixture]
public sealed class LACNEquipmentTest : GameTest
{
    [TestCase("CMULACNBackpackEngineer", 650)]
    [TestCase("CMULACNSatchelEngineer", 250)]
    public async Task EngineeringPacksCanRefillAnEmptyWelder(string packId, float capacity)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var pack = SEntMan.SpawnEntity(packId, map.GridCoords);
            var welder = SEntMan.SpawnEntity("RMCWelderEmpty", map.GridCoords);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetDrainableSolution(pack, out var tank, out var fuel), Is.True);
            Assert.That(fuel!.GetReagentQuantity(new ReagentId("WeldingFuel", null)).Float(), Is.EqualTo(capacity));
            Assert.That(solutions.TryGetRefillableSolution(welder, out var destination, out var contents), Is.True);
            Assert.That(Server.System<SolutionTransferSystem>().Transfer(null, pack, tank!.Value, welder, destination!.Value, 50).Float(), Is.EqualTo(50));
            Assert.That(contents!.GetReagentQuantity(new ReagentId("WeldingFuel", null)).Float(), Is.EqualTo(50));
            Assert.That(fuel.Volume.Float(), Is.EqualTo(capacity - 50));
        });
    }

    [Test]
    public async Task GrenadeWebbingHoldsEightGrenades()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var webbing = SEntMan.SpawnEntity("CMULACNWebbingGrenade", map.GridCoords);
            var storage = Server.System<SharedStorageSystem>();
            for (var i = 0; i < 8; i++)
            {
                var grenade = SEntMan.SpawnEntity("RMCGrenadeFlashBang", map.GridCoords);
                Assert.That(storage.Insert(webbing, grenade, out _), Is.True, $"grenade {i + 1} did not fit");
            }

            var extra = SEntMan.SpawnEntity("RMCGrenadeFlashBang", map.GridCoords);
            Assert.That(storage.Insert(webbing, extra, out _), Is.False);
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
