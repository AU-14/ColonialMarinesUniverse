namespace Content.Server._CMU14.ZLevels.Core;

[RegisterComponent]
public sealed partial class CMUZLevelLadderWatchingComponent : Component
{
    public EntityUid? Ladder;
    public int Offset;
    public EntityUid? PeekTarget;
    public EntityUid? PreviousTarget;
    public int LookOffset;
}
