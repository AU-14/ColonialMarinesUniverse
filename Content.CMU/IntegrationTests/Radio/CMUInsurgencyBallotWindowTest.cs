using Content.Client.CMU14.Insurgency.Selection;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CMU14.Insurgency.Selection;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.CMU14.Radio;

public sealed class CMUInsurgencyBallotWindowTest : InteractionTest
{
    [Test]
    public async Task KitStaysInsideDetailPaneAndVoteRemainsReachable()
    {
        InsurgencyFactionSelectWindow window = null!;
        await Client.WaitPost(() =>
        {
            window = new InsurgencyFactionSelectWindow();
            window.SetState(new InsurgencyFactionSelectEuiState([
                new DefaultFactionOption(1, "A faction with a long name", new string('a', 250), "Briefing", null, null,
                    ["Crowbar", "Wrench"], true),
                new DefaultFactionOption(2, "Unavailable faction", "Unavailable", "", null, null, [], false),
            ], "GOVFOR", TimeSpan.FromDays(1)));
            window.SetSize = window.MinSize;
            window.OpenCentered();
            window.Measure(window.SetSize);
            window.Arrange(UIBox2.FromDimensions(window.Position, window.SetSize));
        });
        await RunTicks(1);
        try
        {
            var vote = window.FindControl<Button>("Vote");
            var kit = window.FindControl<Button>("KitTab");
            await ClickControl(kit);
            await Client.WaitAssertion(() =>
            {
                window.Measure(window.SetSize);
                window.Arrange(UIBox2.FromDimensions(window.Position, window.SetSize));
                var scroll = window.FindControl<ScrollContainer>("DetailScroll");
                var body = window.FindControl<BoxContainer>("DetailBody");
                var grid = body.Children.OfType<WrapContainer>().Single();
                Assert.That(grid.ChildCount, Is.EqualTo(2));
                Assert.That(grid.GetChild(0).Size, Is.EqualTo(new Vector2(44, 44)));
                Assert.That(scroll.GlobalPosition.X, Is.GreaterThanOrEqualTo(window.FindControl<PanelContainer>("Navigation").GlobalPosition.X +
                    window.FindControl<PanelContainer>("Navigation").Width));
                Assert.That(vote.GlobalPosition.Y, Is.GreaterThanOrEqualTo(scroll.GlobalPosition.Y + scroll.Height));
                Assert.That(vote.Disabled, Is.False);
            });
            await ClickControl(window.FindControl<BoxContainer>("FactionList").Children.OfType<ContainerButton>().Last());
            Assert.That(vote.Disabled, Is.True);
        }
        finally
        {
            await Client.WaitPost(window.Close);
        }
    }
}
