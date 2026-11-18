using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Station;

/// <summary>
///     An admin's gamemode lock makes the vote offer only the locked gamemodes, for as many rounds as they asked.
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
            votes.LockPresets(["Insurgency"], 2);

            var presets = new Dictionary<string, string> { ["DistressSignal"] = "Distress Signal", ["ColonyFall"] = "Colony Fall" };
            votes.FilterVotePresets(presets);
            Assert.That(presets.Keys, Is.EquivalentTo(new[] { "Insurgency" }));

            var roundStarted = new GameRunLevelChangedEvent(GameRunLevel.PreRoundLobby, GameRunLevel.InRound);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, roundStarted);
            Assert.That(votes.LockedRounds, Is.EqualTo(1));

            SEntMan.EventBus.RaiseEvent(EventSource.Local, roundStarted);
            Assert.That(votes.LockedPresets, Is.Empty, "The lock ends after its rounds.");

            presets = new Dictionary<string, string> { ["DistressSignal"] = "Distress Signal", ["ColonyFall"] = "Colony Fall" };
            votes.FilterVotePresets(presets);
            Assert.That(presets, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(presets.ContainsKey("Insurgency"), Is.False);

            votes.LockPresets(["Insurgency"]);
            for (var i = 0; i < 5; i++)
                SEntMan.EventBus.RaiseEvent(EventSource.Local, roundStarted);
            Assert.That(votes.LockedPresets.Select(preset => preset.Id), Is.EqualTo(new[] { "Insurgency" }), "With no round count it lasts until cleared.");
            votes.ClearLock();
        });
    }

    [Test]
    public async Task LockOffersEveryLockedGamemode()
    {
        await Server.WaitAssertion(() =>
        {
            var votes = SEntMan.System<CMUPresetVoteSystem>();
            votes.LockPresets(["DistressSignal", "ColonyFall"]);

            var presets = new Dictionary<string, string> { ["Insurgency"] = "Insurgency" };
            votes.FilterVotePresets(presets);
            Assert.That(presets.Keys, Is.EquivalentTo(new[] { "DistressSignal", "ColonyFall" }));

            votes.ClearLock();
        });
    }
}
