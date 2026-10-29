namespace Content.Shared.Climbing.Components;

public sealed partial class ClimbingComponent
{
    // Local deferred work; state is rechecked after contact dispatch before applying it.
    public bool CMUEndClimbPending;
}
