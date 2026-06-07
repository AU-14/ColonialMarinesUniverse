using System.Collections.Generic;
using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Moq;
using NUnit.Framework;
using Robust.Shared.Configuration;

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

    [Test]
    public void ResolveMarkupFontSizeUsesExistingRadioFontSize()
    {
        var markup = "[color=#E][font=Default size=15]\\[Command\\] [bold]Bob[/bold] says, \"Alice\"[/font][/color]";

        var result = ChatUserSettings.ResolveMarkupFontSize(markup);

        Assert.That(result, Is.EqualTo(15));
    }

    [Test]
    public void AutoHighlightMatchesRadioMessageBody()
    {
        var controller = new ChatUIController();
        var config = new Mock<IConfigurationManager>();
        SetPrivateField(controller, "_config", config.Object);

        controller.UpdateHighlights("@Alice", true);
        var highlights = GetPrivateField<List<string>>(controller, "_highlights");

        var message = new ChatMessage(
            ChatChannel.Radio,
            "Alice",
            "[color=#E][font=Default size=15]\\[Command\\] [bold]Bob[/bold] says, \"Alice\"[/font][/color]",
            default,
            null);

        message.WrappedMessage = SharedChatSystem.InjectTagAroundString(
            message,
            highlights.Single(),
            "color",
            "#17FFC1FF",
            targetIsRegex: true);

        Assert.That(message.WrappedMessage, Does.Contain("\"[color=#17FFC1FF]Alice[/color]\""));
        Assert.That(message.WrappedMessage, Does.Contain("[bold]Bob[/bold]"));
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field!.SetValue(instance, value);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T) field!.GetValue(instance)!;
    }
}
