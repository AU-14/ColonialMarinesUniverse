using System.Numerics;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUDroneControlSafeguardsTest
{
    [TestCase("CMUCombatDrone")]
    [TestCase("CMUFlamerDrone")]
    [TestCase("CMUDroneAndroid")]
    public async Task BodyDamageMovementAndStunReturnControl(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid user = default, drone = default, tablet = default, mind = default;

        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.AddComponent<CMUDroneOperatorComponent>(user);
            drone = entities.SpawnEntity(prototype, map.GridCoords.Offset(new Vector2(2, 0)));
            tablet = entities.SpawnEntity("CMUDroneControlTablet", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, tablet, checkActionBlocker: false), Is.True);
            var link = new InteractUsingEvent(user, tablet, drone, entities.GetComponent<TransformComponent>(drone).Coordinates);
            entities.EventBus.RaiseLocalEvent(drone, link);
            Assert.That(link.Handled, Is.True);
            var minds = entities.System<SharedMindSystem>();
            mind = minds.CreateMind(null).Owner;
            minds.TransferTo(mind, user);
            // Start with existing damage to verify healing does not break control.
            ChangeDamage(user, 10);
            StartControl();
            ChangeDamage(user, -5);
            // Damage to the drone must not be mistaken for damage to its operator.
            ChangeDamage(drone, 5);
        });
        await server.WaitRunTicks(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<MindComponent>(mind).VisitingEntity, Is.EqualTo(drone));
            ChangeDamage(user, 1);
        });
        await server.WaitRunTicks(3);
        await server.WaitAssertion(() =>
        {
            AssertDisconnected();
            StartControl();
            // Several sub-threshold pushes during the grace period still add up to movement.
            var transform = entities.System<SharedTransformSystem>();
            var start = entities.GetComponent<TransformComponent>(user).Coordinates;
            for (var i = 1; i <= 4; i++)
                transform.SetCoordinates(user, start.Offset(new Vector2(0, 0.1f * i)));
        });
        await server.WaitRunTicks(3);
        await server.WaitAssertion(() =>
        {
            AssertDisconnected();
            StartControl();
            // Once settled, even a small forced move must disconnect.
            entities.GetComponent<CMURemotePilotingComponent>(user).BodyMoveGraceUntil = TimeSpan.Zero;
            var transform = entities.System<SharedTransformSystem>();
            transform.SetCoordinates(user, entities.GetComponent<TransformComponent>(user).Coordinates.Offset(new Vector2(0, 0.05f)));
        });
        await server.WaitRunTicks(3);
        await server.WaitAssertion(() =>
        {
            AssertDisconnected();
            StartControl();
            Assert.That(entities.System<SharedStunSystem>().TryAddStunDuration(user, TimeSpan.FromSeconds(1), force: true), Is.True);
        });
        await server.WaitRunTicks(3);
        await server.WaitAssertion(AssertDisconnected);
        await pair.CleanReturnAsync();

        void StartControl()
        {
            entities.EventBus.RaiseLocalEvent(tablet, new UseInHandEvent(user));
            Assert.That(entities.GetComponent<MindComponent>(mind).VisitingEntity, Is.EqualTo(drone));
            Assert.That(entities.HasComponent<CMURemotePilotingComponent>(user), Is.True);
        }

        void AssertDisconnected()
        {
            var component = entities.GetComponent<MindComponent>(mind);
            Assert.That(component.VisitingEntity, Is.Null);
            Assert.That(component.OwnedEntity, Is.EqualTo(user));
            Assert.That(entities.HasComponent<CMUDroneControlSessionComponent>(drone), Is.False);
            Assert.That(entities.HasComponent<CMURemotePilotingComponent>(user), Is.False);
            Assert.That(entities.GetComponent<CMUDroneOperatorComponent>(user).ControlledDrone, Is.Null);
            if (entities.TryGetComponent<InputMoverComponent>(drone, out var mover))
                Assert.That(mover.HeldMoveButtons, Is.EqualTo(MoveButtons.None));
            Assert.That(entities.GetComponent<PhysicsComponent>(drone).LinearVelocity, Is.EqualTo(Vector2.Zero));
            Assert.That(entities.GetComponent<CMUDroneControlTabletComponent>(tablet).LinkedDrone, Is.EqualTo(drone));
        }

        void ChangeDamage(EntityUid target, int amount)
        {
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", amount);
            entities.System<DamageableSystem>().TryChangeDamage(target, damage, ignoreResistances: true, ignoreGlobalModifiers: true);
        }
    }
}
