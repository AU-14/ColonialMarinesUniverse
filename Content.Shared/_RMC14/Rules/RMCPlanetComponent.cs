using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Rules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCPlanetComponent : Component
{
    [DataField]
    public Vector2i Offset;

    /// <summary>
    /// Factions permitted to use withdraw consoles on this planet.
    /// Empty list means no faction may initiate withdrawal.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> AllowedWithdrawFactions = new() {"govfor","opfor","colony"};
}
