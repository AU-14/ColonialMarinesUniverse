namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-603: a freed Bad Blood body (an unclaimed landing body, or one a player left with /ghost) on offer as a ghost
/// role. While the Blood Reserve has anyone in it, only reserve members may ask for it for
/// <see cref="BadBloodCampRuleComponent.ReserveBodyPriority"/> after <see cref="BodyOfferedAt"/>.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodOfferedBodyComponent : Component
{
    [ViewVariables]
    public TimeSpan BodyOfferedAt;
}
