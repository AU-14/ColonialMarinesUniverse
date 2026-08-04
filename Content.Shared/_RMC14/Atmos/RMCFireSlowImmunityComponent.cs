using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Atmos;

/// <summary>
/// When present on an entity, all contact based speed modifiers from fire are completely ignored.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RMCFireSlowImmunityComponent : Component { }
