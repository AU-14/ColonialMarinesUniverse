using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Stun;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Chat;

[TestFixture]
public sealed class UnconsciousWhisperTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task UnconsciousLocalSpeechBecomesAWhisper(bool knockedOut)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var listener = Server.System<UnconsciousWhisperTestListenerSystem>();
            listener.Spoken.Clear();

            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            if (knockedOut)
                Assert.That(Server.System<RMCSizeStunSystem>().TryKnockOut(human, TimeSpan.FromMinutes(1)), Is.True);
            else
                Assert.That(Server.System<SleepingSystem>().TrySleeping(human), Is.True);

            Server.System<ChatSystem>().TrySendInGameICMessage(
                human,
                "can anyone hear me",
                InGameICChatType.Speak,
                ChatTransmitRange.Normal);

            var spoken = listener.Spoken.Where(s => s.Source == human).ToList();
            Assert.That(spoken, Has.Count.EqualTo(1), "unconscious speech must still be sent");
            Assert.That(spoken[0].Whispered, Is.True, "unconscious speech must be sent as a whisper");
        });
    }
}

public sealed class UnconsciousWhisperTestListenerSystem : EntitySystem
{
    public readonly List<(EntityUid Source, bool Whispered)> Spoken = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);
    }

    private void OnSpoke(EntitySpokeEvent ev)
    {
        Spoken.Add((ev.Source, ev.ObfuscatedMessage != null));
    }
}
