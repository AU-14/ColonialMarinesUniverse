using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Ghost;

/// <summary>
///     The Colonial Guard squad leaders wear their tactical wrap rather than leaving it at their feet.
/// </summary>
[TestFixture]
public sealed class CMUUACGSquadLeaderGearTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("CMUJobUACGPatrolSquadLeader")]
    [TestCase("CMUJobUACGPlatoonSquadLeader")]
    [TestCase("CMUJobUACGPatrolRifleman")]
    public async Task WrapIsWorn(string role)
    {
        EntityUid mob = default;
        MapCoordinates at = default;
        EntityUid map = default;
        await Server.WaitAssertion(() =>
        {
            map = SEntMan.System<SharedMapSystem>().CreateMap(out var mapId, runMapInit: true);
            at = new MapCoordinates(Vector2.Zero, mapId);
            mob = SEntMan.SpawnEntity(role, at);
        });

        await Server.WaitRunTicks(10);

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            inventory.TryGetSlotEntity(mob, "mask", out var mask);
            var maskId = mask is { } m ? SEntMan.GetComponent<MetaDataComponent>(m).EntityPrototype?.ID : null;

            var loose = SEntMan.System<EntityLookupSystem>().GetEntitiesInRange(at, 3f)
                .Where(e => e != mob && SEntMan.HasComponent<ItemComponent>(e) && !containers.IsEntityInContainer(e))
                .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID ?? "?")
                .ToList();

            TestContext.Out.WriteLine($"{role}: mask={maskId ?? "<empty>"}, on the floor=[{string.Join(", ", loose)}]");
            Assert.That(maskId, Is.EqualTo("RMCMaskScarfTanDown"), $"{role} isn't wearing the wrap");
            Assert.That(loose, Is.Empty, $"{role} has gear on the floor");
            SEntMan.DeleteEntity(map);
        });
    }
}
