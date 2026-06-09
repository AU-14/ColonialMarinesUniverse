using Content.Shared.Damage;
namespace Content.Shared._RMC14.Weapons.Ranged.Vulture;

[RegisterComponent]
public sealed partial class VultureRifleComponent : Component
{
    [DataField]
    public string BipodSlot = "rmc-aslot-underbarrel";

    [DataField]
    public DamageSpecifier UnbracedDamage = new()
    {
        DamageDict =
        {
            ["Blunt"] = 15,
        },
    };

    [DataField]
    public TimeSpan UnbracedKnockdown = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan UnbracedSlowdown = TimeSpan.FromSeconds(2);

    [DataField]
    public float UnbracedWalkModifier = 0.5f;

    [DataField]
    public float UnbracedSprintModifier = 0.5f;

    [DataField]
    public float UnbracedKnockback = 3f;

    [DataField]
    public float UnbracedKnockbackSpeed = 8f;
}
