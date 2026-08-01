using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.Allegiance;

/// <summary>
/// Represents an allegiance a player can select for their character.
/// Allegiance determines which platoon/colony the character preferentially spawns on.
/// </summary>
[Prototype("Allegiance")]
public sealed partial class AllegiancePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Display name for the allegiance.
    /// </summary>
    [DataField(required: true)]
    public LocId Name { get; private set; } = string.Empty;

    /// <summary>
    /// Description shown in the UI.
    /// </summary>
    [DataField]
    public LocId Description { get; private set; } = string.Empty;


    /// <summary>
    /// Whether this allegiance can be selected at round start.
    /// </summary>
    [DataField]
    public bool RoundStart { get; private set; } = true;
}

