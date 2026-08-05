using Content.Client._CMU14.ZLevels.Lighting;
using Content.Client.Viewport;
using Robust.Shared.Console;
using Robust.Shared.Log;

namespace Content.Client._CMU14.ZLevels.Core;

/// <summary>
/// Opt-in live client sampler for CMU Multi-Z render validation.
/// </summary>
public sealed partial class CMUZLevelRenderCaptureSystem : EntitySystem
{
    private const int DefaultFrames = 600;
    private const int MaxFrames = 3600;

    private readonly RenderCaptureSample[] _samples = new RenderCaptureSample[MaxFrames];
    private int _targetFrames;
    private int _sampleCount;
    private int _lastRenderSequence;
    private string _label = string.Empty;

    public bool Active => _targetFrames > 0;

    public string Status =>
        Active
            ? $"{_label}: {_sampleCount}/{_targetFrames} frames"
            : "inactive";

    public bool Start(string label, int frames, out string error)
    {
        if (Active)
        {
            error = $"A capture is already active ({Status}).";
            return false;
        }

        _label = string.IsNullOrWhiteSpace(label) ? "unnamed" : label.Trim();
        _targetFrames = Math.Clamp(frames, 1, MaxFrames);
        _sampleCount = 0;
        _lastRenderSequence = ScalingViewport.LastZRenderDebugStats.Sequence;
        error = string.Empty;
        return true;
    }

    public void Stop()
    {
        _targetFrames = 0;
        _sampleCount = 0;
        _label = string.Empty;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!Active)
            return;

        var render = ScalingViewport.LastZRenderDebugStats;
        if (render.Sequence == _lastRenderSequence)
            return;

        _lastRenderSequence = render.Sequence;
        if (!render.UsedZRender)
            return;

        var projected = CMUZLevelProjectedLightingSystem.LastProjectedLightingDebugStats;
        var blur = CMUZLevelBlurOverlay.LastBlurDebugStats;
        _samples[_sampleCount++] = new RenderCaptureSample
        {
            FrameMs = frameTime * 1000d,
            RenderMs = render.TotalRenderMs,
            OpeningMs = render.OpeningQueryTotalMs,
            LowerRenderMs = render.LowerRenderMs,
            StairRenderMs = render.StairPreviewRenderMs,
            StairCullMs = render.StairPreviewCullMs,
            StairFovMaskMs = render.StairPreviewFovMaskMs,
            StairSpriteCandidates = render.StairPreviewSpriteCandidates,
            StairSpriteChecks = render.StairPreviewSpriteVisibilityChecks,
            StairTilesScanned = render.StairPreviewTilesScanned,
            StairTilesDrawn = render.StairPreviewTilesDrawn,
            StairLosChecks = render.StairPreviewLosChecks,
            LowerPasses = render.LowerPassesRendered,
            StairComposites = render.StairPreviewCompositesRendered,
            ProjectedMs = projected.TotalMs,
            ProjectedOpeningMs = projected.CurrentOpeningMs,
            ProjectedSourceMs = projected.SourceQueryMs,
            ProjectedCandidateMs = projected.CandidateMs,
            ProjectedRaycasts = projected.Raycasts,
            ProjectedCandidates = projected.Candidates,
            ProjectedApplied = projected.ProjectedLightsApplied,
            BlurPasses = blur.Passes,
            BlurDrawMs = blur.DrawMs,
        };

        if (_sampleCount < _targetFrames)
            return;

