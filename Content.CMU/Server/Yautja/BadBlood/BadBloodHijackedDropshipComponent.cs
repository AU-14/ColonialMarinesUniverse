namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14: a dropship a Bad Blood hijacked (docs/GraphAudit-BadBlood-ObjectivesPlan.md STATE-503). Tracking and logs
/// only, it changes nothing about the hijack or the Vessel Strike.
/// </summary>
[RegisterComponent, Access(typeof(BadBloodHijackSystem))]
public sealed partial class BadBloodHijackedDropshipComponent : Component
{
    /// <summary>
    /// The Bad Blood who hijacked the dropship.
    /// </summary>
    [DataField]
    public EntityUid Hijacker;

    /// <summary>
    /// When the hijack flight left.
    /// </summary>
    [DataField]
    public TimeSpan StartedAt;

    /// <summary>
    /// The crash landing was logged, so a later hijack landing does not log it again.
    /// </summary>
    [DataField]
    public bool CrashLogged;
}
