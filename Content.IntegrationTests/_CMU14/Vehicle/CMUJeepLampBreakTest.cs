using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A crash breaks the lamps where it hits, more surely the faster, and leaves the far ones alone;
/// a slow bump breaks none. A shot landing on a lamp shatters it outright.
/// </summary>
[TestFixture]
public sealed class CMUJeepLampBreakTest : GameTest
{
    private async Task<(TestMapData Map, EntityUid Jeep)> SpawnJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);
        return (map, jeep);
    }

    private Vector2 LampOffset(EntityUid jeep, string id)
    {
        return SEntMan.GetComponent<CMUVehicleLampsComponent>(jeep).Lamps.First(l => l.Id == id).Offset;
    }

    [Test]
    public async Task CrashBreaksTheLampsWhereItHits()
    {
        var (_, jeep) = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            var lamps = SEntMan.GetComponent<CMUVehicleLampsComponent>(jeep);
            var corner = LampOffset(jeep, "headlight_left");

            var slow = new CMUVehicleCrashedEvent(30f, true, lamps.CrashMinSpeed - 0.05f, corner);
            SEntMan.EventBus.RaiseLocalEvent(jeep, ref slow);
            Assert.That(lamps.Broken, Is.Empty, "A slow bump breaks nothing.");

            var hard = new CMUVehicleCrashedEvent(30f, true, 1f, corner);
            SEntMan.EventBus.RaiseLocalEvent(jeep, ref hard);
            Assert.That(lamps.Broken, Does.Contain("headlight_left"), "A hard hit right on a lamp breaks it.");
            Assert.That(lamps.Broken, Does.Not.Contain("headlight_right"), "The far corner's lamp is out of reach.");
            Assert.That(lamps.Broken.Any(l => l.StartsWith("taillight")), Is.False, "The back end is untouched.");
        });
    }

    [Test]
    public async Task ShotOnALampShattersIt()
    {
        var (map, jeep) = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            var lamps = SEntMan.GetComponent<CMUVehicleLampsComponent>(jeep);
            var lamp = LampOffset(jeep, "headlight_right");

            // Facing south, the front is -y: a shooter ahead fires north into the right headlight. The
            // round is light, but where it lands is all that counts.
            var position = SEntMan.System<SharedTransformSystem>().GetWorldPosition(jeep);
            var shooter = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f + lamp.X, -4.5f));
            var bullet = SEntMan.SpawnEntity("CMBulletSentry10x50mm", new MapCoordinates(position + lamp, map.MapId));
            var piercing = Server.ResolveDependency<IPrototypeManager>().Index<DamageTypePrototype>("Piercing");
            SEntMan.System<DamageableSystem>().TryChangeDamage(jeep, new DamageSpecifier(piercing, 1),
                origin: shooter, tool: bullet, impact: DamageImpact.Projectile);

            Assert.That(lamps.Broken, Is.EquivalentTo(new[] { "headlight_right" }), "Only the lamp it lands on shatters.");
        });
    }
}
