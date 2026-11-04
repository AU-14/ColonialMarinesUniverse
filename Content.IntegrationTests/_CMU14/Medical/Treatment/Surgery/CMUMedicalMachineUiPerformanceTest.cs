using Stopwatch = System.Diagnostics.Stopwatch;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.Surgery;

/// <summary>
/// Live BUI state application and actual style/layout processing in one headless client.
/// Does not measure server projection, transport, GPU drawing, or multiple network clients.
/// The same fixture compiles against both the preserved original and retained implementations.
/// </summary>
[TestFixture, Explicit]
public sealed class CMUMedicalMachineUiPerformanceTest : CMUMedicalMachineUiTestBase
{
    private const int Blocks = 20;
    private const int FramesPerBlock = 32;

    [TestCase(false, 1, "healthy")]
    [TestCase(false, 4, "healthy")]
    [TestCase(false, 8, "healthy")]
    [TestCase(true, 1, "healthy")]
    [TestCase(true, 4, "healthy")]
    [TestCase(true, 8, "healthy")]
    [TestCase(false, 1, "resize")]
    [TestCase(false, 4, "resize")]
    [TestCase(false, 8, "resize")]
    [TestCase(true, 1, "resize")]
    [TestCase(true, 4, "resize")]
    [TestCase(true, 8, "resize")]
    [TestCase(false, 1, "steady")]
    [TestCase(false, 4, "steady")]
    [TestCase(false, 8, "steady")]
    [TestCase(true, 1, "steady")]
    [TestCase(true, 4, "steady")]
    [TestCase(true, 8, "steady")]
    [TestCase(false, 1, "values")]
    [TestCase(false, 4, "values")]
    [TestCase(false, 8, "values")]
    [TestCase(true, 1, "values")]
    [TestCase(true, 4, "values")]
    [TestCase(true, 8, "values")]
    [TestCase(false, 1, "burst")]
    [TestCase(false, 4, "burst")]
    [TestCase(false, 8, "burst")]
    [TestCase(true, 1, "burst")]
    [TestCase(true, 4, "burst")]
    [TestCase(true, 8, "burst")]
    public async Task ApplyStatesAndProcessLayout(bool scanner, int windows, string workload)
    {
        var machines = new Machine[windows];
        for (var i = 0; i < machines.Length; i++) machines[i] = await OpenMachine(scanner);
        var states = new BoundUserInterfaceState[FramesPerBlock, windows];
        await Server.WaitPost(() =>
        {
            for (var frame = 0; frame < FramesPerBlock; frame++)
            for (var viewer = 0; viewer < windows; viewer++)
            {
                var projected = Projection(machines[viewer], workload is "steady" or "healthy" ? 0 : frame,
                    workload == "burst" && frame % 8 >= 4);
                if (workload == "healthy")
                {
                    if (projected is CMUAutodocBuiState autodoc)
                    {
                        autodoc.Queue.Clear();
                        autodoc.Running = false;
                        autodoc.CurrentStep = null;
                        autodoc.NextStepAt = null;
                        for (var i = 0; i < autodoc.Parts.Count; i++)
                            autodoc.Parts[i] = autodoc.Parts[i] with { ConditionSummary = "Healthy", EligibleSurgeries = [] };
                    }
                    else if (projected is CMUBodyScannerBuiState bodyScanner)
                    {
                        bodyScanner.Targets.Clear();
                        bodyScanner.CalibrationStartedAt = null;
                        bodyScanner.CalibrationEndsAt = null;
                        for (var i = 0; i < bodyScanner.ScanLines.Count; i++)
                            bodyScanner.ScanLines[i] = bodyScanner.ScanLines[i] with
                                { Severity = CMUBodyScannerScanSeverity.Stable, Detail = "Healthy", Details = [], Current = 100 };
                    }
                }
                states[frame, viewer] = projected;
            }
        });

        UiReport report = default!;
        await Client.WaitAssertion(() =>
        {
            var ui = CEntMan.System<SharedUserInterfaceSystem>();
            Enum key = scanner ? CMUBodyScannerUIKey.Key : CMUAutodocUIKey.Key;
            // Reflection only resolves the public method on Robust's internal concrete type.
            // Its real method processes styles, measure and arrange queues; no mock or per-call reflection.
            var method = UiMan.GetType().GetMethod("FrameUpdate", BindingFlags.Instance | BindingFlags.Public)!;
            var frameUpdate = method.CreateDelegate<Action<FrameEventArgs>>(UiMan);
            var frameArgs = new FrameEventArgs(1f / 60f);
            var samples = new UiSample[Blocks];
            var frameTicks = new long[Blocks * FramesPerBlock];
            var emptyTicks = new long[Blocks];

            void ApplyFrame(int frame)
            {
                for (var viewer = 0; viewer < windows; viewer++)
                {
                    if (workload == "resize")
                        machines[viewer].Window.SetSize = frame % 2 == 0
                            ? new Vector2(720, 480) : new Vector2(1000, 650);
                    // Distinct equivalent state instances prevent SetUiState's reference-equality shortcut.
                    // The state objects and nested collections are prepared before any timing or allocation sample.
                    ui.SetUiState(machines[viewer].ClientConsole, key, states[frame, viewer]);
                }
                frameUpdate(frameArgs);
            }

            for (var warmup = 0; warmup < 3; warmup++)
                for (var frame = 0; frame < FramesPerBlock; frame++) ApplyFrame(frame);
            ApplyFrame(0);
            frameUpdate(frameArgs);
            var retainedBefore = GC.GetTotalMemory(true);
            var beforeControls = machines.Sum(machine => Descendants<Control>(machine.Window).Count());
            for (var block = 0; block < Blocks; block++)
            {
                var emptyStart = Stopwatch.GetTimestamp();
                frameUpdate(frameArgs);
                emptyTicks[block] = Stopwatch.GetTimestamp() - emptyStart;
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var cpuBefore = ThreadCpuTicks();
                var wallBefore = Stopwatch.GetTimestamp();
                for (var frame = 0; frame < FramesPerBlock; frame++)
                {
                    var before = Stopwatch.GetTimestamp();
                    ApplyFrame(frame);
                    frameTicks[block * FramesPerBlock + frame] = Stopwatch.GetTimestamp() - before;
                }
                samples[block] = new UiSample(block,
                    (Stopwatch.GetTimestamp() - wallBefore) * 1000d / Stopwatch.Frequency,
                    CpuMilliseconds(cpuBefore, ThreadCpuTicks()),
                    GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
            }
            // Match the final graph/model with the pre-sample snapshot, then drain UI queues.
            ApplyFrame(0);
            frameUpdate(frameArgs);
            var retainedAfter = GC.GetTotalMemory(true);
            var afterControls = machines.Sum(machine => Descendants<Control>(machine.Window).Count());
            var visibleCharacters = machines.Sum(machine => Descendants<Robust.Client.UserInterface.Controls.Label>(machine.Window)
                .Where(label => label.VisibleInTree).Sum(label => label.TextMemory.Length));
            Assert.Multiple(() =>
            {
                Assert.That(afterControls, Is.EqualTo(beforeControls), "matched final state must have a bounded live control graph");
                Assert.That(visibleCharacters, Is.GreaterThan(0), "the workload must drive real populated windows");
                Assert.That(samples.All(sample => sample.AllocatedBytes >= 0), Is.True);
            });
            Array.Sort(frameTicks);
            Array.Sort(emptyTicks);
            report = new UiReport(scanner ? "scanner" : "autodoc", windows, workload,
                RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription,
                Blocks, FramesPerBlock, samples,
                PercentileMilliseconds(frameTicks, .50), PercentileMilliseconds(frameTicks, .95),
                PercentileMilliseconds(frameTicks, .99), PercentileMilliseconds(emptyTicks, .50),
                beforeControls, afterControls, retainedBefore, retainedAfter, visibleCharacters);
            GC.KeepAlive(states);
            GC.KeepAlive(machines);
        });
        TestContext.Progress.WriteLine("CMU_UI_REPORT " + JsonSerializer.Serialize(report));
    }

    private static double PercentileMilliseconds(long[] sorted, double percentile) =>
        sorted[Math.Clamp((int) Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)]
        * 1000d / Stopwatch.Frequency;

    private static ulong? ThreadCpuTicks()
    {
        if (!OperatingSystem.IsWindows() || !GetThreadTimes(GetCurrentThread(), out _, out _, out var kernel, out var user))
            return null;
        return kernel + user;
    }

    private static double? CpuMilliseconds(ulong? before, ulong? after) =>
        before is { } start && after is { } end ? (end - start) / 10000d : null;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(IntPtr thread, out ulong creation, out ulong exit, out ulong kernel, out ulong user);

    private sealed record UiSample(int Block, double WallMilliseconds, double? ThreadCpuMilliseconds, long AllocatedBytes);
    private sealed record UiReport(string Machine, int LogicalWindows, string Workload, string Runtime, string OS,
        int Blocks, int FramesPerBlock, UiSample[] Samples,
        double UpdateAndLayoutP50Milliseconds, double UpdateAndLayoutP95Milliseconds,
        double UpdateAndLayoutP99Milliseconds, double EmptyUiFrameP50Milliseconds,
        int ControlsBefore, int ControlsAfter, long ProcessRetainedBefore, long ProcessRetainedAfter, int VisibleCharacters);
}
