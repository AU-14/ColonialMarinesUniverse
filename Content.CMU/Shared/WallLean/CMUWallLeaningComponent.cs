using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.WallLean;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUWallLeaningComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Wall;

    [DataField, AutoNetworkedField]
    public Vector2 Offset;

    [DataField, AutoNetworkedField]
    public bool BehindWall;

    [DataField]
    public Vector2 Position;

    [DataField]
    public TimeSpan SuperSlowAfter = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan SlowAfter = TimeSpan.FromSeconds(2);
}
