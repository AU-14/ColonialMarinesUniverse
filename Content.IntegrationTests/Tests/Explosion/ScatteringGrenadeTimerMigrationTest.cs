using Content.Shared.Explosion.Components;
using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Explosion;

[TestFixture]
public sealed class ScatteringGrenadeTimerMigrationTest
{
    private static readonly (EntProtoId Grenade, EntProtoId Payload, float Delay)[] DelayedPayloads =
    [
        ("ClusterBananaPeel", "TrashBananaPeelExplosive", 20f),
        ("SlipocalypseClusterSoap", "SoapletSyndie", 60f),
    ];

    private static readonly EntProtoId[] TimerlessRmcPayloads =
    [
        "CMFlare",
        "RMCFlareCAS",
        "RMCFlareL96",
        "RMCStarShellAsh",
        "RMCBatonSlugHIRR",
    ];

    [Test]
    public async Task DelayedPayloadsCarryExplicitFinalTriggerTimersOnlyWhereRequired()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var (grenadeId, payloadId, delay) in DelayedPayloads)
            {
                var grenade = prototypes.Index<EntityPrototype>(grenadeId);
                Assert.That(grenade.TryComp<ScatteringGrenadeComponent>(out var scattering, factory), Is.True,
                    grenadeId.ToString());
                Assert.Multiple(() =>
                {
                    Assert.That(scattering!.FillPrototype, Is.EqualTo(payloadId), grenadeId.ToString());
                    Assert.That(scattering.DelayBeforeTriggerContents, Is.EqualTo(delay), grenadeId.ToString());
                    Assert.That(scattering.TriggerKey, Is.EqualTo("trigger"), grenadeId.ToString());
                });

                var payload = prototypes.Index<EntityPrototype>(payloadId);
                Assert.That(payload.TryComp<TimerTriggerComponent>(out var timer, factory), Is.True,
                    payloadId.ToString());
                Assert.That(timer!.KeyOut, Is.EqualTo("trigger"), payloadId.ToString());
            }

            foreach (var payloadId in TimerlessRmcPayloads)
            {
                var payload = prototypes.Index<EntityPrototype>(payloadId);
                Assert.That(payload.TryComp<TimerTriggerComponent>(out _, factory), Is.False,
                    $"{payloadId} must remain timerless; its scattering parent uses immediate or RMC-specific behavior.");
            }
        });

        await pair.CleanReturnAsync();
    }
}
