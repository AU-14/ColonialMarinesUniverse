using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Projectile.Spit.Stacks;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSpitSystem))]
public sealed partial class ApplyAcidStacksComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Amount = 1;

    [DataField, AutoNetworkedField]
    public int Max = 5;

    [DataField, AutoNetworkedField]
    public DamageSpecifier? Damage;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist = new() { Components = ["Marine"] };

    // CMU14 restored from master: only stack acid on targets that already have it
    [DataField, AutoNetworkedField]
    public bool RequireExisting;
}
