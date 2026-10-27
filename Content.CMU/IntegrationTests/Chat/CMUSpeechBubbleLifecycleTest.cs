using System.Collections;
using System.Reflection;
using System.Text;
using Content.Client.Chat.UI;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Chat;

[TestFixture]
public sealed class CMUSpeechBubbleLifecycleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(true)]
    [TestCase(false)]
    public async Task EmptyQueueStillUpdatesVisibilityAndExpiresHiddenBubbles(bool runechat)
    {
        await WithScene(runechat, (chat, eye, sender, root) =>
        {
            Enqueue(chat, sender, "Hold here.");
            chat.FrameUpdate(new FrameEventArgs(0));
            var bubble = (SpeechBubble) root.GetChild(0);
            Deadline(bubble, TimeSpan.FromMinutes(1));
            chat.FrameUpdate(new FrameEventArgs(60));
            Assert.That(Queues(chat).Count, Is.Zero);

            var origin = eye.Position;
            eye.Position = MapCoordinates.Nullspace;
            chat.FrameUpdate(new FrameEventArgs(0));
            Assert.That(bubble.Visible, Is.False, "Visibility must keep updating after the last queue expires.");
            eye.Position = origin;
            chat.FrameUpdate(new FrameEventArgs(0));
            Assert.That(bubble.Visible, Is.True, "Returning to the map must restore an unexpired bubble.");

            eye.Position = MapCoordinates.Nullspace;
            chat.FrameUpdate(new FrameEventArgs(0));
            Deadline(bubble, TimeSpan.FromSeconds(-1));
            chat.FrameUpdate(new FrameEventArgs(0));
            Assert.Multiple(() =>
            {
                Assert.That(root.ChildCount, Is.Zero, "Invisible expired controls must leave the UI tree.");
                Assert.That(Active(chat).Count, Is.Zero, "The controller must release its last reference to the speaker's bubbles.");
                Assert.That(bubble.Parent, Is.Null);
            });
        });
    }

    [Test]
    public async Task QueuedMessagesKeepOrderAndDelayWhileDeletedSendersAreRemoved()
    {
        await WithScene(true, (chat, eye, sender, root) =>
        {
            var other = CEntMan.SpawnEntity(null, CEntMan.GetComponent<TransformComponent>(sender).Coordinates);
            Enqueue(chat, sender, "First.");
            Enqueue(chat, sender, "Second.");
            Enqueue(chat, other, "Other.");
            chat.FrameUpdate(new FrameEventArgs(0));
            var first = Active(chat)[sender].Single();
            Deadline(first, TimeSpan.FromMinutes(1));
            Assert.That(BubbleText(first), Is.EqualTo("First."));
            chat.FrameUpdate(new FrameEventArgs(0));
            Assert.That(Active(chat)[sender], Has.Count.EqualTo(1), "A second frame must not bypass the inter-message delay.");

            CEntMan.DeleteEntity(other);
            chat.FrameUpdate(new FrameEventArgs(60));
            Assert.Multiple(() =>
            {
                Assert.That(Active(chat).ContainsKey(other), Is.False);
                Assert.That(Queues(chat).Contains(other), Is.False);
                Assert.That(Active(chat)[sender], Has.Count.EqualTo(2));
                Assert.That(Active(chat)[sender][0], Is.SameAs(first));
                Assert.That(BubbleText(Active(chat)[sender][1]), Is.EqualTo("Second."));
                Assert.That(root.ChildCount, Is.EqualTo(2));
            });
        });
    }

    [Test]
    public async Task ScreenUnloadReleasesActiveAndQueuedBubblesAndAllowsNewRoot()
    {
        await WithScene(true, (chat, eye, sender, root) =>
        {
            Enqueue(chat, sender, "Visible.");
            Enqueue(chat, sender, "Waiting.");
            chat.FrameUpdate(new FrameEventArgs(0));
            Assert.That(root.ChildCount, Is.EqualTo(1));
            Assert.That(Queues(chat).Count, Is.EqualTo(1));

            chat.OnScreenUnload();
            Assert.Multiple(() =>
            {
                Assert.That(root.ChildCount, Is.Zero);
                Assert.That(Active(chat).Count, Is.Zero);
                Assert.That(Queues(chat).Count, Is.Zero);
                Assert.That(root.Parent, Is.Null);
            });

            using var replacement = new LayoutContainer();
            chat.SetSpeechBubbleRoot(replacement);
            Enqueue(chat, sender, "New screen.");
            chat.FrameUpdate(new FrameEventArgs(0));
            Assert.That(root.ChildCount, Is.EqualTo(1));
            Assert.That(BubbleText((SpeechBubble) root.GetChild(0)), Is.EqualTo("New screen."));
            chat.OnScreenUnload();
        });
    }

    private async Task WithScene(bool runechat, Action<ChatUIController, FixedEye, EntityUid, Control> action)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var player = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, player);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var chat = ui.GetUIController<ChatUIController>();
            var eyeManager = Client.ResolveDependency<IEyeManager>();
            var oldEye = eyeManager.CurrentEye;
            var player = Client.Session!.AttachedEntity!.Value;
            var eye = new FixedEye { Position = Client.System<SharedTransformSystem>().GetMapCoordinates(player) };
            eyeManager.CurrentEye = eye;
            Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.ChatEnableRunechatBubbles, runechat);
            var sender = CEntMan.SpawnEntity(null, CEntMan.GetComponent<TransformComponent>(player).Coordinates);
            var root = (Control) Field(chat, "_speechBubbleRoot");
            var oldParent = root.Parent;
            using var host = new LayoutContainer();
            ui.WindowRoot.AddChild(host);
            chat.SetSpeechBubbleRoot(host);
            try
            {
                action(chat, eye, sender, root);
            }
            finally
            {
                chat.OnScreenUnload();
                if (oldParent != null) oldParent.AddChild(root);
                eyeManager.CurrentEye = oldEye;
                CEntMan.DeleteEntity(sender);
            }
        });
    }

    private void Deadline(SpeechBubble bubble, TimeSpan offset) => typeof(SpeechBubble)
        .GetField("_deathTime", Private)!.SetValue(bubble, Client.ResolveDependency<IGameTiming>().RealTime + offset);

    private static void Enqueue(ChatUIController chat, EntityUid sender, string text) => typeof(ChatUIController)
        .GetMethod("EnqueueSpeechBubble", Private)!.Invoke(chat,
            [sender, new ChatMessage(ChatChannel.Local, text, $"[BubbleContent]{text}[/BubbleContent]", NetEntity.Invalid, null), SpeechBubble.SpeechType.Say]);

    private static Dictionary<EntityUid, List<SpeechBubble>> Active(ChatUIController chat) =>
        (Dictionary<EntityUid, List<SpeechBubble>>) Field(chat, "_activeSpeechBubbles");

    private static IDictionary Queues(ChatUIController chat) => (IDictionary) Field(chat, "_queuedSpeechBubbles");
    private static object Field(object target, string name) => target.GetType().GetField(name, Private)!.GetValue(target)!;

    private static string BubbleText(SpeechBubble bubble)
    {
        var text = new StringBuilder();
        foreach (IEnumerable page in (IEnumerable) Field(bubble.GetChild(0), "_pages"))
        foreach (var run in page)
            text.Append(run.GetType().GetProperty("Text")!.GetValue(run));
        return text.ToString();
    }
}
