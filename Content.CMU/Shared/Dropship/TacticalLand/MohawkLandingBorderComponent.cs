using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Dropship.TacticalLand;

/// <summary>The approaching ship's local footprint, projected with its hull shadow.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), AutoGenerateComponentPause]
public sealed partial class MohawkLandingBorderComponent : Component
{
    [DataField, AutoNetworkedField]
    public Box2 Bounds;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ApproachStart;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan Touchdown;

    [AutoNetworkedField]
    public Box2 RoofBounds;

    /// <summary>The actual upper deck, including its current paint scheme and missing tiles.</summary>
    [AutoNetworkedField]
    public List<DropshipShadowTile> RoofTiles = new();
}
