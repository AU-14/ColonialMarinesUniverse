using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Areas;

/// <summary>
/// Lets King/ape destroy leaps land in this area even if it's noTunnel.
/// Ship maps flag the whole hull noTunnel to stop burrowers, which also locked out the leap.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUDestroyLeapAllowedComponent : Component;
