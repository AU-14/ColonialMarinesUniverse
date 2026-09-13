using System.Diagnostics;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical.Core;

/// <summary>
/// Test-only brackets around the server entity-system update phase. This excludes
/// entity event-bus flushing, component culling, network serialization and client rendering.
/// Arrays are installed only for an explicit workload, and never allocate in Update.
/// </summary>
public sealed class CMUMedicalFrameProbeStartSystem : EntitySystem
{
    [Dependency] private IEntitySystemManager _systems = default!;
    public bool Capturing;
    public long Started;
    public long AllocatedBefore;

    public override void Initialize()
    {
        base.Initialize();
        foreach (var type in _systems.GetEntitySystemTypes())
        {
            if (type != typeof(CMUMedicalFrameProbeStartSystem))
                UpdatesBefore.Add(type);
        }
    }

    public override void Update(float frameTime)
    {
        if (!Capturing)
            return;
        AllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Started = Stopwatch.GetTimestamp();
    }
}

public sealed class CMUMedicalFrameProbeEndSystem : EntitySystem
{
    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private CMUMedicalFrameProbeStartSystem _start = default!;
    private double[]? _wall;
    private long[]? _allocated;
    public int Recorded { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        foreach (var type in _systems.GetEntitySystemTypes())
        {
            if (type != typeof(CMUMedicalFrameProbeEndSystem))
                UpdatesAfter.Add(type);
        }
    }

    public void Begin(double[] wall, long[] allocated)
    {
        _wall = wall;
        _allocated = allocated;
        Recorded = 0;
        _start.Capturing = true;
    }

    public void End()
    {
        _start.Capturing = false;
        _wall = null;
        _allocated = null;
    }

    public override void Update(float frameTime)
    {
        if (!_start.Capturing || _wall == null || _allocated == null)
            return;
        var elapsed = Stopwatch.GetElapsedTime(_start.Started).TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - _start.AllocatedBefore;
        if (Recorded < _wall.Length)
        {
            _wall[Recorded] = elapsed;
            _allocated[Recorded] = allocated;
        }
        Recorded++;
    }
}
