using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Dropship;
using Content.Shared.Radio;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUMedevacRadioTest : GameTest
{
    [Test]
    public async Task EmptyStretcherAnnouncesAvailabilityWithoutAnInvalidPatient()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var recorder = Server.System<CMUMedevacRadioRecorderSystem>();
            recorder.Messages.Clear();
            var stretcher = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<StrapComponent>(stretcher);
            SEntMan.AddComponent<DropshipTargetComponent>(stretcher);
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<MarineComponent>(user).Faction = "govfor";
            var available = new CMUMedevacBeaconEvent(stretcher, user, false);

            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref available);

            Assert.That(recorder.Messages, Has.Count.EqualTo(1),
                "An empty stretcher should still announce its active pickup beacon.");
            Assert.That(recorder.Messages[0].Channel, Is.EqualTo("radioGovforJTAC"));
            var staleStrap = new CMUMedevacBeaconEvent(stretcher, user, true);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref staleStrap);
            Assert.That(recorder.Messages, Has.Count.EqualTo(1),
                "A stale buckle notification must not announce a patient that is no longer aboard.");
        });
    }

    private sealed class CMUMedevacRadioRecorderSystem : EntitySystem
    {
        public readonly List<(string Channel, string Message)> Messages = new();

        public override void Initialize()
        {
            SubscribeLocalEvent<RadioSendAttemptEvent>(OnSend);
        }

        private void OnSend(ref RadioSendAttemptEvent ev)
        {
            Messages.Add((ev.Channel.ID, ev.Message));
        }
    }
}
