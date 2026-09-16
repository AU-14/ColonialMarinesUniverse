using System.Collections.Generic;
using Content.Shared._RMC14.Wieldable.Components;
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

    [TestCaseSource(nameof(FlamerWieldDelays))]
    public async Task FlamersKeepWieldDelayWithoutUseDelay(EntProtoId prototype, double expectedWieldDelaySeconds)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(prototype, out var flamer), Is.True);
            Assert.That(flamer!.TryComp<UseDelayComponent>(out var useDelay, factory), Is.True);
            Assert.That(flamer.TryComp<WieldDelayComponent>(out var wieldDelay, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(useDelay!.Delay, Is.EqualTo(TimeSpan.Zero));
                Assert.That(wieldDelay!.BaseDelay, Is.EqualTo(TimeSpan.FromSeconds(expectedWieldDelaySeconds)));
            });
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
            Assert.That(weapon!.TryComp<CorrodibleComponent>(out var corrodible, factory), Is.True);
            Assert.That(corrodible!.IsCorrodible, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static IEnumerable<TestCaseData> FlamerWieldDelays()
    {
        yield return new TestCaseData(M240Flamer, 1).SetName("M240IncineratorHasOneSecondWieldDelayWithoutUseDelay");
        yield return new TestCaseData(M34TFlamer, 1.75).SetName("M240TIncineratorHasOnePointSevenFiveSecondWieldDelayWithoutUseDelay");
    }

    private static IEnumerable<TestCaseData> MeltableWeaponPrototypes()
    {
        yield return new TestCaseData(M240Flamer).SetName("M240IncineratorIsMeltable");
        //yield return new TestCaseData(M34TFlamer).SetName("M34TIncineratorIsMeltable");
        //yield return new TestCaseData(Smaw).SetName("SmawIsMeltable");
    }
}
