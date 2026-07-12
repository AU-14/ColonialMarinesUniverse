using System.Linq;

namespace Content.Server._CMU14.Diagnostics.Performance;

internal sealed record CMUPerformanceChurnSnapshot(
    int ComponentCount,
    long EntitiesCreated,
    long EntitiesDeleted,
    long ComponentsAdded,
    long ComponentsRemoved,
    long EntitiesDirtied,
    IReadOnlyDictionary<string, int> PrototypeCounts,
    IReadOnlyDictionary<string, long> PrototypeCreates,
    IReadOnlyDictionary<string, long> PrototypeDeletes,
    IReadOnlyDictionary<string, int> ComponentCounts,
    IReadOnlyDictionary<string, long> ComponentAdds,
    IReadOnlyDictionary<string, long> ComponentRemoves,
    IReadOnlyDictionary<string, long> MapCreates);

internal readonly record struct CMUPerformanceChurnRow(
    string Name,
    long CreatedOrAdded,
    long DeletedOrRemoved,
    long Net,
    int Current);

/// <summary>
///     Tracks bounded aggregate ECS churn. Keys are prototype, component, and map identifiers; entity UIDs are never retained.
/// </summary>
internal sealed class CMUPerformanceChurnTracker
{
    private readonly Dictionary<string, int> _prototypeCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _prototypeCreates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _prototypeDeletes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _componentCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _componentAdds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _componentRemoves = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _mapCreates = new(StringComparer.Ordinal);

    public int ComponentCount { get; private set; }
    public long EntitiesCreated { get; private set; }
    public long EntitiesDeleted { get; private set; }
    public long ComponentsAdded { get; private set; }
    public long ComponentsRemoved { get; private set; }
    public long EntitiesDirtied { get; private set; }

    public void Reset(
        int componentCount,
        IReadOnlyDictionary<string, int> prototypeCounts,
        IReadOnlyDictionary<string, int> componentCounts)
    {
        ComponentCount = Math.Max(0, componentCount);
        EntitiesCreated = 0;
        EntitiesDeleted = 0;
        ComponentsAdded = 0;
        ComponentsRemoved = 0;
        EntitiesDirtied = 0;

        _prototypeCounts.Clear();
        _componentCounts.Clear();
        CopyPositive(prototypeCounts, _prototypeCounts);
        CopyPositive(componentCounts, _componentCounts);

        _prototypeCreates.Clear();
        _prototypeDeletes.Clear();
        _componentAdds.Clear();
        _componentRemoves.Clear();
        _mapCreates.Clear();
    }

    public void EntityCreated(string prototype, string map)
    {
        EntitiesCreated++;
        Increment(_prototypeCounts, prototype, 1);
        Increment(_prototypeCreates, prototype, 1);
        Increment(_mapCreates, map, 1);
    }

    public void EntityDeleted(string prototype)
    {
        EntitiesDeleted++;
        Increment(_prototypeCounts, prototype, -1, clampToZero: true);
        Increment(_prototypeDeletes, prototype, 1);
    }

    public void EntityDirtied()
    {
        EntitiesDirtied++;
    }

    public void ComponentAdded(string component)
    {
        ComponentsAdded++;
        ComponentCount++;
        Increment(_componentCounts, component, 1);
        Increment(_componentAdds, component, 1);
    }

    public void ComponentRemoved(string component)
    {
        ComponentsRemoved++;
        ComponentCount = Math.Max(0, ComponentCount - 1);
        Increment(_componentCounts, component, -1, clampToZero: true);
        Increment(_componentRemoves, component, 1);
    }

    public CMUPerformanceChurnSnapshot Snapshot()
    {
        return new(
            ComponentCount,
            EntitiesCreated,
            EntitiesDeleted,
            ComponentsAdded,
            ComponentsRemoved,
            EntitiesDirtied,
            new Dictionary<string, int>(_prototypeCounts, StringComparer.Ordinal),
            new Dictionary<string, long>(_prototypeCreates, StringComparer.Ordinal),
            new Dictionary<string, long>(_prototypeDeletes, StringComparer.Ordinal),
            new Dictionary<string, int>(_componentCounts, StringComparer.Ordinal),
            new Dictionary<string, long>(_componentAdds, StringComparer.Ordinal),
            new Dictionary<string, long>(_componentRemoves, StringComparer.Ordinal),
            new Dictionary<string, long>(_mapCreates, StringComparer.Ordinal));
    }

    public static IReadOnlyList<CMUPerformanceChurnRow> GetPrototypeRows(
        CMUPerformanceChurnSnapshot baseline,
        CMUPerformanceChurnSnapshot current,
        int top)
    {
        return GetRows(
            baseline.PrototypeCreates,
            current.PrototypeCreates,
            baseline.PrototypeDeletes,
            current.PrototypeDeletes,
            current.PrototypeCounts,
            top);
    }

    public static IReadOnlyList<CMUPerformanceChurnRow> GetComponentRows(
        CMUPerformanceChurnSnapshot baseline,
        CMUPerformanceChurnSnapshot current,
        int top)
    {
        return GetRows(
            baseline.ComponentAdds,
            current.ComponentAdds,
            baseline.ComponentRemoves,
            current.ComponentRemoves,
            current.ComponentCounts,
            top);
    }

    public static IReadOnlyList<CMUPerformanceChurnRow> GetMapCreationRows(
        CMUPerformanceChurnSnapshot baseline,
        CMUPerformanceChurnSnapshot current,
        int top)
    {
        return current.MapCreates.Keys
            .Concat(baseline.MapCreates.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(name =>
            {
                long created = NonNegativeDelta(current.MapCreates, baseline.MapCreates, name);
                return new CMUPerformanceChurnRow(name, created, 0, created, 0);
            })
            .Where(row => row.CreatedOrAdded > 0)
            .OrderByDescending(row => row.CreatedOrAdded)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .Take(Math.Max(0, top))
            .ToArray();
    }

    private static IReadOnlyList<CMUPerformanceChurnRow> GetRows(
        IReadOnlyDictionary<string, long> baselineAdds,
        IReadOnlyDictionary<string, long> currentAdds,
        IReadOnlyDictionary<string, long> baselineRemoves,
        IReadOnlyDictionary<string, long> currentRemoves,
        IReadOnlyDictionary<string, int> currentCounts,
        int top)
    {
        return currentAdds.Keys
            .Concat(baselineAdds.Keys)
            .Concat(currentRemoves.Keys)
            .Concat(baselineRemoves.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(name =>
            {
                long added = NonNegativeDelta(currentAdds, baselineAdds, name);
                long removed = NonNegativeDelta(currentRemoves, baselineRemoves, name);
                currentCounts.TryGetValue(name, out int count);
                return new CMUPerformanceChurnRow(name, added, removed, added - removed, count);
            })
            .Where(row => row.CreatedOrAdded != 0 || row.DeletedOrRemoved != 0)
            .OrderByDescending(row => Math.Abs(row.Net))
            .ThenByDescending(row => row.CreatedOrAdded + row.DeletedOrRemoved)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .Take(Math.Max(0, top))
            .ToArray();
    }

    private static long NonNegativeDelta(
        IReadOnlyDictionary<string, long> current,
        IReadOnlyDictionary<string, long> baseline,
        string key)
    {
        current.TryGetValue(key, out long currentValue);
        baseline.TryGetValue(key, out long baselineValue);
        return Math.Max(0, currentValue - baselineValue);
    }

    private static void CopyPositive(
        IReadOnlyDictionary<string, int> source,
        Dictionary<string, int> destination)
    {
        foreach (var (key, value) in source)
        {
            if (value > 0)
                destination[key] = value;
        }
    }

    private static void Increment(Dictionary<string, int> values, string key, int amount, bool clampToZero = false)
    {
        values.TryGetValue(key, out int value);
        value += amount;
        if (clampToZero)
            value = Math.Max(0, value);
        values[key] = value;
    }

    private static void Increment(Dictionary<string, long> values, string key, long amount)
    {
        values.TryGetValue(key, out long value);
        values[key] = value + amount;
    }
}
