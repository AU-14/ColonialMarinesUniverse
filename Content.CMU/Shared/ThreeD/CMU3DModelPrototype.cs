using System.Numerics;
using Content.Shared.Doors.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

/// <summary>
/// Editable solid-part model used by the content preview and the offline glTF exporter.
/// X/Y are horizontal, Z points up, the pivot is at ground level, and one unit is one map tile.
/// Source prototypes record references, not an automatic replacement for their live appearance.
/// </summary>
[Prototype("cmu3DModel")]
public sealed partial class CMU3DModelPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public string Label = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public string Status = "draft";

    /// <summary>Complete wearable volume, admitted through equipment poses rather than world bindings.</summary>
    [DataField]
    public bool EquipmentOnly;

    [DataField]
    public string[] SourcePrototypes = [];

    /// <summary>Exact prototypes that may use this assembly only when their selected random layer matches ReferenceState.</summary>
    [DataField]
    public string[] RandomSpritePrototypes = [];

    /// <summary>Single RandomSprite layer represented by this assembly. Unknown or additional layers remain unsupported.</summary>
    [DataField]
    public string? RandomSpriteLayer;

    /// <summary>Art reference for a state-only model that does not claim another exact prototype mapping.</summary>
    [DataField]
    public string? ReferencePrototype;

    /// <summary>Explicit RSI/state for comparing a modeled pose rather than the prototype's default icon.</summary>
    [DataField]
    public string? ReferenceRsi;

    [DataField]
    public string? ReferenceState;

    /// <summary>Static bulb poses keyed by the existing PoweredLightLayers.Base RSI state. Blinking remains source-owned.</summary>
    [DataField]
    public Dictionary<string, CMU3DModelFrame> PoweredLightStates = [];

    /// <summary>Source-matched door-control geometry for each RSI frame, including the power-loss overlay.</summary>
    [DataField]
    public Dictionary<string, CMU3DDoorButtonState> DoorButtonStates = [];

    /// <summary>Complete Base-layer door poses keyed by the original RSI state and current sprite frame.</summary>
    [DataField]
    public Dictionary<string, CMU3DDoorSpriteState> DoorSpriteStates = [];

    /// <summary>Source DoorSystem flick durations for portable export. Live playback uses the source sprite owner.</summary>
    [DataField]
    public Dictionary<string, float> DoorAnimationDurations = [];

    /// <summary>Static body/reinforcement damage poses, keyed by the existing DamageOverlay state suffix.</summary>
    [DataField]
    public Dictionary<string, CMU3DModelFrame> BarricadeDamageStates = [];

    /// <summary>RSI of the additional reinforcement layer; ReferenceRsi identifies the body layer.</summary>
    [DataField]
    public string? BarricadeReinforcementRsi;

    /// <summary>Complete wired compositions for the same damage suffixes; selected by the original barbWired layer.</summary>
    [DataField]
    public Dictionary<string, CMU3DModelFrame> BarricadeWiredStates = [];

    [DataField]
    public string? BarricadeWireRsi;

    [DataField]
    public string? BarricadeWireState;

    /// <summary>Complete acid frame compositions keyed by damage suffix, with and without wire.</summary>
    [DataField]
    public Dictionary<string, CMU3DBarricadeAcidState> BarricadeAcidStates = [];

    [DataField]
    public string? BarricadeAcidRsi;

    [DataField]
    public string? BarricadeAcidState;

    /// <summary>Source strip timing for portable export only. The live preview reads the owner's current frame.</summary>
    [DataField]
    public List<float> BarricadeAcidDelays = [];

    /// <summary>Portable export timelines. The live preview follows the existing sprite owner instead of replaying these.</summary>
    [DataField]
    public Dictionary<string, List<CMU3DFrameKey>> FrameAnimations = [];

    /// <summary>Optional fixed RSI direction index for art authored from a specific source frame.</summary>
    [DataField]
    public int? ReferenceDirection;

    /// <summary>Explicit assemblies in RSI order (S, N, E, W, SE, SW, NE, NW). Empty slots have no authored pose.</summary>
    [DataField]
    public string[] DirectionalModels = [];

    /// <summary>Combined source sprite/layer tint for an explicit uncomposited RSI reference.</summary>
    [DataField]
    public Color ReferenceTint = Color.White;

    /// <summary>Source Sprite.Color already included in the authored part colors; avoid multiplying it twice.</summary>
    [DataField]
    public Color BakedSpriteTint = Color.White;

    /// <summary>Stable door pose represented by this assembly. Other states require a linked assembly.</summary>
    [DataField]
    public DoorState DoorState = DoorState.Closed;

    [DataField]
    public ProtoId<CMU3DModelPrototype>? AlternateDoorModel;

    /// <summary>FoldableComponent pose represented by this assembly. Unfolded is the default.</summary>
    [DataField]
    public bool Folded;

    /// <summary>Reciprocal link to the other authored fold pose; absent geometry remains unsupported.</summary>
    [DataField]
    public ProtoId<CMU3DModelPrototype>? AlternateFoldModel;

    /// <summary>Surface props rest on an explicitly authored support under their pivot; otherwise stay at ground level.</summary>
    [DataField]
    public string Placement = "floor";

    /// <summary>Authored horizontal pivot correction in map axes, independent of model facing. Never copies screen-height offsets.</summary>
    [DataField]
    public Vector2 GroundOffset;

    /// <summary>Unique part label whose top face is a usable tabletop or counter.</summary>
    [DataField]
    public string? SupportSurface;

    /// <summary>Separate flat support parts, such as pallet boards. Gaps between parts remain unsupported.</summary>
    [DataField]
    public string[] SupportSurfaces = [];

    /// <summary>Number of directions in the authored source RSI states; noRot still selects directional frames.</summary>
    [DataField]
    public int SourceDirections = 1;

    /// <summary>Some four-direction RSI states store east/west art in reversed slots; preserve their visible facing.</summary>
    [DataField]
    public bool SwapEastWest;

    /// <summary>Optional physical cardinal facings for source frames, indexed South/East/North/West (0..3). Repeated facings represent source aliases.</summary>
    [DataField]
    public int[] SourceCardinalFacings = [];

    /// <summary>Authored model axis correction in degrees, applied after source facing rules.</summary>
    [DataField]
    public float YawOffset;

    /// <summary>Use the physical entity axis when a single-frame source billboards a directional fixture.</summary>
    [DataField]
    public bool UseEntityRotation;

    /// <summary>Infer a directionless counter appliance's front from adjacent walls, preserving ambiguous layouts.</summary>
    [DataField]
    public bool FaceAwayFromWall;

    /// <summary>Also treat window runs as backing for an appliance using wall-context facing.</summary>
    [DataField]
    public bool FaceAwayFromWindows;

    /// <summary>Join grid panels, or extend an authored support surface, using IconSmooth neighbour keys.</summary>
    [DataField]
    public bool ConnectToNeighbours;

    /// <summary>Authored free-end inset. Members at these shortened endpoints extend to tile boundaries only at joined edges.</summary>
    [DataField]
    public float ConnectionEndInset;

    /// <summary>Eight CornerFill states in RSI S/N/E/W order, applied to the first four SE/NE/NW/SW surface patches. Remaining geometry is retained.</summary>
    [DataField]
    public ProtoId<CMU3DSurfacePrototype>[] CornerSurfaces = [];

    /// <summary>Physical fixture attached to a wall face; follows the wall cutaway.</summary>
    [DataField]
    public bool WallMounted;

    /// <summary>Explicit workstation families whose adjacent tile determines a wall display's visible side.</summary>
    [DataField]
    public string[] WallFacingTargets = [];

    /// <summary>Exact co-located glazing prototypes whose modeled face supports this shutter. Saved facing selects the mounting side.</summary>
    [DataField]
    public string[] WindowMountTargets = [];

    /// <summary>Fit this curtain on the opposite side of its saved front, inside co-located glazing.</summary>
    [DataField]
    public bool WindowMountInside;

    /// <summary>Exact solid trim or perpendicular panes defining the ends of this independent edge panel.</summary>
    [DataField]
    public string[] PanelEndTargets = [];

    /// <summary>Co-located source fixtures whose saved facing can replace a curtain opening facing directly into a wall.</summary>
    [DataField]
    public string[] OpeningFacingTargets = [];

    /// <summary>Exact static walls that may clear a fixture toward its resolved front. Adjacent-room mirrored mounts retain their placement unless FitInsideWall is enabled.</summary>
    [DataField]
    public string[] BackWallMountTargets = [];

    /// <summary>Also fit an adjacent-room mirrored fixture against the explicitly named backing walls.</summary>
    [DataField]
    public bool FitInsideWall;

    /// <summary>Opt in static one-frame wall paper to source-ordered, separated layers after wall attachment.</summary>
    [DataField]
    public bool WallPaper;

    [DataField(required: true)]
    public List<CMU3DModelPart> Parts = [];
}

