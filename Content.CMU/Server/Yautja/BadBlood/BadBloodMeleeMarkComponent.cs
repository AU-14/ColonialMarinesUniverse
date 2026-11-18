namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 STATE-603: the last Bad Blood melee hit on this mob. If it dies within the rule's
/// <c>BloodedKillWindow</c> of <see cref="At"/>, <see cref="Striker"/> earns the Blooded kill. Also holds the
/// stagger cooldown (ECS-604), which is per target. Removed when the mob dies.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodMeleeMarkComponent : Component
{
    [ViewVariables]
    public EntityUid Striker;

    [ViewVariables]
    public TimeSpan At;

    /// <summary>
    /// No Bad Blood weapon staggers this mob again before this time.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextStagger;
}
