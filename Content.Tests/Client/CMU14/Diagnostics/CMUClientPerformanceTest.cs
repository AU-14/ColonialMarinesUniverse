using System;
using System.Linq;
using Content.Client.CMU14.Diagnostics.Performance;
using NUnit.Framework;
using Robust.Shared.Profiling;

namespace Content.Tests.Client.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUClientPerformanceTest
{
    [Test]
    public void NestedPathsSeparatePredictionFromFrameUpdateAndPreserveCounterOrder()
    {
        var profiler = CreateProfiler();
        var start = BeginFrame(profiler, 10, out var root);
        var update = profiler.WriteGroupStart();
        profiler.WriteValue("ExpensiveSystem", Timing(0.002f, 40));
        profiler.WriteGroupEnd(update, "Update", Timing(0.003f, 50));
        var prediction = profiler.WriteGroupStart();
        profiler.WriteValue("ExpensiveSystem", Timing(0.006f, 100));
        profiler.WriteValue("State count", 3);
        profiler.WriteValue("State count", 7);
        profiler.WriteGroupEnd(prediction, "Prediction", Timing(0.008f, 120));
        EndFrame(profiler, start, root, 0.012f, 200);

        var reader = new CMUClientProfileReader(0);
        reader.Read(profiler, -1);
        Assert.Multiple(() =>
        {
            Assert.That(reader.Frames, Is.EqualTo(1));
            Assert.That(reader.TotalWorkMs, Is.EqualTo(12).Within(0.001));
            Assert.That(reader.TotalAllocatedBytes, Is.EqualTo(200), "Do not double-count inclusive allocations.");
            Assert.That(reader.Scopes.Single(s => s.Path == "Frame / Update / ExpensiveSystem").Window.TotalMs, Is.EqualTo(2).Within(0.001));
            Assert.That(reader.Scopes.Single(s => s.Path == "Frame / Prediction / ExpensiveSystem").Window.TotalMs, Is.EqualTo(6).Within(0.001));
        });
        var counter = reader.Scopes.Single(s => s.Path.EndsWith("State count")).Window;
        Assert.Multiple(() =>
        {
            Assert.That(counter.CounterTotal, Is.EqualTo(10));
            Assert.That(counter.CounterMax, Is.EqualTo(7));
            Assert.That(counter.CounterLast, Is.EqualTo(7), "Reverse parsing must still preserve chronological last.");
        });
    }

