using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Explosion;

[TestFixture, TestOf(typeof(ExplosionSystem))]
public sealed class RMCExplosionReceivedTest : GameTest
{
    public sealed class ExplosionReceivedListenerSystem : TestListenerSystem<ExplosionReceivedEvent>;
    public sealed class ExplosiveTriggeredListenerSystem : TestListenerSystem<CMExplosiveTriggeredEvent>;

    [Test]
    public async Task ExplosionDeletesMarkedSmoke()
    {
        var map = await Pair.CreateTestMap();
        EntityUid smoke = default;

        await Server.WaitAssertion(() =>
        {
            smoke = SEntMan.SpawnEntity("RMCSmoke", map.GridCoords);
            Assert.That(SEntMan.HasComponent<DeleteOnExplosionComponent>(smoke), Is.True);

            Server.System<ExplosionSystem>().QueueExplosion(
                map.MapCoords,
                "RMC",
                10,
                5,
                10,
                null,
                canCreateVacuum: false);
        });

        await RunTicksSync(10);

        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(smoke), Is.True));
    }

    [Test]
    public async Task TriggerExplosiveRaisesRMCTriggeredEvent()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var explosive = SEntMan.SpawnEntity("CMGrenadeHighExplosive", map.GridCoords);
            SEntMan.EnsureComponent<TestListenerComponent>(explosive);

            Server.System<ExplosionSystem>().TriggerExplosive(explosive, delete: false);

            var listener = Server.System<ExplosiveTriggeredListenerSystem>();
            Assert.That(listener.Count(explosive), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExplosionRaisesRMCReceivedEvent()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        EntityUid nonDamageableTarget = default;

        await Server.WaitAssertion(() =>
        {
            target = SEntMan.SpawnEntity("CMTestDummy", map.GridCoords);
            SEntMan.EnsureComponent<TestListenerComponent>(target);
            nonDamageableTarget = SEntMan.SpawnEntity("IntegrationTestMarker", map.GridCoords);
            SEntMan.EnsureComponent<TestListenerComponent>(nonDamageableTarget);

            Server.System<ExplosionSystem>().QueueExplosion(
                map.MapCoords,
                "RMC",
                10,
                5,
                10,
                null,
                canCreateVacuum: false);
        });

        await RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            var listener = Server.System<ExplosionReceivedListenerSystem>();
            var received = listener.GetEvents(target).ToArray();
            var nonDamageableReceived = listener.GetEvents(nonDamageableTarget).ToArray();
            var damageable = Server.System<DamageableSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(damageable.GetTotalDamage(target), Is.GreaterThan(FixedPoint2.Zero), "The generic explosion damage must reach the target.");
                Assert.That(received, Has.Length.EqualTo(1), "The same hit must emit one RMC explosion event.");
                Assert.That(nonDamageableReceived,
                    Has.Length.EqualTo(1),
                    "RMC explosion consumers such as xeno tunnels do not require Damageable.");
            });

            if (received.Length != 1)
                return;

            Assert.Multiple(() =>
            {
                Assert.That(received[0].Explosion.Id, Is.EqualTo("RMC"));
                Assert.That(received[0].Epicenter, Is.EqualTo(map.MapCoords));
                Assert.That(received[0].Damage.GetTotal(), Is.GreaterThan(FixedPoint2.Zero));
            });
        });
    }
}
