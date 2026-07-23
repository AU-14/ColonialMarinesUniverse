using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

/// <summary>
/// Allows entity to see through Z-levels
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), UnsavedComponent, Access(typeof(CMUSharedZLevelsSystem))]
public sealed partial class CMUZLevelViewerComponent : Component
{
    public const int MaxStairPreviewPositions = 4;

    public readonly List<EntityUid> Eyes = new();

    /// <summary>
    /// We can look at 1 z-level up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LookUp;

    /// <summary>
    /// Temporarily draws the level above when a visible stair is close enough to the viewer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StairPreviewUp;

    /// <summary>
    /// Number of stair preview origins currently active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int StairPreviewPositionCount;

    /// <summary>
    /// Primary world position on the viewer's current map to use as the FOV/PVS origin for automatic stair previews.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition;

    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition2;

    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition3;

    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition4;

    [DataField]
    public EntProtoId ActionProto = "CMUActionToggleLookUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelActionEntity;

    public Vector2 GetStairPreviewPosition(int index)
    {
        return index switch
        {
            0 => StairPreviewPosition,
            1 => StairPreviewPosition2,
            2 => StairPreviewPosition3,
            3 => StairPreviewPosition4,
            _ => default,
        };
    }

    public void SetStairPreviewPosition(int index, Vector2 value)
    {
        switch (index)
        {
            case 0:
                StairPreviewPosition = value;
                break;
            case 1:
                StairPreviewPosition2 = value;
                break;
            case 2:
                StairPreviewPosition3 = value;
                break;
            case 3:
                StairPreviewPosition4 = value;
                break;
        }
    }
}
