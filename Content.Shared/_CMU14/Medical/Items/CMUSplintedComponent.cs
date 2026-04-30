using Content.Shared._CMU14.Medical.Bones;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Items;

/// <summary>
///     The actual fracture data is untouched, so removing the splint restores the
///     underlying severity. Read by <see cref="SharedFractureSystem.GetEffectiveSeverity"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSplintedComponent : Component
{
    [DataField, AutoNetworkedField]
    public FractureSeverity MaxSuppressed = FractureSeverity.Simple;
}
