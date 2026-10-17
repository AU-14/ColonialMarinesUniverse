using Content.Shared.Body.Part;

namespace Content.Shared.CMU14.Yautja;

[RegisterComponent]
public sealed partial class YautjaLimbRegenerationComponent : Component
{
    [DataField]
    public int RequiredSeverAttempts = 2;

    [DataField]
    public TimeSpan SeverAttemptWindow = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan RegrowDelay = TimeSpan.FromSeconds(120);

    public Dictionary<EntityUid, YautjaSeverAttemptProgress> SeverAttempts = new();

    public Dictionary<YautjaLimbSite, TimeSpan> PendingRegrowth = new();
}

public readonly record struct YautjaSeverAttemptProgress(int Count, TimeSpan LastAttempt);

public readonly record struct YautjaLimbSite(BodyPartType Type, BodyPartSymmetry Symmetry);
