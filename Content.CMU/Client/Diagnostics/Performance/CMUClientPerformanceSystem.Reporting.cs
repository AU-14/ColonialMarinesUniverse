using System.Linq;
using System.Text;
using Content.Client.CMU14.ZLevels.Lighting;
using Content.Client.Viewport;
using Content.Shared.CMU14.ZLevels;
using Robust.Client.GameObjects;
using Robust.Shared;

namespace Content.Client.CMU14.Diagnostics.Performance;

public sealed partial class CMUClientPerformanceSystem
{
    private const int TopRows = 30;
    private const int MaxInventoryEntities = 50000;
    private const int MaxInventoryComponents = 300000;
    private Dictionary<string, int>? _inventoryBaseline;

    private static void AppendProfile(StringBuilder text, CMUClientProfileReader reader, long wallFrame)
    {
        var rows = reader.Scopes.Where(s => s.Window.Count > 0)
            .Select(s => new CMUClientProfileReader.Row(s.Path, s.Window)).ToArray();
        AppendRows(text, "window", rows, reader.Frames);
        AppendFrame(text, "worst-work", reader.WorstWork);
        if (reader.WorstAllocation?.Number != reader.WorstWork?.Number)
            AppendFrame(text, "worst-allocation", reader.WorstAllocation);
        else
            text.AppendLine("worst-allocation: same frame as worst-work");
        if (reader.WorstWall == null || reader.WorstWall.Number != wallFrame)
            text.AppendLine("worst-wall: corresponding profiler detail unavailable");
        else if (reader.WorstWall.Number != reader.WorstWork?.Number && reader.WorstWall.Number != reader.WorstAllocation?.Number)
            AppendFrame(text, "worst-wall", reader.WorstWall);
        else
            text.AppendLine($"worst-wall: frame={reader.WorstWall.Number}; detail already shown above");
    }

    private static void AppendFrame(StringBuilder text, string label, CMUClientProfileReader.Frame? frame)
    {
        if (frame == null)
        {
            text.AppendLine($"{label}: no completed profiler frames available");
            return;
        }
        text.AppendLine($"{label}: frame={frame.Number} workMs={F(frame.WorkMs)} allocatedBytes={frame.AllocatedBytes} detailed={frame.Detailed}");
        AppendRows(text, label, frame.Rows, 1);
    }

    private static void AppendRows(StringBuilder text, string label, CMUClientProfileReader.Row[] rows, int frames)
    {
        var timings = rows.Where(r => r.Sample.Timing).ToArray();
        // Include isolated stalls and allocation hotspots even if their cumulative time is low.
        var selected = timings.OrderByDescending(r => r.Sample.TotalMs).Take(TopRows)
            .Union(timings.OrderByDescending(r => r.Sample.MaxMs).Take(TopRows))
            .Union(timings.OrderByDescending(r => r.Sample.Bytes).Take(TopRows))
            .OrderByDescending(r => r.Sample.TotalMs).ToArray();
        text.AppendLine($"{label} timing-scopes: shown={selected.Length} omitted={timings.Length - selected.Length}; inclusive, bytes=main-thread allocations");
        foreach (var row in selected)
        {
            var s = row.Sample;
            text.AppendLine($"  {row.Path} | calls={s.Count} totalMs={F(s.TotalMs)} msPerFrame={F(s.TotalMs / Math.Max(1, frames))} maxCallMs={F(s.MaxMs)} bytes={s.Bytes} maxCallBytes={s.MaxBytes}");
        }

        var counters = rows.Where(r => !r.Sample.Timing).OrderBy(r => r.Path, StringComparer.Ordinal).ToArray();
        text.AppendLine($"{label} counters: shown={Math.Min(128, counters.Length)} omitted={Math.Max(0, counters.Length - 128)}; last/max are individual readings, sum includes repeated viewports/prediction ticks");
        foreach (var row in counters.Take(128))
        {
            var s = row.Sample;
            text.AppendLine($"  {row.Path} | samples={s.Count} sum={s.CounterTotal} max={s.CounterMax} last={s.CounterLast}");
        }
    }

