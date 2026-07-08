using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Armor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMArmorSystem))]
public sealed partial class CMArmorComponent : Component
{
    // TODO RMC14 other types of armor
    [DataField, AutoNetworkedField]
    public int XenoArmor;

    [DataField, AutoNetworkedField]
    public int FrontalArmor;

    [DataField, AutoNetworkedField]
    public int SideArmor;

    [DataField, AutoNetworkedField]
    public int Melee;

    [DataField, AutoNetworkedField]
    public int Bullet;

    [DataField, AutoNetworkedField]
    public int Bio;

    // TODO RMC14 some rockets should penetrate armor
    // TODO RMC14 tank/sniper flak/shotgun incendiary burst is resisted by this but penetrated
    [DataField, AutoNetworkedField]
    public int ExplosionArmor;

    /// <summary>
    ///     Optional locational override. Empty means the item uses inventory slot
    ///     targeting only; filled means the item only protects these body zones
    ///     after the hit has reached one of its equipped slots.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<TargetBodyZone> CoveredZones = new();

    [DataField, AutoNetworkedField]
    public bool ImmuneToAP = false;
}
