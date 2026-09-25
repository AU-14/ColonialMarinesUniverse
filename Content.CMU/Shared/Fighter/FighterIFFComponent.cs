using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Persistent ownership of a fighter airframe or its portable landing pad.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FighterIFFComponent : Component
{
    [DataField, AutoNetworkedField] public string? Faction;
}
