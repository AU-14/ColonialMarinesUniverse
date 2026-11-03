using Content.Client.CMU14.VoiceChat;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.VoiceChat;

[TestFixture]
public sealed class CMUVoiceChatIntroTest
{
    [Test]
    public async Task IntroWindowBuildsWithItsTextAndKeys()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            // Builds every markup string; a broken Loc string or markup throws here.
            var window = new CMUVoiceChatIntroWindow();
            window.OpenCentered();
            Assert.That(window.IsOpen, Is.True);
            Assert.That(window.DontShowAgain, Is.False);
            window.Close();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IntroShowsOncePerConnectionWhenEnabled()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        var cfg = client.ResolveDependency<IConfigurationManager>();
        var ui = client.ResolveDependency<IUserInterfaceManager>();

        await client.WaitAssertion(() =>
        {
            var controller = ui.GetUIController<CMUVoiceChatIntroUIController>();

            // Test clients start with the intro turned off.
            controller.TryShow();
            Assert.That(ui.WindowRoot.Children.OfType<CMUVoiceChatIntroWindow>(), Is.Empty);

            cfg.SetCVar(CCVars.CMUVoiceChatShowIntro, true);
            controller.TryShow();
            var windows = ui.WindowRoot.Children.OfType<CMUVoiceChatIntroWindow>().ToList();
            Assert.That(windows, Has.Count.EqualTo(1), "The intro opens on joining when voice chat is on.");
            windows[0].Close();

            controller.TryShow();
            Assert.That(ui.WindowRoot.Children.OfType<CMUVoiceChatIntroWindow>(), Is.Empty,
                "The intro shows only once per connection.");

            cfg.SetCVar(CCVars.CMUVoiceChatShowIntro, false);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeadsetWindowAndSpeakerListBuild()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<Robust.Shared.Prototypes.IPrototypeManager>();
            var channel = prototypes.Index<Content.Shared.Radio.RadioChannelPrototype>("MarineCommand");

            var window = new CMUVoiceRadioWindow();
            window.Populate(EntityUid.Invalid, "headset",
                new[] { new CMUVoiceRadioWindow.ChannelRow(channel, true, true, true) }, "J", "Shift+J");
            window.OpenCentered();
            Assert.That(window.IsOpen, Is.True);
            window.Close();

            // Your own row appears while you talk, and the list empties cleanly.
            var voice = client.System<CMUVoiceChatSystem>();
            voice.Self = (7, 0.5f, 1f, Content.Shared.CMU14.VoiceChat.CMUVoiceSelfFlags.Radio);
            var list = new CMUVoiceSpeakerList();
            list.Update(0.016f, voice);
            Assert.That(list.ChildCount, Is.EqualTo(1));
            Assert.That(list.GetChild(0).ChildCount, Is.EqualTo(1), "The talking player's own row is shown.");
            list.Clear();
            Assert.That(list.GetChild(0).ChildCount, Is.Zero);
            voice.Self = default;
        });

        await pair.CleanReturnAsync();
    }
}
