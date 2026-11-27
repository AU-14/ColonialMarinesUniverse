using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Buckle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RMCBuckleSystem))]
public sealed partial class RMCStrapDrawDepthComponent : Component
{
    [DataField, AutoNetworkedField]
    public DrawDepth.DrawDepth UnstrappedDepth = DrawDepth.DrawDepth.Mobs;

    [DataField, AutoNetworkedField]
    public DrawDepth.DrawDepth StrappedDepth = DrawDepth.DrawDepth.Mobs;

    // CMU14 Begin: an overlay strap must draw over its rider, but the default rule puts riders one depth above the strap.
    /// <summary>
    /// Draw depth for entities buckled to this strap. Null keeps the default of one above the strap.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DrawDepth.DrawDepth? BuckledDepth;
    // CMU14 End
}