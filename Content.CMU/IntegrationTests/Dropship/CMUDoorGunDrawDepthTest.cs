using System.Numerics;
using Content.Client._RMC14.Sprite;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUDoorGunDrawDepthTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase("CMUM2CDoorGunDropship", Direction.South, true)]
    [TestCase("CMUM2CDoorGunDropship", Direction.East, true)]
    [TestCase("CMUM2CDoorGunDropship", Direction.North, true)]
    [TestCase("CMUM2CDoorGunDropship", Direction.West, true)]
    [TestCase("CMUUTESCDoorGunDropship", Direction.South, true)]
    [TestCase("RMCML66DNestDropship", Direction.South, true)]
    [TestCase("RMCML66DNestMetal", Direction.South, false)]
    public async Task DoorGunsDrawAboveOperator(string prototype, Direction facing, bool gunAboveOperator)
    {
        var map = await Pair.CreateTestMap();
        NetEntity gunNet = default, userNet = default;
        await Server.WaitAssertion(() =>
        {
            var coordinates = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            var gun = SEntMan.SpawnAttachedTo(prototype, coordinates);
            var user = SEntMan.SpawnAttachedTo("CMMobHuman", coordinates);
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(gun, facing.ToAngle());
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(user, user, gun), Is.True);
            gunNet = SEntMan.GetNetEntity(gun);
            userNet = SEntMan.GetNetEntity(user);
        });

        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var gun = CEntMan.GetEntity(gunNet);
            var user = CEntMan.GetEntity(userNet);
            CEntMan.System<RMCSpriteSystem>().UpdateDrawDepth(user);
            var gunDepth = CEntMan.GetComponent<SpriteComponent>(gun).DrawDepth;
            var userDepth = CEntMan.GetComponent<SpriteComponent>(user).DrawDepth;
            Assert.That(gunDepth > userDepth, Is.EqualTo(gunAboveOperator));
        });
    }
}
