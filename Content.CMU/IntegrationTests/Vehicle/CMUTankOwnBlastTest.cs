using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using VehicleSystem = Content.Shared.Vehicle.Systems.VehicleSystem;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUTankOwnBlastTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [Test]
    public async Task QueuedShellRetainsFiringTankAfterDeletionWithoutExemptingEnemyBlasts()
    {
        var map = await Pair.CreateTestMap();
        EntityUid tank = default;
        EntityUid enemy = default;
        EntityUid driver = default;
        await Server.WaitAssertion(() =>
        {
            tank = SEntMan.SpawnEntity("VehicleTank", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            enemy = SEntMan.SpawnEntity("VehicleTank", map.GridCoords.Offset(new Vector2(40, 0)));
            driver = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(40, 2)));
            // Keep ambient medical simulation from masquerading as transferred blast damage.
            SEntMan.EnsureComponent<CMInStasisComponent>(driver);
            Assert.That(Server.System<VehicleSystem>().TrySetOperator(
                (tank, SEntMan.GetComponent<VehicleComponent>(tank)), driver), Is.True);
        });
        await RunTicksSync(2);
        await Server.WaitAssertion(() => QueueShell(tank));
        await RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage(tank), Is.GreaterThan(0), "own shells must still damage the hull");
            Assert.That(Damage(driver), Is.Zero, "own shells must not forward blast damage to the driver");
            // Same type, location and frame: provenance must prevent these blasts from merging.
            QueueShell(tank);
            QueueShell(enemy);
        });
        await RunTicksSync(30);
        await Server.WaitAssertion(() =>
            Assert.That(Damage(driver), Is.GreaterThan(0), "an enemy shell must still injure the driver"));

        void QueueShell(EntityUid shooter)
        {
            var coords = SEntMan.GetComponent<TransformComponent>(tank).Coordinates;
            var projectile = SEntMan.SpawnEntity("CMUProjectileLTBCannonHE", coords);
            SEntMan.GetComponent<ProjectileComponent>(projectile).Shooter = shooter;
            Server.System<ExplosionSystem>().TriggerExplosive(projectile);
            // Deletion also spawns the shell's separate HE impact explosion.
            SEntMan.DeleteEntity(projectile);
        }

        float Damage(EntityUid uid)
            => Server.System<DamageableSystem>().GetPositiveDamage(
                (uid, SEntMan.GetComponent<DamageableComponent>(uid))).GetTotal().Float();
    }
}
