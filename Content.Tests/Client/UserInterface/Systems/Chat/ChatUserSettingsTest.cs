using Content.Client.UserInterface.Systems.Chat;
using NUnit.Framework;

namespace Content.Tests.Client.UserInterface.Systems.Chat;

[TestFixture]
[TestOf(typeof(ChatUserSettings))]
public sealed class ChatUserSettingsTest
{
    [Test]
    public void ApplyFontMarkupPreservesExistingFontSizeWithoutOverride()
    {
        var markup = "[font=Default size=15]radio message[/font]";

        var result = ChatUserSettings.ApplyFontMarkup(markup, null, ChatUserSettings.DefaultFontSize);

        Assert.That(result, Does.Contain("size=15"));
        Assert.That(result, Does.Not.Contain("size=12"));
    }

    [Test]
    public void ApplyFontMarkupUsesExplicitStyleFontSize()
    {
        var markup = "[font=Default size=15]radio message[/font]";
        var style = new ChatStyleSettings { FontSize = 14 };

        var result = ChatUserSettings.ApplyFontMarkup(markup, style, ChatUserSettings.DefaultFontSize);

        Assert.That(result, Does.Contain("size=14"));
        Assert.That(result, Does.Not.Contain("size=15"));
    }
}
