using Content.Server.Station.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaOriginalSpawnLoadoutTest
{
    [Test]
    public async Task HunterPlayerSpawnStartsOnlyWithBracerAndCommunicator()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stationSpawning = entMan.System<StationSpawningSystem>();
            var inventory = entMan.System<InventorySystem>();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithYautjaProfile(YautjaCharacterProfile.Default
                    .WithArmor(YautjaGearMaterial.Bronze, 3)
                    .WithMask(YautjaGearMaterial.Bone, 12)
                    .WithGreaves(YautjaGearMaterial.Silver, 2)
                    .WithCapeStyle(YautjaCapeStyle.Damaged));

            var hunter = stationSpawning.SpawnPlayerMob(map.GridCoords, "CMUYautjaHunter", profile, station: null);

            try
            {
                AssertSlotPrototype(entMan, inventory, hunter, "ears", "CMUYautjaCommunicator");
                AssertSlotPrototype(entMan, inventory, hunter, "gloves", "CMUYautjaBracer");
                AssertEmptySlots(inventory, hunter,
                    "ears2", "mask", "outerClothing", "shoes", "back", "jumpsuit", "belt", "pocket1", "pocket2", "id");
            }
            finally
            {
                entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodPlayerSpawnStartsOnlyWithBadBloodBracerAndCommunicator()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stationSpawning = entMan.System<StationSpawningSystem>();
            var inventory = entMan.System<InventorySystem>();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithYautjaProfile(YautjaCharacterProfile.Default);

            var badBlood = stationSpawning.SpawnPlayerMob(map.GridCoords, "CMUYautjaBadBlood", profile, station: null);

            try
            {
                AssertSlotPrototype(entMan, inventory, badBlood, "ears", "CMUYautjaBadBloodCommunicator");
                AssertSlotPrototype(entMan, inventory, badBlood, "gloves", "CMUYautjaBadBloodBracer");
                AssertEmptySlots(inventory, badBlood,
                    "ears2", "mask", "outerClothing", "shoes", "back", "jumpsuit", "belt", "pocket1", "pocket2", "id");
            }
            finally
            {
                entMan.DeleteEntity(badBlood);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodStartingGearContainsOnlyBracerAndCommunicator()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var gear = prototypes.Index<StartingGearPrototype>("CMUYautjaYoungbloodGear");

            Assert.That(gear.Equipment.Keys, Is.EquivalentTo(new[] { "ears", "gloves" }));
            Assert.That(gear.Equipment["ears"], Is.EqualTo("CMUYautjaCommunicator"));
            Assert.That(gear.Equipment["gloves"], Is.EqualTo("CMUYautjaBracer"));
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertSlotPrototype(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        string expectedPrototype)
    {
        Assert.That(inventory.TryGetSlotEntity(wearer, slot, out var equipped), Is.True, slot);
        Assert.That(entMan.GetComponent<MetaDataComponent>(equipped.Value).EntityPrototype?.ID,
            Is.EqualTo(expectedPrototype), slot);
    }

    private static void AssertEmptySlots(InventorySystem inventory, EntityUid wearer, params string[] slots)
    {
        foreach (var slot in slots)
            Assert.That(inventory.TryGetSlotEntity(wearer, slot, out _), Is.False, slot);
    }
}
