namespace Content.Server._CMU14.Ops.Sfx;

public sealed class ActiveScriptedSound
{
    public string SequenceId = string.Empty;
    public TimeSpan StartTime;
    public int NextEntryIndex;
    public EntityUid? AnchorEntity;
    public bool WarnedEmptyFilter;

    public Dictionary<string, EntityUid> ActiveLoops = new();
    public List<(TimeSpan StopAt, EntityUid Entity)> ScheduledLoopStops = new();
    public Dictionary<int, TimeSpan> JitteredDelays = new();
    public List<(int Index, TimeSpan NextFire)> RepeatingEntries = new();
}
