using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.MineDetector;

// RUMC: something that a mine detector finds but that is not a land mine (the plate over a pit): it shows on the detector only when the detector is no further than Range from it.
[RegisterComponent, NetworkedComponent]
public sealed partial class MineDetectableComponent : Component
{
    // How near the detector must be, in tiles: a tile away and the diagonal of it.
    [DataField]
    public float Range = 1.5f;
}
