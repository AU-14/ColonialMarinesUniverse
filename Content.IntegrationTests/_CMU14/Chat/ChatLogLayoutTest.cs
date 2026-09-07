using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Chat;

[TestFixture, NonParallelizable]
public sealed class ChatLogLayoutTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task LongHistoryOnlyKeepsViewportRowsInTheControlTree(bool crt)
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var update = ui.GetType().GetMethod("FrameUpdate")!.CreateDelegate<Action<FrameEventArgs>>(ui);
            Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CrtUiEnabled, crt);
            using var panel = new ChatLogPanel { SetSize = new Vector2(540, 400) };
            ui.WindowRoot.AddChild(panel);
            for (var i = 0; i < ChatLogPanel.MaxEntries; i++)
            {
                var text = $"Radio message {i}: A long transmission that wraps across multiple lines in the chat panel.";
                panel.AddMessage(new ChatMessage(ChatChannel.Radio, text, "", default, null),
                    FormattedMessage.FromUnformatted(text), Color.White);
            }

            for (var frame = 0; frame < 20; frame++)
                update(new FrameEventArgs(1f / 60f));

            Assert.That(panel.EntryCount, Is.EqualTo(ChatLogPanel.MaxEntries), "Keep the full scrollback history");
            Assert.That(Descendants(panel).OfType<ChatMessageRow>().Count(), Is.LessThan(120),
                "Offscreen history must not remain in the UI update/layout tree");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ScrollbackIsLazyAndPreservesRepeatsAndTheReaderWhenHistoryIsTrimmed(bool crt)
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var update = ui.GetType().GetMethod("FrameUpdate")!.CreateDelegate<Action<FrameEventArgs>>(ui);
            Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CrtUiEnabled, crt);
            using var panel = new ChatLogPanel { SetSize = new Vector2(540, 400) };
            ui.WindowRoot.AddChild(panel);
            var entries = new List<ChatLogEntry>();
            var formattedCount = 0;
            for (var i = 0; i < ChatLogPanel.MaxEntries; i++)
            {
                var text = $"Radio {i}: " + string.Concat(Enumerable.Repeat("A wrapping message. ", i % 7 + 1));
                entries.Add(panel.AddMessage(new ChatMessage(ChatChannel.Radio, text, "", default, null),
                    () =>
                    {
                        formattedCount++;
                        return FormattedMessage.FromUnformatted(text);
                    }, Color.White));
            }
            Assert.That(formattedCount, Is.Zero, "Repopulating a tab must not format its entire history");

            void Settle()
            {
                for (var frame = 0; frame < 20; frame++)
                    update(new FrameEventArgs(1f / 60f));
            }

            Settle();
            var scroll = Descendants(panel).OfType<ScrollContainer>().Single();
            var bar = Descendants(panel).OfType<VScrollBar>().Single(b => b.Parent != scroll);
            Assert.That(formattedCount, Is.LessThan(120));
            Assert.That(entries[^1].Row, Is.Not.Null, "The newest message must be visible initially");
            Assert.That(entries[0].Row, Is.Null);
            TestContext.Progress.WriteLine($"CRT={crt}: {panel.EntryCount} history entries, " +
                $"{Descendants(panel).OfType<ChatMessageRow>().Count()} active rows, {formattedCount} formatted messages");

            entries[0].SetRepeatCount(9);
            bar.Value = 0;
            Settle();
            Assert.That(entries[0].Row, Is.Not.Null, "The oldest retained message remains accessible");
            Assert.That(Descendants(entries[0].Row!).OfType<Label>().Any(l => l.Visible && l.Text == "x9"), Is.True,
                "An offscreen repeat count must survive materialization");
            Assert.That(entries[^1].Row, Is.Null);

            bar.Value = (bar.MaxValue - bar.Page) / 2;
            Settle();
            var anchor = entries.Where(e => e.Row != null && e.Row.Position.Y + e.Row.Height > scroll.VScroll)
                .MinBy(e => e.Row!.Position.Y)!;
            var previousY = anchor.Row!.Position.Y - scroll.VScroll;
            panel.AddMessage(new ChatMessage(ChatChannel.Radio, "new", "", default, null),
                FormattedMessage.FromUnformatted("new"), Color.White);
            Settle();
            Assert.That(panel.EntryCount, Is.EqualTo(ChatLogPanel.MaxEntries));
            Assert.That(anchor.Row, Is.Not.Null);
            Assert.That(anchor.Row!.Position.Y - scroll.VScroll, Is.EqualTo(previousY).Within(0.1f),
                "Dropping the oldest history entry must preserve the visible message and pixel offset");
            AssertRowsDoNotOverlap(panel, ChatLogPanel.MaxEntries);
            panel.ScrollToBottom();
            Settle();
            Assert.That(bar.IsAtEnd, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task WrappedHistoryLayoutSettlesAfterChanges(bool crt)
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var update = ui.GetType().GetMethod("FrameUpdate")!.CreateDelegate<Action<FrameEventArgs>>(ui);
            Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CrtUiEnabled, crt);
            using var panel = new ChatLogPanel { SetSize = new Vector2(540, 650) };
            ui.WindowRoot.AddChild(panel);
            var scroll = Descendants(panel).OfType<ScrollContainer>().Single();
            var scrollBar = Descendants(panel).OfType<VScrollBar>().Single(bar => bar.Parent != scroll);

            void AddMessages(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    var text = $"Marine {i} says, This is a representative radio message " +
                        "with enough text to wrap in the chat panel.";
                    panel.AddMessage(new ChatMessage(ChatChannel.Radio, text, "", default, null)
                        { GhostFollowEntity = new NetEntity(123) },
                        FormattedMessage.FromUnformatted(text), Color.White);
                }
            }

            void AssertSettled(int expectedCount)
            {
                // Allow startup and resize layout to settle. Once settled,
                // a width mismatch must not keep requeuing text measurement on every idle frame.
                for (var frame = 0; frame < 20; frame++)
                    update(new FrameEventArgs(1f / 60f));

                for (var frame = 0; frame < 3; frame++)
                {
                    update(new FrameEventArgs(1f / 60f));
                    Assert.That(scroll.IsMeasureValid, Is.True, "Chat must finish measuring between idle frames");
                    Assert.That(scroll.IsArrangeValid, Is.True);
                }

                Assert.That(panel.EntryCount, Is.EqualTo(expectedCount));
                Assert.That(scrollBar.Position.X, Is.EqualTo(scroll.Width).Within(0.01f),
                    "Scrollbar must not cover messages");
                Assert.That(scroll.Width + scrollBar.Width, Is.EqualTo(scroll.Parent!.Width).Within(0.01f));
                AssertRowsDoNotOverlap(panel, expectedCount);
            }

            AddMessages(500);
            AssertSettled(500);
            Assert.That(scrollBar.IsAtEnd, Is.True);

            // Showing the scroll-to-latest button changes the space available to the viewport.
            scrollBar.Value = (scrollBar.MaxValue - scrollBar.Page) / 2;
            AssertSettled(500);
            var previousScroll = scroll.VScroll;
            AddMessages(1);
            AssertSettled(501);
            Assert.That(scroll.VScroll, Is.EqualTo(previousScroll).Within(0.01f),
                "Incoming chat must preserve the reader's position");
            panel.ScrollToBottom();
            AssertSettled(501);
            Assert.That(scrollBar.IsAtEnd, Is.True);

            foreach (var size in new[] { new Vector2(300, 160), new Vector2(700, 320), new Vector2(540, 650) })
            {
                panel.SetSize = size;
                AssertSettled(501);
                Assert.That(scrollBar.IsAtEnd, Is.True);
            }

            // Tabs clear and rebuild the same log with a different selection from history.
            panel.Clear();
            AssertSettled(0);
            AddMessages(50);
            AssertSettled(50);
            Assert.That(scrollBar.IsAtEnd, Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task MessagesReceivedWhileChatIsDetachedAreLaidOutWhenItReturns(bool split, bool horizontal)
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var update = ui.GetType().GetMethod("FrameUpdate")!;
            var config = Client.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.ChatLegacyMode, false);
            config.SetCVar(CCVars.ChatSplitPane, ChatUserSettings.SaveSplitPane(
                split, ChatUserSettings.RadioTabId, ChatUserSettings.DefaultSplitSecondaryRatioPercent, horizontal));
            using var host = new Control { SetSize = new Vector2(300, 160) };
            // Theme/font settings give cached ChatBoxes an explicit sheet. Reattaching their screen
            // then skips the usual recursive restyle, which would otherwise refresh layout for us.
            var chat = new ChatBox { Stylesheet = ui.Stylesheet, MinSize = Vector2.Zero };
            var panel = chat.Contents;
            panel.Clear();
            chat.SecondaryContents.Clear();
            host.AddChild(chat);
            ui.WindowRoot.AddChild(host);

            void Settle()
            {
                for (var frame = 0; frame < 20; frame++)
                    update.Invoke(ui, [new FrameEventArgs(1f / 60f)]);
            }

            Settle();
            ui.WindowRoot.RemoveChild(host);
            for (var i = 0; i < 30; i++)
            {
                var text = $"Message {i}: This radio message arrives before the game screen opens.";
                panel.AddMessage(new ChatMessage(ChatChannel.Radio, text, "", default, null),
                    FormattedMessage.FromUnformatted(text), Color.White);
                if (split)
                {
                    chat.SecondaryContents.AddMessage(new ChatMessage(ChatChannel.Radio, text, "", default, null),
                        FormattedMessage.FromUnformatted(text), Color.White);
                }
            }

            // Drain layout queues while the screen is detached, as happens while joining a server.
            Settle();
            ui.WindowRoot.AddChild(host);
            Settle();
            AssertRowsDoNotOverlap(panel, 30);
            foreach (var scroll in Descendants(chat).OfType<ScrollContainer>())
            {
                if (!scroll.VisibleInTree)
                    continue;
                TestContext.Progress.WriteLine($"Split={split}, horizontal={horizontal}: {scroll.GetType().Name} " +
                    $"name={scroll.Name} parent={scroll.Parent?.Name} size={scroll.Size} " +
                    $"measure={scroll.IsMeasureValid} arrange={scroll.IsArrangeValid}");
                Assert.That(scroll.IsMeasureValid, Is.True, $"Split={split}, horizontal={horizontal} must settle");
                Assert.That(scroll.IsArrangeValid, Is.True);
            }
            if (split)
                AssertRowsDoNotOverlap(chat.SecondaryContents, 30);
        });
    }

    [Test]
    public async Task MessagesRemainSeparateAfterStartupAndResize()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var update = ui.GetType().GetMethod("FrameUpdate")!;
            using var panel = new ChatLogPanel { SetSize = Vector2.Zero };
            ui.WindowRoot.AddChild(panel);

            void Settle(int frames = 20)
            {
                for (var frame = 0; frame < frames; frame++)
                    update.Invoke(ui, [new FrameEventArgs(1f / 60f)]);
            }

            for (var i = 0; i < 100; i++)
            {
                var text = $"Message {i}: This is a radio message that wraps onto several lines in a narrow chat panel.";
                panel.AddMessage(new ChatMessage(ChatChannel.Radio, text, "", default, null),
                    FormattedMessage.FromUnformatted(text), Color.White);
            }

            Settle();
            foreach (var size in new[]
                     {
                         new Vector2(300, 160), new Vector2(160, 60), new Vector2(600, 400), new Vector2(300, 160),
                     })
            {
                panel.SetSize = size;
                Settle(1);
                AssertRowsDoNotOverlap(panel, 100);
            }
        });
    }

    private static void AssertRowsDoNotOverlap(ChatLogPanel panel, int expectedCount)
    {
        var rows = Descendants(panel).OfType<ChatMessageRow>().OrderBy(row => row.Position.Y).ToArray();
        Assert.That(panel.EntryCount, Is.EqualTo(expectedCount));
        Assert.That(rows.Length, Is.LessThanOrEqualTo(expectedCount));
        if (expectedCount > 0)
            Assert.That(rows, Is.Not.Empty);
        for (var i = 0; i < rows.Length; i++)
        {
            Assert.That(rows[i].Height, Is.GreaterThan(0), $"Row {i} must be measured at panel size {panel.Size}");
            if (i > 0)
            {
                Assert.That(rows[i].Position.Y,
                    Is.GreaterThanOrEqualTo(rows[i - 1].Position.Y + rows[i - 1].Height),
                    $"Row {i} overlaps its predecessor at panel size {panel.Size}");
            }
        }
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
