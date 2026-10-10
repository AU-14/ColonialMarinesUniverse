using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ZLevels.Core.Components;

/// <summary>
/// One half of a cross-z pair. The pairing system links this entity to the
/// nearest unpaired holder at the same world position Offset levels away.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUZPairedComponent : Component
{
    /// <summary>Deck distance to the twin: 1 is one level up, -1 one down.</summary>
    [DataField, AutoNetworkedField]
    public int Offset;

    /// <summary>Only twins of the same kind pair, e.g. "power" or "vent".</summary>
    [DataField]
    public string PairKind = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Twin;
}
