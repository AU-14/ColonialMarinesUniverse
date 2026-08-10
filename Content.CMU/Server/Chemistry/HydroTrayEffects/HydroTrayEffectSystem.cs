using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects.Negative;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;

namespace Content.Server._CMU14.Chemistry.HydroTrayEffects;

public sealed partial class HydroTrayEffectSystem : EntitySystem
{
    [Dependency] private PlantHolderSystem _plantHolder = default!;
    [Dependency] private PlantTraySystem _plantTray = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HydroTickEvent<Carcinogenic>>(OnCarcinogenic);
    }

    private void OnCarcinogenic(ref HydroTickEvent<Carcinogenic> args)
    {
        var target = args.Args.TargetEntity;
        var quantity = (float) args.Args.Quantity;
        var potency = (float) args.Potency;

        if (TryComp<PlantTrayComponent>(target, out var tray))
            _plantTray.AdjustToxin((target, tray), 1.5f * potency * 2f * quantity);

        if (!TryComp<PlantHolderComponent>(target, out var plant) || plant.Dead)
            return;

        _plantHolder.AdjustsMutationLevel((target, plant), 10f * potency * 2f * quantity);
    }
}
