using System.Linq;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaShipWeaponWrapperTest
{
    [Test]
    public async Task LargeHarpoonWrappersAreMeleeHarpoonsWithoutSpikeAmmo()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var componentFactory = server.EntMan.ComponentFactory;

        await server.WaitAssertion(() =>
        {
            var wrappers = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.ID.StartsWith(
                    "CMUHunterShipPlacedCMUYautjaSpikeLauncherSpike",
                    StringComparison.Ordinal))
                .OrderBy(proto => proto.ID)
                .ToArray();

            Assert.That(wrappers, Has.Length.EqualTo(8),
                "All eight hunter-ship large-harpoon wrappers must be covered by this regression.");

            foreach (var wrapper in wrappers)
            {
                Assert.That(wrapper.Parents, Does.Contain("CMUYautjaHarpoon"), wrapper.ID);
                Assert.That(wrapper.Parents, Does.Not.Contain("CMUYautjaSpikeLauncher"), wrapper.ID);
                Assert.That(wrapper.TryComp<GunComponent>(out _, componentFactory), Is.False, wrapper.ID);
                Assert.That(wrapper.TryComp<BasicEntityAmmoProviderComponent>(out _, componentFactory), Is.False, wrapper.ID);
                Assert.That(wrapper.TryComp<YautjaSpikeLauncherComponent>(out _, componentFactory), Is.False, wrapper.ID);
            }
        });

        await pair.CleanReturnAsync();
    }
}
