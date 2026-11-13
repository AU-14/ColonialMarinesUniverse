using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ConstructionRobot;

[Serializable, NetSerializable]
public sealed class ConstructionRobotGhostPlacedMessage : EntityEventArgs
{
    public readonly NetCoordinates Location;
    public readonly string Prototype;
    public readonly Angle Rotation;
    public readonly int GhostId;

    public ConstructionRobotGhostPlacedMessage(NetCoordinates location, string prototype, Angle rotation, int ghostId)
    {
        Location = location;
        Prototype = prototype;
        Rotation = rotation;
        GhostId = ghostId;
    }
}

[Serializable, NetSerializable]
public sealed class ConstructionRobotGhostRemovedMessage : EntityEventArgs
{
    public readonly int GhostId;

    public ConstructionRobotGhostRemovedMessage(int ghostId)
    {
        GhostId = ghostId;
    }
}
