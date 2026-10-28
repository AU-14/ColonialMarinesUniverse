using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.ViewVariables;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUPhysicsViewVariablesTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task CombinedBodyTypeEditPreservesLivePhysics()
    {
        var map = await Pair.CreateTestMap();
        EntityUid entity = default;
        await Server.WaitAssertion(() =>
        {
            entity = SEntMan.SpawnEntity("Jukebox", map.GridCoords);
            SEntMan.SpawnEntity("Jukebox", map.GridCoords);
            Server.System<SharedTransformSystem>().Unanchor(entity);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.GetComponent<PhysicsComponent>(entity);
            var physics = Server.System<SharedPhysicsSystem>();
            var contacts = body.ContactCount;
            Assert.That(contacts, Is.GreaterThan(0));
            physics.SetLinearVelocity(entity, Vector2.UnitX);
            try
            {
                Assert.Throws<ArgumentException>(() => Server.ResolveDependency<IViewVariablesManager>()
                    .WritePath($"/entity/{entity}/Physics/BodyType", "Static, Dynamic"));
                Assert.That(body.BodyType, Is.EqualTo(BodyType.Dynamic));
                Assert.That(body.ContactCount, Is.EqualTo(contacts));
                Assert.That(body.LinearVelocity, Is.EqualTo(Vector2.UnitX));
            }
            finally
            {
                // A failing reproduction must not let an invalid enum reach the next solver tick.
                physics.SetBodyType(entity, BodyType.Dynamic);
            }
        });
        await Pair.RunTicksSync(3);
    }

    [Test]
    public async Task BodyTypeEditsUpdateSpatialTreesBeforeContainerTransfers()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entity = SEntMan.SpawnEntity("Jukebox", map.GridCoords);
            Server.System<SharedTransformSystem>().Unanchor(entity);
            var body = SEntMan.GetComponent<PhysicsComponent>(entity);
            var physics = Server.System<SharedPhysicsSystem>();
            var lookup = Server.System<EntityLookupSystem>();
            var vv = Server.ResolveDependency<IViewVariablesManager>();
            var path = $"/entity/{entity}/Physics/BodyType";
            try
            {
                physics.SetLinearVelocity(entity, Vector2.UnitX);
                vv.WritePath(path, "Static");
                Assert.That(body.LinearVelocity, Is.EqualTo(Vector2.Zero));
                Assert.That(body.Awake, Is.False);
                Assert.That(lookup.GetEntitiesInRange(map.GridCoords, 2, LookupFlags.Static), Does.Contain(entity));
                Assert.That(lookup.GetEntitiesInRange(map.GridCoords, 2, LookupFlags.Dynamic), Does.Not.Contain(entity));

                vv.WritePath(path, "Dynamic");
                Assert.That(lookup.GetEntitiesInRange(map.GridCoords, 2, LookupFlags.Dynamic), Does.Contain(entity));
                Assert.That(lookup.GetEntitiesInRange(map.GridCoords, 2, LookupFlags.Static), Does.Not.Contain(entity));
                var holder = SEntMan.SpawnEntity(null, map.GridCoords);
                var containers = Server.System<SharedContainerSystem>();
                var slot = containers.EnsureContainer<ContainerSlot>(holder, "cmu-body-type-test");
                Assert.That(containers.Insert(entity, slot), Is.True);
                Assert.That(containers.Remove(entity, slot), Is.True);
                Assert.That(lookup.GetEntitiesInRange(map.GridCoords, 2, LookupFlags.Dynamic), Does.Contain(entity));
            }
            finally
            {
                physics.SetBodyType(entity, BodyType.Dynamic);
            }
        });
        await Pair.RunTicksSync(3);
    }
}
