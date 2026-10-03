using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.Weapon;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUFlareCameraExpiryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task ExpiredCameraFlareClearsViewWithoutClearingAnotherWeaponTarget()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var console = SEntMan.SpawnEntity(null, map.GridCoords);
            var terminal = SEntMan.AddComponent<DropshipTerminalWeaponsComponent>(console);
            var aim = SEntMan.SpawnEntity(null, map.GridCoords);
            var flare = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<DropshipTargetComponent>(flare);
            var weapons = Server.System<SharedDropshipWeaponSystem>();
#pragma warning disable RA0002 // Arrange a separate weapon selection before exercising camera expiry.
            terminal.Target = aim;
#pragma warning restore RA0002
            Assert.That(weapons.TrySetCameraTarget(console, flare), Is.True);

            SEntMan.RemoveComponent<DropshipTargetComponent>(flare);

            Assert.That(terminal.CameraTarget, Is.Null, "the console must release a flare when it burns out");
            Assert.That(terminal.Target, Is.EqualTo(aim), "a different weapon target must remain selected");
        });
    }
}
