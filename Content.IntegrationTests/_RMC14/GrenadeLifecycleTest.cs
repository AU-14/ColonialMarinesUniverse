using Content.Client.Trigger.Components;
using Content.Server.Explosion.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class GrenadeLifecycleTest
{
    [Test]
    public async Task BlastM12HasFlickeringPrimedVisual()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var prototype = prototypes.Index<EntityPrototype>("RMCGrenadeBlastM12");

            Assert.That(
                prototype.TryComp<TimerTriggerVisualsComponent>(out _, client.EntMan.ComponentFactory),
                Is.True,
                "The M12 primed RSI animation needs TimerTriggerVisuals to start flickering.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlastM12DeletesAfterFuse()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid grenade = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var trigger = entities.System<TriggerSystem>();
            grenade = entities.SpawnEntity("RMCGrenadeBlastM12", MapCoordinates.Nullspace);
            var timer = entities.GetComponent<TimerTriggerComponent>(grenade);
            var projectileGrenade = entities.GetComponent<ProjectileGrenadeComponent>(grenade);
            timer.Delay = TimeSpan.FromSeconds(0.1);

            Assert.That(projectileGrenade.TriggerKey, Is.EqualTo(timer.KeyOut),
                "The projectile payload and cleanup must listen for the fuse's final trigger key.");
            Assert.That(trigger.Trigger(grenade, key: "startTimer", predicted: false), Is.True);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(grenade), Is.True);
            Assert.That(
                entities.System<SharedAppearanceSystem>().TryGetData<TriggerVisualState>(
                    grenade,
                    TriggerVisuals.VisualState,
                    out var visualState),
                Is.True);
            Assert.That(visualState, Is.EqualTo(TriggerVisualState.Primed));
        });

        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.EntityExists(grenade), Is.False,
                "The M12 casing remained after its fuse triggered the explosion."));

        await pair.CleanReturnAsync();
    }
}
