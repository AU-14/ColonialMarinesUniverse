using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Ordnance;

public static class RMCAssemblyVisuals
{
    public const string LeftType = "left_type";
    public const string RightType = "right_type";
    public const string Locked = "locked";
}

[Serializable, NetSerializable]
public enum RMCAssemblyVisualKey
{
    LeftType,
    RightType,
    Locked
}

public static class RMCCasingVisuals
{
    public const string State = "state";
}

[Serializable, NetSerializable]
public enum RMCCasingVisualKey
{
    State
}

[Serializable, NetSerializable]
public enum RMCCasingVisualState : byte
{
    Empty,
    Loaded,
    Sealed,
    Armed
}
