using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Projectile.Spit.Slowing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSpitSystem), typeof(XenoProjectileSystem))]
public sealed partial class XenoSlowingSpitProjectileComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool SuperSlow = true;

    [DataField, AutoNetworkedField]
    public TimeSpan Slow = TimeSpan.FromSeconds(8);

    [DataField, AutoNetworkedField]
    public TimeSpan Paralyze = TimeSpan.FromSeconds(3.5);

    [DataField, AutoNetworkedField]
    public bool ArmorResistsKnockdown = true;

    /// <summary>
    /// This spit delivers neurotoxin, so species flagged NO_NEURO shrug its paralysis off.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Neurotoxin; // CMU14: CM-SS13 parity, /proc/apply_neuro returns early for NO_NEURO

    /// <summary>
    /// CM-SS13's insta_neuro argument: the target's Endurance does not shrug this neurotoxin off
    /// (/datum/ammo/xeno/toxin/queen passes TRUE). Only meaningful together with <see cref="Neurotoxin"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InstaNeuro; // CMU14: CM-SS13 /proc/apply_neuro insta_neuro

    /// <summary>
    /// CMU14: this spit still knocks an otherwise neuro-immune target (a Yautja, CM-SS13 NO_NEURO) down.
    /// Only ActionXenoScatteredSpit's XenoScatteredSpitProjectile sets it, and that projectile keeps slow
    /// at 0, so the ability's knockdown lands while its slow and every other neurotoxin source stay immune.
    /// </summary>
    [DataField]
    public bool KnocksDownNeuroImmune;
}
