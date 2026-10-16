using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUSwarmClientCollisionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestSwarmStaticContact
          components:
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              sensor:
                shape: !type:PhysShapeCircle
                  radius: 3
                hard: false
                mask: [MobLayer]
        - type: entity
          parent: CMUTestSwarmStaticContact
          id: CMUTestSwarmDynamicContact
          components:
          - type: Physics
            bodyType: Dynamic
        """;

    [Test]
    public async Task LocalPullRestoresWorldContactsAndReleaseRemovesThem()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var previous = session.AttachedEntity;
        EntityUid queen = default, lesser = default, world = default, dynamic = default;
        NetEntity netLesser = default, netWorld = default, netDynamic = default;
        await Server.WaitPost(() =>
        {
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            lesser = SEntMan.SpawnEntity("CMUSwarmLesser", map.GridCoords.Offset(new Vector2(0.5f, 0)));
            Server.System<SharedXenoHiveSystem>().SetHive(queen, hive);
            Server.System<SharedXenoHiveSystem>().SetHive(lesser, hive);
            world = SEntMan.SpawnEntity("CMUTestSwarmStaticContact", map.GridCoords);
            dynamic = SEntMan.SpawnEntity("CMUTestSwarmDynamicContact", map.GridCoords);
            netLesser = SEntMan.GetNetEntity(lesser);
            netWorld = SEntMan.GetNetEntity(world);
            netDynamic = SEntMan.GetNetEntity(dynamic);
            Server.PlayerMan.SetAttachedEntity(session, queen);
        });
        try
        {
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
                Assert.That(Server.System<SharedPhysicsSystem>().GetContactingEntities(lesser), Does.Contain(world),
                    "World contacts remain authoritative on the server."));
            await CheckClient(false);
            await Server.WaitAssertion(() =>
                Assert.That(Server.System<PullingSystem>().TryStartPull(queen, lesser), Is.True));
            await Pair.RunUntilSynced();
            await CheckClient(true);
            await Server.WaitAssertion(() =>
                Assert.That(Server.System<PullingSystem>().TryStopPull(lesser,
                    SEntMan.GetComponent<PullableComponent>(lesser), queen), Is.True));
            await Pair.RunUntilSynced();
            await CheckClient(false);
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, previous));
            await Pair.RunUntilSynced();
        }

        async Task CheckClient(bool predicted)
        {
            await Client.WaitAssertion(() =>
            {
                var uid = CEntMan.GetEntity(netLesser);
                var body = CEntMan.GetComponent<PhysicsComponent>(uid);
                var physics = Client.System<SharedPhysicsSystem>();
                physics.SetAwake((uid, body), true);
                physics.Update(0);
                var contacts = physics.GetContactingEntities(uid);
                Assert.That(body.Predict, Is.EqualTo(predicted));
                Assert.That(contacts.Contains(CEntMan.GetEntity(netWorld)), Is.EqualTo(predicted));
                Assert.That(contacts, Does.Contain(CEntMan.GetEntity(netDynamic)),
                    "Dynamic collision contacts must remain available in both prediction states.");
            });
        }
    }
}
