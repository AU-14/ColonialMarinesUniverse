using System.Collections.Generic;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class PowerLoaderTeardownTest
{
    [Test]
    public async Task DeletingLoaderWithCargoDoesNotRecreateVirtualHands()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var loader = entMan.SpawnEntity("RMCMechPowerLoader", MapCoordinates.Nullspace);
            var cargo = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<ItemComponent>(cargo);
            var hands = entMan.System<SharedHandsSystem>();

            var spawnedVirtuals = new List<string>();
            void OnEntityInitialized(Entity<MetaDataComponent> entity)
            {
                if (entity.Comp.EntityPrototype?.ID is { } prototype &&
                    prototype.StartsWith("RMCVirtual", StringComparison.Ordinal))
                {
                    spawnedVirtuals.Add(prototype);
                }
            }

            entMan.EntityInitialized += OnEntityInitialized;
            try
            {
                Assert.That(hands.TryPickupAnyHand(loader, cargo, checkActionBlocker: false), Is.True);
                Assert.That(spawnedVirtuals, Is.Not.Empty, "A live loader must still synchronize its virtual hands.");
                spawnedVirtuals.Clear();

                // Removing held cargo during recursive deletion raises DidUnequipHandEvent.
                // That must not repopulate a loader whose teardown is already in progress.
                entMan.DeleteEntity(loader);
                Assert.That(spawnedVirtuals, Is.Empty);
                Assert.That(entMan.EntityExists(loader), Is.False);
                Assert.That(entMan.EntityExists(cargo), Is.False);
            }
            finally
            {
                entMan.EntityInitialized -= OnEntityInitialized;
            }
        });

        await pair.CleanReturnAsync();
    }
}
