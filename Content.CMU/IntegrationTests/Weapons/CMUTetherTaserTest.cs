using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.Stun;
using Content.Shared.CMU14.Weapons.Taser;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
public sealed class CMUTetherTaserTest
{
    private sealed record Setup(EntityUid Taser, EntityUid Shooter, EntityUid Target);

    private static async Task<Setup> Spawn(TestPair pair, TestMapData map)
    {
        var entities = pair.Server.EntMan;
        Setup setup = default!;
        await pair.Server.WaitAssertion(() =>
        {
            var shooter = entities.SpawnAtPosition("CMMobHuman", map.GridCoords);
            var target = entities.SpawnAtPosition("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var taser = entities.SpawnAtPosition("RMCWeaponTaser", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(shooter, taser, checkActionBlocker: false));
            setup = new Setup(taser, shooter, target);
        });

        return setup;
    }

    private static async Task HitWithProbe(TestPair pair, Setup setup)
    {
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            var probe = entities.Spawn("CMUProjectileTaserProbe", MapCoordinates.Nullspace);
            var projectile = entities.GetComponent<ProjectileComponent>(probe);
            projectile.Shooter = setup.Shooter;
            projectile.Weapon = setup.Taser;

            var ev = new ProjectileHitEvent(projectile.Damage, setup.Target, setup.Shooter);
            entities.EventBus.RaiseLocalEvent(probe, ref ev);
            entities.DeleteEntity(probe);
        });
    }

    /// <summary>Squeezes the trigger every tick for <paramref name="seconds"/>, the way a held mouse button does.</summary>
    private static async Task Hold(TestPair pair, Setup setup, float seconds)
    {
        var entities = pair.Server.EntMan;
        var ticks = (int) Math.Ceiling(seconds / pair.Server.Timing.TickPeriod.TotalSeconds);
        for (var i = 0; i < ticks; i++)
        {
            await pair.Server.WaitPost(() =>
            {
                var gun = entities.GetComponent<GunComponent>(setup.Taser);
                var ev = new ShotAttemptedEvent { User = setup.Shooter, Used = (setup.Taser, gun) };
                entities.EventBus.RaiseLocalEvent(setup.Taser, ref ev);
                Assert.That(ev.Cancelled, Is.True, "With the probe in, the trigger must work the current instead of firing.");
            });
            await pair.RunTicksSync(1);
        }
    }

    [Test]
    public async Task ProbeEmbedsAndHoldingBuildsFromCollapseToFire()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var setup = await Spawn(pair, map);

        await HitWithProbe(pair, setup);

        double stamina = 0;
        await pair.Server.WaitAssertion(() =>
        {
            var taser = entities.GetComponent<CMUTetherTaserComponent>(setup.Taser);
            Assert.That(taser.Target, Is.EqualTo(setup.Target));
            Assert.That(taser.Probe, Is.Not.Null);
            Assert.That(entities.GetComponent<CMUTaserTetheredComponent>(setup.Target).Tasers, Does.Contain(setup.Taser));
            stamina = entities.GetComponent<RMCStaminaComponent>(setup.Target).Current;
        });

        // An embedded probe does nothing until the trigger is held.
        await pair.RunSeconds(2);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<RMCStaminaComponent>(setup.Target).Current, Is.EqualTo(stamina));
            Assert.That(entities.HasComponent<JitteringComponent>(setup.Target), Is.False);
        });

        await Hold(pair, setup, 1f);
        float earlyAmplitude = 0;
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<RMCStaminaComponent>(setup.Target).Current, Is.LessThan(stamina));
            Assert.That(entities.System<RMCSizeStunSystem>().IsKnockedOut(setup.Target), Is.False,
                "A short squeeze should only jolt the target.");
            earlyAmplitude = entities.GetComponent<JitteringComponent>(setup.Target).Amplitude;
        });

        await Hold(pair, setup, 6f);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.System<RMCSizeStunSystem>().IsKnockedOut(setup.Target), Is.True,
                "Holding the trigger for several seconds must collapse the target.");
            Assert.That(entities.GetComponent<JitteringComponent>(setup.Target).Amplitude, Is.GreaterThan(earlyAmplitude),
                "The shakes must grow the longer the current runs.");
            Assert.That(entities.System<SharedRMCFlammableSystem>().IsOnFire(setup.Target), Is.False);
        });

        await Hold(pair, setup, 10f);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.System<SharedRMCFlammableSystem>().IsOnFire(setup.Target), Is.True,
                "Keep the current running long enough and the target must catch fire.");
        });

        await pair.Server.WaitPost(() =>
        {
            entities.DeleteEntity(setup.Taser);
            entities.DeleteEntity(setup.Shooter);
            entities.DeleteEntity(setup.Target);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// By the time they catch fire a tased target has usually collapsed, and a knocked-out mob is muted. The burning
    /// scream has to be heard anyway.
    /// </summary>
    [Test]
    public async Task BurningScreamIsHeardWhileCollapsed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var setup = await Spawn(pair, map);

        // Someone has to be around to hear it.
        await pair.Server.WaitPost(() => pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, setup.Shooter));
        await HitWithProbe(pair, setup);

        var seen = new HashSet<EntityUid>();
        var burningHeard = false;
        var ticks = (int) Math.Ceiling(20 / pair.Server.Timing.TickPeriod.TotalSeconds);
        for (var i = 0; i < ticks && !burningHeard; i++)
        {
            await pair.Server.WaitPost(() =>
            {
                var gun = entities.GetComponent<GunComponent>(setup.Taser);
                var ev = new ShotAttemptedEvent { User = setup.Shooter, Used = (setup.Taser, gun) };
                entities.EventBus.RaiseLocalEvent(setup.Taser, ref ev);

                var audio = entities.EntityQueryEnumerator<AudioComponent>();
                while (audio.MoveNext(out var uid, out var comp))
                {
                    string file = comp.FileName;
                    if (seen.Add(uid) && file.StartsWith("/Audio/CMU14/Voice/Burning/"))
                        burningHeard = true;
                }
            });
            await pair.RunTicksSync(1);
        }

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.System<SharedRMCFlammableSystem>().IsOnFire(setup.Target), Is.True);
            Assert.That(entities.System<RMCSizeStunSystem>().IsKnockedOut(setup.Target), Is.True);
            Assert.That(burningHeard, Is.True, "Bursting into flames from the taser must play the burning scream.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WireSnapsOutOfReach()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var setup = await Spawn(pair, map);

        await HitWithProbe(pair, setup);
        await pair.Server.WaitPost(() =>
        {
            entities.System<SharedTransformSystem>().SetMapCoordinates(setup.Shooter,
                new MapCoordinates(new Vector2(20, 0), map.MapId));
        });
        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<CMUTetherTaserComponent>(setup.Taser).Target, Is.Null);
            Assert.That(entities.HasComponent<CMUTaserTetheredComponent>(setup.Target), Is.False);
        });

        await pair.Server.WaitPost(() =>
        {
            entities.DeleteEntity(setup.Taser);
            entities.DeleteEntity(setup.Shooter);
            entities.DeleteEntity(setup.Target);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProbeCanBePulledOut()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var setup = await Spawn(pair, map);

        await HitWithProbe(pair, setup);
        await pair.Server.WaitAssertion(() =>
        {
            var tethered = entities.GetComponent<CMUTaserTetheredComponent>(setup.Target);
            var args = new DoAfterArgs(entities, setup.Target, tethered.PullOutDelay,
                new CMUTaserProbePullOutDoAfterEvent(), setup.Target, setup.Target)
            {
                NeedHand = true,
            };
            Assert.That(entities.System<SharedDoAfterSystem>().TryStartDoAfter(args), Is.True);
        });

        await pair.RunSeconds(3);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<CMUTetherTaserComponent>(setup.Taser).Target, Is.Null);
            Assert.That(entities.HasComponent<CMUTaserTetheredComponent>(setup.Target), Is.False);
        });

        await pair.Server.WaitPost(() =>
        {
            entities.DeleteEntity(setup.Taser);
            entities.DeleteEntity(setup.Shooter);
            entities.DeleteEntity(setup.Target);
        });

        await pair.CleanReturnAsync();
    }
}
