using Content.Client.CMU14.Lobby;
using Content.Client.LateJoin;
using Content.Shared.Roles;
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
            var lobby = new JoinRoundWindow();
            try
            {
                lobby.OpenCentered();
                Assert.That(lobby.JoinHuntButton.VisibleInTree, Is.False);
                lobby.SetHuntVisible(true);
                Assert.That(lobby.JoinHuntButton.VisibleInTree, Is.True);
                lobby.SetHuntVisible(false);
                Assert.That(lobby.JoinHuntButton.VisibleInTree, Is.False);

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
