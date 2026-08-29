using Content.Shared.CMU14.Intel;
using Content.Shared._RMC14.Intel;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Intel;

[TestFixture]
public sealed class IntelConsoleClaimPrototypeTest
{
    [Test]
    public async Task ClfBaseIntelConsoleIsClaimableByGovfor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();
            var prototype = prototypes.Index<EntityPrototype>("CMUComputerIntelCLFClaimable");

            Assert.Multiple(() =>
            {
                Assert.That(prototype.TryComp<IntelConsoleComponent>(out _, factory), Is.True);
                Assert.That(prototype.TryComp<ClaimableIntelConsoleComponent>(out var claim, factory), Is.True);
                Assert.That(claim!.ClaimingTeam, Is.EqualTo(Team.GovFor));
                Assert.That(claim.RequiredPreset, Is.EqualTo("Insurgency"));
                Assert.That(claim.ClaimTime, Is.EqualTo(TimeSpan.FromSeconds(10)));
            });
        });

        await pair.CleanReturnAsync();
    }
}
