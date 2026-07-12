using Content.Client.Viewport;
using Content.Client._CMU14.ZLevels.Lighting;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Client._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelRenderDebugCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IConfigurationManager _config = default!;

    private readonly HashSet<EntityUid> _entityScratch = new();
    private readonly HashSet<Entity<SpriteComponent>> _spriteScratch = new();
    private readonly HashSet<Entity<PointLightComponent>> _lightScratch = new();

    public string Command => "cmu_zrender_debug";
    public string Description => "Reports the last CMU multi-Z render decision for the last ScalingViewport frame.";
    public string Help => "Usage: cmu_zrender_debug [counts]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var stats = ScalingViewport.LastZRenderDebugStats;
        var counts = args.Length > 0 && args[0].Equals("counts", StringComparison.OrdinalIgnoreCase);

        if (args.Length > 0 && !counts)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine("CMU Z render debug:");
        shell.WriteLine($"  sample: #{stats.Sequence}, result={stats.SkipReason}");
        shell.WriteLine(
            $"  cvars: enabled={_config.GetCVar(CMUZLevelsCVars.Enabled)}, " +
            $"render={_config.GetCVar(CMUZLevelsCVars.RenderEnabled)}, " +
            $"maxDepth={_config.GetCVar(CMUZLevelsCVars.MaxRenderDepth)}, " +
            $"maxOpeningRects={_config.GetCVar(CMUZLevelsCVars.MaxOpeningRectsPerPass)}");
        shell.WriteLine(
            $"  cvars perf: blur={_config.GetCVar(CMUZLevelsCVars.BlurEnabled)}, " +
            $"dynamicCull={_config.GetCVar(CMUZLevelsCVars.CullOccludedDynamicSprites)}, " +
            $"lowerGrace={_config.GetCVar(CMUZLevelsCVars.LowerRenderVisibilityGrace):F2}s, " +
            $"projectedLights={_config.GetCVar(CMUZLevelsCVars.ProjectedLightingEnabled)}, " +
            $"projectedGrace={_config.GetCVar(CMUZLevelsCVars.ProjectedLightingVisibilityGrace):F2}s, " +
            $"maxProjectedLights={_config.GetCVar(CMUZLevelsCVars.MaxProjectedLightsPerLevel)}, " +
            $"projectLowerSources={_config.GetCVar(CMUZLevelsCVars.ProjectedLightingLowerSources)}, " +
            $"projectLowerReceivers={_config.GetCVar(CMUZLevelsCVars.ProjectedLightingLowerReceivers)}");
        shell.WriteLine(
            $"  viewer: usedZRender={stats.UsedZRender}, map={stats.BaseMapId}, " +
            $"lookUp={stats.ViewerLookUp}, stairPreviewUp={stats.StairPreviewUp}, " +
            $"lookUpDepth={stats.LookUpDepth}");
        shell.WriteLine(
            $"  openings: ran={stats.OpeningQueryRan}, found={stats.OpeningQueryFoundOpening}, " +
            $"beforeLos={stats.OpeningsBeforeLos}, losChecks={stats.OpeningLosChecks}, " +
            $"afterLos={stats.OpeningsAfterLos}, removedByLos={stats.OpeningsRemovedByLos}");
        shell.WriteLine(
            $"  opening fallback: mode={stats.OpeningLosMode}, noBounds={stats.OpeningQueryConservativeNoBounds}, " +
            $"truncated={stats.OpeningBoundsTruncated}, " +
            $"losConservative={stats.OpeningLosConservativeFallback}, " +
            $"visibleCurrent={stats.VisibleCurrentOpenings}");
        shell.WriteLine(
            $"  opening geometry: viewArea={stats.ViewportWorldArea:F1}, " +
            $"openingAreaBefore={stats.OpeningAreaBeforeLos:F1} ({FormatPercent(stats.OpeningAreaBeforeLos, stats.ViewportWorldArea)}), " +
            $"openingAreaAfter={stats.OpeningAreaAfterLos:F1} ({FormatPercent(stats.OpeningAreaAfterLos, stats.ViewportWorldArea)})");
        shell.WriteLine(
            $"  render bounds: zOffsetPerDepth=({stats.ZRenderOffsetPerDepth.X:F2}, {stats.ZRenderOffsetPerDepth.Y:F2})");
        shell.WriteLine(
            $"  lower: hasMap={stats.HasLowerMap}, " +
            $"suppressedByOpeningGate={stats.LowerSuppressedByOpeningGate}, " +
            $"lowestDepth={stats.LowestDepth}, renderedPasses={stats.LowerPassesRendered}, " +
            $"renderedDepths={FormatDepths(stats.LowerRenderedDepths)}");
        shell.WriteLine(
            $"  lower grace: active={stats.LowerRenderGraceActive}, " +
            $"depth={stats.LowerRenderGraceLowestDepth}, configured={stats.LowerRenderGraceSeconds:F2}s, " +
            $"remaining={stats.LowerRenderGraceRemainingMs:F0}ms");
        shell.WriteLine(
            $"  lower discovery: checked={stats.LowerDepthsChecked}, maps={stats.LowerDepthsWithMaps}, " +
            $"breakDepth={stats.LowerDepthBreakDepth}, openingLosChecks={stats.LowerDepthOpeningLosChecks}");
        shell.WriteLine(
            $"  other passes: base={stats.BasePassRendered}, upper={stats.UpperPassesRendered}, " +
            $"stairPreviewComposite={stats.StairPreviewCompositesRendered}");
        shell.WriteLine(
            $"  timings ms: total={stats.TotalRenderMs:F2}, opening={stats.OpeningQueryTotalMs:F2}, " +
            $"currentOpen={stats.CurrentOpeningQueryMs:F2}, los={stats.OpeningLosMs:F2}, " +
            $"lowerDiscover={stats.LowerDepthDiscoveryMs:F2}, lowerOpen={stats.LowerDepthOpeningQueryMs:F2}");
        shell.WriteLine(
            $"  render ms: base={stats.BaseRenderMs:F2}, lower={stats.LowerRenderMs:F2}, " +
            $"upper={stats.UpperRenderMs:F2}, stairPreview={stats.StairPreviewRenderMs:F2}, " +
            $"lowerShare={FormatPercent(stats.LowerRenderMs, stats.TotalRenderMs)}");
        PrintProjectedLighting(shell);

        if (!counts)
        {
            shell.WriteLine("  counts: run `cmu_zrender_debug counts` for heavier per-map candidate counts.");
            return;
        }

        PrintCounts(shell, stats);
    }

    private static void PrintProjectedLighting(IConsoleShell shell)
    {
        var stats = CMUZLevelProjectedLightingSystem.LastProjectedLightingDebugStats;
        shell.WriteLine(
            $"  projected lighting: sample=#{stats.Sequence}, result={stats.SkipReason}, " +
            $"ran={stats.Ran}, visibleCurrentOpenings={stats.VisibleCurrentOpenings}, " +
            $"upperSourceOpenings={stats.UpperSourceOpenings}, bounds={stats.CurrentOpeningBounds}, " +
            $"boundsComplete={stats.CurrentOpeningBoundsComplete}");
        shell.WriteLine(
            $"  projected opening gate: found={stats.CurrentOpeningQueryFoundOpening}, " +
            $"losMode={stats.CurrentOpeningLosMode}, losChecks={stats.CurrentOpeningLosChecks}, " +
            $"truncated={stats.CurrentOpeningBoundsTruncated}, " +
            $"fromGrace={stats.CurrentOpeningBoundsFromGrace}, " +
            $"graceRemaining={stats.CurrentOpeningGraceRemainingMs:F0}ms, " +
            $"conservative={stats.CurrentOpeningLosConservativeFallback}");
        shell.WriteLine(
            $"  projected render gate: valid={stats.RenderVisibilityGateValid}, " +
            $"renderedLowerDepths={FormatDepths(stats.RenderedLowerDepths)}, " +
            $"sourceMapSkips={stats.SourceMapsSkippedByRenderVisibility}, " +
            $"lowerSourcePassSkips={stats.LowerSourcePassesSkippedByRenderVisibility}, " +
            $"lowerReceiverPassSkips={stats.LowerReceiverPassesSkippedByRenderVisibility}");
        shell.WriteLine(
            $"  projected candidates: sourceMaps={stats.SourceMapsChecked}, queries={stats.SourceQueries}, " +
            $"lightsScanned={stats.LightsScanned}, lightsAccepted={stats.LightsAccepted}, " +
            $"lightsRejectedByCap={stats.LightsRejectedBySourceCap}, " +
            $"lightsRejectedByOpenings={stats.LightsRejectedByOpeningBounds}, " +
            $"openingSearches={stats.OpeningSearches}, openingsFound={stats.OpeningsFound}, " +
            $"openingsRejected={stats.OpeningsRejectedByCurrentView}, " +
            $"openingsRejectedByCap={stats.OpeningsRejectedBySourceCap}");
        shell.WriteLine(
            $"  projected portal path: lightBounds={stats.PortalLightQueryBounds}, " +
            $"candidateBounds={stats.PortalOpeningCandidateBounds}, " +
            $"lightQueries={stats.PortalLightQueries}, portalLightsAccepted={stats.PortalLightsAccepted}, " +
            $"openingSearchesSkipped={stats.OpeningSearchesSkippedByPortal}, " +
            $"portalOpeningCandidates={stats.PortalOpeningCandidates}");
        shell.WriteLine(
            $"  projected work: raycasts={stats.Raycasts}, candidates={stats.Candidates}, " +
            $"applied={stats.ProjectedLightsApplied}, active={stats.ActiveProjectedLights}, " +
            $"heldByGrace={stats.ProjectedLightsHeldByVisibilityGrace}, " +
            $"cleanup={stats.CleanupCount}, grace={stats.VisibilityGraceSeconds:F2}s");
        shell.WriteLine(
            $"  projected ms: total={stats.TotalMs:F2}, currentOpen={stats.CurrentOpeningMs:F2}, " +
            $"sourceQuery={stats.SourceQueryMs:F2}, candidates={stats.CandidateMs:F2}");
    }

    private void PrintCounts(IConsoleShell shell, ScalingViewport.ZLevelRenderDebugStats stats)
    {
        if (stats.BaseMapUid is not { } baseMapUid ||
            stats.BaseMapId == MapId.Nullspace ||
            stats.ViewportWorldArea <= 0f)
        {
            shell.WriteLine("  counts: no valid last viewport bounds.");
            return;
        }

        shell.WriteLine("  counts:");
        var baseCounts = PrintMapCounts(shell, "base", 0, stats.BaseMapId, stats.ViewportWorldAabb, rendered: true);
        var lowerTotal = new CountStats();
        var renderedLowerTotal = new CountStats();
        var lowerMaps = 0;
        var renderedLowerMaps = 0;

        var zLevels = _entities.System<CMUClientZLevelsSystem>();
        var maxDepth = Math.Clamp(
            stats.MaxDepth,
            0,
            CMUSharedZLevelsSystem.MaxZLevelsBelowRendering);

        for (var depth = -1; depth >= -maxDepth; depth--)
        {
            if (!zLevels.TryMapOffset(baseMapUid, depth, out _, out var mapComp) ||
                mapComp.MapId == MapId.Nullspace)
            {
                continue;
            }

            lowerMaps++;
            var rendered = stats.LowerRenderedDepths.Contains(depth);
            var lowerBounds = stats.ViewportWorldAabb.Translated(stats.ZRenderOffsetPerDepth * depth);
            var counts = PrintMapCounts(
                shell,
                "lower",
                depth,
                mapComp.MapId,
                lowerBounds,
                rendered);

            lowerTotal.Add(counts);
            if (rendered)
            {
                renderedLowerMaps++;
                renderedLowerTotal.Add(counts);
            }
        }

        shell.WriteLine(
            $"    summary base: entities={baseCounts.Entities}, sprites={baseCounts.Sprites}, " +
            $"visibleSprites={baseCounts.VisibleSprites}, enabledLights={baseCounts.EnabledLights}");
        shell.WriteLine(
            $"    summary lower all maps={lowerMaps}: entities={lowerTotal.Entities}, sprites={lowerTotal.Sprites}, " +
            $"visibleSprites={lowerTotal.VisibleSprites}, enabledLights={lowerTotal.EnabledLights}");
        shell.WriteLine(
            $"    summary lower rendered maps={renderedLowerMaps}: entities={renderedLowerTotal.Entities}, " +
            $"sprites={renderedLowerTotal.Sprites}, visibleSprites={renderedLowerTotal.VisibleSprites}, " +
            $"enabledLights={renderedLowerTotal.EnabledLights}");
    }

    private CountStats PrintMapCounts(
        IConsoleShell shell,
        string label,
        int depth,
        MapId mapId,
        Box2 bounds,
        bool rendered)
    {
        var lookup = _entities.System<EntityLookupSystem>();
        var xforms = _entities.GetEntityQuery<TransformComponent>();

        _entityScratch.Clear();
        _spriteScratch.Clear();
        _lightScratch.Clear();

        lookup.GetEntitiesIntersecting(mapId, bounds, _entityScratch, LookupFlags.All);
        lookup.GetEntitiesIntersecting(mapId, bounds, _spriteScratch, LookupFlags.All);
        lookup.GetEntitiesIntersecting(mapId, bounds, _lightScratch, LookupFlags.All);

        var visibleSprites = 0;
        var anchoredSprites = 0;
        foreach (var sprite in _spriteScratch)
        {
            if (sprite.Comp.Visible)
                visibleSprites++;

            if (xforms.TryComp(sprite.Owner, out var xform) && xform.Anchored)
                anchoredSprites++;
        }

        var enabledLights = 0;
        foreach (var light in _lightScratch)
        {
            if (light.Comp.Enabled)
                enabledLights++;
        }

        shell.WriteLine(
            $"    {label} depth={depth}, map={mapId}, rendered={rendered}: " +
            $"area={GetArea(bounds):F1}, center=({bounds.Center.X:F1}, {bounds.Center.Y:F1}), " +
            $"entities={_entityScratch.Count}, sprites={_spriteScratch.Count}, " +
            $"visibleSprites={visibleSprites}, anchoredSprites={anchoredSprites}, " +
            $"dynamicSprites={_spriteScratch.Count - anchoredSprites}, " +
            $"lights={_lightScratch.Count}, enabledLights={enabledLights}");

        return new CountStats
        {
            Entities = _entityScratch.Count,
            Sprites = _spriteScratch.Count,
            VisibleSprites = visibleSprites,
            AnchoredSprites = anchoredSprites,
            Lights = _lightScratch.Count,
            EnabledLights = enabledLights,
        };
    }

    private static string FormatDepths(IReadOnlyList<int> depths)
    {
        return depths.Count == 0 ? "none" : string.Join(",", depths);
    }

    private static string FormatPercent(double value, double total)
    {
        return total <= 0d ? "n/a" : $"{value / total * 100d:F1}%";
    }

    private static float GetArea(Box2 bounds)
    {
        return Math.Max(0f, bounds.Width) * Math.Max(0f, bounds.Height);
    }

    private struct CountStats
    {
        public int Entities;
        public int Sprites;
        public int VisibleSprites;
        public int AnchoredSprites;
        public int Lights;
        public int EnabledLights;

        public void Add(CountStats other)
        {
            Entities += other.Entities;
            Sprites += other.Sprites;
            VisibleSprites += other.VisibleSprites;
            AnchoredSprites += other.AnchoredSprites;
            Lights += other.Lights;
            EnabledLights += other.EnabledLights;
        }
    }
}
