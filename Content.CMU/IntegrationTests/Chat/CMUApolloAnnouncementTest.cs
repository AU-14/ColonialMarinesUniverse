using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RMC14.Language.Systems;
using Content.Server._RMC14.Marines;
using Content.Server.CMU14.Chat;
using Content.Server.CMU14.Radio;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Marines;
using Content.Shared.Chat;
using Content.Shared.CMU14.util;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Radio.Components;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Chat;

[TestFixture]
public sealed class CMUApolloAnnouncementTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [SidedDependency(Side.Client)]
    private readonly IUserInterfaceManager _ui = null!;

    private static readonly ProtoId<PlatoonPrototype> GovforPlatoon = "UPP";
    private static readonly ProtoId<PlatoonPrototype> OpforPlatoon = "LACN";

    [Test]
    public async Task BroadcastsAndRadioUseTheAnnouncingFactionLanguage()
    {
        const string message = "Priority orders received.";
        var map = await Pair.CreateTestMap();
        var original = ServerSession!.AttachedEntity;
        var controller = _ui.GetUIController<ChatUIController>();
        var heard = new List<ChatMessage>();
        void OnMessage(ChatMessage chat)
        {
            if (chat.Channel == ChatChannel.Radio && chat.WrappedMessage.Contains("APOLLO"))
                heard.Add(chat);
        }

        await Client.WaitPost(() => controller.MessageAdded += OnMessage);
        PlatoonPrototype? originalGovfor = null;
        PlatoonPrototype? originalOpfor = null;
        try
        {
            EntityUid listener = default;
            EntityUid core = default;
            await Server.WaitAssertion(() =>
            {
                var platoons = Server.System<PlatoonSpawnRuleSystem>();
                originalGovfor = platoons.SelectedGovforPlatoon;
                originalOpfor = platoons.SelectedOpforPlatoon;
                platoons.SelectedGovforPlatoon = Server.ProtoMan.Index(GovforPlatoon);
                platoons.SelectedOpforPlatoon = Server.ProtoMan.Index(OpforPlatoon);
                Assert.That(Server.System<AuRoundSystem>().SetPlanet("AUPlanetSorokyne"), Is.True);

                listener = SEntMan.SpawnAtPosition("CMMobHuman", map.GridCoords);
                Server.System<LanguageSystem>().SetExclusiveLanguage(listener, "Mandarin");
                SEntMan.EnsureComponent<MarineComponent>(listener).Faction = "govfor";
                SEntMan.EnsureComponent<IntrinsicRadioReceiverComponent>(listener);
                SEntMan.EnsureComponent<ActiveRadioComponent>(listener).Channels.Add("radioOpforCommand");
                core = SEntMan.SpawnAtPosition("AU14AICoreApolloGOVFOR", map.GridCoords);
                SEntMan.EnsureComponent<CMUClearRadioTransmissionComponent>(core);
                Server.PlayerMan.SetAttachedEntity(ServerSession!, listener);
            });
            await Pair.RunUntilSynced();

            await Server.WaitAssertion(() =>
            {
                var announce = Server.System<MarineAnnounceSystem>();
                announce.AnnounceARES(core, message, faction: "opfor"); // Outside the audience.
                announce.AnnounceARES(core, message); // GOVFOR hears Mandarin.
                SEntMan.GetComponent<MarineComponent>(listener).Faction = "opfor";
                announce.AnnounceARES(core, message); // Same broadcast on the OPFOR feed.
                var apollo = Server.System<CMUApolloAnnouncementSystem>();
                var overlay = apollo.OverlayLines(listener, [$"[bold]{message}[/bold]"], core);
                Assert.That(overlay[0], Does.StartWith("[bold]").And.EndWith("[/bold]").And.Not.Contain(message));
                apollo.AnnounceColony(message, "APOLLO",
                    new SoundPathSpecifier("/Audio/Announcements/announce.ogg"));
                announce.AnnounceRadio(core, message, "radioOpforCommand");
            });
            await Pair.RunTicksSync(5);

            await Client.WaitAssertion(() =>
            {
                Assert.That(heard, Has.Count.EqualTo(4));
                Assert.That(heard[0].LanguageIcon, Is.EqualTo("Mandarin"));
                Assert.That(heard[0].Message, Is.EqualTo(message));
                Assert.That(heard[1].LanguageIcon, Is.EqualTo("Portuguese"));
                Assert.That(heard[1].Message, Is.Not.EqualTo(message));
                Assert.That(heard[1].WrappedMessage, Does.Not.Contain(message));
                Assert.That(heard[2].LanguageIcon, Is.EqualTo("Mandarin"));
                Assert.That(heard[2].Message, Is.EqualTo(message));
                Assert.That(heard[3].LanguageIcon, Is.EqualTo("Portuguese"));
                Assert.That(heard[3].WrappedMessage, Does.Not.Contain(message));
            });

            await Server.WaitPost(() =>
            {
                var xeno = SEntMan.SpawnAtPosition("CMXenoDrone", map.GridCoords);
                Server.System<LanguageSystem>().SetExclusiveLanguage(xeno, "Xeno");
                Server.PlayerMan.SetAttachedEntity(ServerSession!, xeno);
                Server.System<CMUApolloAnnouncementSystem>().AnnounceColony(message, "APOLLO", null);
            });
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                Assert.That(heard, Has.Count.EqualTo(5));
                Assert.That(heard[4].Message, Is.Not.EqualTo(message));
                Assert.That(heard[4].WrappedMessage, Does.Not.Contain(message));
            });

            await Server.WaitPost(() =>
            {
                SEntMan.EnsureComponent<ThreatComponent>(listener);
                Server.System<LanguageSystem>().SetExclusiveLanguage(listener, "Xeno");
                Server.PlayerMan.SetAttachedEntity(ServerSession!, listener);
                Server.System<CMUApolloAnnouncementSystem>().AnnounceColony(message, "APOLLO", null);
            });
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                Assert.That(heard, Has.Count.EqualTo(6));
                Assert.That(heard[5].Message, Is.Not.EqualTo(message));
                Assert.That(heard[5].WrappedMessage, Does.Not.Contain(message));
            });

            EntityUid yautja = default;
            await Server.WaitPost(() =>
            {
                yautja = SEntMan.SpawnAtPosition("CMMobHuman", map.GridCoords);
                SEntMan.EnsureComponent<YautjaComponent>(yautja);
                Server.System<LanguageSystem>().SetExclusiveLanguage(yautja, "Xeno");
                Server.PlayerMan.SetAttachedEntity(ServerSession!, yautja);
                Server.System<CMUApolloAnnouncementSystem>().AnnounceColony(message, "APOLLO", null);
            });
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                Assert.That(heard, Has.Count.EqualTo(7));
                Assert.That(heard[6].Message, Is.Not.EqualTo(message));
                Assert.That(heard[6].WrappedMessage, Does.Not.Contain(message));
            });

            await Server.WaitPost(() =>
            {
                Server.System<LanguageSystem>().SetExclusiveLanguage(yautja, "Mandarin");
                Server.System<CMUApolloAnnouncementSystem>().AnnounceColony(message, "APOLLO", null);
            });
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                Assert.That(heard, Has.Count.EqualTo(8));
                Assert.That(heard[7].Message, Is.EqualTo(message));
            });
        }
        finally
        {
            await Client.WaitPost(() => controller.MessageAdded -= OnMessage);
            await Server.WaitPost(() =>
            {
                // Pooled pairs keep the platoon selection across round restarts.
                var platoons = Server.System<PlatoonSpawnRuleSystem>();
                platoons.SelectedGovforPlatoon = originalGovfor;
                platoons.SelectedOpforPlatoon = originalOpfor;
                Server.PlayerMan.SetAttachedEntity(ServerSession!, original);
            });
        }
    }
}
