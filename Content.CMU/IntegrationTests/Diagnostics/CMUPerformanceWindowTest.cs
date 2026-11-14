using System.Collections.Concurrent;
using System.Globalization;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Diagnostics.Performance;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Hearing;
using Robust.Server.GameStates;
using Robust.Shared.Log;
using Robust.Shared.Timing;
using Serilog.Events;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUPerformanceWindowTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ToggleAndMonitorDisableDoNotLeakEventsIntoLaterWindows()
    {
        var map = await Pair.CreateTestMap();
        await Pair.RunTicksSync(10);
        var capture = new WindowLogCapture();
        var diagnostics = Server.ResolveDependency<ICMUServerPerformanceDiagnostics>();
        var gameStates = Server.ResolveDependency<IServerGameStateManager>();
        var sawmill = Server.ResolveDependency<ILogManager>().GetSawmill("cmu.server-performance");
        EntityUid shooter = default;
        sawmill.AddHandler(capture);
        try
        {
            await Server.WaitPost(() =>
            {
                Server.CfgMan.SetCVar(CCVars.CMUServerPerformanceDiagnosticsEnabled, true);
                Server.CfgMan.SetCVar(CCVars.CMUServerPerformanceLogEnabled, true);
                Server.CfgMan.SetCVar(CCVars.CMUServerPerformanceWindowEnabled, true);
                shooter = SEntMan.SpawnEntity(null, map.GridCoords);
                Fire();
                Fire();
                gameStates.ClientRequestFull?.Invoke(ServerSession!, SGameTiming.CurTick - 1, null);
            });
            await Pair.RunTicksSync(1);
            await Server.WaitPost(() =>
            {
                Assert.That(diagnostics.CaptureWindowReport(), Is.True);
                var first = capture.Windows.Last();
                Assert.That(first, Does.Contain(" gunfireEvents=2 "));
                Assert.That(first, Does.Contain(" recoveryFullRequests=1 "));
                Server.CfgMan.SetCVar(CCVars.CMUServerPerformanceWindowEnabled, false);
                Fire();
                Assert.That(diagnostics.CaptureWindowReport(), Is.False);
                Server.CfgMan.SetCVar(CCVars.CMUServerPerformanceWindowEnabled, true);
                Server.CfgMan.SetCVar(CCVars.CMUServerPerformanceDiagnosticsEnabled, false);
                Fire();
                Assert.That(diagnostics.CaptureWindowReport(), Is.False);
                Server.CfgMan.SetCVar(CCVars.CMUServerPerformanceDiagnosticsEnabled, true);
                Fire();
            });
            await Pair.RunTicksSync(1);
            await Server.WaitPost(() =>
            {
                Assert.That(diagnostics.CaptureWindowReport(), Is.True);
                var restarted = capture.Windows.Last();
                Assert.That(restarted, Does.Contain(" gunfireEvents=1 "),
                    "Disabled events and earlier windows must not be counted after re-enabling.");
                Assert.That(restarted, Does.Contain(" recoveryFullRequests=0 "));
                Assert.That(restarted, Does.Not.Contain(ServerSession!.UserId.ToString()),
                    "Event telemetry must remain aggregate-only.");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.CfgMan.SetCVar(CCVars.CMUServerPerformanceWindowEnabled, false));
            sawmill.RemoveHandler(capture);
        }
        return;

        void Fire()
        {
            var shot = new CMUGunFiredEvent(shooter, shooter, map.GridCoords, map.GridCoords);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref shot);
        }
    }

    private sealed class WindowLogCapture : ILogHandler
    {
        public readonly ConcurrentQueue<string> Windows = new();

        public void Log(string sawmillName, LogEvent message)
        {
            var text = message.RenderMessage(CultureInfo.InvariantCulture);
            if (text.StartsWith("[CMU-PERF] performance-window ", StringComparison.Ordinal))
                Windows.Enqueue(text);
        }
    }
}