    [Test]
    public void WorstTimeAndAllocationFramesSurviveLaterFastFramesAndAreReadOnlyOnce()
    {
        var profiler = CreateProfiler();
        EmitFrame(profiler, 1, 0.200f, 100);
        EmitFrame(profiler, 2, 0.010f, 9000);
        EmitFrame(profiler, 3, 0.001f, 1);
        var reader = new CMUClientProfileReader(0);
        reader.Read(profiler, -1, worstWallFrame: 3);
        reader.Read(profiler, -1);
        Assert.Multiple(() =>
        {
            Assert.That(reader.Frames, Is.EqualTo(3));
            Assert.That(reader.WorstWork.Number, Is.EqualTo(1));
            Assert.That(reader.WorstWork.Rows.Single(r => r.Path == "Frame / Work").Sample.Bytes, Is.EqualTo(100));
            Assert.That(reader.WorstAllocation.Number, Is.EqualTo(2));
            Assert.That(reader.WorstWall.Number, Is.EqualTo(3), "Waiting can produce a slow wall interval with little profiled work.");
        });
        reader.ResetWindow();
        EmitFrame(profiler, 4, 0.002f, 5);
        reader.Read(profiler, -1);
        Assert.Multiple(() =>
        {
            Assert.That(reader.Frames, Is.EqualTo(1));
            Assert.That(reader.TotalAllocatedBytes, Is.EqualTo(5));
            Assert.That(reader.WorstWork.Number, Is.EqualTo(4));
            Assert.That(reader.Scopes.Single(s => s.Path == "Frame / Work").Window.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void PartialFrameIsNotReadAndDiagnosticFrameIsExcluded()
    {
        var profiler = CreateProfiler();
        EmitFrame(profiler, 1, 2f, 90000);
        var reader = new CMUClientProfileReader(0);
        var start = BeginFrame(profiler, 2, out var root);
        profiler.WriteValue("Work", Timing(0.005f, 30));
        reader.Read(profiler, 1);
        Assert.Multiple(() =>
        {
            Assert.That(reader.Frames, Is.Zero);
            Assert.That(reader.ExcludedFrames, Is.EqualTo(1));
        });
        EndFrame(profiler, start, root, 0.006f, 40);
        reader.Read(profiler, 1);
        Assert.That(reader.WorstWork.Number, Is.EqualTo(2));
        Assert.That(reader.TotalAllocatedBytes, Is.EqualTo(40));
    }

    [Test]
    public void OverwrittenFramesAreReportedAndNeverParsedAsLiveData()
    {
        var profiler = CreateProfiler(logSize: 32, indexSize: 8);
        for (var i = 0; i < 10; i++)
            EmitFrame(profiler, i, 0.001f, 1);
        var reader = new CMUClientProfileReader(0);
        reader.Read(profiler, -1);
        Assert.Multiple(() =>
        {
            Assert.That(reader.Frames + reader.LostFrames, Is.EqualTo(10));
            Assert.That(reader.LostFrames, Is.GreaterThan(0));
            Assert.That(reader.InvalidFrames, Is.Zero);
            Assert.That(reader.Scopes.Single(s => s.Path == "Frame / Work").Window.Count, Is.EqualTo(reader.Frames));
        });
    }

    [Test]
    public void CaptureBufferRetainsBusyFrameWhileNextFrameIsBeingWritten()
    {
        var profiler = CreateProfiler(logSize: CMUClientPerformanceSystem.MinimumProfileLogSize);
        var start = BeginFrame(profiler, 1, out var root);
        for (var i = 0; i < 45000; i++)
            profiler.WriteValue("Count", i);
        EndFrame(profiler, start, root, 0.04f, 12000);

        var nextStart = BeginFrame(profiler, 2, out var nextRoot);
        for (var i = 0; i < 45000; i++)
            profiler.WriteValue("Count", i);
        var reader = new CMUClientProfileReader(0);
        reader.Read(profiler, -1);
        Assert.That(reader.LostFrames, Is.Zero);
        Assert.That(reader.Frames, Is.EqualTo(1));
        Assert.That(reader.WorstWork.Detailed, Is.True);
        Assert.That(reader.TotalAllocatedBytes, Is.EqualTo(12000));

        EndFrame(profiler, nextStart, nextRoot, 0.05f, 14000);
        reader.Read(profiler, -1);
        Assert.That(reader.LostFrames, Is.Zero);
        Assert.That(reader.Frames, Is.EqualTo(2));
        Assert.That(reader.TotalAllocatedBytes, Is.EqualTo(26000));
    }

    [Test]
    public void OversizedStallRetainsRootTimingAndExplicitlyReportsMissingDetails()
    {
        var profiler = CreateProfiler(logSize: 131072);
        var start = BeginFrame(profiler, 1, out var root);
        for (var i = 0; i < CMUClientProfileReader.MaxEventsPerFrame; i++)
            profiler.WriteValue("Count", i);
        EndFrame(profiler, start, root, 0.5f, 5000);
        var reader = new CMUClientProfileReader(0);
        reader.Read(profiler, -1);
        Assert.Multiple(() =>
        {
            Assert.That(reader.OversizedFrames, Is.EqualTo(1));
            Assert.That(reader.WorstWork.WorkMs, Is.EqualTo(500));
            Assert.That(reader.WorstWork.Detailed, Is.False);
            Assert.That(reader.WorstWork.Rows, Is.Empty);
            Assert.That(reader.Scopes, Is.Empty);
        });
    }

    [Test]
    public void MalformedNestingCannotPolluteTheNextFrame()
    {
        var profiler = CreateProfiler();
        var start = BeginFrame(profiler, 1, out var root);
        profiler.WriteGroupEnd(root, "Unmatched", Timing(0.002f, 500));
        EndFrame(profiler, start, root, 0.004f, 600);
        EmitFrame(profiler, 2, 0.001f, 5);
        var reader = new CMUClientProfileReader(0);
        reader.Read(profiler, -1);
        Assert.Multiple(() =>
        {
            Assert.That(reader.InvalidFrames, Is.EqualTo(1));
            Assert.That(reader.Scopes.Single(s => s.Path == "Frame").Window.Count, Is.EqualTo(1));
            Assert.That(reader.Scopes.Single(s => s.Path == "Frame / Work").Window.Bytes, Is.EqualTo(5));
        });
    }

    [TestCase("start", "0", "33")]
    [TestCase("start", "1801", "33")]
    [TestCase("start", "10", "NaN")]
    [TestCase("start", "10", "Infinity")]
    [TestCase("start", "10", "-1")]
    [TestCase("start", "10", "0")]
    [TestCase("start", "10", "10001")]
    [TestCase("stop", "10", "33")]
    [TestCase("typo", "10", "33")]
    public void InvalidArgumentsAreRejectedBeforeStarting(string action, string seconds, string ms)
    {
        Assert.That(CMUClientPerformanceCommand.TryParse([action, seconds, ms], out _, out _, out _), Is.False);
    }

    [Test]
    public void NoArgumentsStartATwoMinuteCapture()
    {
        Assert.That(CMUClientPerformanceCommand.TryParse([], out var action, out var seconds, out var ms), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(action, Is.EqualTo("start"));
            Assert.That(seconds, Is.EqualTo(120));
            Assert.That(ms, Is.EqualTo(1000d / 30));
        });
    }

    private static ProfManager CreateProfiler(int logSize = 4096, int indexSize = 32)
    {
        var profiler = new ProfManager
        {
            Buffer = new ProfBuffer { LogBuffer = new ProfLog[logSize], IndexBuffer = new ProfIndex[indexSize] },
        };
        // The engine owns initialization. Enable its real writer here without booting a renderer/network stack.
        typeof(ProfManager).GetProperty(nameof(ProfManager.IsEnabled))!.SetValue(profiler, true);
        return profiler;
    }

    private static ProfValue Timing(float seconds, long bytes) => new()
    {
        Type = ProfValueType.TimeAllocSample,
        TimeAllocSample = new TimeAndAllocSample { Time = seconds, Alloc = bytes },
    };

    private static long BeginFrame(ProfManager profiler, long number, out long root)
    {
        var start = profiler.WriteValue("Start Frame", number);
        root = profiler.WriteGroupStart();
        return start;
    }

    private static void EndFrame(ProfManager profiler, long start, long root, float seconds, long bytes)
    {
        profiler.WriteGroupEnd(root, "Frame", Timing(seconds, bytes));
        profiler.MarkIndex(start, ProfIndexType.Frame);
    }

    private static void EmitFrame(ProfManager profiler, long number, float seconds, long bytes)
    {
        var start = BeginFrame(profiler, number, out var root);
        profiler.WriteValue("Work", Timing(seconds, bytes));
        EndFrame(profiler, start, root, seconds, bytes);
    }
}