    private void AppendSettings(StringBuilder text)
    {
        text.AppendLine($"settings: vsync={_config.GetCVar(CVars.DisplayVSync)} maxFps={_config.GetCVar(CVars.DisplayMaxFPS)} rendererSetting={_config.GetCVar(CVars.DisplayRenderer)} lightResolutionScale={F(_config.GetCVar(CVars.LightResolutionScale))} prediction={_gameStates.IsPredictionEnabled}");
        text.AppendLine($"z-settings: enabled={_config.GetCVar(CMUZLevelsCVars.Enabled)} render={_config.GetCVar(CMUZLevelsCVars.RenderEnabled)} diagnostics={_config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled)} maxDepth={_config.GetCVar(CMUZLevelsCVars.MaxRenderDepth)} maxOpeningRects={_config.GetCVar(CMUZLevelsCVars.MaxOpeningRectsPerPass)} blur={_config.GetCVar(CMUZLevelsCVars.BlurEnabled)} dynamicCull={_config.GetCVar(CMUZLevelsCVars.CullOccludedDynamicSprites)} projectedLights={_config.GetCVar(CMUZLevelsCVars.ProjectedLightingEnabled)} maxProjectedLights={_config.GetCVar(CMUZLevelsCVars.MaxProjectedLightsPerLevel)} lowerSources={_config.GetCVar(CMUZLevelsCVars.ProjectedLightingLowerSources)} lowerReceivers={_config.GetCVar(CMUZLevelsCVars.ProjectedLightingLowerReceivers)}");
        text.AppendLine("active-overlays: " + string.Join(", ", _overlays.AllOverlays.Select(o => o.GetType().Name).OrderBy(n => n, StringComparer.Ordinal)));
    }

    private void AppendContext(StringBuilder text, string label)
    {
        text.AppendLine($"context={label} observedFrame={_timing.CurFrame} tick={_timing.CurTick} processedTick={_timing.LastProcessedTick} realTick={_timing.LastRealTick} tickRate={_timing.TickRate} tickBacklogMs={F(_timing.TickRemainderRealtime.TotalMilliseconds)} timingAdjustment={F(_timing.TickTimingAdjustment)} engineAvgFps={F(_timing.FramesPerSecondAvg)} focused={_clyde.IsFocused} screen={_clyde.ScreenSize} entities={EntityManager.EntityCount}");
        text.AppendLine($"network-context: connected={_network.IsConnected} pingMs={_network.ServerChannel?.Ping ?? -1} stateBuffer={_gameStates.StateCount} targetBuffer={_gameStates.TargetBufferSize} minBuffer={_gameStates.MinBufferSize} mergeThreshold={_gameStates.StateBufferMergeThreshold}");
        // These are the last viewport sample and latest lighting update, not necessarily the same frame.
        // Keep sequence numbers and explicitly label observation time instead of claiming exact attribution.
        var z = ScalingViewport.LastZRenderDebugStats;
        text.AppendLine($"z-last-viewport: sequence={z.Sequence} reason={z.SkipReason} used={z.UsedZRender} map={z.BaseMapId} lookUp={z.ViewerLookUp} stairPreview={z.StairPreviewUp} worldArea={F(z.ViewportWorldArea)} bounds={z.ViewportWorldAabb}");
        text.AppendLine($"z-openings: ran={z.OpeningQueryRan} found={z.OpeningQueryFoundOpening} beforeLos={z.OpeningsBeforeLos} afterLos={z.OpeningsAfterLos} losChecks={z.OpeningLosChecks} losMode={z.OpeningLosMode} truncated={z.OpeningBoundsTruncated} conservative={z.OpeningLosConservativeFallback} areaBefore={F(z.OpeningAreaBeforeLos)} areaAfter={F(z.OpeningAreaAfterLos)}");
        text.AppendLine($"z-passes: base={z.BasePassRendered} lower={z.LowerPassesRendered} upper={z.UpperPassesRendered} stairComposites={z.StairPreviewCompositesRendered} lowestDepth={z.LowestDepth} lowerGate={z.LowerSuppressedByOpeningGate} lowerGrace={z.LowerRenderGraceActive} depthChecks={z.LowerDepthsChecked} cullCandidates={z.SpriteCullCandidates} culled={z.SpritesCulled} stairTiles={z.StairPreviewTilesExamined} stairLosChecks={z.StairPreviewLosChecks}");
        text.AppendLine($"z-ms: total={F(z.TotalRenderMs)} base={F(z.BaseRenderMs)} lower={F(z.LowerRenderMs)} upper={F(z.UpperRenderMs)} stair={F(z.StairPreviewRenderMs)} opening={F(z.OpeningQueryTotalMs)} los={F(z.OpeningLosMs)} lowerDiscovery={F(z.LowerDepthDiscoveryMs)} lowerOpening={F(z.LowerDepthOpeningQueryMs)}");
        var p = CMUZLevelProjectedLightingSystem.LastProjectedLightingDebugStats;
        text.AppendLine($"projected-lighting-latest: sequence={p.Sequence} reason={p.SkipReason} ran={p.Ran} sourceMaps={p.SourceMapsChecked} sourceQueries={p.SourceQueries} scanned={p.LightsScanned} accepted={p.LightsAccepted} rejectedCap={p.LightsRejectedBySourceCap} rejectedOpenings={p.LightsRejectedByOpeningBounds} openingSearches={p.OpeningSearches} checks={p.TransmissionChecks} raycasts={p.Raycasts} candidates={p.Candidates} applied={p.ProjectedLightsApplied} active={p.ActiveProjectedLights} cleanup={p.CleanupCount} graceHeld={p.ProjectedLightsHeldByVisibilityGrace}");
        text.AppendLine($"projected-portals: builds={p.PortalLightQueryBuilds} bounds={p.PortalLightQueryBounds} lightQueries={p.PortalLightQueries} accepted={p.PortalLightsAccepted} skippedSearches={p.OpeningSearchesSkippedByPortal} sourceMapSkips={p.SourceMapsSkippedByRenderVisibility} lowerSourceSkips={p.LowerSourcePassesSkippedByRenderVisibility} lowerReceiverSkips={p.LowerReceiverPassesSkippedByRenderVisibility}");
        text.AppendLine($"projected-ms: total={F(p.TotalMs)} opening={F(p.CurrentOpeningMs)} sourceQuery={F(p.SourceQueryMs)} candidate={F(p.CandidateMs)}");
    }

