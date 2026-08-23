using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.NightVision;
using NUnit.Framework;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaWallVisionPrototypeTest
{
    [Test]
    public async Task YautjaWallVisionIsSeparateFromTheNightVisionVisor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            var yautja = prototypes.Index<EntityPrototype>("CMUMobYautja");
            var visor = prototypes.Index<EntityPrototype>("CMUYautjaNightVisionGlasses");

            Assert.Multiple(() =>
            {
                Assert.That(yautja.TryGetComponent<YautjaComponent>(out _, factory), Is.True);
                Assert.That(visor.TryGetComponent<NightVisionItemComponent>(out var nightVision, factory), Is.True);
                Assert.That(visor.TryGetComponent<YautjaMaskVisorGlassesComponent>(out var thermalVisor, factory), Is.True);
                Assert.That(nightVision!.DefaultState, Is.EqualTo(NightVisionState.Full));
                Assert.That(thermalVisor!.ThermalVisionEnabled, Is.False,
                    "Only a server-created, mask-linked visor may activate thermal wall vision.");
            });
        });

        await pair.CleanReturnAsync();
    }
}
