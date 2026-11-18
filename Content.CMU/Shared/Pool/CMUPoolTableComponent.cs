using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Pool;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUPoolTableComponent : Component
{
    [AutoNetworkedField] public Vector2[] Positions = Array.Empty<Vector2>();
    [AutoNetworkedField] public int PocketedMask;
    [AutoNetworkedField] public bool Rolling;
    [AutoNetworkedField] public int Revision;
}

[Serializable, NetSerializable]
public enum CMUPoolUiKey
{
    FreePlay,
}

[Serializable, NetSerializable]
public enum CMUPoolAction
{
    Shoot,
    PlaceCueBall,
    Rack,
}

[Serializable, NetSerializable]
public sealed class CMUPoolMessage(
    CMUPoolAction action,
    int revision,
    Vector2 vector,
    float power = 0) : BoundUserInterfaceMessage
{
    public readonly CMUPoolAction Action = action;
    public readonly int Revision = revision;
    public readonly Vector2 Vector = vector;
    public readonly float Power = power;
}
