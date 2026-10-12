using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Riding;

/// <summary>
///     On an entity riding a <see cref="CMUMountComponent"/>. Removed on every unbuckle.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUMountSystem))]
public sealed partial class CMURiderComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Mount;

    [DataField, AutoNetworkedField]
    public CMUMountSeat Seat;

    /// <summary>The rider's own buckle offset before mounting, restored afterwards. Null if it had none.</summary>
    [DataField, AutoNetworkedField]
    public Vector2? PreviousOffset;
}
