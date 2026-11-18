using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CaveSurvey;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCaveSurveyAccessComponent : Component;

[RegisterComponent]
public sealed partial class CMUCaveSurveyMapComponent : Component;

[Serializable, NetSerializable]
public enum CMUCaveSurveyUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class CMUCaveSurveyFromColonyMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUCaveSurveyViewMessage : BoundUserInterfaceMessage
{
    public Vector2i Focus;
    public int MinDepth = -8;
    public bool Center = true;
    public bool NextWorld;
    public int RequestId;
}

[Serializable, NetSerializable]
public sealed class CMUCaveSurveySurfaceMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUCaveSurveySectionMessage(int requestId, NetEntity world, string name, int blocks,
    Vector2i min, Vector2i max) : BoundUserInterfaceMessage
{
    public int RequestId = requestId;
    public NetEntity World = world;
    public string Name = name;
    public int Blocks = blocks;
    public Vector2i Min = min;
    public Vector2i Max = max;
}
