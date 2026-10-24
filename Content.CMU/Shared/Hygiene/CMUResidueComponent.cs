using Robust.Shared.GameStates;
using System;

namespace Content.Shared.CMU14.Hygiene;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUResidueComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color? Color;

    [DataField, AutoNetworkedField]
    public bool Alien;

    [DataField, AutoNetworkedField]
    public float Buildup;

    public TimeSpan NextBuildup;

}
