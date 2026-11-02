#pragma warning disable RA0002 // Exercise real firing, physics, roofing and reserve state with compressed flight timing.

using System.Numerics;
using Content.Server.CMU14.Fob;
using Content.Shared.CMU14.Fob;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobDefenseTest
{
    [TestCase("range")]
    [TestCase("roof")]
    [TestCase("sensor")]
    [TestCase("hit")]
    public async Task SensorRocketKeepsOriginalTargetThenCommitsToLastPosition(string loss)
    {
        await SetupGuns();
        EntityUid launcher = default;
        Entity<CMUFobMissileComponent> missile = default;
        var sensor = await DeployEquipment("CMUFobSensor", new Vector2(-14, 1));
        Robust.Shared.Map.NetCoordinates covered = default;
        await Server.WaitPost(() => covered = SEntMan.GetNetCoordinates(_gunCoords.Offset(new Vector2(-17, 6))));
        await SetTile(Plating, covered);
        await Server.WaitAssertion(() =>
        {
            Transform.SetCoordinates(SPlayer, _gunCoords.Offset(new Vector2(-1, 0)));
            Assert.That(Defense.TryRemoveModule((_gun, Gun), SPlayer), Is.True);
            var loader = SEntMan.SpawnAtPosition("RMCMechPowerLoader", _gunCoords.Offset(new Vector2(-1, 0)));
            launcher = SEntMan.SpawnAtPosition("CMUFobRocketModule", _gunCoords.Offset(new Vector2(-1, 0)));
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(loader, launcher, checkActionBlocker: false), Is.True);
            Assert.That(Defense.TryInstallModule((launcher, SEntMan.GetComponent<CMUFobSupportGunComponent>(launcher)), _mount, SPlayer, loader), Is.True);
            Transform.SetCoordinates(_victim, _gunCoords.Offset(new Vector2(-17, 0)));
            Transform.SetCoordinates(SPlayer, _gunCoords.Offset(new Vector2(0, -4)));
            SEntMan.DeleteEntity(loader);
            Assert.That(Reserve.TryActivate(_network), Is.True);
        });
        await RunSeconds(3);
        var launched = false;
        for (var i = 0; i < 70 && !launched; i++)
        {
            await RunSeconds(0.1f);
            await Server.WaitPost(() =>
            {
                var query = SEntMan.EntityQueryEnumerator<CMUFobMissileComponent>();
                launched = query.MoveNext(out var uid, out var comp);
                if (launched) missile = (uid, comp);
            });
        }
        await Server.WaitAssertion(() =>
        {
            var rocket = SEntMan.GetComponent<CMUFobSupportGunComponent>(launcher);
            Assert.That(launched, Is.True,
                $"Distant sensor launch: state={_network.Comp.State}, powered={Equipment.IsPowered(sensor)}, sees={Equipment.CanSense(_network, _victim)}, target={rocket.Target}, ammo={Guns.GetAmmoCount(launcher)}, ready={rocket.ReadyAt}, next={SEntMan.GetComponent<Content.Shared.Weapons.Ranged.Components.GunComponent>(launcher).NextFire}, now={SGameTiming.CurTime}.");
            var comp = missile.Comp;
            Assert.That(comp.Target, Is.EqualTo(_victim));
            Assert.That(comp.Sensor, Is.EqualTo(sensor.Owner));
            comp.Speed = 2;
            SetAmmo(Magazine(launcher), 0);
            // A subsequent gun target selection must never retarget a missile already in flight.
            SEntMan.GetComponent<CMUFobSupportGunComponent>(launcher).Target = SPlayer;
            var coveredCoords = SEntMan.GetCoordinates(covered);
            var map = Server.System<SharedMapSystem>();
            Assert.That(map.TryFindGridAt(Transform.ToMapCoordinates(coveredCoords), out var ground, out var groundGrid), Is.True);
            coveredCoords = Transform.WithEntityId(coveredCoords, ground);
            // Keep the contact blast on ground instead of testing propagation from a one-tile island in space.
            for (var x = -2; x <= 2; x++)
                for (var y = -2; y <= 2; y++)
                    map.SetTile(ground, groundGrid, coveredCoords.Offset(new Vector2(x, y)), new Robust.Shared.Map.Tile(TileMan[Plating].TileId));
            Transform.SetCoordinates(_victim, coveredCoords);
            SEntMan.SpawnAtPosition("CMWallMetal", _gunCoords.Offset(new Vector2(-15, 3)));
        });
        await RunSeconds(0.3f);
        if (loss == "hit")
        {
            await RunSeconds(9);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Damage.GetTotalDamage(_victim).Float(), Is.GreaterThan(50), "A guided impact delivers the direct hit and its blast.");
                Assert.That(SEntMan.Deleted(missile), Is.True);
            });
            return;
        }
        var remembered = default(Robust.Shared.Map.MapCoordinates);
        await Server.WaitAssertion(() =>
        {
            Assert.That(missile.Comp.Target, Is.EqualTo(_victim));
            Assert.That(Equipment.CanSense(_network, _victim), Is.False, "The wall must block fresh acquisition in this scenario.");
            Assert.That(missile.Comp.TrackingLost, Is.False, "Losing line of sight alone must not break an airborne lock.");
            remembered = Transform.GetMapCoordinates(_victim);
            Assert.That(missile.Comp.LastPosition, Is.EqualTo(remembered));
            if (loss == "range")
                Transform.SetCoordinates(_victim, _gunCoords.Offset(new Vector2(-27, 3)));
            else if (loss == "sensor")
                Hurt(sensor, 200);
            else
            {
                var grid = SEntMan.GetComponent<TransformComponent>(_victim).GridUid!.Value;
                var roof = SEntMan.EnsureComponent<RoofComponent>(grid);
                var mapGrid = SEntMan.GetComponent<MapGridComponent>(grid);
                var tile = Server.System<SharedMapSystem>().GetTileRef(grid, mapGrid, SEntMan.GetComponent<TransformComponent>(_victim).Coordinates);
                Server.System<SharedRoofSystem>().SetRoof((grid, mapGrid, roof), tile.GridIndices, true);
            }
        });
        await RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(missile.Comp.TrackingLost, Is.True);
            Assert.That(missile.Comp.LastPosition, Is.EqualTo(remembered));
            Transform.SetCoordinates(_victim, _gunCoords.Offset(new Vector2(-17, -6)));
        });
        await RunSeconds(0.2f);
        await Server.WaitAssertion(() => Assert.That(missile.Comp.LastPosition, Is.EqualTo(remembered), "A lost lock never reacquires or follows hidden movement."));
        await RunSeconds(11);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(missile), Is.True, "The missile must detonate rather than orbit the last position indefinitely.");
            Assert.That(Damage.GetTotalDamage(_victim).Float(), Is.Zero, "The escaped target cannot receive a synthetic direct hit at its new position.");
        });
    }
}
