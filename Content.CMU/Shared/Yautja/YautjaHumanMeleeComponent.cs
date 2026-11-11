namespace Content.Shared.CMU14.Yautja;

// cmss13's attacked_by for humans: the melee skill bonus only lands on non-humans, and sharp (30) / edged (10)
// weapons punch through that much melee armour. our marines wear ~35 melee where cm wore 20, so without this a
// clan sword did 61 to a naked colonist and 24 through standard armour instead of a flat 35
[RegisterComponent]
public sealed partial class YautjaHumanMeleeComponent : Component
{
    [DataField]
    public int Penetration = 40;

    // cm rolls prob(armour) per hit to blunt the edge, and then the armour counts in full. 1% per point
    [DataField]
    public float StripChancePerArmor = 0.01f;

    // cm adds brute to pain 1:1, but a human's pain is out of 200 (horrible at 80%) and ours is out of 100,
    // so it's half per point here. a naked marine shocks on the 4th wielded glaive hit, not the 2nd
    [DataField]
    public float PainPerDamage = 0.5f;
}
