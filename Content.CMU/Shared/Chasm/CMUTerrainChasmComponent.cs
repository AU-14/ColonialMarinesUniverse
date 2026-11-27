using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Chasm;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUTerrainChasmComponent : Component
{
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUChasmLedgeGripComponent : Component
{
    public const int RequiredPresses = 5;
    public const int MaxMistakes = 2;
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(4.5);

    [AutoNetworkedField, DataField]
    public CMUChasmLedgeKey Key;

    [AutoNetworkedField, DataField]
    public int GripId;

    [AutoNetworkedField, DataField]
    public int Presses;

    [AutoNetworkedField, DataField]
    public int Mistakes;

    // The source, destination and deadline are authoritative.
    public EntityUid Chasm;
    public EntityCoordinates ReturnTo;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan Expires;
}

[Serializable, NetSerializable]
public enum CMUChasmLedgeKey : byte
{
    X,
    F,
    C,
}

[Serializable, NetSerializable]
public sealed class CMUChasmLedgePressEvent(int gripId, int presses, CMUChasmLedgeKey key) : EntityEventArgs
{
    public readonly int GripId = gripId;
    public readonly int Presses = presses;
    public readonly CMUChasmLedgeKey Key = key;
}
