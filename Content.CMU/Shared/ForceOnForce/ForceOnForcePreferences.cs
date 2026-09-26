using Robust.Shared.Serialization;

namespace Content.Shared.Preferences;

[Serializable, NetSerializable]
public enum ForceOnForceSide : byte
{
    Either,
    Govfor,
    Opfor,
}

[Flags, Serializable, NetSerializable]
public enum ForceOnForceFallback : byte
{
    StayInLobby = 0,
    OtherSide = 1,
    OtherRole = 2,
    Both = OtherSide | OtherRole,
}

public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public ForceOnForceSide FoFSide { get; private set; }

    [DataField]
    public ForceOnForceFallback FoFFallback { get; private set; }

    public HumanoidCharacterProfile WithForceOnForcePreferences(ForceOnForceSide side, ForceOnForceFallback fallback)
    {
        return new(this)
        {
            FoFSide = Enum.IsDefined(side) ? side : ForceOnForceSide.Either,
            FoFFallback = Enum.IsDefined(fallback) ? fallback : ForceOnForceFallback.StayInLobby,
        };
    }
}
