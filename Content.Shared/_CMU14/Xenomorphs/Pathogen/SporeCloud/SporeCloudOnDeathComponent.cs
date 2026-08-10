using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.SporeCloud;

/// <summary>
/// Marker for xenos (e.g. spore poppers) that release a spore cloud on death.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSporeCloudOnDeathComponent : Component
{
    [DataField]
    public EntProtoId CloudPrototype = "CMUPathogenSporeCloud";
}