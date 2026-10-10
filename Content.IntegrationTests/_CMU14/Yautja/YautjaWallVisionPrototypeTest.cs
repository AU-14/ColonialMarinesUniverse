using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.NightVision;
using NUnit.Framework;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaWallVisionPrototypeTest
{
    private static readonly Robust.Shared.Prototypes.EntProtoId CMUMobYautjaPrototype = "CMUMobYautja";
    private static readonly Robust.Shared.Prototypes.EntProtoId CMUYautjaNightVisionGlassesPrototype = "CMUYautjaNightVisionGlasses";

    [Test]
    public async Task YautjaWallVisionIsSeparateFromTheNightVisionVisor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            var yautja = prototypes.Index<EntityPrototype>(CMUMobYautjaPrototype);
            var visor = prototypes.Index<EntityPrototype>(CMUYautjaNightVisionGlassesPrototype);

            Assert.Multiple(() =>
            {
                Assert.That(yautja.TryComp<YautjaComponent>(out _, factory), Is.True);
                Assert.That(visor.TryComp<NightVisionItemComponent>(out var nightVision, factory), Is.True);
                Assert.That(visor.TryComp<YautjaMaskVisorGlassesComponent>(out var thermalVisor, factory), Is.True);
                Assert.That(nightVision!.DefaultState, Is.EqualTo(NightVisionState.Full));
                Assert.That(thermalVisor!.ThermalVisionEnabled, Is.False,
                    "Only a server-created, mask-linked visor may activate thermal wall vision.");
            });
        });

        await pair.CleanReturnAsync();
    }
}
