using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Weapons.Ranged;

// sits on the gun, so one ammo type can get extra comps from this gun only and the shell
// itself stays the same everywhere else
[RegisterComponent]
[Access(typeof(CMUAmmoProjectileComponentsSystem))]
public sealed partial class CMUAmmoProjectileComponentsComponent : Component
{
    // spread pellets are their own projectiles, so list the pellet proto, not the shell
    [DataField(required: true)]
    public HashSet<EntProtoId> Projectiles = new();

    [DataField(required: true)]
    public ComponentRegistry Components = new();
}
