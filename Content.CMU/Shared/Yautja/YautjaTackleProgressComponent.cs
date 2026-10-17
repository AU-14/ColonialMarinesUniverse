namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// Server-authoritative count of recent successful Xeno tackle rolls against a Yautja.
/// </summary>
[RegisterComponent]
public sealed partial class YautjaTackleProgressComponent : Component
{
    public int Successes;

    public TimeSpan LastSuccessAt;
}
