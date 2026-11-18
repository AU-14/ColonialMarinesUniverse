namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ARCH-703: raised on a living Bad Blood standing at a Totem or a Hunt Ground (BadBloodTotemSystem.CountNear).
/// <see cref="Weight"/> is how much they count toward seizing it: 1 unless a caste says otherwise (a raging Berserker
/// counts 2, an Engineer 1.5). Subscribers multiply or raise it; they never lower it below 0.
/// </summary>
[ByRefEvent]
public record struct BadBloodContestWeightEvent()
{
    public float Weight = 1f;
}

/// <summary>
/// CMU14 ARCH-703: raised on a user starting to desecrate (<see cref="Desecrate"/>) or reconsecrate a Totem.
/// The do-after takes <see cref="Multiplier"/> times its time (an Engineer reconsecrates in half).
/// </summary>
[ByRefEvent]
public record struct BadBloodConsecrationTimeEvent(bool Desecrate)
{
    public float Multiplier = 1f;
}
