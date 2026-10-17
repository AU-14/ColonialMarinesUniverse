using Content.Shared._RMC14.Weapons.Ranged.AimedShot;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedGunSystem), typeof(XenoProjectileSystem), typeof(SharedRMCAimedShotSystem))]
public sealed partial class TargetedProjectileComponent : Component
{
    [DataField]
    public EntityUid Target;
}

[Serializable, NetSerializable]
public sealed class TargetedProjectileComponentState : ComponentState
{
    public NetEntity Target { get; init; }
}
