using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Shared.Damage.Components;
// ReSharper restore CheckNamespace

public sealed partial class InjurableComponent
{
    /// <summary>
    ///     Minimum total damage required for the target's health bar to be visible.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2? HealthBarThreshold;
}