[DataDefinition]
public sealed partial class CMU3DDoorSpriteState
{
    [DataField(required: true)]
    public List<CMU3DModelFrame> Frames = [];

    [DataField(required: true)]
    public List<float> Delays = [];
}

[DataDefinition]
public sealed partial class CMU3DDoorButtonState
{
    [DataField(required: true)]
    public List<CMU3DModelFrame> Frames = [];

    [DataField(required: true)]
    public List<CMU3DModelFrame> UnpoweredFrames = [];
}

[DataDefinition]
public sealed partial class CMU3DBarricadeAcidState
{
    [DataField(required: true)]
    public List<CMU3DModelFrame> Frames = [];

    [DataField(required: true)]
    public List<CMU3DModelFrame> WiredFrames = [];
}

[DataDefinition]
public sealed partial class CMU3DModelFrame
{
    [DataField(required: true)]
    public List<CMU3DModelPart> Parts = [];
}

[DataDefinition]
public sealed partial class CMU3DFrameKey
{
    [DataField]
    public float Time;

    [DataField(required: true)]
    public string State = string.Empty;

    [DataField]
    public int Frame;

    [DataField]
    public bool Unpowered;
}

public enum CMU3DPartShape : byte
{
    Box,
    Ellipsoid,
    CylinderX,
    CylinderY,
    CylinderZ,
    WedgeY,
    WedgeYReverse,
    SlantedX,
    SlantedXReverse,
    SlantedY,
    SlantedYReverse,
    Foliage,
}

