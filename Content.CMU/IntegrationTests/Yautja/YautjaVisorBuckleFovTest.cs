using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaVisorBuckleFovTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    // buckling reparents you to the seat, the client reapplied night vision on that and turned FOV back on
    // over the visor until the mask got cycled
    [Test]
    public async Task BucklingKeepsTheVisorFovOff()
    {
        var map = await Pair.CreateTestMap();
        NetEntity hunterNet = default;
        EntityUid hunter = default, chair = default;

        await Server.WaitAssertion(() =>
        {
            hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, hunter);
            var glasses = SEntMan.SpawnEntity("CMUYautjaNightVisionGlasses", map.GridCoords);
            Assert.That(Server.System<InventorySystem>().TryEquip(hunter, glasses, "eyes", silent: true, force: true), Is.True);
            // what the mask does when the visor comes on
            Server.System<SharedEyeSystem>().SetDrawFov(hunter, false);
            chair = SEntMan.SpawnEntity("Chair", map.GridCoords);
            hunterNet = SEntMan.GetNetEntity(hunter);
        });
        await Pair.RunUntilSynced();

        await Server.WaitAssertion(() =>
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(hunter, hunter, chair, popup: false), Is.True));
        await Pair.RunUntilSynced();
        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            var eye = CEntMan.GetComponent<EyeComponent>(CEntMan.GetEntity(hunterNet));
            Assert.That(eye.DrawFov, Is.False);
        });
    }
}
