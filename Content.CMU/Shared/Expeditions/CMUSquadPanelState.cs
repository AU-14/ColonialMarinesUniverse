using System.Numerics;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Expeditions;

[Serializable, NetSerializable]
public sealed class CMUSquadPanelState : EuiStateBase
{
    public List<CMUSquadSummary> Squads = new();
    public List<CMUSquadMemberView> Members = new();
    public List<string> Variants = new();
    public List<string> Outfits = new();
    public List<string> Doctrines = new();
    public List<string> Factions = new();
    public NetEntity? Selected;
    public string Status = "";
    public string Friendlies = "default";
    public string Targets = "default";
    public string Overview = "";
    public string CurrentDoctrine = "balanced";
    public bool AutomaticPatrol;
    public bool Coordinating;
    public int AimSkillPercent = 100;
}

[Serializable, NetSerializable]
public sealed record CMUSquadSummary(NetEntity Root, string Label);

[Serializable, NetSerializable]
public sealed record CMUSquadMemberView(NetEntity Entity, string Name, int Map, Vector2 Position,
    Vector2? Target, Vector2? Destination, Vector2? Cover, List<Vector2> Route, List<Vector2> RejectedCover,
    string Summary, string Detail, bool Active, bool Injured,
    string Order = "", string Tactic = "", string TacticReason = "", string Action = "", string Status = "", bool Recorded = false);

[Serializable, NetSerializable]
public enum CMUSquadPanelAction : byte
{
    Refresh, Select, Spawn, Move, Guard, Hold, Regroup, PatrolAdd, PatrolStart, PatrolStop,
    Resupply, Doctrine, Friendly, Target,
    PatrolClear, AutoPatrol, Cooperation,
    AimSkill,
    Assault,
}

[Serializable, NetSerializable]
public sealed class CMUSquadPanelMessage : EuiMessageBase
{
    public CMUSquadPanelAction Action;
    public NetEntity? Root;
    public string Value = "";
    public int Count = 3;
    public string Variant = "mixed";
    public string Outfit = "scavenger";
    public string Doctrine = "balanced";
    public bool Here = true;
    public int Map;
    public float X;
    public float Y;
    public string Facing = "auto";
    public bool Enabled;
    public int AimSkillPercent = 100;
}
