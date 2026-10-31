namespace Content.Shared.CMU14.Yautja;

// cmss13's attacked_by for humans: the melee skill bonus only lands on non-humans, and sharp (30) / edged (10)
// weapons punch through that much melee armour. our marines wear ~35 melee where cm wore 20, so without this a
// clan sword did 61 to a naked colonist and 24 through standard armour instead of a flat 35
[RegisterComponent]
public sealed partial class YautjaHumanMeleeComponent : Component
{
    [DataField]
    public int Penetration = 40;
}
