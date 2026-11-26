using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Pool;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUPoolStickComponent : Component
{
    public bool Spent;
}

[RegisterComponent]
public sealed partial class CMUPoolStickHolderComponent : Component
{
    public const string ContainerId = "sticks";
    public const int Capacity = 6;
}

[Serializable, NetSerializable]
public enum CMUPoolStickHolderVisuals : byte
{
    Fill,
}

[Serializable, NetSerializable]
public enum CMUPoolStickHolderFill : byte
{
    Empty,
    One,
    Two,
    Three,
    Four,
    Five,
    Six,
}
