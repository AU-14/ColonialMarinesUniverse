using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Abilities;

/// <summary>
/// Wendigo pounce extras on top of <c>XenoLeap</c>: a hit strips everything from the target's hands,
/// and the leap passes over Wendigo allies instead of tackling them.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUWendigoPounceComponent : Component
{
    [DataField]
    public bool DropHeldItems = true;
}
