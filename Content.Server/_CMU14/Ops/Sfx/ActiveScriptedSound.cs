using Robust.Shared.Audio;

namespace Content.Server._CMU14.Ops.Sfx;

public sealed class ActiveScriptedSound
{
    public string SequenceId = string.Empty;
    public TimeSpan StartTime;
    public int NextEntryIndex;
    public EntityUid? AnchorEntity;

    public Dictionary<int, TimeSpan> JitteredDelays = new();
    public List<(int Index, TimeSpan NextFire)> RepeatingEntries = new();

    /// <summary>Named loop layers currently playing</summary>
    public readonly Dictionary<string, TrackedLoop> Loops = new();
}

public sealed record TrackedLoop(SoundSpecifier Sound, AudioParams Params, bool Global, TimeSpan FiredAt, float? Duration);
