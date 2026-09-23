using System.Numerics;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.TacticalMap.Reconstruction;

[Serializable, NetSerializable]
public enum CMUReconstructionUiKey : byte { Key }

[Serializable, NetSerializable]
public enum CMUReconMaterial : byte
{
    Empty, Floor, Wall, Door, Glass, Barricade, Ground, Machinery, Furniture, Crate, Tree, Rock,
    Stairs, Railing, Water, DoubleDoor, OpenDoor, OpenDoubleDoor, Sprite,
    Chair, FoldingChair, OfficeChair, Armchair, Stool, BenchLeft, BenchRight, Sofa, Bed, BunkBed,
    Desk, Shelf, Bookcase, WoodChair, WoodWingChair, Table, WoodTable, OperatingTable, Counter,
    CouchMiddle, CouchLeft, CouchRight,
}

[Serializable, NetSerializable]
public enum CMUReconOrderKind : byte { Rally, Move, Route, Text }

[Serializable, NetSerializable]
public enum CMUReconInk : byte { Yellow, Red, Blue, Green, White, Purple }

[Serializable, NetSerializable]
public sealed class CMUReconViewMessage(Vector2i offset) : BoundUserInterfaceMessage
{
    // Zero requests the complete linked map. Navigation is local to the viewer.
    public Vector2i Offset = offset;
    public CMUReconMapChoice MapChoice;
    public bool PreferPlanetOnShip;
    public int RequestId;
    public int AtlasId;
    public int[] Revisions = [];
    public int SurfaceCount;
}

[Serializable, NetSerializable]
public sealed class CMUReconClassicMessage : BoundUserInterfaceMessage;

// Personal geometry only. The server resolves the actor, faction and destination from the session.
[Serializable, NetSerializable]
public sealed class CMUReconPreloadRequest(int requestId, bool preferPlanetOnShip, bool cancel = false) : EntityEventArgs
{
    public int RequestId = requestId;
    public bool PreferPlanetOnShip = preferPlanetOnShip;
    public bool Cancel = cancel;
}

[Serializable, NetSerializable]
public sealed class CMUReconPreloadState(int requestId, CMUReconSnapshotMessage? snapshot, CMUReconPatchMessage? patch) : EntityEventArgs
{
    public int RequestId = requestId;
    public CMUReconSnapshotMessage? Snapshot = snapshot;
    public CMUReconPatchMessage? Patch = patch;
}

[Serializable, NetSerializable]
public sealed class CMUReconOrderMessage(int generation, int depth, Vector2i tile, CMUReconOrderKind kind) : BoundUserInterfaceMessage
{
    public int Generation = generation;
    public int Depth = depth;
    public Vector2i Tile = tile;
    public CMUReconOrderKind Kind = kind;
}

[Serializable, NetSerializable]
public sealed class CMUReconClearOrdersMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUReconRouteMessage(int generation, int depth, Vector2[] waypoints, CMUReconInk ink) : BoundUserInterfaceMessage
{
    public int Generation = generation;
    public int Depth = depth;
    public Vector2[] Waypoints = waypoints;
    public CMUReconInk Ink = ink;
}

[Serializable, NetSerializable]
public sealed class CMUReconCancelOrderMessage(int generation, int id) : BoundUserInterfaceMessage
{
    public int Generation = generation;
    public int Id = id;
}

[Serializable, NetSerializable]
public readonly record struct CMUReconAnnotation(int Depth, Vector2[] Points, CMUReconInk Ink,
    float Width = 3, string? Text = null);

[Serializable, NetSerializable]
public sealed class CMUReconSendMessage(int generation, int requestId, CMUReconAnnotation[] additions, int[] removals) : BoundUserInterfaceMessage
{
    public int Generation = generation;
    public int RequestId = requestId;
    public CMUReconAnnotation[] Additions = additions;
    public int[] Removals = removals;
}

