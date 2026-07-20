namespace Content.Shared.Flash;

public abstract partial class SharedFlashSystem
{
    /// <summary>
    /// RMC compatibility overload for the legacy millisecond flash duration API.
    /// </summary>
    public bool Flash(
        EntityUid target,
        EntityUid? user,
        EntityUid? used,
        float flashDuration,
        float slowTo = 0.8f,
        bool displayPopup = true,
        bool melee = false,
        TimeSpan? stunDuration = null)
    {
        return Flash(target,
            user,
            used,
            TimeSpan.FromMilliseconds(flashDuration),
            slowTo,
            displayPopup,
            melee,
            stunDuration);
    }
}
