namespace Content.Shared.CMU14.Fireman;

/// <summary>
/// Raised on a puller before RMC's pull-paralyze punishment (RMCPullingSystem.OnParalyzeOnPullAttempt) applies to it
/// for trying to pull <see cref="Target"/>. Set <see cref="Immune"/> to let the pull through unpunished, the same way
/// a synthetic may pull the castes on its ParalyzeImmuneFrom whitelist.
/// </summary>
[ByRefEvent]
public record struct CMUPullParalyzeImmuneEvent(EntityUid Target, bool Immune = false);
