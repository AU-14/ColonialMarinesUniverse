using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Wounds;

/// <summary>
///     Multiple sources are NOT additive: the highest single rate wins. The
///     <see cref="Source"/> tag carries the contributing reason for
///     diagnostics only; recompute is source-agnostic so a fracture clearing
///     while a damaged organ is still in the part keeps the bleed alive.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedCMUWoundsSystem))]
public sealed partial class InternalBleedingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BloodlossPerSecond = 0.5f;

    /// <summary>
    ///     Diagnostic tag — e.g. <c>fracture:Compound</c> or
    ///     <c>organ:Heart</c>. Surfaced by the X-ray scanner on the readout.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Source = "unknown";

    [DataField, AutoPausedField]
    public TimeSpan NextBleedTick;
}
