using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;
using Robust.Shared.Profiling;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture, NonParallelizable]
[Explicit("Diagnostic instrumentation benchmark; run explicitly in Release.")]
public sealed class DiagnosticsOverheadMeasurementTest
{
    [Test]
    public void MeasureInstrumentationAndBoundedHistoryParsing()
    {
        var profiler = new ProfManager
        {
            Buffer = new ProfBuffer { LogBuffer = new ProfLog[65536], IndexBuffer = new ProfIndex[512] },
        };
        var enabled = typeof(ProfManager).GetProperty(nameof(ProfManager.IsEnabled))!;
        // Alternating ABBA blocks expose warmup/order effects. No wall-time pass threshold.
        for (var pass = 0; pass < 8; pass++)
        {
            var on = pass % 4 is 1 or 2;
            enabled.SetValue(profiler, on);
            for (var i = 0; i < 10000; i++) { using (profiler.Group("measurement")) { } }
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < 100000; i++) { using (profiler.Group("measurement")) { } }
            var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            TestContext.Out.WriteLine($"PERF diagnostic_scopes pass={pass} enabled={on} scopes=100000 ms={ms:F4} bytes={bytes}");
        }
        enabled.SetValue(profiler, true);
        var index = profiler.WriteValue("Start Frame", 1L);
        var root = profiler.WriteGroupStart();
        for (var i = 0; i < 65000; i++) profiler.WriteValue("work", i);
        profiler.WriteGroupEnd(root, "Frame", new ProfValue
        {
            Type = ProfValueType.TimeAllocSample,
            TimeAllocSample = new TimeAndAllocSample { Time = 0.1f, Alloc = 0 },
        });
        profiler.MarkIndex(index, ProfIndexType.Frame);
        var names = new HashSet<string>();
        CMUPerformanceProfilerReader.Capture(profiler, names, 4, 65536);
        var cost = new CMUPerformanceDiagnosticsCost();
        for (var pass = 0; pass < 5; pass++)
        {
            CMUPerformanceProfileReport result;
            using (cost.Measure()) result = CMUPerformanceProfilerReader.Capture(profiler, names, 4, 65536);
            Assert.That(result.EventsRead, Is.EqualTo(65003));
            Assert.That(result.Truncated, Is.False);
            Assert.That(result.Counters.Single(row => row.Name == "work").Count, Is.EqualTo(65000));
        }
        TestContext.Out.WriteLine($"PERF diagnostic_parse events=65003 passes={cost.Calls} totalMs={cost.TotalMilliseconds:F4} maxMs={cost.MaximumMilliseconds:F4} bytes={cost.AllocatedBytes} loggingExcluded=true");
    }
}
