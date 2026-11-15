using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUVehicleBlastPenetrationTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [Test]
    public async Task GrenadesPenetrateSoftVansWhileArmoredCabinsOnlyBreachToHeavyOrdnance()
    {
        var map = await Pair.CreateTestMap();
        EntityUid van = default;
        EntityUid humvee = default;
        EntityUid vanPassenger = default;
        EntityUid humveePassenger = default;
        await Server.WaitAssertion(() =>
        {
            van = SEntMan.SpawnEntity("VehicleVan", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            humvee = SEntMan.SpawnEntity("VehicleHumvee", map.GridCoords.Offset(new Vector2(60, 0.5f)));
            vanPassenger = SpawnPassenger(van);
            humveePassenger = SpawnPassenger(humvee);
        });
        await RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            QueueBlast(van, "RMC");
            QueueBlast(humvee, "RMC");
        });
        await RunTicksSync(60);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage(vanPassenger), Is.GreaterThan(0), "a grenade blast must penetrate a soft van's cabin");
            Assert.That(Damage(humveePassenger), Is.Zero, "a grenade blast must not breach an armored humvee's cabin");
            QueueBlast(humvee, "CMUIED");
        });
        await RunTicksSync(60);
        await Server.WaitAssertion(() =>
            Assert.That(Damage(humveePassenger), Is.GreaterThan(0), "an IED blast must breach an armored humvee's cabin"));

        EntityUid SpawnPassenger(EntityUid vehicle)
        {
            // Loads the interior, then puts the passenger where the replicated blast will be centred.
            Assert.That(Server.System<VehicleSystem>().TryGetInteriorEntryCoordinates(vehicle, 0, out _), Is.True);
            var interiorGrid = SEntMan.GetComponent<VehicleInteriorComponent>(vehicle).Grid;
            var center = SEntMan.GetComponent<MapGridComponent>(interiorGrid).LocalAABB.Center;
            var passenger = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(interiorGrid, center));
            // Keep ambient medical simulation from masquerading as penetration damage.
            SEntMan.EnsureComponent<CMInStasisComponent>(passenger);
            return passenger;
        }

        void QueueBlast(EntityUid vehicle, string type)
        {
            var epicenter = Server.System<SharedTransformSystem>().GetMapCoordinates(vehicle);
            // Small enough to leave the hull standing, big enough to register a hit on it.
            Server.System<ExplosionSystem>().QueueExplosion(epicenter, type, 15f, 5f, 4f, null);
        }

        float Damage(EntityUid uid)
            => Server.System<DamageableSystem>().GetPositiveDamage(
                (uid, SEntMan.GetComponent<DamageableComponent>(uid))).GetTotal().Float();
    }
}
