using Content.Server._RMC14.Language.Systems;
using Content.Server.Electrocution;
using Content.Server.Humanoid.Systems;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaLanguageAndInsulationTest
{

    [TestCase("CMUYautjaHunterGear")]
    public async Task RegularLoadoutBracerInsulatesOnlyWhileWorn(string loadoutId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var spawning = entMan.System<StationSpawningSystem>();
            var electrocution = entMan.System<ElectrocutionSystem>();
            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gear = server.ProtoMan.Index<StartingGearPrototype>(loadoutId);

            try
            {
                spawning.EquipStartingGear(hunter, gear);
                Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);
                Assert.That(bracer, Is.Not.Null);
                Assert.That(PrototypeId(entMan, bracer!.Value), Is.EqualTo("CMUYautjaBracer"));
                Assert.That(entMan.HasComponent<YautjaBracerComponent>(bracer.Value), Is.True);
                var gearContainer = entMan.GetComponent<YautjaGearContainerComponent>(bracer.Value);
                Assert.That(gearContainer.Container, Is.Not.Null,
                    "adding insulation must not disturb the bracer's stored gear container");

                Assert.That(TryShock(electrocution, hunter), Is.False,
                    "a bracer in the gloves slot must fully insulate its wearer");

                Assert.That(inventory.TryUnequip(hunter, "gloves", out var removed, force: true), Is.True);
                Assert.That(removed, Is.EqualTo(bracer));
                Assert.That(TryShock(electrocution, hunter), Is.True,
                    "removing the bracer must restore ordinary electrocution");

                Assert.That(hands.TryPickupAnyHand(hunter, bracer.Value, checkActionBlocker: false, animate: false),
                    Is.True);
                Assert.That(TryShock(electrocution, hunter), Is.True,
                    "holding the bracer must not insulate the holder");
            }
            finally
            {
                entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static bool TryShock(
        ElectrocutionSystem electrocution,
        EntityUid target,
        bool ignoreInsulation = false)
    {
        return electrocution.TryDoElectrocution(
            target,
            null,
            10,
            TimeSpan.FromSeconds(1),
            refresh: true,
            ignoreInsulation: ignoreInsulation);
    }

    private static string? PrototypeId(IEntityManager entMan, EntityUid uid)
    {
        return entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
    }
}
