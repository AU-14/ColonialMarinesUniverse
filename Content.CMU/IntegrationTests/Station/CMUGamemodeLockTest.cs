using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Station;

/// <summary>
///     An admin's gamemode lock makes the vote offer only that gamemode, for as many rounds as they asked.
/// </summary>
[TestFixture]
public sealed class CMUGamemodeLockTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task LockOffersOneGamemodeForItsRounds()
    {
        await Server.WaitAssertion(() =>
        {
            var votes = SEntMan.System<CMUPresetVoteSystem>();
            votes.LockPreset("Insurgency", 2);

            var presets = new Dictionary<string, string> { ["DistressSignal"] = "Distress Signal", ["ColonyFall"] = "Colony Fall" };
            votes.FilterVotePresets(presets);
            Assert.That(presets.Keys, Is.EquivalentTo(new[] { "Insurgency" }));

            var roundStarted = new GameRunLevelChangedEvent(GameRunLevel.PreRoundLobby, GameRunLevel.InRound);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, roundStarted);
            Assert.That(votes.LockedRounds, Is.EqualTo(1));

            SEntMan.EventBus.RaiseEvent(EventSource.Local, roundStarted);
            Assert.That(votes.LockedPreset, Is.Null, "The lock ends after its rounds.");

            presets = new Dictionary<string, string> { ["DistressSignal"] = "Distress Signal", ["ColonyFall"] = "Colony Fall" };
            votes.FilterVotePresets(presets);
            Assert.That(presets, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(presets.ContainsKey("Insurgency"), Is.False);

            votes.LockPreset("Insurgency");
            for (var i = 0; i < 5; i++)
                SEntMan.EventBus.RaiseEvent(EventSource.Local, roundStarted);
            Assert.That(votes.LockedPreset?.Id, Is.EqualTo("Insurgency"), "With no round count it lasts until cleared.");
            votes.ClearLock();
        });
    }
}
