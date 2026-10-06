using System;
using System.Linq;
using Content.Server.CMU14.Administration.Console;
using NUnit.Framework;
using Robust.Shared.Profiling;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class LagProfileCaptureTest
{
    [Test]
    public void WrappedCompletedFrameExcludesOverwrittenAndUnindexedEvents()
    {
        var profiler = CreateProfiler(16);
        EmitFrame(profiler, 1, 10);
        while (profiler.Buffer.LogWriteOffset < 14)
            profiler.WriteValue("padding", 0);
        EmitFrame(profiler, 2, 20);
        profiler.WriteValue("unfinished", 999);

        var capture = LagProfileCommand.Capture(profiler, 0);

        Assert.Multiple(() =>
        {
            Assert.That(capture.Frames.Select(frame => frame.Frame), Is.EqualTo(new long?[] { 2 }));
            Assert.That(capture.Counters["count"].Last, Is.EqualTo(20));
            Assert.That(capture.Counters.ContainsKey("unfinished"), Is.False);
            Assert.That(capture.Samples[("sample", "Work")].Count, Is.EqualTo(1));
            Assert.That(capture.Frames.Single().AllocatedBytes, Is.EqualTo(1024));
        });

        // Console output may itself write profiler events after capture. Returned aggregates must
        // still describe the captured frame after those writes overwrite all of its source data.
        for (var i = 0; i < 32; i++)
            profiler.WriteValue("later output", i);
        Assert.That(capture.Counters["count"].Last, Is.EqualTo(20));
        Assert.That(capture.Samples[("sample", "Work")].AllocatedBytes, Is.EqualTo(128));
    }

    [Test]
    public void FrameLimitAndStartMarkerRetainChronologicalCounterValues()
    {
        var profiler = CreateProfiler(128);
        EmitFrame(profiler, 1, 50);
        var start = profiler.Buffer.IndexWriteOffset;
        EmitFrame(profiler, 2, 30);
        EmitFrame(profiler, 3, 10);

        var sinceStart = LagProfileCommand.Capture(profiler, 0, start);
        var recent = LagProfileCommand.Capture(profiler, 1);
        Assert.Multiple(() =>
        {
            Assert.That(sinceStart.Frames.Select(frame => frame.Frame), Is.EqualTo(new long?[] { 2, 3 }));
            Assert.That(sinceStart.Counters["count"].Count, Is.EqualTo(2));
            Assert.That(sinceStart.Counters["count"].Max, Is.EqualTo(30));
            Assert.That(sinceStart.Counters["count"].Last, Is.EqualTo(10));
            Assert.That(recent.Frames.Single().Frame, Is.EqualTo(3));
            Assert.That(recent.Counters["count"].Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void InvalidAndEmptyFrameRangesAreExcluded()
    {
        var profiler = CreateProfiler(32);
        EmitFrame(profiler, 1, 5);
        AddIndex(-1, 2);
        AddIndex(2, 2);
        AddIndex(2, 1);
        AddIndex(0, profiler.Buffer.LogWriteOffset + 1);

        Assert.That(LagProfileCommand.Capture(profiler, 0).Frames.Single().Frame, Is.EqualTo(1));
        Assert.That(LagProfileCommand.Capture(CreateProfiler(0, 0), 0).Frames, Is.Empty);

        void AddIndex(long start, long end)
        {
            var offset = profiler.Buffer.IndexWriteOffset++;
            profiler.Buffer.Index(offset) = new ProfIndex { Type = ProfIndexType.Frame, StartPos = start, EndPos = end };
        }
    }

    [Test]
    public void SmallCaptureDoesNotCloneLargeHistoryBuffer()
    {
        var profiler = CreateProfiler(131072);
        EmitFrame(profiler, 1, 5);
        LagProfileCommand.Capture(profiler, 1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var capture = LagProfileCommand.Capture(profiler, 1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(capture.Frames, Has.Count.EqualTo(1));
        Assert.That(allocated, Is.LessThan(256 * 1024),
            "Reading one frame must not clone megabytes of unused profiler history.");
    }

    [Test]
    public void RepeatedSamplesReuseAggregationKeys()
    {
        var profiler = CreateProfiler(16384);
        var start = profiler.WriteValue("Start Frame", 1L);
        for (var i = 0; i < 10000; i++)
            profiler.WriteValue("Repeated workload sample", Timing(0.001f, 16));
        profiler.WriteGroupEnd(start, "Frame", Timing(10, 160000));
        profiler.MarkIndex(start, ProfIndexType.Frame);
        LagProfileCommand.Capture(profiler, 1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var capture = LagProfileCommand.Capture(profiler, 1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(capture.Samples[("sample", "Repeated workload sample")].Count, Is.EqualTo(10000));
        Assert.That(allocated, Is.LessThan(256 * 1024),
            "Aggregating repeated samples must not allocate a new string key for every event.");
    }

    private static ProfManager CreateProfiler(int logSize, int indexSize = 8)
    {
        var profiler = new ProfManager
        {
            Buffer = new ProfBuffer { LogBuffer = new ProfLog[logSize], IndexBuffer = new ProfIndex[indexSize] },
        };
        typeof(ProfManager).GetProperty(nameof(ProfManager.IsEnabled))!.SetValue(profiler, true);
        return profiler;
    }

    private static void EmitFrame(ProfManager profiler, long number, int count)
    {
        var start = profiler.WriteValue("Start Frame", number);
        var root = profiler.WriteGroupStart();
        profiler.WriteValue("count", count);
        profiler.WriteValue("Work", Timing(0.01f, 128));
        profiler.WriteGroupEnd(root, "Frame", Timing(0.02f, 1024));
        profiler.MarkIndex(start, ProfIndexType.Frame);
    }

    private static ProfValue Timing(float seconds, long bytes) => new()
    {
        Type = ProfValueType.TimeAllocSample,
        TimeAllocSample = new TimeAndAllocSample { Time = seconds, Alloc = bytes },
    };
}
