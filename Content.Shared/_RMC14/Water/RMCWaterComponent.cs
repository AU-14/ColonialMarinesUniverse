using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Water;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RMCWaterSystem))]
public sealed partial class RMCWaterComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntityWhitelist? Cover;

    /// <summary>Visual immersion in pixels. Does not change movement speed or collision.</summary>
    [DataField, AutoNetworkedField]
    public float Depth = 8;
}
