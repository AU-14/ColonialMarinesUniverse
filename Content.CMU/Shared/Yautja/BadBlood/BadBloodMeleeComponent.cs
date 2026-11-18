namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-604, ECS-609, STATE-603: a Bad Blood melee weapon. A hit on a living human who is not of the clan
/// staggers them (a short slow, at most once per <see cref="StaggerCooldown"/> per target) and pays
/// <see cref="ChargeRefund"/> back into the wielder's bracer. Every hit marks the target for a Blooded kill.
/// Only the Bad Blood weapon children carry it, so honourable weapons are unchanged. BadBloodMeleeSystem (server).
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodMeleeComponent : Component
{
    [DataField]
    public TimeSpan StaggerSlow = TimeSpan.FromSeconds(0.8);

    /// <summary>
    /// A target staggered by any Bad Blood weapon is not staggered again for this long.
    /// </summary>
    [DataField]
    public TimeSpan StaggerCooldown = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Bracer charge the wielder gets back per hit on a living human who is not of the clan.
    /// </summary>
    [DataField]
    public float ChargeRefund = 10f;
}
