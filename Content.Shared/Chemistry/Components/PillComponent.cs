using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PillComponent : Component
{
    /// <summary>
    /// The pill id. Used for networking & serializing pill visuals.
    /// </summary>
    [AutoNetworkedField]
    [DataField("pillType")]
    [ViewVariables(VVAccess.ReadWrite)]
    public uint PillType;

    /// <summary>
    /// Whether swallowing this pill transfers its complete solution directly into the bloodstream.
    /// Pills using the upstream digestion model leave this disabled.
    /// </summary>
    [DataField]
    public bool DirectBloodstream;
}
