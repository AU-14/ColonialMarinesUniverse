using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.Tests.Client.UserInterface.Systems.Chat;

[TestFixture]
[TestOf(typeof(ChatMessageRow))]
public sealed class ChatMessageRowTest : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;

    [OneTimeSetUp]
    public void Setup()
    {
        IoCManager.Resolve<IUserInterfaceManager>().InitializeTesting();
    }

    [TestCase("Players: [cmdlink=\"Trevor Muggins\" command=\"tpto 428881\"/]", "Trevor Muggins")]
    [TestCase("Coords: [cmdlink=\"96885,272.42,-113.49\" command=\"tp 272.42 -113.49 96885\"/]", "96885,272.42,-113.49")]
    public void AdminAlertCommandLinksCreateClickableLabels(string markup, string linkText)
    {
        var message = new FormattedMessage();
        var error = ChatMarkupParser.AddMarkup(message, markup);
        var label = new RichTextLabel();

        label.SetMessage(message, ChatMessageRow.AllowedMarkupTags);

        var commandLink = label.Controls.OfType<Label>().SingleOrDefault();
        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Null);
            Assert.That(commandLink, Is.Not.Null);
            Assert.That(commandLink?.Text, Is.EqualTo(linkText));
            Assert.That(commandLink?.DefaultCursorShape, Is.EqualTo(Control.CursorShape.Hand));
            Assert.That(commandLink?.MouseFilter, Is.EqualTo(Control.MouseFilterMode.Stop));
        });
    }
}
