namespace Content.Shared._CMU14.Ops.Sfx;

[ByRefEvent]
public record struct StartScriptedSequenceEvent(
    string SequenceId,
    EntityUid? Anchor = null,
    int? SequenceHandle = null);

[ByRefEvent]
public readonly record struct StopScriptedSequenceEvent(int SequenceHandle);

[ByRefEvent]
public readonly record struct ScriptedSequenceMarkerEvent(string SequenceId, string Marker, int SequenceHandle);
