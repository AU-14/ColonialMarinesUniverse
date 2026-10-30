namespace Content.Shared.CMU14.Xenomorphs.Melee;

// scales the per-hit limb sever roll of a XenoMeleeSever attacker (the queen) against this mob
[RegisterComponent]
public sealed partial class XenoMeleeSeverResistanceComponent : Component
{
    [DataField]
    public float ChanceMultiplier = 1f;
}
