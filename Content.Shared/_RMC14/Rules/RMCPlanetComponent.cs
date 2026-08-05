using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Rules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCPlanetComponent : Component
{
    [DataField]
    public Vector2i Offset;

    [DataField, AutoNetworkedField]
    public List<string> AllowedWithdrawFactions = new() { "govfor", "opfor", "colony" };
}