    private void AppendInventory(StringBuilder text)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var entities = 0;
        var components = 0;
        var truncated = false;
        var sprites = 0;
        var visibleSprites = 0;
        var lights = 0;
        foreach (var uid in EntityManager.GetEntities())
        {
            if (entities >= MaxInventoryEntities || components >= MaxInventoryComponents)
            {
                truncated = true;
                break;
            }
            entities++;
            if (TryComp(uid, out MetaDataComponent? meta))
                Increment(counts, $"prototype/{meta.EntityPrototype?.ID ?? "<none>"}");
            if (TryComp(uid, out TransformComponent? transform))
                Increment(counts, $"map/{transform.MapID}");
            if (TryComp(uid, out SpriteComponent? sprite))
            {
                sprites++;
                if (sprite.Visible && !sprite.ContainerOccluded)
                    visibleSprites++;
            }
            if (HasComp<PointLightComponent>(uid))
                lights++;
            foreach (var component in AllComps(uid))
            {
                if (component.Deleted)
                    continue;
                if (components >= MaxInventoryComponents)
                {
                    truncated = true;
                    break;
                }
                components++;
                Increment(counts, component.GetType().Name);
            }
        }
        text.AppendLine($"inventory: entities={entities} components={components} truncated={truncated} sprites={sprites} visibleNonContainerSprites={visibleSprites} lightComponents={lights}; loaded client entities, not on-screen counts. Deltas compare consecutive complete inventories.");
        foreach (var category in new[] { "prototype/", "map/", "components" })
        {
            bool Matches(string key) => category == "components" ? !key.Contains('/') : key.StartsWith(category, StringComparison.Ordinal);
            var keys = counts.Keys.AsEnumerable();
            if (_inventoryBaseline != null && !truncated)
                keys = keys.Union(_inventoryBaseline.Keys);
            var rows = keys.Where(Matches).Select(key => (Key: key, Count: counts.GetValueOrDefault(key),
                Delta: _inventoryBaseline == null || truncated ? 0 : counts.GetValueOrDefault(key) - _inventoryBaseline.GetValueOrDefault(key))).ToArray();
            var selected = rows.OrderByDescending(r => r.Count).Take(TopRows)
                .Union(rows.OrderByDescending(r => Math.Abs(r.Delta)).Take(TopRows)).ToArray();
            text.AppendLine($"inventory-{category}: shown={selected.Length} omitted={rows.Length - selected.Length} deltaAvailable={_inventoryBaseline != null && !truncated}");
            foreach (var row in selected)
                text.AppendLine($"  {row.Key} count={row.Count} delta={row.Delta}");
        }
        _inventoryBaseline = truncated ? null : counts;
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }
}
