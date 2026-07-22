using Content.Server.Fluids.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._RMC14.Fluids;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.Fluids.Components;
using Robust.Shared.Map;

namespace Content.Server._RMC14.Fluids;

public sealed partial class RMCSpraySystem : SharedRMCSpraySystem
{
    [Dependency] private SpraySystem _spray = default!;

    public override void Spray(EntityUid entity, EntityUid user, MapCoordinates mapcoord, bool hitUser = false)
    {
        base.Spray(entity, user, mapcoord, hitUser);

        if (TryComp(entity, out SprayComponent? spray) &&
            TryComp(entity, out RMCSprayAmmoProviderComponent? provider))
        {
            _spray.Spray(
                (entity, spray),
                mapcoord,
                user,
                predictedSound: true,
                transferAmountOverride: provider.Cost);
        }
    }
}
