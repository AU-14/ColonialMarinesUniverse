using System.Collections.Generic;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Light.Components;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Explosion.Components;
using Content.Shared.Light.Components;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture, TestOf(typeof(ProjectileGrenadeSystem))]
public sealed class RMCAirBurstGrenadeTest
{
    private static readonly EntProtoId<IFFFactionComponent> MarineFaction = "FactionMarine";

    [TestCase("RMCAirBurstProjectileFrag", "RMCShrapnelJagged", 16, 5, 40f, false)]
    [TestCase("RMCAirBurstProjectileIncendiary", "RMCShrapnelIncendiary", 5, 0, 40f, false)]
    [TestCase("RMCAirBurstProjectileHornet", "RMCHornetRound", 15, 0, 25f, false)]
    [TestCase("RMCAirBurstProjectileStarShell", "RMCStarShellBullet", 8, 0, 360f, true)]
    public async Task FixedDistanceStopUsesRmcAirburstPayload(
        string grenadePrototype,
        string payloadPrototype,
        int capacity,
        int hitLimit,
        float spreadAngle,
        bool expectPayloadTimer)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid grenade = default;
        EntityUid shooter = default;
        EntityUid weapon = default;

        await server.WaitAssertion(() =>
        {
            shooter = entMan.SpawnEntity(null, map.GridCoords);
            weapon = entMan.SpawnEntity(null, map.GridCoords);
            grenade = entMan.SpawnEntity(grenadePrototype, map.GridCoords);

            var projectile = entMan.GetComponent<ProjectileComponent>(grenade);
            projectile.Shooter = shooter;
            projectile.Weapon = weapon;

            if (expectPayloadTimer)
                entMan.System<GunIFFSystem>().GiveAmmoIFF(grenade, MarineFaction, true);

            Assert.That(entMan.System<TriggerSystem>().Trigger(grenade), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var payloads = FindEntities(payloadPrototype);
            Assert.Multiple(() =>
            {
                Assert.That(payloads, Has.Count.EqualTo(capacity));
                Assert.That(FindEntities("CMExplosionEffectGrenade"), Has.Count.EqualTo(1));
            });

            foreach (var payload in payloads)
            {
                var projectile = entMan.GetComponent<ProjectileComponent>(payload);
                var limit = entMan.GetComponent<ProjectileLimitHitsComponent>(payload);

                Assert.Multiple(() =>
                {
                    Assert.That(projectile.Shooter, Is.EqualTo(shooter));
                    Assert.That(projectile.Weapon, Is.EqualTo(weapon));
                    Assert.That(limit.OriginEntityId, Is.EqualTo(grenade.Id));
                    Assert.That(limit.Limit, Is.EqualTo(hitLimit));
                    Assert.That(
                        entMan.HasComponent<ActiveTimerTriggerComponent>(payload),
                        Is.EqualTo(expectPayloadTimer));
                });

                if (expectPayloadTimer)
                {
                    var iff = entMan.GetComponent<ProjectileIFFComponent>(payload);
#pragma warning disable RA0002 // This regression test intentionally verifies the copied projectile IFF state.
                    Assert.Multiple(() =>
                    {
                        Assert.That(iff.Enabled, Is.True);
                        Assert.That(iff.Factions, Does.Contain(MarineFaction));
                    });
#pragma warning restore RA0002
                }
            }

            if (spreadAngle < 360)
            {
                var expectedDirection = Angle.FromDegrees(-90).ToVec();
                var halfSpreadWithVelocityTolerance = spreadAngle / 2 + 5;
                foreach (var payload in payloads)
                {
                    var velocity = entMan.GetComponent<PhysicsComponent>(payload).LinearVelocity;
                    var dot = Math.Clamp(Vector2.Dot(expectedDirection, Vector2.Normalize(velocity)), -1f, 1f);
                    var degreesFromCenter = MathF.Acos(dot) * 180 / MathF.PI;
                    Assert.That(degreesFromCenter, Is.LessThanOrEqualTo(halfSpreadWithVelocityTolerance));
                }
            }
        });

        await pair.CleanReturnAsync();

        List<EntityUid> FindEntities(string prototype)
        {
            var found = new List<EntityUid>();
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var metadata))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == prototype)
                    found.Add(uid);
            }

            return found;
        }
    }

    [Test]
    public async Task StarShellPayloadShootsOutThenBurstsIntoPhosphorAsh()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Fresh = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();
        var clientTiming = client.ResolveDependency<IGameTiming>();
        await server.WaitAssertion(() => timing.TickRate = 30);
        await client.WaitAssertion(() => clientTiming.TickRate = 30);
        var map = await pair.CreateTestMap();
        var transform = entMan.System<SharedTransformSystem>();
        var trigger = entMan.System<TriggerSystem>();
        var burstOrigin = Vector2.Zero;
        var flightDelay = TimeSpan.FromSeconds(0.25);
        var latestTriggerAt = TimeSpan.Zero;

        await server.WaitAssertion(() =>
        {
            var grenade = entMan.SpawnEntity("RMCAirBurstProjectileStarShell", map.GridCoords);
            burstOrigin = transform.GetMapCoordinates(grenade).Position;
            Assert.That(trigger.Trigger(grenade), Is.True);

            var bullets = FindEntities("RMCStarShellBullet");
            Assert.That(bullets, Has.Count.EqualTo(8));
            foreach (var bullet in bullets)
            {
                var scattering = entMan.GetComponent<ScatteringGrenadeComponent>(bullet);
                var timer = entMan.GetComponent<TimerTriggerComponent>(bullet);
                var velocity = entMan.GetComponent<PhysicsComponent>(bullet).LinearVelocity;
                Assert.Multiple(() =>
                {
                    Assert.That(scattering.IsTriggered, Is.False,
                        "Star shell intermediates must not start triggered before their first physics step.");
                    Assert.That(velocity.Length(), Is.GreaterThan(15f),
                        "Star shell intermediates must be visibly fired away from the airburst.");
                    Assert.That(timer.Delay, Is.EqualTo(flightDelay));
                    Assert.That(trigger.GetRemainingTime((bullet, timer)), Is.EqualTo(flightDelay));
                });
            }
        });

        var preDelayTicks = (int) Math.Ceiling(flightDelay / timing.TickPeriod) - 1;
        await pair.RunTicksSync(preDelayTicks);
        await server.WaitAssertion(() =>
        {
            var bullets = FindEntities("RMCStarShellBullet");
            Assert.That(bullets, Has.Count.EqualTo(8));

            var radialSum = Vector2.Zero;
            foreach (var bullet in bullets)
            {
                var scattering = entMan.GetComponent<ScatteringGrenadeComponent>(bullet);
                var timer = entMan.GetComponent<TimerTriggerComponent>(bullet);
                var displacement = transform.GetMapCoordinates(bullet).Position - burstOrigin;
                latestTriggerAt = TimeSpan.FromTicks(Math.Max(latestTriggerAt.Ticks, timer.NextTrigger.Ticks));
                radialSum += Vector2.Normalize(displacement);
                Assert.Multiple(() =>
                {
                    Assert.That(scattering.IsTriggered, Is.False,
                        "Star shell intermediates must not treat projectile sensors as wall collisions.");
                    Assert.That(displacement.Length(), Is.GreaterThan(2.5f),
                        "Star shell intermediates must visibly travel before becoming burning ash.");
                    Assert.That(trigger.GetRemainingTime((bullet, timer)), Is.GreaterThan(TimeSpan.Zero));
                });
            }

            Assert.That(radialSum.Length(), Is.LessThan(0.1f),
                "Star shell intermediates must disperse evenly around the airburst.");
        });

        var remainingUntilDeadline = latestTriggerAt - timing.CurTime;
        var ticksUntilAfterDeadline = (int) Math.Floor(remainingUntilDeadline / timing.TickPeriod) + 1;
        // RunTicksSync advances the clock after Update, so the timer is processed on the tick after crossing its deadline.
        await pair.RunTicksSync(ticksUntilAfterDeadline + 1);
        await server.WaitAssertion(() =>
        {
            var bullets = FindEntities("RMCStarShellBullet");
            Assert.That(bullets, Has.Count.EqualTo(8));
            foreach (var bullet in bullets)
            {
                var scattering = entMan.GetComponent<ScatteringGrenadeComponent>(bullet);
                var timer = entMan.GetComponent<TimerTriggerComponent>(bullet);
                Assert.Multiple(() =>
                {
                    Assert.That(scattering.IsTriggered, Is.True,
                        "The intermediate must trigger when its flight timer expires.");
                    Assert.That(trigger.GetRemainingTime((bullet, timer)), Is.Null,
                        "The expired flight timer must no longer be active.");
                });
            }
        });

        // ScatteringGrenadeSystem deliberately consumes the trigger on the following frame.
        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            var ashes = FindEntities("RMCStarShellAsh");
            Assert.That(ashes, Has.Count.EqualTo(8));
            var radialSum = Vector2.Zero;
            foreach (var ash in ashes)
            {
                var light = entMan.GetComponent<ExpendableLightComponent>(ash);
                var displacement = transform.GetMapCoordinates(ash).Position - burstOrigin;
                radialSum += Vector2.Normalize(displacement);
                Assert.Multiple(() =>
                {
                    Assert.That(light.Activated, Is.True);
                    Assert.That(light.CurrentState, Is.EqualTo(ExpendableLightState.Lit));
                    Assert.That(displacement.Length(), Is.GreaterThan(2.5f),
                        "Burning ash must appear where its fired intermediate lands, not at the airburst origin.");
                });
            }

            Assert.That(radialSum.Length(), Is.LessThan(0.1f),
                "Burning ash must retain the radial Star Shell dispersion pattern.");
        });

        await pair.CleanReturnAsync();

        List<EntityUid> FindEntities(string prototype)
        {
            var found = new List<EntityUid>();
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var metadata))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == prototype)
                    found.Add(uid);
            }

            return found;
        }
    }

    [Test]
    public async Task SmokeShellSpawnsSmokeAtFixedDistance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid grenade = default;

        await server.WaitAssertion(() =>
        {
            grenade = entMan.SpawnEntity("RMCAirBurstProjectileSmoke", map.GridCoords);
            var stop = new ProjectileFixedDistanceStopEvent();
            entMan.EventBus.RaiseLocalEvent(grenade, ref stop);
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var smokeCount = 0;
            var effectCount = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var metadata))
            {
                if (metadata.Deleted)
                    continue;

                if (metadata.EntityPrototype?.ID == "RMCSmoke")
                    smokeCount++;
                else if (metadata.EntityPrototype?.ID == "CMExplosionEffectGrenade")
                    effectCount++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(grenade), Is.False);
                Assert.That(smokeCount, Is.EqualTo(1));
                Assert.That(effectCount, Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VehicleSmokeShellTimerSpawnsSmoke()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid grenade = default;

        await server.WaitAssertion(() =>
        {
            grenade = entMan.SpawnEntity("RMCAirBurstProjectileSmokeVehicle", map.GridCoords);
            Assert.That(entMan.System<TriggerSystem>()
                .Trigger(grenade, key: "timer"), Is.True);
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var smokeCount = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var metadata))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == "RMCSmoke")
                    smokeCount++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(grenade), Is.False);
                Assert.That(smokeCount, Is.EqualTo(1));
                Assert.That(CountEntities("CMExplosionEffectGrenade"), Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();

        int CountEntities(string prototype)
        {
            var count = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var metadata))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == prototype)
                    count++;
            }

            return count;
        }
    }
}
