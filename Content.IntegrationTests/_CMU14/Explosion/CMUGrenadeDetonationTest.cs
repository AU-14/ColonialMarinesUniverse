using Content.Server._CMU14.Explosion;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Smoke;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests._CMU14.Explosion;

[TestFixture]
[TestOf(typeof(CMUGrenadeDetonationSystem))]
public sealed class CMUGrenadeDetonationTest
{
    [TestCase("CMGrenadeHighExplosive", 4f)]
    [TestCase("CMGrenadeFrag", 4f)]
    [TestCase("CMGrenadeSmoke", 2.5f)]
    public async Task TargetGrenadesHaveCMUModeAndKeepExistingFuse(
        string prototype,
        float expectedDelay)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var grenade = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);

            try
            {
                var mode = entMan.GetComponent<CMUGrenadeDetonationModeComponent>(grenade);
                var timer = entMan.GetComponent<OnUseTimerTriggerComponent>(grenade);

                Assert.Multiple(() =>
                {
                    Assert.That(mode.Mode, Is.EqualTo(CMUGrenadeDetonationMode.Timed));
                    Assert.That(mode.Armed, Is.False);
                    Assert.That(mode.ImpactPayloadMultiplier, Is.EqualTo(0.75f));
                    Assert.That(timer.Delay, Is.EqualTo(expectedDelay).Within(0.001f));
                });
            }
            finally
            {
                entMan.DeleteEntity(grenade);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ModeCanChangeBeforeArming()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUGrenadeDetonationSystem>();
            var grenade = entMan.SpawnEntity("CMGrenadeHighExplosive", MapCoordinates.Nullspace);

            try
            {
                var component = entMan.GetComponent<CMUGrenadeDetonationModeComponent>(grenade);

                Assert.That(
                    system.TrySetMode(grenade, CMUGrenadeDetonationMode.Impact),
                    Is.True);
                Assert.That(component.Mode, Is.EqualTo(CMUGrenadeDetonationMode.Impact));

                Assert.That(
                    system.TrySetMode(grenade, CMUGrenadeDetonationMode.Timed),
                    Is.True);
                Assert.That(component.Mode, Is.EqualTo(CMUGrenadeDetonationMode.Timed));
            }
            finally
            {
                entMan.DeleteEntity(grenade);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ImpactModeReducesExplosionAndFragmentPayloadByTwentyFivePercent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUGrenadeDetonationSystem>();
            var grenade = entMan.SpawnEntity("CMGrenadeFrag", MapCoordinates.Nullspace);

            try
            {
                Assert.That(system.TrySetMode(grenade, CMUGrenadeDetonationMode.Impact), Is.True);

                var explosion = new GetExplosionTriggerPropertiesEvent(110f, 8f);
                entMan.EventBus.RaiseLocalEvent(grenade, ref explosion);

                var fragments = new GetProjectileGrenadePayloadEvent(48);
                entMan.EventBus.RaiseLocalEvent(grenade, ref fragments);

                Assert.Multiple(() =>
                {
                    Assert.That(explosion.TotalIntensity, Is.EqualTo(82.5f).Within(0.001f));
                    Assert.That(explosion.MaxIntensity, Is.EqualTo(6f).Within(0.001f));
                    Assert.That(fragments.Count, Is.EqualTo(36));
                    Assert.That(fragments.DamageMultiplier, Is.EqualTo(0.75f).Within(0.001f));
                });
            }
            finally
            {
                entMan.DeleteEntity(grenade);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TimedModeLeavesPayloadUnchanged()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var grenade = entMan.SpawnEntity("CMGrenadeFrag", MapCoordinates.Nullspace);

            try
            {
                var explosion = new GetExplosionTriggerPropertiesEvent(110f, 8f);
                entMan.EventBus.RaiseLocalEvent(grenade, ref explosion);

                var fragments = new GetProjectileGrenadePayloadEvent(48);
                entMan.EventBus.RaiseLocalEvent(grenade, ref fragments);

                var spawn = new GetSpawnOnTriggerPrototypeEvent("RMCSmoke");
                entMan.EventBus.RaiseLocalEvent(grenade, ref spawn);

                Assert.Multiple(() =>
                {
                    Assert.That(explosion.TotalIntensity, Is.EqualTo(110f));
                    Assert.That(explosion.MaxIntensity, Is.EqualTo(8f));
                    Assert.That(fragments.Count, Is.EqualTo(48));
                    Assert.That(fragments.DamageMultiplier, Is.EqualTo(1f));
                    Assert.That(spawn.Prototype.ToString(), Is.EqualTo("RMCSmoke"));
                });
            }
            finally
            {
                entMan.DeleteEntity(grenade);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GrenadeOutsideExistingM40ProfileRemainsIneligible()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUGrenadeDetonationSystem>();
            var grenade = entMan.SpawnEntity("CMGrenadeFragOld", MapCoordinates.Nullspace);

            try
            {
                var mode = entMan.GetComponent<CMUGrenadeDetonationModeComponent>(grenade);
                Assert.That(system.TrySetMode(grenade, CMUGrenadeDetonationMode.Impact), Is.False);
                Assert.That(mode.Mode, Is.EqualTo(CMUGrenadeDetonationMode.Timed));
            }
            finally
            {
                entMan.DeleteEntity(grenade);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMGrenadeSmoke", "RMCSmoke", "CMUImpactRMCSmoke", 2, 7.5f)]
    [TestCase("RMCGrenadeWhitePhosphorus", "RMCSmokePhosphorusWeak", "CMUImpactRMCSmokePhosphorusWeak", 2, 1.5f)]
    [TestCase("RMCGrenadeWhitePhosphorusCompound", "RMCSmokePhosphorus", "CMUImpactRMCSmokePhosphorus", 2, 3.75f)]
    [TestCase("CMU14TearGasGrenade", "CMU14TearGas", "CMUImpactTearGas", 1, 9f)]
    [TestCase("AU14GrenadeNeuroRMC", "AU14GrenadeNeuroGas", "CMUImpactNeuroGas", 3, 9f)]
    public async Task ImpactSmokeAndChemicalPayloadsUseReducedVariant(
        string grenadePrototype,
        string normalPayload,
        string impactPayload,
        int expectedRange,
        float expectedLifetime)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUGrenadeDetonationSystem>();
            var grenade = entMan.SpawnEntity(grenadePrototype, MapCoordinates.Nullspace);
            var payload = EntityUid.Invalid;

            try
            {
                Assert.That(system.TrySetMode(grenade, CMUGrenadeDetonationMode.Impact), Is.True);

                var spawn = new GetSpawnOnTriggerPrototypeEvent(normalPayload);
                entMan.EventBus.RaiseLocalEvent(grenade, ref spawn);
                Assert.That(spawn.Prototype.ToString(), Is.EqualTo(impactPayload));

                payload = entMan.SpawnEntity(impactPayload, MapCoordinates.Nullspace);
                var smoke = entMan.GetComponent<EvenSmokeComponent>(payload);
                var despawn = entMan.GetComponent<TimedDespawnComponent>(payload);

                Assert.Multiple(() =>
                {
                    Assert.That(smoke.Range, Is.EqualTo(expectedRange));
                    Assert.That(despawn.Lifetime, Is.EqualTo(expectedLifetime).Within(0.001f));
                });
            }
            finally
            {
                if (entMan.EntityExists(payload))
                    entMan.DeleteEntity(payload);
                entMan.DeleteEntity(grenade);
            }
        });

        await pair.CleanReturnAsync();
    }
}
