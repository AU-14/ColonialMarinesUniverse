using Content.Client.UserInterface.Systems.Ghost.Controls.Roles;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Roles;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class GhostRoleRulesWindowTest
{
    [Test]
    public async Task PositiveGhostRoleTimeDoesNotDisableRequestButton()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });

        await pair.Server.WaitPost(() => pair.Server.CfgMan.SetCVar(CCVars.GhostRoleTime, 3f));

        await pair.Client.WaitAssertion(() =>
        {
            var window = new GhostRoleRulesWindow("rules", GhostRoleKind.RaffleReady, _ => { });

            try
            {
                var requestButton = window.FindControl<Button>("RequestButton");
                Assert.That(requestButton.Disabled, Is.False);
            }
            finally
            {
                window.DisposeAllChildren();
            }
        });

        await pair.CleanReturnAsync();
    }
}
