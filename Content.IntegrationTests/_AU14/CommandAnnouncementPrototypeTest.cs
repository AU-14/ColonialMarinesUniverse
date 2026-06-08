using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.ControlComputer;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14;

[TestFixture]
public sealed class CommandAnnouncementPrototypeTest
{
    [Test]
    public async Task CommandAnnouncementPrototypesDeclareFaction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();

            AssertCommunicationsFaction(prototypes, factory, "RMCGroundsideOperationsConsoleGovfor", "govfor");
            AssertCommunicationsFaction(prototypes, factory, "RMCGroundsideOperationsConsoleOpfor", "opfor");
            AssertCommunicationsFaction(prototypes, factory, "AU14TabletGovfor", "govfor");
            AssertCommunicationsFaction(prototypes, factory, "AU14TabletOpfor", "opfor");
            AssertControlFaction(prototypes, factory, "AU14TabletGovfor", "govfor");
            AssertControlFaction(prototypes, factory, "AU14TabletOpfor", "opfor");
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertCommunicationsFaction(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string prototype,
        string expected)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(prototype, out var entity), Is.True, prototype);
        Assert.That(entity!.TryGetComponent<MarineCommunicationsComputerComponent>(out var computer, factory), Is.True, prototype);
        Assert.That(computer!.Faction, Is.EqualTo(expected), prototype);
    }

    private static void AssertControlFaction(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string prototype,
        string expected)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(prototype, out var entity), Is.True, prototype);
        Assert.That(entity!.TryGetComponent<MarineControlComputerComponent>(out var computer, factory), Is.True, prototype);
        Assert.That(computer!.Faction, Is.EqualTo(expected), prototype);
    }
}
