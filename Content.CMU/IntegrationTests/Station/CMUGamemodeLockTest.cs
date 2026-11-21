using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round.Commands;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Utility;

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

    [Test]
    public async Task CommandLocksMultiplePresetsAndParsesTrailingRounds()
    {
        await Server.WaitAssertion(() =>
        {
            var votes = SEntMan.System<CMUPresetVoteSystem>();

            Server.ConsoleHost.ExecuteCommand("lockgamemode ColonyFall DistressSignal");
            Assert.That(votes.LockedPresets.Select(preset => preset.Id), Is.EqualTo(new[] { "ColonyFall", "DistressSignal" }));
            Assert.That(votes.LockedRounds, Is.Null);

            Server.ConsoleHost.ExecuteCommand("lockgamemode Insurgency 3");
            Assert.That(votes.LockedPresets.Select(preset => preset.Id), Is.EqualTo(new[] { "Insurgency" }));
            Assert.That(votes.LockedRounds, Is.EqualTo(3));

            // Bad input refuses without touching the standing lock. The test console host
            // asserts on any WriteError, so refusal paths invoke the command directly.
            var command = new LockGamemodeCommand();
            Server.InstanceDependencyCollection.InjectDependencies(command);
            var shell = new SilentTestShell();

            command.Execute(shell, "lockgamemode NotAPreset", ["NotAPreset"]);
            Assert.That(shell.Errors, Has.Count.EqualTo(1), "An unknown preset is refused.");
            Assert.That(shell.Errors[0], Does.Contain("NotAPreset"), "The refusal names the preset it did not recognise.");
            Assert.That(votes.LockedPresets.Select(preset => preset.Id), Is.EqualTo(new[] { "Insurgency" }), "An unknown preset leaves the lock alone.");

            command.Execute(shell, "lockgamemode ColonyFall 0", ["ColonyFall", "0"]);
            Assert.That(shell.Errors, Has.Count.EqualTo(2), "A zero round count is refused.");
            Assert.That(shell.Errors[1], Does.Not.Contain("preset"), "The zero round count is refused for its count, not for an unknown preset.");
            Assert.That(votes.LockedRounds, Is.EqualTo(3), "A zero round count leaves the lock alone.");

            Server.ConsoleHost.ExecuteCommand("lockgamemode clear");
            Assert.That(votes.LockedPresets, Is.Empty);
        });
    }

    /// <summary>
    ///     Records console errors instead of asserting on them, for testing command refusal paths.
    /// </summary>
    private sealed class SilentTestShell : IConsoleShell
    {
        public List<string> Errors { get; } = new();

        public IConsoleHost ConsoleHost => throw new NotSupportedException();
        public bool IsLocal => true;
        public bool IsServer => true;
        public ICommonSession? Player => null;
        public void ExecuteCommand(string command) => throw new NotSupportedException();
        public void RemoteExecuteCommand(string command) => throw new NotSupportedException();
        public void WriteLine(string text) { }
        public void WriteLine(FormattedMessage message) { }
        public void WriteError(string text) => Errors.Add(text);
        public void Clear() { }
    }
}
