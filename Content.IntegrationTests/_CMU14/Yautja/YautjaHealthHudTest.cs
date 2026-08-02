using System.Linq;
using Content.Shared.Overlays;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHealthHudTest
{
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
                var mask = prototypes.Index<EntityPrototype>("CMUYautjaMask");

                Assert.That(mask.TryGetComponent<ShowHealthBarsComponent>(out var bars, factory), Is.True);
                Assert.That(mask.TryGetComponent<ShowHealthIconsComponent>(out var icons, factory), Is.True);

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
