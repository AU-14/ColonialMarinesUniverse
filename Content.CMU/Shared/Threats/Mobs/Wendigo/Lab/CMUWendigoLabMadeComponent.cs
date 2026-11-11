using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Lab;

/// <summary>
/// Marks a Wendigo made from a test subject. Lab-made Wendigos never count toward round state
/// or any win condition.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUWendigoLabMadeComponent : Component;
