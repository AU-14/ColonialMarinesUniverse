using System.Reflection;
using Content.Client.Voting;
using Content.Client.Voting.UI;
using Content.IntegrationTests.Fixtures;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests.Voting;

[TestFixture]
public sealed class VotePopupTest : GameTest
{
    [Test]
    public async Task HideAndShowButtonsTogglePopupContent()
    {
        await Client.WaitAssertion(() =>
        {
            var vote = new VoteManager.ActiveVote(1)
            {
                Entries = [],
                Title = "Test vote",
                Initiator = "Test user",
                StartTime = TimeSpan.Zero,
                EndTime = TimeSpan.FromMinutes(1),
            };
            var popup = new VotePopup(vote);
            var minimizeButton = popup.FindControl<Button>("MinimizeButton");
            var restoreButton = popup.FindControl<Button>("RestoreButton");
            var mainContent = popup.FindControl<BoxContainer>("MainContent");
            var minimizedContent = popup.FindControl<BoxContainer>("MinimizedContent");
            var minimizedTitle = popup.FindControl<Label>("MinimizedTitle");

            Press(minimizeButton);

            Assert.Multiple(() =>
            {
                Assert.That(mainContent.Visible, Is.False);
                Assert.That(minimizedContent.Visible, Is.True);
                Assert.That(minimizedTitle.Text, Is.EqualTo(vote.Title));
            });

            Press(restoreButton);

            Assert.Multiple(() =>
            {
                Assert.That(mainContent.Visible, Is.True);
                Assert.That(minimizedContent.Visible, Is.False);
            });
        });
    }

    private static void Press(BaseButton button)
    {
        var eventField = typeof(BaseButton).GetField(
            nameof(BaseButton.OnPressed),
            BindingFlags.Instance | BindingFlags.NonPublic);
        var subscribers = eventField?.GetValue(button) as Action<BaseButton.ButtonEventArgs>;

        Assert.That(eventField, Is.Not.Null);
        subscribers?.Invoke(null!);
    }
}
