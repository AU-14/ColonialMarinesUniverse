using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Cosmetic;

/// <summary>
///     Records the outer-clothing prototype the wearer had equipped at
///     severance time, used to determine visual layers for dropped parts.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSeveredPartClothingComponent : Component
{
    [DataField]
    public string? OuterClothingProto;
}
