// ReSharper disable CheckNamespace

namespace Content.Shared.StationRecords;

// CMU14: shown on the records and security consoles.
public sealed partial record GeneralStationRecord
{
    /// <summary>
    ///     The person's religion, as a <see cref="Content.Shared.CMU14.Religion.CMUReligionPrototype"/> ID.
    /// </summary>
    [DataField]
    public string? Religion;
}
