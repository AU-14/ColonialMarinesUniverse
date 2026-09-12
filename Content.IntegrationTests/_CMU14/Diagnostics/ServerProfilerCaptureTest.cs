using Content.Server.CMU14.Diagnostics.Performance;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Profiling;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class ServerProfilerCaptureTest
{
    [Test]
    public async Task SlowOperationSurvivesOutsideProfilerHistory()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var diagnostics = pair.Server.ResolveDependency<ICMUServerPerformanceDiagnostics>();
            var logger = pair.Server.ResolveDependency<ILogManager>().GetSawmill("cmu.server-performance");
            var capture = new OperationCapture();
            logger.AddHandler(capture);
            try
            {
                using (diagnostics.MeasureOperation("regression-fill", "TestPrototype"))
                    GC.KeepAlive(new byte[2 * 1024 * 1024]);
                Assert.That(diagnostics.CaptureManualReport(), Is.True);
                var operation = capture.Messages.Single(message => message.Contains("name=regression-fill "));
                Assert.That(operation, Does.Contain("prototype=TestPrototype "));
                Assert.That(operation, Does.Contain("source=content-scope"));
                Assert.That(operation, Does.Contain("inclusive=true"));
                Assert.That(operation, Does.Not.Contain("ageMs=-"),
                    "A cached manual-report observation must not predate the operation's logging timestamp.");
            }
            finally
            {
                logger.RemoveHandler(capture);
            }
        });
        await pair.CleanReturnAsync();
    }

    private sealed class OperationCapture : ILogHandler
    {
        public readonly List<string> Messages = new();
        public void Log(string sawmillName, LogEvent message) => Messages.Add(message.RenderMessage());
    }

    [Test]
    public async Task SlowInputRemainsAttributableWhenOlderCompletedFramesExist()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            var profiler = pair.Server.ResolveDependency<ProfManager>();
            var previous = profiler.Buffer;
            var enabled = profiler.IsEnabled;
            try
            {
                profiler.Buffer = new ProfBuffer { LogBuffer = new ProfLog[1024], IndexBuffer = new ProfIndex[8] };
                config.SetCVar(CVars.ProfEnabled, true);
                var old = profiler.WriteValue("Start Frame", 1L);
                End(old, "Frame", 0.01);
                profiler.MarkIndex(old, ProfIndexType.Frame);
                var current = profiler.WriteValue("Start Frame", 2L);
                var input = profiler.WriteValue("Input start", 1L);
                End(input, "CMU PlayerSpawn", 0.2);
                var pending = Capture();
                Assert.That(pending.Frames, Has.Count.EqualTo(1));
                Assert.That(pending.Coverage.UnindexedEvents, Is.GreaterThan(0),
                    "A successful older capture must not prevent a completion retry for slow input.");
                End(current, "Frame", 0.21);
                profiler.MarkIndex(current, ProfIndexType.Frame);
                var completed = Capture();
                Assert.That(completed.FrameSamples, Has.Count.EqualTo(2));
                Assert.That(completed.FrameSamples.Single(row => row.IndexOffset == 0).Samples, Is.Empty);
                var spawn = completed.FrameSamples.Single(row => row.IndexOffset == 1).Samples.Single();
                Assert.That(spawn.Name, Is.EqualTo("CMU PlayerSpawn"));
                Assert.That(spawn.TotalSeconds, Is.EqualTo(0.2).Within(0.000001));
            }
            finally
            {
                profiler.Buffer = previous;
                config.SetCVar(CVars.ProfEnabled, enabled);
            }

            CMUPerformanceProfileReport Capture() => CMUPerformanceProfilerReader.Capture(profiler, new HashSet<string>(), 8, 1024);
            void End(long start, string name, double seconds) => profiler.WriteGroupEnd(start, name, new ProfValue
            {
                Type = ProfValueType.TimeAllocSample,
                TimeAllocSample = new TimeAndAllocSample { Time = (float) seconds, Alloc = 1024 },
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BusyUnfinishedFrameExplainsLossAndRetainsItsTailAfterCompletion()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            var profiler = pair.Server.ResolveDependency<ProfManager>();
            var previousBuffer = profiler.Buffer;
            var previousEnabled = profiler.IsEnabled;
            try
            {
                profiler.Buffer = new ProfBuffer { LogBuffer = new ProfLog[256], IndexBuffer = new ProfIndex[8] };
                config.SetCVar(CVars.ProfEnabled, false);
                Assert.That(Capture().Coverage.Status, Is.EqualTo("disabled"));
                config.SetCVar(CVars.ProfEnabled, true);
                Assert.That(Capture().Coverage.Status, Is.EqualTo("no-completed-frames"));

                var first = profiler.WriteValue("Start Frame", 1L);
                profiler.WriteValue("Tick", 100L);
                profiler.WriteValue("Tick count", 1);
                EndFrame(first);
                var completed = Capture();
                Assert.Multiple(() =>
                {
                    Assert.That(completed.Coverage.Status, Is.EqualTo("available"));
                    Assert.That(completed.Frames.Single().FirstTick, Is.EqualTo(100));
                    Assert.That(completed.Frames.Single().Partial, Is.False);
                });

                // This reproduces capture from FramePostEngine: the current frame has overwritten the
                // previous frame, but has not yet published its own index.
                var busy = profiler.WriteValue("Start Frame", 2L);
                for (var i = 0; i < 300; i++)
                    profiler.WriteValue("busy", i);
                var unfinished = Capture();
                Assert.Multiple(() =>
                {
                    Assert.That(unfinished.Frames, Is.Empty);
                    Assert.That(unfinished.Coverage.Status, Is.EqualTo("history-overwritten"));
                    Assert.That(unfinished.Coverage.OverwrittenFrames, Is.EqualTo(1));
                    Assert.That(unfinished.Coverage.UnindexedEvents, Is.GreaterThan(256));
                });

                profiler.WriteValue("Tick", 101L);
                profiler.WriteValue("Tick count", 1);
                profiler.WriteValue("Gen 2 Count", 1);
                EndFrame(busy);
                var partial = Capture();
                Assert.Multiple(() =>
                {
                    Assert.That(partial.Coverage.Status, Is.EqualTo("partial-history"));
                    Assert.That(partial.Frames.Single().Partial, Is.True);
                    Assert.That(partial.Frames.Single().TimeSeconds, Is.EqualTo(2));
                    Assert.That(partial.Frames.Single().AllocatedBytes, Is.EqualTo(4096));
                    Assert.That(partial.Frames.Single().LastTick, Is.EqualTo(101));
                    Assert.That(partial.Counters.Single(row => row.Name == "Gen 2 Count").Total, Is.EqualTo(1));
                    Assert.That(partial.EventsRead, Is.LessThanOrEqualTo(128));
                    Assert.That(partial.Truncated, Is.True);
                });
            }
            finally
            {
                profiler.Buffer = previousBuffer;
                config.SetCVar(CVars.ProfEnabled, previousEnabled);
            }

            CMUPerformanceProfileReport Capture() => CMUPerformanceProfilerReader.Capture(profiler, new HashSet<string>(), 8, 128);
            void EndFrame(long start)
            {
                profiler.WriteGroupEnd(start, "Frame", new ProfValue
                {
                    Type = ProfValueType.TimeAllocSample,
                    TimeAllocSample = new TimeAndAllocSample { Time = 2, Alloc = 4096 },
                });
                profiler.MarkIndex(start, ProfIndexType.Frame);
            }
        });
        await pair.CleanReturnAsync();
    }
}
