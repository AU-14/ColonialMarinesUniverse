using Content.Shared._RMC14.Throwing;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;

namespace Content.Server.Fluids.EntitySystems;

public sealed partial class SpraySystem
{
    private void ApplyRMCSprayVaporPolicy(EntityUid sprayer, EntityUid vapor)
    {
        if (TryComp<RMCSprayAmmoProviderComponent>(sprayer, out var provider) && provider.HitUser)
            EnsureComp<ThrownHitUserComponent>(vapor);
    }
}
