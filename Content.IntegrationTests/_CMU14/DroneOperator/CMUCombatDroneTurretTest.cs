using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client.CMU14.DroneOperator;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUCombatDroneTurretTest
{
    [Test]
    public async Task CursorAimReplicatesWithoutFiringAndFlashTracksElevatedBarrel()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid drone = default;
        NetEntity netDrone = default;
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.AddComponent<CMUDroneOperatorComponent>(user);
            drone = entities.SpawnEntity("CMUCombatDrone", map.GridCoords.Offset(new Vector2(2, 0)));
            var tablet = entities.SpawnEntity("CMUDroneControlTablet", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, tablet, checkActionBlocker: false), Is.True);
            entities.EventBus.RaiseLocalEvent(drone, new InteractUsingEvent(user, tablet, drone, entities.GetComponent<TransformComponent>(drone).Coordinates));
            var minds = entities.System<SharedMindSystem>();
            var mind = minds.CreateMind(null).Owner;
            minds.TransferTo(mind, user);
            entities.EventBus.RaiseLocalEvent(tablet, new UseInHandEvent(user));
            Assert.That(entities.HasComponent<CMUDroneControlSessionComponent>(drone), Is.True);
            entities.System<SharedTransformSystem>().SetWorldRotation(drone, Direction.East.ToAngle());
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, drone);
            netDrone = entities.GetNetEntity(drone);
        });
        await pair.RunUntilSynced();
        foreach (var (requested, expected) in new[] { (135, 135), (225, 180), (90, 90) })
        {
            await pair.Client.WaitPost(() => pair.Client.EntMan.RaisePredictiveEvent(new CMUCombatDroneAimEvent(Angle.FromDegrees(requested))));
            await pair.RunTicksSync(5);
            await pair.Server.WaitAssertion(() =>
            {
                var turret = entities.GetComponent<CMUCombatDroneComponent>(drone).TurretVisual!.Value;
                var transform = entities.System<SharedTransformSystem>();
                Assert.That(Math.Abs(Angle.ShortestDistance(transform.GetWorldRotation(turret), Angle.FromDegrees(expected)).Degrees), Is.LessThan(0.01));
                Assert.That(transform.GetWorldRotation(drone).GetCardinalDir(), Is.EqualTo(Direction.East), "Cursor aiming must not turn the hull to bypass the firing arc.");
            });
        }
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var clientEntities = pair.Client.EntMan;
            var clientDrone = clientEntities.GetEntity(netDrone);
            var turret = clientEntities.GetComponent<CMUCombatDroneComponent>(clientDrone).TurretVisual!.Value;
            var transform = clientEntities.System<SharedTransformSystem>();
            var visuals = clientEntities.System<CMUCombatDroneTurretSystem>();
            visuals.FrameUpdate(0);
            Assert.That(clientEntities.GetComponent<SpriteComponent>(turret).Offset, Is.EqualTo(new Vector2(-11, -6) / 32));

            // Exercise the real client gun effect path, including its animation and light.
            var muzzle = typeof(SharedGunSystem).GetMethod("MuzzleFlash", BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, [typeof(EntityUid), typeof(AmmoComponent), typeof(Angle), typeof(EntityUid?)], modifiers: null);
            Assert.That(muzzle, Is.Not.Null);
            muzzle!.Invoke(clientEntities.System<Content.Client.Weapons.Ranged.Systems.GunSystem>(),
                [clientDrone, new AmmoComponent(), Angle.Zero, clientDrone]);
            var flashes = clientEntities.EntityQuery<CMUCombatDroneMuzzleFlashComponent>().ToList();
            Assert.That(flashes, Has.Count.EqualTo(1), "UGV shots must use the barrel-tracking flash path.");
            var flash = flashes[0].Owner;
            var relative = transform.GetWorldPosition(flash) - transform.GetWorldPosition(clientDrone);
            Assert.That(Vector2.Distance(relative, new Vector2(13, 10) / 32), Is.LessThan(0.001f), "The east flash must be raised to the gun barrel, not remain at hull height.");
            Assert.That(clientEntities.HasComponent<PointLightComponent>(flash), Is.True);

            transform.SetWorldRotation(turret, Direction.North.ToAngle());
            visuals.FrameUpdate(0);
            relative = transform.GetWorldPosition(flash) - transform.GetWorldPosition(clientDrone);
            Assert.That(Vector2.Distance(relative, new Vector2(-11, 12) / 32), Is.LessThan(0.001f), "An active flash must follow the turret independently of the hull.");
        });
        await pair.CleanReturnAsync();
    }
}
