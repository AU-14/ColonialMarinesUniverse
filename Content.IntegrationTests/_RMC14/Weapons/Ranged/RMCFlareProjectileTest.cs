using Content.Server.Light.Components;
using Content.Server._RMC14.Trigger;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Light.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture, TestOf(typeof(RMCTriggerSystem))]
public sealed class RMCFlareProjectileTest
{
    [Test]
    public async Task FixedDistanceStop_ScattersLitFlareThatAdvancesToPhaseOne()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid projectile = default;
        EntityUid flare = default;

        await server.WaitAssertion(() =>
        {
            projectile = entMan.SpawnEntity("RMCFlareBullet", map.GridCoords);
            var stop = new ProjectileFixedDistanceStopEvent();
            entMan.EventBus.RaiseLocalEvent(projectile, ref stop);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var flareCount = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var metadata))
            {
                if (metadata.Deleted || metadata.EntityPrototype?.ID != "CMFlare")
                    continue;

                flare = uid;
                flareCount++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(projectile), Is.False);
                Assert.That(flareCount, Is.EqualTo(1));
                Assert.That(entMan.EntityExists(flare), Is.True);

                var light = entMan.GetComponent<ExpendableLightComponent>(flare);
                Assert.That(light.Activated, Is.True);
                Assert.That(light.CurrentState, Is.EqualTo(ExpendableLightState.Lit));
            });
        });

        await server.WaitAssertion(() =>
        {
            entMan.GetComponent<ExpendableLightComponent>(flare).StateExpiryTime = 0;
        });
        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            var appearance = entMan.System<SharedAppearanceSystem>();
            var hasBehaviour = appearance.TryGetData<string>(
                flare,
                ExpendableLightVisuals.Behavior,
                out var behaviour);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(flare), Is.True);

                var light = entMan.GetComponent<ExpendableLightComponent>(flare);
                Assert.That(light.Activated, Is.True);
                Assert.That(light.CurrentState, Is.EqualTo(ExpendableLightState.PhaseOne));
                Assert.That(hasBehaviour, Is.True);
                Assert.That(behaviour, Is.EqualTo("phase_1"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SignalFlare_UsesLocalizedCasNameAndFullRmcLifetime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid signalFlare = default;

        await server.WaitAssertion(() =>
        {
            var projectile = entMan.SpawnEntity("RMCFlareCASBullet", map.GridCoords);
            var stop = new ProjectileFixedDistanceStopEvent();
            entMan.EventBus.RaiseLocalEvent(projectile, ref stop);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var flareCount = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var metadata))
            {
                if (metadata.Deleted || metadata.EntityPrototype?.ID != "RMCFlareCAS")
                    continue;

                signalFlare = uid;
                flareCount++;
            }

            Assert.That(flareCount, Is.EqualTo(1));

            var light = entMan.GetComponent<ExpendableLightComponent>(signalFlare);
            var lifetime = light.GlowDuration
                + light.PhaseOneDuration
                + light.PhaseTwoDuration
                + light.PhaseThreeDuration
                + light.PhaseFourDuration
                + light.PhaseFiveDuration
                + light.FadeOutDuration;

            Assert.Multiple(() =>
            {
                Assert.That(light.CurrentState, Is.EqualTo(ExpendableLightState.Lit));
                Assert.That(lifetime, Is.EqualTo(TimeSpan.FromSeconds(180)));
            });

            light.StateExpiryTime = 0;
        });

        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            var light = entMan.GetComponent<ExpendableLightComponent>(signalFlare);

            Assert.Multiple(() =>
            {
                Assert.That(light.CurrentState, Is.EqualTo(ExpendableLightState.PhaseOne));
                Assert.That(light.StateExpiryTime, Is.EqualTo(150).Within(0.1));
            });
        });

        var activationTicks = (int)Math.Ceiling(TimeSpan.FromSeconds(5) / timing.TickPeriod);
        await pair.RunTicksSync(activationTicks + 15);
        await server.WaitAssertion(() =>
        {
            var target = entMan.GetComponent<DropshipTargetComponent>(signalFlare);
            var metadata = entMan.GetComponent<MetaDataComponent>(signalFlare);

            Assert.Multiple(() =>
            {
                Assert.That(target.Abbreviation, Is.Not.Null.And.Not.Empty);
                Assert.That(metadata.EntityName, Is.EqualTo(target.Abbreviation));
                Assert.That(entMan.HasComponent<ActiveFlareSignalComponent>(signalFlare), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
