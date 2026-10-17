using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Read-only airspace telemetry, sent only to the wielding operator.</summary>
[Serializable, NetSerializable]
public sealed class FighterAirspaceRadarEvent : EntityEventArgs
{
    public NetEntity Launcher;
    public NetEntity TerrainMap;
    public Box2 Battlefield;
    // Static terrain is sent on acquiring the display or changing map/launcher.
    public byte[]? Terrain;
    public Vector2 OperatorPosition;
    public bool IgnoreIFF;
    public bool Organic;
    public bool Aiming;
    public bool Loaded;
    public float CooldownSeconds;
    public float LockSeconds;
    public List<FighterAirspaceContact> Contacts = [];
}

[Serializable, NetSerializable]
public enum FighterContactDisposition : byte { Unknown, Friendly, Hostile }

[Serializable, NetSerializable]
public sealed record FighterAirspaceContact(Vector2 Position, float Heading, float Height,
    FighterContactDisposition Disposition, bool Locked);
