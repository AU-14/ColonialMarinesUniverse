using System.Linq;
using Content.Shared.Overlays;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHealthHudTest
{
    private static readonly Robust.Shared.Prototypes.EntProtoId CMUYautjaMaskPrototype = "CMUYautjaMask";
    private static readonly Robust.Shared.Prototypes.EntProtoId CMUYautjaPoweredHelmetPrototype = "CMUYautjaPoweredHelmet";

    [Test]
    public async Task YautjaMaskShowsBiologicalBarsAndBiologicalOrXenoIcons()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var factory = server.EntMan.ComponentFactory;
                var mask = prototypes.Index<EntityPrototype>(CMUYautjaMaskPrototype);

                Assert.That(mask.TryComp<ShowHealthBarsComponent>(out var bars, factory), Is.True);
                Assert.That(mask.TryComp<ShowHealthIconsComponent>(out var icons, factory), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(bars!.DamageContainers.Select(container => container.Id),
                        Is.EquivalentTo(new[] { "Biological" }));
                    Assert.That(icons!.DamageContainers.Select(container => container.Id),
                        Is.EquivalentTo(new[] { "Biological", "Xeno" }));
                });
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task MilitaryHelmetShowsTheSameHealthHudAsTheYautjaMask()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var factory = server.EntMan.ComponentFactory;
                var helmet = prototypes.Index<EntityPrototype>(CMUYautjaPoweredHelmetPrototype);

                Assert.That(helmet.TryComp<ShowHealthBarsComponent>(out var bars, factory), Is.True);
                Assert.That(helmet.TryComp<ShowHealthIconsComponent>(out var icons, factory), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(bars!.DamageContainers.Select(container => container.Id),
                        Is.EquivalentTo(new[] { "Biological" }));
                    Assert.That(icons!.DamageContainers.Select(container => container.Id),
                        Is.EquivalentTo(new[] { "Biological", "Xeno" }));
                });
            });
        }
        finally
        {
            server.Dispose();
        }
    }
}
