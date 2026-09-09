#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.IntegrationTests.Fixtures;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
[TestOf(typeof(RadioDeviceSystem))]
public sealed class RadioDeviceSystemTest : GameTest
{
    [Test]
    public async Task MicrophonesFilterXenosWithoutDuplicateRelays()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<RadioDeviceTestProbeSystem>();
            var xeno = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<XenoComponent>(xeno);
            var human = SEntMan.SpawnEntity(null, map.GridCoords);

            var filtered = SpawnMicrophone(map.GridCoords, ignoreXenos: true);
            var ordinary = SpawnMicrophone(map.GridCoords, ignoreXenos: false);

            RaiseListen(filtered, "filtered xeno", xeno);
            RaiseListen(filtered, "accepted human", human);
            RaiseListen(ordinary, "accepted xeno", xeno);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<RadioDeviceTestProbeComponent>(filtered).SendAttempts,
                    Is.EqualTo(1),
                    "An IgnoreXenos microphone must reject xeno speech and relay non-xeno speech exactly once.");
                Assert.That(SEntMan.GetComponent<RadioDeviceTestProbeComponent>(ordinary).SendAttempts,
                    Is.EqualTo(1),
                    "An ordinary microphone must relay xeno speech exactly once.");
            });

            SEntMan.DeleteEntity(filtered);
            SEntMan.DeleteEntity(ordinary);
            SEntMan.DeleteEntity(xeno);
            SEntMan.DeleteEntity(human);
        });
    }

    [Test]
    public async Task SpeakerPreservesLanguageAndSkipsItsOwnRadioSource()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<RadioDeviceTestProbeSystem>();
            var speaker = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<RadioSpeakerComponent>(speaker);
            var speakerProbe = SEntMan.AddComponent<RadioDeviceTestProbeComponent>(speaker);

            var originalSpeaker = SEntMan.SpawnEntity(null, map.GridCoords);
            var sourceProbe = SEntMan.AddComponent<RadioDeviceTestProbeComponent>(originalSpeaker);
            sourceProbe.TransformedName = "Transformed radio voice";

            var radioSource = SEntMan.SpawnEntity(null, map.GridCoords);
            var channel = SProtoMan.Index<RadioChannelPrototype>("Common");
            ProtoId<LanguagePrototype> language = "English";
            const string message = "One language-aware radio whisper";

            var received = CreateReceiveEvent(message, originalSpeaker, channel, radioSource, language);
            SEntMan.EventBus.RaiseLocalEvent(speaker, ref received);

            Assert.Multiple(() =>
            {
                Assert.That(sourceProbe.NameTransforms, Is.EqualTo(1),
                    "The original speaker's transformed voice must be resolved once.");
                Assert.That(speakerProbe.SpokenMessages, Is.EqualTo(1),
                    "One received radio event must create exactly one speaker whisper.");
                Assert.That(speakerProbe.LastMessage, Is.EqualTo(message));
                Assert.That(speakerProbe.LastLanguage, Is.EqualTo(language));
                Assert.That(speakerProbe.LastSpokenSource, Is.EqualTo(speaker));
            });

            var selfReceived = CreateReceiveEvent(
                "This feedback must be skipped",
                originalSpeaker,
                channel,
                speaker,
                language);
            SEntMan.EventBus.RaiseLocalEvent(speaker, ref selfReceived);

            Assert.Multiple(() =>
            {
                Assert.That(sourceProbe.NameTransforms, Is.EqualTo(1),
                    "A speaker must reject its own radio source before transforming the voice.");
                Assert.That(speakerProbe.SpokenMessages, Is.EqualTo(1),
                    "A speaker must not repeat a message originating from itself.");
            });

            SEntMan.DeleteEntity(speaker);
            SEntMan.DeleteEntity(originalSpeaker);
            SEntMan.DeleteEntity(radioSource);
        });
    }

    private EntityUid SpawnMicrophone(EntityCoordinates coordinates, bool ignoreXenos)
    {
        var entity = SEntMan.SpawnEntity(null, coordinates);
        var microphone = SEntMan.AddComponent<RadioMicrophoneComponent>(entity);
        microphone.BroadcastChannel = "Common";
        microphone.IgnoreXenos = ignoreXenos;
        SEntMan.AddComponent<RadioDeviceTestProbeComponent>(entity);
        return entity;
    }

    private void RaiseListen(EntityUid microphone, string message, EntityUid source)
    {
        var listen = new ListenEvent(message, source);
        SEntMan.EventBus.RaiseLocalEvent(microphone, listen);
    }

    private static RadioReceiveEvent CreateReceiveEvent(
        string message,
        EntityUid messageSource,
        RadioChannelPrototype channel,
        EntityUid radioSource,
        ProtoId<LanguagePrototype> language)
    {
        var chat = new ChatMessage(
            ChatChannel.Radio,
            message,
            message,
            NetEntity.Invalid,
            null);
        return new RadioReceiveEvent(
            message,
            messageSource,
            channel,
            radioSource,
            new MsgChatMessage { Message = chat },
            language);
    }
}

[RegisterComponent]
public sealed partial class RadioDeviceTestProbeComponent : Component
{
    public int SendAttempts;
    public int NameTransforms;
    public int SpokenMessages;
    public string? TransformedName;
    public string? LastMessage;
    public ProtoId<LanguagePrototype> LastLanguage;
    public EntityUid LastSpokenSource;
}

public sealed class RadioDeviceTestProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioDeviceTestProbeComponent, RadioSendAttemptEvent>(OnSendAttempt);
        SubscribeLocalEvent<RadioDeviceTestProbeComponent, TransformSpeakerNameEvent>(OnTransformName);
        SubscribeLocalEvent<RadioDeviceTestProbeComponent, EntitySpokeEvent>(OnEntitySpoke);
    }

    private static void OnSendAttempt(
        Entity<RadioDeviceTestProbeComponent> ent,
        ref RadioSendAttemptEvent args)
    {
        ent.Comp.SendAttempts++;
    }

    private static void OnTransformName(
        Entity<RadioDeviceTestProbeComponent> ent,
        ref TransformSpeakerNameEvent args)
    {
        ent.Comp.NameTransforms++;
        if (ent.Comp.TransformedName is { } name)
            args.VoiceName = name;
    }

    private static void OnEntitySpoke(
        Entity<RadioDeviceTestProbeComponent> ent,
        ref EntitySpokeEvent args)
    {
        ent.Comp.SpokenMessages++;
        ent.Comp.LastMessage = args.Message;
        ent.Comp.LastLanguage = args.Language;
        ent.Comp.LastSpokenSource = args.Source;
    }
}

#pragma warning restore RA0002
