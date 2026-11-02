using Content.Server.CMU14.Radio;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Callsigns;
using Content.Shared.CMU14.Radio;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUScoutReportsTest
{
    [TestCase("GOVFOR", "radioGovforJTAC", "radioGovforCommand")]
    [TestCase("OPFOR", "radioOpforJTAC", "radioOpforCommand")]
    public async Task RadioReportsUseCallsignAndBothFactionNetsAndObeyRange(string faction, string jtac, string command)
    {
        await Scope();
        await Server.WaitAssertion(() =>
        {
            var recorder = SEntMan.System<CMUScoutRadioRecorderSystem>();
            recorder.Sent.Clear();
            recorder.Received.Clear();
            SComp<MarineComponent>(_actor).Faction = faction;
            var callsign = SEntMan.EnsureComponent<AU14CallsignComponent>(_actor);
            callsign.Faction = faction.ToLowerInvariant();
            callsign.Callsign = "PROPHET 2-1";
            SEntMan.EnsureComponent<RMCPlanetComponent>(_map).Offset = new Vector2i(100, 200);

            var relay = SEntMan.SpawnEntity(null, Target);
            var anchor = SEntMan.AddComponent<ANPRCRelayAnchorComponent>(relay);
            anchor.Channels.UnionWith([jtac, command]);
            SEntMan.System<ANPRCRangeSystem>().RefreshAnchorChannels((relay, anchor));
            var friendly = SEntMan.SpawnEntity(null, Target);
            SEntMan.AddComponent<CMUScoutRadioReceiverComponent>(friendly);
            SEntMan.AddComponent<ActiveRadioComponent>(friendly).Channels.UnionWith([jtac, command]);
            var enemy = SEntMan.SpawnEntity(null, Target);
            SEntMan.AddComponent<CMUScoutRadioReceiverComponent>(enemy);
            SEntMan.AddComponent<ActiveRadioComponent>(enemy).Channels.UnionWith(
                faction == "GOVFOR" ? ["radioOpforJTAC", "radioOpforCommand"] : ["radioGovforJTAC", "radioGovforCommand"]);

            Assert.That(Report(), Is.True);
            Assert.That(recorder.Sent.Select(message => message.Channel.ID), Is.EquivalentTo(new[] { jtac, command }));
            Assert.That(recorder.Received, Has.Count.EqualTo(2));
            foreach (var (receiver, message) in recorder.Received)
            {
                Assert.That(receiver, Is.EqualTo(friendly));
                Assert.That(message.MessageSource, Is.EqualTo(_actor));
                Assert.That(message.ChatMsg.Message.Display.SenderName, Is.EqualTo(callsign.Callsign));
                Assert.That(message.Message, Does.StartWith("All stations, this is PROPHET 2-1. Contact report: Vehicle"));
                Assert.That(message.Message, Does.Contain("grid 103, 200").And.EndsWith("Over."));
                Assert.That(message.ChatMsg.Message.WrappedMessage, Does.Not.Contain(SComp<MetaDataComponent>(_actor).EntityName));
            }
            Assert.That(Report(), Is.False);
            Assert.That(recorder.Sent, Has.Count.EqualTo(2), "Rejected reports cannot create radio spam.");

            SEntMan.DeleteEntity(relay);
            recorder.Sent.Clear();
            recorder.Received.Clear();
            Reporter.NextReport = SGameTiming.CurTime;
            Assert.That(Report(), Is.True, "Radio failure must not block the tactical-map mark.");
            Assert.That(Reporter.Reports, Has.Count.EqualTo(2));
            Assert.That(recorder.Sent, Has.Count.EqualTo(2));
            Assert.That(recorder.Sent.All(message => message.Cancelled), Is.True);
            Assert.That(recorder.Received, Is.Empty, "No relay coverage means no radio delivery.");
        });
    }

    [Test]
    public async Task JammingBlocksRadioButKeepsTheMapReport()
    {
        await Scope();
        await Server.WaitAssertion(() =>
        {
            var recorder = SEntMan.System<CMUScoutRadioRecorderSystem>();
            recorder.Sent.Clear();
            var callsign = SEntMan.EnsureComponent<AU14CallsignComponent>(_actor);
            callsign.Faction = "govfor";
            callsign.Callsign = "PROPHET 2-1";
            var relay = SEntMan.SpawnEntity(null, Target);
            var anchor = SEntMan.AddComponent<ANPRCRelayAnchorComponent>(relay);
            anchor.Channels.UnionWith(["radioGovforJTAC", "radioGovforCommand"]);
            SEntMan.System<ANPRCRangeSystem>().RefreshAnchorChannels((relay, anchor));
            var jammer = SEntMan.SpawnEntity("AU14RadioJammer", Target);
            Assert.That(SEntMan.System<ItemToggleSystem>().TryActivate(jammer), Is.True);
            Assert.That(SEntMan.HasComponent<ActiveRadioJammerComponent>(jammer), Is.True);
            Assert.That(Report(), Is.True);
            Assert.That(Reporter.Reports, Has.Count.EqualTo(1));
            Assert.That(recorder.Sent, Has.Count.EqualTo(2));
            Assert.That(recorder.Sent.All(message => message.Cancelled), Is.True);
        });
    }

    [Test]
    public async Task MissingOrWrongFactionCallsignNeverFallsBackToPersonalName()
    {
        await Scope();
        await Server.WaitAssertion(() =>
        {
            var recorder = SEntMan.System<CMUScoutRadioRecorderSystem>();
            recorder.Sent.Clear();
            Assert.That(Report(), Is.True);
            Assert.That(recorder.Sent, Is.Empty);
            var callsign = SEntMan.EnsureComponent<AU14CallsignComponent>(_actor);
            callsign.Callsign = "VICTOR 1";
            callsign.Faction = "opfor";
            Reporter.NextReport = SGameTiming.CurTime;
            Assert.That(Report(), Is.True);
            Assert.That(recorder.Sent, Is.Empty);
        });
    }

    [RegisterComponent]
    private sealed partial class CMUScoutRadioReceiverComponent : Component;

    private sealed partial class CMUScoutRadioRecorderSystem : EntitySystem
    {
        public readonly List<RadioSendAttemptEvent> Sent = new();
        public readonly List<(EntityUid Receiver, RadioReceiveEvent Message)> Received = new();

        public override void Initialize()
        {
            SubscribeLocalEvent<RadioSendAttemptEvent>(OnSend, after: [typeof(ANPRCRangeSystem)]);
            SubscribeLocalEvent<CMUScoutRadioReceiverComponent, RadioReceiveEvent>(OnReceive);
        }

        private void OnSend(ref RadioSendAttemptEvent ev) => Sent.Add(ev);

        private void OnReceive(Entity<CMUScoutRadioReceiverComponent> ent, ref RadioReceiveEvent ev) =>
            Received.Add((ent, ev));
    }
}
