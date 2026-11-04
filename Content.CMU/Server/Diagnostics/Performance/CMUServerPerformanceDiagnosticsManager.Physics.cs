using System.Globalization;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Systems;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server.CMU14.Diagnostics.Performance;

public sealed partial class CMUServerPerformanceDiagnosticsManager
{
    private CMUPerformancePhysicsMetrics _physicsMetrics = default!;

    private void InitializePhysicsMetrics()
    {
        var controllers = _entitySystemManager.GetEntitySystemTypes()
            .Where(type => typeof(VirtualController).IsAssignableFrom(type))
            .Select(type => (VirtualController) _entitySystemManager.GetEntitySystem(type));
        _physicsMetrics = new(controllers.Select(controller =>
            (controller.GetType().Name, controller.BeforeMonitor, controller.AfterMonitor)), _timing.RealTime);
    }

    /// <summary>Explicitly requested census. Automatic reports only read controller counters.</summary>
    public bool CapturePhysicsReport()
    {
        if (!_initialized || !_enabled)
            return false;

        _sawmill.Warning($"[CMU-PERF] manual-physics {GetCorrelationContext()}");
        LogPhysicsStages();

        var started = Stopwatch.GetTimestamp();
        var groups = new Dictionary<(string Prototype, BodyType Type), PhysicsCounts>();
        var total = new PhysicsCounts();
        var query = _entityManager.AllEntityQueryEnumerator<PhysicsComponent, MetaDataComponent>();
        while (query.MoveNext(out var physics, out var meta))
        {
            var key = (meta.EntityPrototype?.ID ?? "none", physics.BodyType);
            groups.TryGetValue(key, out var counts);
            counts.Add(physics);
            groups[key] = counts;
            total.Add(physics);
        }

        _sawmill.Warning(Invariant(
            $"[CMU-PERF] physics-census incidentId={_activeIncidentId} bodies={total.Bodies} awake={total.Awake} ",
            $"canCollide={total.CanCollide} sleepDisabled={total.SleepDisabled} contactEdges={total.ContactEdges} ",
            $"groups={groups.Count} censusMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3} includesPaused=true source=manual-census"));
        foreach (var (key, counts) in groups.OrderByDescending(row => row.Value.ContactEdges)
                     .ThenByDescending(row => row.Value.Awake).ThenByDescending(row => row.Value.Bodies).Take(20))
        {
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] physics-bodies incidentId={_activeIncidentId} prototype={SanitizeName(key.Prototype)} bodyType={key.Type} ",
                $"bodies={counts.Bodies} awake={counts.Awake} canCollide={counts.CanCollide} ",
                $"sleepDisabled={counts.SleepDisabled} contactEdges={counts.ContactEdges} source=manual-census"));
        }
        return true;
    }

    private void LogPhysicsStages()
    {
        var window = _physicsMetrics.Drain(_timing.RealTime);
        var enabled = _entitySystemManager.GetEntitySystem<SharedPhysicsSystem>().MetricsEnabled;
        if (!enabled && window.Stages.All(stage => stage.Status == "no-observations"))
        {
            // Avoid dozens of empty rows in every incident when engine metrics are disabled.
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] physics-controller-window incidentId={_activeIncidentId} controller=all phase=all ",
                $"calls=0 totalMs=unavailable avgMs=unavailable status=metrics-disabled ",
                $"windowSeconds={window.Seconds:F3} metricsEnabled=false processWide=true source=engine-histogram"));
            return;
        }

        foreach (var stage in window.Stages)
        {
            var total = stage.TotalMs?.ToString("F3", CultureInfo.InvariantCulture) ?? "unavailable";
            var average = stage.AverageMs?.ToString("F3", CultureInfo.InvariantCulture) ?? "unavailable";
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] physics-controller-window incidentId={_activeIncidentId} controller={SanitizeName(stage.Controller)} phase={stage.Phase} ",
                $"calls={stage.Calls} totalMs={total} avgMs={average} status={stage.Status} ",
                $"windowSeconds={window.Seconds:F3} metricsEnabled={enabled} processWide=true source=engine-histogram"));
        }
    }

    private struct PhysicsCounts
    {
        public long Bodies;
        public long Awake;
        public long CanCollide;
        public long SleepDisabled;
        public long ContactEdges;

        public void Add(PhysicsComponent body)
        {
            Bodies++;
            if (body.Awake) Awake++;
            if (body.CanCollide) CanCollide++;
            if (!body.SleepingAllowed) SleepDisabled++;
            ContactEdges += body.ContactCount;
        }
    }
}
