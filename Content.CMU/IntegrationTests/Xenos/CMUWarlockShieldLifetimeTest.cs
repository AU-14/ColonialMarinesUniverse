using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Threats.Mobs.Xeno.Caste.Warlock;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Xenonids;

[TestFixture]
public sealed class CMUWarlockShieldLifetimeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task ReflectedGrenadeStartsANewThrowAfterBeingFrozen()
    {
        var map = await Pair.CreateTestMap();
        EntityUid warlock = default;
        EntityUid grenade = default;
        NetEntity grenadeNet = default;
        await Server.WaitAssertion(() =>
        {
            var shield = RaiseShield(map.GridCoords, out warlock);
            grenade = SEntMan.SpawnEntity("CMGrenadeHighExplosive", SEntMan.GetComponent<TransformComponent>(shield).Coordinates);
            grenadeNet = SEntMan.GetNetEntity(grenade);
            Assert.That(Server.System<ThrowingSystem>().TryThrow(grenade, -Vector2.UnitX * 6, 20,
                animated: false, playSound: false, doSpin: false), Is.True);
            var thrown = SEntMan.GetComponent<ThrownItemComponent>(grenade);
            var hit = new ThrowHitByEvent(grenade, shield, thrown);
            SEntMan.EventBus.RaiseLocalEvent(shield, ref hit);
            Assert.That(SEntMan.HasComponent<CMUXenoFrozenProjectileComponent>(grenade), Is.True);
        });

        // The original throw expires while the shield holds the grenade.
        await Pair.RunSeconds(0.5f);
        await Client.WaitAssertion(() =>
        {
            var clientGrenade = CEntMan.GetEntity(grenadeNet);
            Assert.That(CEntMan.HasComponent<CMUXenoFrozenProjectileComponent>(clientGrenade), Is.True);
            Assert.That(CEntMan.GetComponent<PhysicsComponent>(clientGrenade).BodyType, Is.EqualTo(BodyType.Static));
        });
        Vector2 before = default;
        await Server.WaitAssertion(() =>
        {
            var detonate = new CMUXenoPsychicShieldDetonateActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(warlock, detonate);
            Assert.That(detonate.Handled, Is.True);
            Assert.That(SEntMan.HasComponent<CMUXenoFrozenProjectileComponent>(grenade), Is.False);
            Assert.That(SEntMan.TryGetComponent<ThrownItemComponent>(grenade, out var thrown), Is.True,
                "Reflection must start a new throw even after freezing has stopped the old one.");
            Assert.That(thrown!.Thrower, Is.EqualTo(warlock));
            Assert.That(thrown.LandTime, Is.GreaterThan(Server.ResolveDependency<IGameTiming>().CurTime));
            var physics = SEntMan.GetComponent<PhysicsComponent>(grenade);
            Assert.That(physics.BodyType, Is.EqualTo(BodyType.Dynamic));
            Assert.That(physics.CanCollide, Is.True);
            Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.InAir));
            Assert.That(physics.LinearVelocity.X,
                Is.EqualTo(SEntMan.GetComponent<CMUXenoWarlockComponent>(warlock).PsychicShieldReflectedThrowSpeed).Within(0.01f),
                "TryThrow's impulse must not be added on top of the original launch velocity.");
            before = Server.System<SharedTransformSystem>().GetWorldPosition(grenade);
        });
        await Pair.RunSeconds(0.1f);
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<SharedTransformSystem>().GetWorldPosition(grenade).X, Is.GreaterThan(before.X)));
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() =>
        {
            var clientGrenade = CEntMan.GetEntity(grenadeNet);
            Assert.That(CEntMan.HasComponent<CMUXenoFrozenProjectileComponent>(clientGrenade), Is.False);
            var physics = CEntMan.GetComponent<PhysicsComponent>(clientGrenade);
            Assert.That(physics.BodyType, Is.EqualTo(BodyType.Dynamic));
            Assert.That(physics.CanCollide, Is.True);
        });
    }

    [Test]
    public async Task DeletingShieldOwnerReleasesCaughtProjectile()
    {
        var map = await Pair.CreateTestMap();
        EntityUid shield = default;
        await Server.WaitAssertion(() =>
        {
            shield = RaiseShield(map.GridCoords, out var warlock);
            var bullet = SEntMan.SpawnEntity("CMBulletPistol9mm", map.GridCoords.Offset(Vector2.UnitX * 3));
            var physics = SEntMan.GetComponent<PhysicsComponent>(bullet);
            var originalBodyType = physics.BodyType;
            Server.System<SharedPhysicsSystem>().SetLinearVelocity(bullet, -Vector2.UnitX * 20, body: physics);
            Assert.That(Server.System<CMUXenoWarlockSystem>().TryFreezeShieldProjectile(
                (shield, SEntMan.GetComponent<CMUXenoPsychicShieldSegmentComponent>(shield)), bullet,
                SEntMan.GetComponent<ProjectileComponent>(bullet), physics), Is.True);

            SEntMan.DeleteEntity(warlock);

            Assert.That(SEntMan.HasComponent<CMUXenoFrozenProjectileComponent>(bullet), Is.False,
                "Deleting the owner must not strand a projectile as a static, non-colliding body.");
            Assert.That(physics.BodyType, Is.EqualTo(originalBodyType));
            Assert.That(physics.CanCollide, Is.True);
            Assert.That(physics.LinearVelocity.X, Is.LessThan(0));
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(shield), Is.True));
    }

    [Test]
    public async Task DisruptedShieldDropsCaughtGrenadeOntoTheGround()
    {
        var map = await Pair.CreateTestMap();
        EntityUid grenade = default;
        NetEntity grenadeNet = default;
        await Server.WaitAssertion(() =>
        {
            var shield = RaiseShield(map.GridCoords, out var warlock);
            grenade = SEntMan.SpawnEntity("CMGrenadeHighExplosive", SEntMan.GetComponent<TransformComponent>(shield).Coordinates);
            grenadeNet = SEntMan.GetNetEntity(grenade);
            Assert.That(Server.System<ThrowingSystem>().TryThrow(grenade, -Vector2.UnitX * 6, 20,
                animated: false, playSound: false, doSpin: false), Is.True);
            var hit = new ThrowHitByEvent(grenade, shield, SEntMan.GetComponent<ThrownItemComponent>(grenade));
            SEntMan.EventBus.RaiseLocalEvent(shield, ref hit);
            Assert.That(SEntMan.HasComponent<CMUXenoFrozenProjectileComponent>(grenade), Is.True);

            var stun = new StunnedEvent();
            SEntMan.EventBus.RaiseLocalEvent(warlock, ref stun);
            Assert.That(SEntMan.HasComponent<CMUXenoFrozenProjectileComponent>(grenade), Is.False);
            Assert.That(SEntMan.HasComponent<ThrownItemComponent>(grenade), Is.False);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var physics = SEntMan.GetComponent<PhysicsComponent>(grenade);
            Assert.That(physics.BodyType, Is.EqualTo(BodyType.Dynamic));
            Assert.That(physics.CanCollide, Is.True);
            Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.OnGround),
                "A dropped capture has no remaining throw timer to land an airborne body.");
        });
        await Client.WaitAssertion(() =>
            Assert.That(CEntMan.GetComponent<PhysicsComponent>(CEntMan.GetEntity(grenadeNet)).BodyStatus,
                Is.EqualTo(BodyStatus.OnGround)));
    }

    private EntityUid RaiseShield(EntityCoordinates coordinates, out EntityUid warlock)
    {
        warlock = SEntMan.SpawnEntity("CMXenoWarlock", coordinates);
        var raise = new CMUXenoPsychicShieldActionEvent { Target = coordinates.Offset(Vector2.UnitX * 3) };
        SEntMan.EventBus.RaiseLocalEvent(warlock, raise);
        Assert.That(raise.Handled, Is.True, "The test must raise the real psychic shield.");
        return SEntMan.GetComponent<CMUXenoWarlockComponent>(warlock).PsychicShieldSegments.Single();
    }
}
