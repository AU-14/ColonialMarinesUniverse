using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    private void HandleHardpointDamageSideEffects(
        EntityUid vehicle,
        EntityUid hardpoint,
        float amount,
        float previous,
        HardpointIntegrityComponent integrity,
        bool wasFunctional)
    {
        RefreshGunModifiers(hardpoint);
        TryTriggerHardpointFailure(vehicle, hardpoint, amount, previous, integrity);

        if (hardpoint != vehicle)
        {
            RefreshSupportModifiers(vehicle);
            RefreshVehicleArmorModifiers(vehicle);
            RefreshVehicleMechanicalFailureModifiers(vehicle);
            RefreshVehicleFrameIntegrityFromHardpoints(vehicle);
        }

        var isFunctional = IsHardpointFunctional(hardpoint, integrity);
        if (wasFunctional != isFunctional || HasComp<GunComponent>(hardpoint))
            RaiseHardpointSlotsChanged(vehicle);
    }
}
