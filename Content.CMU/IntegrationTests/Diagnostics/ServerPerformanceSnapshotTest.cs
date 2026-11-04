using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Commands;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Diagnostics;

public sealed class ServerPerformanceSnapshotTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task SnapshotRetainsChurnAndSelectedComponentDetails()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var command = new ServerPerformanceCommand();
            var shell = new RecordingShell();
            var removed = SEntMan.SpawnEntity("Chair", map.GridCoords);
            command.Execute(shell, "", ["baseline"]);
            SEntMan.DeleteEntity(removed);
            var created = SEntMan.SpawnEntity("Chair", map.GridCoords);
            shell.Lines.Clear();
            command.Execute(shell, "", ["deep", "100", "30"]);
            var output = string.Join('\n', shell.Lines);
            Assert.That(output, Does.Contain("New entities since previous snapshot"));
            Assert.That(output, Does.Contain("Removed entities since previous snapshot"));
            Assert.That(output, Does.Contain($"sample={removed}"));
            Assert.That(output, Does.Contain($"sample={created}"));
            Assert.That(output, Does.Contain("comps=["));
            Assert.That(output, Does.Contain("Transform"));
            Assert.That(output, Does.Contain("created=["));
            command.Execute(shell, "", ["clear"]);
        });
    }

    private sealed class RecordingShell : IConsoleShell
    {
        public List<string> Lines { get; } = new();
        public IConsoleHost ConsoleHost => throw new NotSupportedException();
        public bool IsLocal => true;
        public bool IsServer => true;
        public ICommonSession? Player => null;
        public void ExecuteCommand(string command) => throw new NotSupportedException();
        public void RemoteExecuteCommand(string command) => throw new NotSupportedException();
        public void WriteLine(string text) => Lines.Add(text);
        public void WriteLine(FormattedMessage message) => Lines.Add(message.ToString());
        public void WriteError(string text) => Assert.Fail(text);
        public void Clear() => Lines.Clear();
    }
}
