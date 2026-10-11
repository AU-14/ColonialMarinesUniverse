using System.Numerics;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>Complete poses for a single source-owned RSI layer. Live sampling follows its existing animation frame.</summary>
    [DataField]
    public Dictionary<string, CMU3DSpriteState> SpriteStates = [];

    /// <summary>Expected source screen offset, used only to reject unsupported appearances. GroundOffset owns physical placement.</summary>
    [DataField]
    public Vector2 SourceSpriteOffset;

    /// <summary>Opt in to an audited rotating source sprite instead of the default no-rotation frame contract.</summary>
    [DataField]
    public bool SourceSpriteRotates;
}

[DataDefinition]
public sealed partial class CMU3DSpriteState
{
    [DataField(required: true)]
    public List<CMU3DModelFrame> Frames = [];

    /// <summary>Original RSI delays for validation and portable export; never a second live animation clock.</summary>
    [DataField(required: true)]
    public List<float> Delays = [];
}
