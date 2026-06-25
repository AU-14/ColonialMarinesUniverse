using System.Collections.Generic;
using Content.Shared.AU14.Ambassador;
using Content.Shared._CMU14.Threats;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.ThirdParty;

[TestFixture]
public sealed class AmbassadorThirdPartyCallInsTest
{
    [Test]
    public async Task AmbassadorCallableThirdPartiesUseShuttleEntry()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var checkedParties = new HashSet<string>();

            foreach (var (entity, console) in pair.GetPrototypesWithComponent<AmbassadorConsoleComponent>())
            {
                foreach (var id in console.CallableParties.Keys)
                {
                    checkedParties.Add(id);
                    Assert.That(
                        prototypes.TryIndex<ThirdPartyPrototype>(id, out var party),
                        Is.True,
                        $"{entity.ID} references missing ambassador third party '{id}'.");

                    Assert.That(
                        party.EntryMethod,
                        Is.EqualTo("shuttle").IgnoreCase,
                        $"{entity.ID} ambassador third party '{id}' must use shuttle entry.");

                    Assert.That(
                        party.dropshippath.ToString(),
                        Is.Not.Empty,
                        $"{entity.ID} ambassador third party '{id}' must define a dropship path.");
                }
            }

            Assert.That(checkedParties, Is.Not.Empty, "No ambassador third-party call-ins were checked.");
        });

        await pair.CleanReturnAsync();
    }
}
