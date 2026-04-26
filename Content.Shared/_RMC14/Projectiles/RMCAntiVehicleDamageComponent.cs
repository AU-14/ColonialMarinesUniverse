using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Projectiles;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RMCProjectileSystem))]
public sealed partial class RMCAntiVehicleDamageComponent : Component
{
    [DataField]
    public float Multiplier = 5f;
}
