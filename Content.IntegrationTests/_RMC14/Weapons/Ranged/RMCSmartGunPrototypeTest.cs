using Content.IntegrationTests.Fixtures;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture]
public sealed class RMCSmartGunPrototypeTest : GameTest
{
    private static readonly EntProtoId PmcSmartGun = "RMCSmartGunPMC";

    [Test]
    public async Task PmcSmartGunDoesNotUseCamouflageTest()
    {
        var prototypeManager = Pair.Server.ResolveDependency<IPrototypeManager>();

        await Pair.Server.WaitAssertion(() =>
        {
            var prototype = prototypeManager.Index<EntityPrototype>(PmcSmartGun);

            Assert.That(prototype.Components.TryGetComponent("ItemCamouflage", out _), Is.False,
                "The ML79A sprite has no camouflage color-mask states.");
        });
    }
}
