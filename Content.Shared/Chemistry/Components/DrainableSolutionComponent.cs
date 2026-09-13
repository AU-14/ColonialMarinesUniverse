using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components;

/// <summary>
/// Denotes a specific solution contained within this entity that can can be
/// easily "drained". This means things with taps/spigots, or easily poured
/// items.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DrainableSolutionComponent : Component
{
    /// <summary>
    /// Solution name that can be drained.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public string Solution = "default";

    /// <summary>
    /// The drain do-after time required to transfer reagents from the solution.
    /// Networked because predicted transfers read this value and it may be changed dynamically.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DrainTime = TimeSpan.Zero;
}
