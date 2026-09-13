namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// Private, round-lived memory. Only the worn bracer's UI receives snapshots; this component is not networked.
/// </summary>
[RegisterComponent]
[Access(typeof(YautjaMarkSystem))]
public sealed partial class YautjaHuntJournalComponent : Component
{
    [DataField] public int RecentLimit = 8;
    [DataField] public float ObservationRange = 16f;
    public readonly Dictionary<int, YautjaHuntRecord> Records = new();
    public readonly Dictionary<EntityUid, int> Targets = new();
    public readonly List<int> Recent = new();
    public readonly HashSet<EntityUid> Visible = new();
    public int NextId;
    public uint Revision;
    public bool History;
    public int Page;
}

public sealed class YautjaHuntRecord(int id, EntityUid target, string name, bool isXeno)
{
    public readonly int Id = id;
    public EntityUid? Target = target;
    public string Name = name;
    public readonly bool IsXeno = isXeno;
    public bool WasMarked;
    public TimeSpan LastSeen;
    public readonly List<YautjaMarkKind> LastKnownMarks = new();
    public readonly List<YautjaMarkKind> LastOwnedMarks = new();
}