[Serializable, NetSerializable]
public sealed class CMUReconSentMessage(int requestId, bool accepted, CMUReconOrder[] orders) : BoundUserInterfaceMessage
{
    public int RequestId = requestId;
    public bool Accepted = accepted;
    public CMUReconOrder[] Orders = orders;
}

[Serializable, NetSerializable]
public sealed class CMUReconSnapshotMessage(int generation, Vector2i origin, int minDepth, int levels,
    byte[] cells, CMUReconOrder[] orders, bool canOrder, int width = CMUReconGeometry.Size,
    int height = CMUReconGeometry.Size) : BoundUserInterfaceMessage
{
    public int Generation = generation;
    public Vector2i Origin = origin;
    public int MinDepth = minDepth;
    public int Levels = levels;
    public byte[] Cells = cells;
    public CMUReconOrder[] Orders = orders;
    public bool CanOrder = canOrder;
    public int Width = width;
    public int Height = height;
    public uint[] Appearance = [];
    public byte[] Directions = [];
    public CMUReconSurface[] Surfaces = [];
    public CMUReconLabel[] Labels = [];
    // Opening intel travels with its generation so a separate contact packet cannot arrive too early.
    public CMUReconContact[] Contacts = [];
    public Vector2i OperatorTile;
    public Vector2? OperatorPosition;
    public int OperatorDepth;
    public CMUReconMapChoice MapChoice;
    public bool AboardShip;
    public bool HasPlanet;
    public bool HasShip;
    public int RequestId;
    public int LoadedChunks;
    public int TotalChunks;
    // Stable across openings; generation still identifies an individual subscription.
    public int AtlasId;
    public bool ReuseGeometry;
    public int[] Revisions = [];
    // One bit per chunk known to be empty in the initial survey; avoids individual empty packets.
    public byte[] EmptyChunks = [];
}

[Serializable, NetSerializable]
public sealed class CMUReconPatchMessage(int generation, CMUReconChunk[] chunks, CMUReconOrder[] orders,
    bool canOrder) : BoundUserInterfaceMessage
{
    public int Generation = generation;
    public CMUReconChunk[] Chunks = chunks;
    public CMUReconOrder[] Orders = orders;
    public bool OrdersChanged = true;
    public bool CanOrder = canOrder;
    public CMUReconSurface[] Surfaces = [];
    public int LoadedChunks;
    public int TotalChunks;
}

[Serializable, NetSerializable]
public sealed class CMUReconFeedbackMessage(string localizationKey) : BoundUserInterfaceMessage
{
    public string LocalizationKey = localizationKey;
    public int RequestId;
}

[Serializable, NetSerializable]
public readonly record struct CMUReconContact(int Depth, TacticalMapBlip Blip);

[Serializable, NetSerializable]
public sealed class CMUReconContactsMessage(int generation, CMUReconContact[] contacts) : BoundUserInterfaceMessage
{
    public int Generation = generation;
    public CMUReconContact[] Contacts = contacts;
    public Vector2? OperatorPosition;
    public int OperatorDepth;
}

[Serializable, NetSerializable]
public readonly record struct CMUReconChunk(int Level, int X, int Y, byte[] Cells,
    uint[]? Appearance = null, byte[]? Directions = null, bool Empty = false, int Revision = 0, byte[]? Packed = null);

[Serializable, NetSerializable]
public readonly record struct CMUReconSurface(ushort Id, string Prototype, byte Variant, bool Entity);

[Serializable, NetSerializable]
public readonly record struct CMUReconLabel(Vector2i Tile, int Depth, string Text);

[Serializable, NetSerializable]
public readonly record struct CMUReconOrder(int Id, Vector2i Tile, int Depth, CMUReconOrderKind Kind,
    Vector2[]? Waypoints = null, CMUReconInk Ink = CMUReconInk.Yellow, float Width = 3, string? Text = null, Color? Color = null);
