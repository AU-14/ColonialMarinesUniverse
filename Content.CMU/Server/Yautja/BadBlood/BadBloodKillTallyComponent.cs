namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// How many worthy kills this Bad Blood has made. Breaks ties in the ascension vote.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodKillTallyComponent : Component
{
    [DataField]
    public int Kills;
}