        WriteSummary();
        Stop();
    }

    private void WriteSummary()
    {
        var count = _sampleCount;
        Logger.InfoS(
            "cmu.zrender.capture",
            $"capture label={_label} frames={count} " +
            FormatMetric("frame_ms", count, static sample => sample.FrameMs) + " " +
            FormatMetric("render_ms", count, static sample => sample.RenderMs) + " " +
            FormatMetric("opening_ms", count, static sample => sample.OpeningMs) + " " +
            FormatMetric("lower_render_ms", count, static sample => sample.LowerRenderMs) + " " +
            FormatMetric("projected_ms", count, static sample => sample.ProjectedMs) + " " +
            FormatMetric("projected_opening_ms", count, static sample => sample.ProjectedOpeningMs) + " " +
            FormatMetric("projected_source_ms", count, static sample => sample.ProjectedSourceMs) + " " +
            FormatMetric("projected_candidate_ms", count, static sample => sample.ProjectedCandidateMs) + " " +
            FormatMetric("projected_rays", count, static sample => sample.ProjectedRaycasts) + " " +
            FormatMetric("projected_candidates", count, static sample => sample.ProjectedCandidates) + " " +
            FormatMetric("projected_applied", count, static sample => sample.ProjectedApplied));
        Logger.InfoS(
            "cmu.zrender.capture",
            $"capture label={_label} frames={count} " +
            FormatMetric("stair_render_ms", count, static sample => sample.StairRenderMs) + " " +
            FormatMetric("stair_cull_ms", count, static sample => sample.StairCullMs) + " " +
            FormatMetric("stair_fov_ms", count, static sample => sample.StairFovMaskMs) + " " +
            FormatMetric("stair_sprite_candidates", count, static sample => sample.StairSpriteCandidates) + " " +
            FormatMetric("stair_sprite_checks", count, static sample => sample.StairSpriteChecks) + " " +
            FormatMetric("stair_tiles_scanned", count, static sample => sample.StairTilesScanned) + " " +
            FormatMetric("stair_tiles_drawn", count, static sample => sample.StairTilesDrawn) + " " +
            FormatMetric("stair_los_checks", count, static sample => sample.StairLosChecks));
        Logger.InfoS(
            "cmu.zrender.capture",
            $"capture label={_label} frames={count} " +
            FormatMetric("lower_passes", count, static sample => sample.LowerPasses) + " " +
            FormatMetric("stair_composites", count, static sample => sample.StairComposites) + " " +
            FormatMetric("blur_passes", count, static sample => sample.BlurPasses) + " " +
            FormatMetric("blur_draw_ms", count, static sample => sample.BlurDrawMs));
    }

    private string FormatMetric(string name, int count, Func<RenderCaptureSample, double> selector)
    {
        var values = new double[count];
        var sum = 0d;
        for (var i = 0; i < count; i++)
        {
            var value = selector(_samples[i]);
            values[i] = value;
            sum += value;
        }

        Array.Sort(values);
        return $"{name}=" +
               $"p50:{Percentile(values, 0.50):F4}," +
               $"p95:{Percentile(values, 0.95):F4}," +
               $"mean:{sum / count:F4}," +
               $"max:{values[^1]:F4}";
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = (int) Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private struct RenderCaptureSample
    {
        public double FrameMs;
        public double RenderMs;
        public double OpeningMs;
        public double LowerRenderMs;
        public double StairRenderMs;
        public double StairCullMs;
        public double StairFovMaskMs;
        public double StairSpriteCandidates;
        public double StairSpriteChecks;
        public double StairTilesScanned;
        public double StairTilesDrawn;
        public double StairLosChecks;
        public double LowerPasses;
        public double StairComposites;
        public double ProjectedMs;
        public double ProjectedOpeningMs;
        public double ProjectedSourceMs;
        public double ProjectedCandidateMs;
        public double ProjectedRaycasts;
        public double ProjectedCandidates;
        public double ProjectedApplied;
        public double BlurPasses;
        public double BlurDrawMs;
    }
}

public sealed partial class CMUZLevelRenderCaptureCommand : IConsoleCommand
{
    private const int DefaultFrames = 600;
    private const int MaxFrames = 3600;

    [Dependency] private IEntityManager _entities = default!;

    public string Command => "cmu_zrender_capture";
    public string Description => "Captures a distribution of live CMU Multi-Z client rendering work.";
    public string Help => $"Usage: {Command} start <label> [frames<=3600] | status | stop";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = _entities.System<CMUZLevelRenderCaptureSystem>();
        if (args.Length == 1 && args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            shell.WriteLine(system.Status);
            return;
        }

        if (args.Length == 1 && args[0].Equals("stop", StringComparison.OrdinalIgnoreCase))
        {
            system.Stop();
            shell.WriteLine("CMU Z render capture stopped.");
            return;
        }

        if (args.Length is < 2 or > 3 ||
            !args[0].Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            shell.WriteError(Help);
            return;
        }

        var frames = DefaultFrames;
        if (args.Length == 3 &&
            (!int.TryParse(args[2], out frames) || frames is < 1 or > MaxFrames))
        {
            shell.WriteError(Help);
            return;
        }

        if (!system.Start(args[1], frames, out var error))
        {
            shell.WriteError(error);
            return;
        }

        shell.WriteLine($"Started CMU Z render capture '{args[1]}' for {frames} rendered frames.");
    }
}
