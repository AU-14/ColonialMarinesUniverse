using Content.Client.UserInterface.Systems.Chat;
using NUnit.Framework;
using Robust.Shared.Utility;

namespace Content.Tests.Client.UserInterface.Systems.Chat;

[TestFixture]
[TestOf(typeof(ChatMarkupParser))]
public sealed class ChatMarkupParserTest
{
    [Test]
    public void ValidMarkupUsesStrictParser()
    {
        var message = new FormattedMessage();

        var error = ChatMarkupParser.AddMarkup(message, "[bold]Valid message.[/bold]");

        Assert.That(error, Is.Null);
        Assert.That(message.ToString(), Is.EqualTo("Valid message."));
    }

    [Test]
    public void InvalidMarkupFallsBackWithoutThrowing()
    {
        var message = new FormattedMessage();
        const string markup = "[bold invalid.attribute]Visible message.[/bold]";

        string? error = null;
        Assert.DoesNotThrow(() => error = ChatMarkupParser.AddMarkup(message, markup));

        Assert.That(error, Is.Not.Null);
        Assert.That(message.ToString(), Does.Contain("Visible message."));
    }
}
