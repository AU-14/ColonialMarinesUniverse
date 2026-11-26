using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Maturing;
using Content.Server._RMC14.Actions;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUQueenLayoutTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Destructive = true };

    [Test]
    public async Task MaturingWhileAttachedReloadsTheCorrectAccountLayout()
    {
        var map = await Pair.CreateTestMap();
        EntityUid queen = default;
        await Server.WaitAssertion(() =>
        {
            queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            var player = ServerSession!;
            var saved = Server.ResolveDependency<RMCActionsManager>();
            saved.SetOrder(player.UserId, "CMUQueenMatureOvipositorOrder", new RMCActionOrderData(["ActionXenoManageHive"], [], true));
            saved.SetOrder(player.UserId, "CMXenoQueen", new RMCActionOrderData(["ActionXenoRest"], [], true));
            Server.PlayerMan.SetAttachedEntity(player, queen);
            SEntMan.EnsureComponent<XenoAttachedOvipositorComponent>(queen);
            var attached = new XenoOvipositorChangedEvent(true, queen);
            SEntMan.EventBus.RaiseLocalEvent(queen, ref attached);
            var order = SEntMan.GetComponent<RMCActionOrderComponent>(queen);
            Assert.That(order.Id.Id, Is.EqualTo("CMUQueenImmatureOvipositorOrder"));
            Assert.That(order.Order, Is.Null);
            Server.System<XenoMaturingSystem>().Mature((queen, SEntMan.GetComponent<XenoMaturingComponent>(queen)));
        });

        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var order = SEntMan.GetComponent<RMCActionOrderComponent>(queen);
            Assert.That(order.Id.Id, Is.EqualTo("CMUQueenMatureOvipositorOrder"));
            Assert.That(order.Order!.Value[0].Id, Is.EqualTo("ActionXenoManageHive"));
            SEntMan.RemoveComponent<XenoAttachedOvipositorComponent>(queen);
            Assert.That(order.Id.Id, Is.EqualTo("CMXenoQueen"));
            Assert.That(order.Order!.Value[0].Id, Is.EqualTo("ActionXenoRest"));
        });
    }
}