/// <summary>A colored solid inscribed in named editable bounds.</summary>
[DataDefinition]
public sealed partial class CMU3DModelPart
{
    [DataField]
    public string Label = string.Empty;

    [DataField]
    public CMU3DPartShape Shape;

    /// <summary>Rotation in degrees around this part's center, about the model's vertical axis.</summary>
    [DataField]
    public float Yaw;

    /// <summary>Local tilt in degrees, lifting +X toward +Z before yaw is applied.</summary>
    [DataField]
    public float Pitch;

    public float YawRadians => Yaw * (MathF.PI / 180);
    public float PitchRadians => Pitch * (MathF.PI / 180);

    public void Bounds(out Vector3 min, out Vector3 max)
    {
        if (Yaw == 0 && Pitch == 0)
        {
            min = Min;
            max = Max;
            return;
        }
        var center = (Min + Max) / 2;
        var half = (Max - Min) / 2;
        var cp = MathF.Abs(MathF.Cos(PitchRadians));
        var sp = MathF.Abs(MathF.Sin(PitchRadians));
        half = new Vector3(half.X * cp + half.Z * sp, half.Y, half.X * sp + half.Z * cp);
        var c = MathF.Abs(MathF.Cos(YawRadians));
        var s = MathF.Abs(MathF.Sin(YawRadians));
        var extent = new Vector3(half.X * c + half.Y * s, half.X * s + half.Y * c, half.Z);
        min = center - extent;
        max = center + extent;
    }

    [DataField(required: true)]
    public Vector3 Min;

    [DataField(required: true)]
    public Vector3 Max;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public ProtoId<CMU3DSurfacePrototype>? Surface;

    [DataField]
    public CMU3DSurfaceAxis SurfaceAxis = CMU3DSurfaceAxis.XZ;

    [DataField]
    public bool SurfaceFlipU;

    /// <summary>Hide joined trim: north 1, south 2, east 4, west 8. Panel flags rotate with its axis; support flags use grid sides.</summary>
    [DataField]
    public int OmitWhenConnected;

    public bool Valid =>
        float.IsFinite(Yaw) && MathF.Abs(Yaw) <= 360 &&
        float.IsFinite(Pitch) && MathF.Abs(Pitch) <= 90 && (Pitch == 0 || Surface == null) &&
        OmitWhenConnected is >= 0 and <= 15 &&
        (byte) Shape <= (byte) CMU3DPartShape.Foliage &&
        (Surface == null || Shape is CMU3DPartShape.Box or CMU3DPartShape.WedgeY or CMU3DPartShape.WedgeYReverse && SurfaceAxis is CMU3DSurfaceAxis.XZ or CMU3DSurfaceAxis.XY or CMU3DSurfaceAxis.YZ) &&
        float.IsFinite(Min.X) && float.IsFinite(Min.Y) && float.IsFinite(Min.Z) &&
        float.IsFinite(Max.X) && float.IsFinite(Max.Y) && float.IsFinite(Max.Z) &&
        Min.X < Max.X && Min.Y < Max.Y && Min.Z < Max.Z;
}
