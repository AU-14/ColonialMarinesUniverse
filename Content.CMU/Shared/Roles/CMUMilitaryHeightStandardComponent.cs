namespace Content.Shared.CMU14.Roles;

/// <summary>
/// Marker for a job's <c>roundComponents</c>, the same way <c>RegulationAppearance</c> is used: jobs carrying it
/// can only be picked by characters whose height is within the limits and whose weight suits that height.
/// Put it on a shared abstract job so every descendant inherits it.
/// </summary>
[RegisterComponent]
public sealed partial class CMUMilitaryHeightStandardComponent : Component
{
    /// <summary>Shortest allowed height, in inches. Defaults to 4'10".</summary>
    [DataField]
    public int MinInches = 4 * 12 + 10;

    /// <summary>Tallest allowed height, in inches. Defaults to 6'6".</summary>
    [DataField]
    public int MaxInches = 6 * 12 + 6;

    /// <summary>
    /// Whether the character's weight must also fall within the weight-for-height range for their height.
    /// See <see cref="CMUMilitaryHeightRequirement"/> for the table.
    /// </summary>
    [DataField]
    public bool WeightStandard = true;

    /// <summary>Pounds of leeway added to the table's maximum weight, so it's slightly more lenient than the Army.</summary>
    [DataField]
    public int MaxWeightLeeway = 10;

    /// <summary>Pounds of leeway taken off the table's minimum weight.</summary>
    [DataField]
    public int MinWeightLeeway = 5;

    /// <summary>Oldest a character can be. 62 is the US military's mandatory retirement age.</summary>
    [DataField]
    public int MaxAge = 62;
}
