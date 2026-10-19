using Content.Server.Body.Systems;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Atmos;
using Content.Shared.Inventory;

namespace Content.Server.CMU14.Xenomorphs.Parasite;

/// <summary>
///     A parasite on someone's face keeps its host breathing, the way a mask on internals would: they breathe
///     clean air through it, whatever is around them.
/// </summary>
public sealed partial class CMUParasiteBreathingSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<InfectableComponent, InhaleLocationEvent>(OnInhaleLocation, after: [typeof(InternalsSystem)]);
    }

    private void OnInhaleLocation(Entity<InfectableComponent> ent, ref InhaleLocationEvent args)
    {
        if (!ent.Comp.BeingInfected ||
            !_inventory.TryGetSlotEntity(ent, "mask", out var mask) ||
            !TryComp<XenoParasiteComponent>(mask, out var parasite) ||
            parasite.InfectedVictim != ent.Owner ||
            parasite.FellOff)
        {
            return;
        }

        var air = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        air.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard);
        air.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard);
        args.Gas = air;
    }
}
