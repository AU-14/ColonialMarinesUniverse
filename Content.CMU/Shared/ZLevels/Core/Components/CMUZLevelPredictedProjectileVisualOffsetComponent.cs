using System.Numerics;

namespace Content.Shared.CMU14.ZLevels.Core.Components;

[RegisterComponent]
public sealed partial class CMUZLevelPredictedProjectileVisualOffsetComponent : Component
{
    public Vector2 Offset;

    public Vector2? OriginalOffset;

    public Vector2 AppliedOffset;
}
