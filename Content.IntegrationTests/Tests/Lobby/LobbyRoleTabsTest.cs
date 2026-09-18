using Content.Client.Lobby.UI;
using Content.Client.LateJoin;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Lobby;

[TestFixture]
public sealed class LobbyRoleTabsTest
{
    [Test]
    public async Task LobbyHasDedicatedHuntTabAndThreatDepartmentIsNotColonist()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var lobby = new LobbyGui();
            try
            {
                Assert.That(lobby.FindControl<Button>("JoinHuntButton"), Is.Not.Null);

                var prototypes = client.ResolveDependency<IPrototypeManager>();
                var threat = prototypes.Index<DepartmentPrototype>("AU14DepartmentThreat");
                Assert.That(threat.Faction, Is.EqualTo("hunt"));
                Assert.That(LateJoinGui.DepartmentMatchesFilter(threat, "colonists"), Is.False);
                Assert.That(LateJoinGui.DepartmentMatchesFilter(threat, "hunt"), Is.True);
            }
            finally
            {
                lobby.Dispose();
            }
        });

        await pair.CleanReturnAsync();
    }
}
