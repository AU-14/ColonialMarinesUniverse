#pragma warning disable RA0002 // Exercise firing requests and inspect authoritative burst state.
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Weapons.Ranged;

[TestFixture]
public sealed class BurstFireRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: BurstRegressionGun
          components:
          - type: Gun
            selectedMode: Burst
            availableModes: [SemiAuto, Burst, FullAuto]
            burstFireRate: 8
            shotsPerBurst: 3
          - type: BasicEntityAmmoProvider
            proto: CartridgeRiflePractice
            capacity: 100
        """;

    [Test]
    public async Task HeldTriggerCannotRestartAfterServerFinishesBurst()
    {
        var map = await Pair.CreateTestMap();
        Entity<GunComponent> gun = default;
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity("BurstRegressionGun", map.GridCoords);
            gun = (uid, SEntMan.GetComponent<GunComponent>(uid));
            gun.Comp.ShootCoordinates = map.GridCoords.Offset(new System.Numerics.Vector2(10, 0));
        });
        // Client held-fire requests interleave with the server's burst continuation.
        for (var i = 0; i < 90; i++)
        {
            await Server.WaitPost(() => Server.System<SharedGunSystem>().AttemptShoot(gun.Owner, gun));
            await Pair.RunTicksSync(1);
        }
        await Server.WaitAssertion(() =>
        {
            var ammo = SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun);
            Assert.That(ammo.Count, Is.EqualTo(97), "one press must fire exactly one burst, including server continuation");
            Assert.That(gun.Comp.BurstActivated, Is.False);
            Server.System<SharedGunSystem>().ResetShotCounter(gun, gun.Comp);
        });
        await Pair.RunTicksSync(20);
        await Server.WaitPost(() => Server.System<SharedGunSystem>().AttemptShoot(gun.Owner, gun));
        await Pair.RunTicksSync(25);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun).Count, Is.EqualTo(94),
            "releasing and pressing again permits the next burst"));
    }

    [Test]
    public async Task CatchUpCannotExceedTheRemainingBurstOrSkipRecovery()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity("BurstRegressionGun", map.GridCoords);
            var gun = SEntMan.GetComponent<GunComponent>(uid);
            gun.ShootCoordinates = map.GridCoords.Offset(new System.Numerics.Vector2(10, 0));
            gun.BurstActivated = true;
            gun.BurstShotsCount = 2;
            gun.ShotCounter = 1; // Trigger was released and pressed during the same burst.
            gun.NextFire = SGameTiming.CurTime - TimeSpan.FromSeconds(0.125);
            Server.System<SharedGunSystem>().AttemptShoot(uid, (uid, gun));
            Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(uid).Count, Is.EqualTo(99));
            Assert.That(gun.BurstActivated, Is.False);
            Assert.That((gun.NextFire - SGameTiming.CurTime).TotalSeconds, Is.GreaterThanOrEqualTo(gun.BurstCooldown));
            Server.System<SharedGunSystem>().ResetShotCounter(uid, gun);
            Server.System<SharedGunSystem>().AttemptShoot(uid, (uid, gun));
            Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(uid).Count, Is.EqualTo(99),
                "another trigger press must respect the burst recovery delay");
        });
    }

}
