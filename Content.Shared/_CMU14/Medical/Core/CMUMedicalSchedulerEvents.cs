namespace Content.Shared._CMU14.Medical.Core;

/// <summary>
///     Identifies one replaceable scheduled work item on an entity.
/// </summary>
public readonly record struct CMUMedicalWorkKey(string Id)
{
    public override string ToString()
    {
        return Id ?? string.Empty;
    }
}

/// <summary>
///     Raised directed on an entity when its keyed medical work becomes due.
/// </summary>
[ByRefEvent]
public readonly record struct CMUMedicalWorkDueEvent(CMUMedicalWorkKey Key);
