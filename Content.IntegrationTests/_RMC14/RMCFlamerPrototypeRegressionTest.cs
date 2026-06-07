using System.Collections.Generic;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class RMCFlamerPrototypeRegressionTest
{
    private static readonly EntProtoId M240Flamer = "RMCWeaponFlamer";
    private static readonly EntProtoId M34TFlamer = "RMCWeaponFlamerSpec";
    private static readonly EntProtoId Smaw = "RMCWeaponLauncherM5ATL";

    [TestCaseSource(nameof(FlamerUseDelays))]
    public async Task FlamersKeepRequestedUseDelay(EntProtoId prototype, double expectedDelaySeconds)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(prototype, out var flamer), Is.True);
            Assert.That(flamer!.TryGetComponent<UseDelayComponent>(out var useDelay, factory), Is.True);
            Assert.That(useDelay!.Delay, Is.EqualTo(TimeSpan.FromSeconds(expectedDelaySeconds)));
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(MeltableWeaponPrototypes))]
    public async Task RequestedWeaponsAreMeltable(EntProtoId prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(prototype, out var weapon), Is.True);
            Assert.That(weapon!.TryGetComponent<CorrodibleComponent>(out var corrodible, factory), Is.True);
            Assert.That(corrodible!.IsCorrodible, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static IEnumerable<TestCaseData> FlamerUseDelays()
    {
        yield return new TestCaseData(M240Flamer, 2).SetName("M240IncineratorHasTwoSecondUseDelay");
        yield return new TestCaseData(M34TFlamer, 3).SetName("M34TIncineratorHasThreeSecondUseDelay");
    }

    private static IEnumerable<TestCaseData> MeltableWeaponPrototypes()
    {
        yield return new TestCaseData(M240Flamer).SetName("M240IncineratorIsMeltable");
        yield return new TestCaseData(M34TFlamer).SetName("M34TIncineratorIsMeltable");
        yield return new TestCaseData(Smaw).SetName("SmawIsMeltable");
    }
}
