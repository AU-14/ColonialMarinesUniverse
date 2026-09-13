namespace Content.Shared._RMC14.Dropship;

[ByRefEvent]
public readonly record struct DropshipHijackStartEvent(
    EntityUid? Dropship,
    string? HijackerFaction = null,
    DropshipHijackerType HijackerType = DropshipHijackerType.Xeno); // CMU14

/// <summary>
///     Who is hijacking the dropship; decides which endgame effects may fire.
/// </summary>
// CMU14 type
public enum DropshipHijackerType : byte
{
    Xeno,     // hive-line xenomorph (queen): full xeno endgame, larva surge
    Pathogen, // pathogen caste (overmind, neomorphs): stranded-crew cleanup only
    Human,    // human-faction hijacker (CLF/OPFOR/GOVFOR): no xeno effects
    Other,    // yautja, abominations, apes: crash effects only
}
