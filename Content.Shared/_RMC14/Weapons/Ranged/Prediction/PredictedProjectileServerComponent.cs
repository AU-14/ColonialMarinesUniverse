using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

// CMU14 Begin: explicit state handling safely resolves references that outlive their entities.
[RegisterComponent, NetworkedComponent]
public sealed partial class PredictedProjectileServerComponent : Component
{
    public ICommonSession? Shooter;

    [DataField]
    public int ClientId;

    [DataField]
    public EntityUid? ClientEnt;

    [DataField]
    public bool Hit;
}
// CMU14 End
