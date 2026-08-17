using System.Collections.Generic;
using Content.Server.Station.Systems;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Preferences;

[TestFixture]
public sealed class LoadoutTests
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: playTimeTracker
  id: PlayTimeLoadoutTester

- type: loadout
  id: TestJumpsuit
  equipment:
    jumpsuit: ClothingUniformJumpsuitColorGrey

- type: loadout
  id: TestPointLoadoutThree
  cost: 3

- type: loadout
  id: TestPointLoadoutFour
  cost: 4

- type: loadoutGroup
  id: LoadoutTesterJumpsuit
  name: generic-unknown
  loadouts:
  - TestJumpsuit

- type: loadoutGroup
  id: LoadoutTesterPoints
  name: generic-unknown
  minLimit: 0
  maxLimit: 1
  loadouts:
  - TestPointLoadoutThree
  - TestPointLoadoutFour

- type: roleLoadout
  id: JobLoadoutTester
  groups:
  - LoadoutTesterJumpsuit

- type: roleLoadout
  id: JobLoadoutPointTester
  points: 5
  groups:
  - LoadoutTesterPoints

- type: job
  id: LoadoutTester
  playTimeTracker: PlayTimeLoadoutTester
";

    private readonly Dictionary<string, EntProtoId> _expectedEquipment = new()
    {
        ["jumpsuit"] = "ClothingUniformJumpsuitColorGrey"
    };

    /// <summary>
    /// Checks that an empty loadout still spawns with default gear and not naked.
    /// </summary>
    [Test]
    public async Task TestEmptyLoadout()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings()
        {
            Dirty = true,
        });
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();

        // Check that an empty role loadout spawns gear
        var stationSystem = entManager.System<StationSpawningSystem>();
        var inventorySystem = entManager.System<InventorySystem>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var profile = new HumanoidCharacterProfile();

            profile.SetLoadout(new RoleLoadout("LoadoutTester"));

            var tester = stationSystem.SpawnPlayerMob(testMap.GridCoords, job: "LoadoutTester", profile, station: null);

            var slotQuery = inventorySystem.GetSlotEnumerator(tester);
            var checkedCount = 0;
            while (slotQuery.NextItem(out var item, out var slot))
            {
                // Make sure the slot is valid
                Assert.That(_expectedEquipment.TryGetValue(slot.Name, out var expectedItem), $"Spawned item in unexpected slot: {slot.Name}");

                // Make sure that the item is the right one
                var meta = entManager.GetComponent<MetaDataComponent>(item);
                Assert.That(meta.EntityPrototype.ID, Is.EqualTo(expectedItem.Id), $"Spawned wrong item in slot {slot.Name}!");

                checkedCount++;
            }
            // Make sure the number of items is the same
            Assert.That(checkedCount, Is.EqualTo(_expectedEquipment.Count), "Number of items does not match expected!");

            entManager.DeleteEntity(tester);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Checks that changing a selection immediately updates its remaining points.
    /// </summary>
    [Test]
    public async Task TestSelectionUpdatesPoints()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var loadout = new RoleLoadout("JobLoadoutPointTester")
            {
                Points = 5,
                SelectedLoadouts =
                {
                    ["LoadoutTesterPoints"] = new List<Loadout>(),
                },
            };

            Assert.That(loadout.AddLoadout("LoadoutTesterPoints", "TestPointLoadoutThree", protoManager), Is.True);
            Assert.That(loadout.Points, Is.EqualTo(2));

            // Replacing an item at the group limit must refund the old item as well.
            Assert.That(loadout.AddLoadout("LoadoutTesterPoints", "TestPointLoadoutFour", protoManager), Is.True);
            Assert.That(loadout.Points, Is.EqualTo(1));

            Assert.That(loadout.RemoveLoadout("LoadoutTesterPoints", "TestPointLoadoutFour", protoManager), Is.True);
            Assert.That(loadout.Points, Is.EqualTo(5));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Checks that every loadout referenced by a group exists.
    /// </summary>
    [Test]
    public async Task TestLoadoutGroupReferences()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var missing = new List<string>();

            foreach (var group in protoManager.EnumeratePrototypes<LoadoutGroupPrototype>())
            {
                foreach (var loadout in group.Loadouts)
                {
                    if (!protoManager.HasIndex<LoadoutPrototype>(loadout))
                        missing.Add($"{group.ID}: {loadout}");
                }
            }

            Assert.That(missing, Is.Empty,
                $"Loadout groups reference unknown loadouts:\n{string.Join('\n', missing)}");
        });

        await pair.CleanReturnAsync();
    }
}
