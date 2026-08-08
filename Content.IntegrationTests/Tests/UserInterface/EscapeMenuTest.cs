using System.Collections.Generic;
using Content.Client.Lobby.UI;
using Content.Client.Options.UI;
using Content.Client.Voting.UI;
using Content.IntegrationTests.Fixtures;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Console;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class EscapeMenuTest : GameTest
{
    [Test]
    public async Task MatchesCMULayoutAndOmitsCallVote()
    {
        await Client.WaitAssertion(() =>
        {
            var menu = new EscapeMenu();
            var controls = Descendants(menu).ToArray();
            var lobby = new LobbyGui();
            var lobbyControls = Descendants(lobby).ToArray();
            var console = Client.ResolveDependency<IConsoleHost>();

            menu.SetRoundStatus("LV-624", "USS Almayer", 42, 17, "Distress Signal", true);
            menu.SetRoundTime(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(5), true);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(controls.OfType<PanelContainer>().Any(control => control.Name == "RoundStatusPanel"), Is.True);
                    Assert.That(controls.OfType<Button>().Any(control => control.Name == "CreditsButton"), Is.True);
                    Assert.That(controls.OfType<Button>().Any(control => control.Name == "PatronPerksButton"), Is.True);
                    Assert.That(controls, Has.None.InstanceOf<VoteCallMenuButton>());
                    Assert.That(lobbyControls, Has.None.InstanceOf<VoteCallMenuButton>());
                    Assert.That(console.AvailableCommands.ContainsKey("votemenu"), Is.False);
                    Assert.That(menu.MapValue.Text, Is.EqualTo("LV-624"));
                    Assert.That(menu.ShipMapValue.Text, Is.EqualTo("USS Almayer"));
                    Assert.That(menu.RoundValue.Text, Is.EqualTo("42"));
                    Assert.That(menu.PlayersValue.Text, Is.EqualTo("17"));
                    Assert.That(menu.GamemodeValue.Text, Is.EqualTo("Distress Signal"));
                    Assert.That(menu.RoundTimeValue.Text, Is.EqualTo("02:05"));
                });
            }
            finally
            {
                lobby.Dispose();
                menu.Dispose();
            }
        });
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
