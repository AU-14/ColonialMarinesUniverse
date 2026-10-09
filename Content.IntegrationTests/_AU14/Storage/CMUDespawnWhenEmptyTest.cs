using Content.Server.CMU14.Storage;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Storage;

[TestFixture]
public sealed class CMUDespawnWhenEmptyTest
{
    private static readonly EntProtoId WeaponKit = "AU14KitWeaponM20A";
    private static readonly EntProtoId FastKit = "CMUTestFastDespawnWeaponKit";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: AU14KitWeaponM20A
  id: CMUTestFastDespawnWeaponKit
  components:
  - type: CMUDespawnWhenEmpty
    delay: 2

- type: entity
  id: CMUTestKitHolder
  components:
  - type: ContainerContainer
";

    [Test]
    public async Task WeaponKitsDespawnAfterOneMinute()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototype = server.ProtoMan.Index(WeaponKit);
            Assert.That(prototype.TryGetComponent<CMUDespawnWhenEmptyComponent>(out var despawn, server.EntMan.ComponentFactory), Is.True);
            Assert.That(despawn!.Delay, Is.EqualTo(TimeSpan.FromMinutes(1)));
        });
    }

    [Test]
    public async Task OnlyEmptyKitsOnTheGroundDespawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid full = default, emptied = default, held = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var containers = server.System<SharedContainerSystem>();

            full = entities.SpawnEntity(FastKit, testMap.GridCoords);
            emptied = entities.SpawnEntity(FastKit, testMap.GridCoords);
            held = entities.SpawnEntity(FastKit, testMap.GridCoords);

            Assert.That(entities.GetComponent<CMUDespawnWhenEmptyComponent>(full).DespawnAt, Is.Null);

            var holder = entities.SpawnEntity("CMUTestKitHolder", testMap.GridCoords);
            var slot = containers.EnsureContainer<Container>(holder, "kit");
            Assert.That(containers.Insert(held, slot), Is.True);

            containers.EmptyContainer(containers.GetContainer(emptied, StorageComponent.ContainerId));
            containers.EmptyContainer(containers.GetContainer(held, StorageComponent.ContainerId));

            Assert.That(entities.GetComponent<CMUDespawnWhenEmptyComponent>(emptied).DespawnAt, Is.Not.Null);
            Assert.That(entities.GetComponent<CMUDespawnWhenEmptyComponent>(held).DespawnAt, Is.Null);
        });

        await pair.RunSeconds(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(emptied), Is.True, "Empty kit despawned before its delay elapsed.");
        });

        await pair.RunSeconds(2);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entities.EntityExists(emptied), Is.False, "Empty kit on the ground did not despawn.");
                Assert.That(entities.EntityExists(full), Is.True, "A kit with contents despawned.");
                Assert.That(entities.EntityExists(held), Is.True, "An empty kit inside a container despawned.");
            });
        });
    }
}
