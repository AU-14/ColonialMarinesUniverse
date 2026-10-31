using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Crate;
using Content.Shared._RMC14.Projectiles;
using Content.Shared.Nutrition.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class HunterShipStorageFillTest : GameTest
{
    // the lockers were full of base SS14 junk
    [Test]
    public async Task HunterShipLockersSpawnEverythingTheyList()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var containers = Server.System<SharedContainerSystem>();
            var lockers = HunterShip(p => p.TryGetComponent<StorageFillComponent>(out _, factory)
                                          && p.TryGetComponent<EntityStorageComponent>(out _, factory));
            Assert.That(lockers, Is.Not.Empty);

            Assert.Multiple(() =>
            {
                foreach (var proto in lockers)
                {
                    proto.TryGetComponent<StorageFillComponent>(out var fill, factory);
                    var expected = CheckEntries(proto.ID, fill!.Contents);

                    var locker = SSpawnAtPosition(proto.ID, map.GridCoords);
                    var stored = containers.GetContainer(locker, "entity_storage").ContainedEntities.Count;
                    Assert.That(stored, Is.EqualTo(expected), $"{proto.ID} dropped part of its fill");
                    SEntMan.DeleteEntity(locker);
                }
            });
        });
    }

    // the spare gear crates at the back only ever gave wood planks when pried open
    [Test]
    public async Task HunterShipSpareGearCratesCarryCmss13HuntLoot()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var crates = HunterShip(p => p.ID.Contains("Densecrate") && p.TryGetComponent<CrateOpenableComponent>(out _, factory));
            Assert.That(crates, Has.Count.EqualTo(11));

            Assert.Multiple(() =>
            {
                foreach (var proto in crates)
                {
                    proto.TryGetComponent<CrateOpenableComponent>(out var openable, factory);
                    Assert.That(CheckEntries(proto.ID, openable!.Spawn), Is.GreaterThan(1), $"{proto.ID} only holds planks");

                    var crate = SSpawnAtPosition(proto.ID, map.GridCoords);
                    Assert.That(SEntMan.HasComponent<SpawnOnTerminateComponent>(crate), Is.False,
                        $"{proto.ID} still rolls random crate loot when it breaks");
                    SEntMan.DeleteEntity(crate);
                }
            });
        });
    }

    private List<EntityPrototype> HunterShip(Func<EntityPrototype, bool> filter)
    {
        return Server.ResolveDependency<IPrototypeManager>().EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && p.ID.StartsWith("CMUHunterShip") && filter(p))
            .ToList();
    }

    private int CheckEntries(string owner, List<EntitySpawnEntry> entries)
    {
        var protos = Server.ResolveDependency<IPrototypeManager>();
        var factory = SEntMan.ComponentFactory;
        var count = 0;
        foreach (var entry in entries)
        {
            count += entry.Amount;
            var item = protos.Index<EntityPrototype>(entry.PrototypeId!);
            var ours = item.ID.StartsWith("CM") || item.ID.StartsWith("RMC") || item.ID.StartsWith("AU14")
                       || item.ID.StartsWith("Weapon") || item.ID.StartsWith("Armor");
            Assert.That(ours || item.TryGetComponent<EdibleComponent>(out _, factory), Is.True,
                $"{owner} stocks base SS14 {item.ID}");
        }

        return count;
    }
}
