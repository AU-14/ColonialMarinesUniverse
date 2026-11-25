using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ConstructionRobot;

[Serializable, NetSerializable]
public enum CMUConstructionRobotUiKey : byte { Key }

[Serializable, NetSerializable]
public enum CMUConstructionRobotCommand : byte
{
    Select,
    Follow,
    Hold,
    Camera,
    AvoidHazards,
    AvoidFire,
    AvoidHostiles,
    AvoidExplosives,
    ClearRepairArea,
}

[Serializable, NetSerializable]
public sealed class CMUConstructionRobotCommandMessage(NetEntity robot, CMUConstructionRobotCommand command)
    : BoundUserInterfaceMessage
{
    public readonly NetEntity Robot = robot;
    public readonly CMUConstructionRobotCommand Command = command;
}

[Serializable, NetSerializable]
public sealed record CMUConstructionRobotEntry(NetEntity Robot, string Name);

[Serializable, NetSerializable]
public sealed class CMUConstructionRobotUiState(
    List<CMUConstructionRobotEntry> robots,
    NetEntity? selected,
    bool following,
    bool repairAreaAssigned,
    bool viewing,
    bool avoidingHazard,
    bool avoidHazards,
    bool avoidFire,
    bool avoidHostiles,
    bool avoidExplosives,
    int fuel,
    int capacity,
    List<string> jobs) : BoundUserInterfaceState
{
    public readonly List<CMUConstructionRobotEntry> Robots = robots;
    public readonly NetEntity? Selected = selected;
    public readonly bool Following = following;
    public readonly bool RepairAreaAssigned = repairAreaAssigned;
    public readonly bool Viewing = viewing;
    public readonly bool AvoidingHazard = avoidingHazard;
    public readonly bool AvoidHazards = avoidHazards;
    public readonly bool AvoidFire = avoidFire;
    public readonly bool AvoidHostiles = avoidHostiles;
    public readonly bool AvoidExplosives = avoidExplosives;
    public readonly int Fuel = fuel;
    public readonly int Capacity = capacity;
    public readonly List<string> Jobs = jobs;
}
