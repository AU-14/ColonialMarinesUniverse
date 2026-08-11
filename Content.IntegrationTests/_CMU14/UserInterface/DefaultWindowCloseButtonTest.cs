#nullable enable
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.IntegrationTests._CMU14.UserInterface;

[TestFixture]
[TestOf(typeof(DefaultWindow))]
public sealed class DefaultWindowCloseButtonTest : GameTest
{
    private static readonly FieldInfo OnPressedField = typeof(BaseButton).GetField(
        nameof(BaseButton.OnPressed),
        BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Test]
    public async Task CloseButtonWorksAfterReopening()
    {
        await Client.WaitAssertion(() =>
        {
            var window = new TestWindow();

            window.Open();
            Press(window.TestCloseButton);
            Assert.That(window.IsOpen, Is.False, "The close button did not close the window the first time.");

            window.Open();
            Press(window.TestCloseButton);
            Assert.That(window.IsOpen, Is.False, "The close button did not close the reopened window.");
        });
    }

    private static void Press(BaseButton button)
    {
        var handlers = (Action<BaseButton.ButtonEventArgs>?) OnPressedField.GetValue(button);
        handlers?.Invoke(null!);
    }

    private sealed class TestWindow : DefaultWindow
    {
        public BaseButton TestCloseButton => CloseButton;
    }
}
