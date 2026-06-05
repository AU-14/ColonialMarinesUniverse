using Content.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class RMCFlamerPrototypeRegressionTest
{
    private static readonly EntProtoId M34TFlamer = "RMCWeaponFlamerSpec";

    [Test]
    public async Task M34TIncineratorHasUseDelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(M34TFlamer, out var flamer), Is.True);
            Assert.That(flamer!.TryGetComponent<UseDelayComponent>(out var useDelay, factory), Is.True);
            Assert.That(useDelay!.Delay, Is.EqualTo(TimeSpan.FromSeconds(0.5)));
        });

        await pair.CleanReturnAsync();
    }
}
