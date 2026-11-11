using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client.Weapons.Ranged.Systems;
using Content.IntegrationTests.Fixtures;
using Robust.Client.ComponentTrees;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
public sealed class CMUGunTargetSelectionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task NearbyTargetFallbackDoesNotSelectShooterAfterTargetDisappears()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        NetEntity shooterNet = default;
        NetEntity targetNet = default;
        await Server.WaitPost(() =>
        {
            var shooter = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            shooterNet = SEntMan.GetNetEntity(shooter);
            targetNet = SEntMan.GetNetEntity(target);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, shooter);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            Client.System<SpriteTreeSystem>().FrameUpdate(0);
            var coordinates = Client.System<SharedTransformSystem>().GetMapCoordinates(CEntMan.GetEntity(targetNet));
            Assert.That(SelectTarget(coordinates), Is.EqualTo(targetNet),
                "automatic selection still finds an actual target");
        });

        await Server.WaitPost(() => SEntMan.DeleteEntity(target));
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var shooter = CEntMan.GetEntity(shooterNet);
            var coordinates = Client.System<SharedTransformSystem>().GetMapCoordinates(shooter);
            var tree = Client.System<SpriteTreeSystem>();
            tree.FrameUpdate(0);
            Assert.That(tree.QueryAabb(coordinates.MapId,
                Box2.CenteredAround(coordinates.Position, Vector2.One)).Any(e => e.Uid == shooter), Is.True,
                "the shooter must actually be a candidate in the proximity query");
            Assert.That(SelectTarget(coordinates), Is.Null,
                "losing the intended target must not fall back to the shooter");
        });
    }

    private NetEntity? SelectTarget(MapCoordinates coordinates)
    {
        var method = typeof(GunSystem).GetMethod("GetBestTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var eye = Client.ResolveDependency<IEyeManager>().CurrentEye;
        return (NetEntity?) method.Invoke(Client.System<GunSystem>(), [eye, coordinates]);
    }
}
