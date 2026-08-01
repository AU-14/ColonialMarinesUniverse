using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects.Negative;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Content.Server._CMU14.Chemistry.HydroTrayEffects;

public sealed partial class HydroTrayEffectSystem : EntitySystem
{
    [Dependency] private PlantHolderSystem _plantHolder = default!;
    [Dependency] private MutationSystem _mutation = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HydroTickEvent<Carcinogenic>>(Carcinogenic);
    }




    private void Carcinogenic(ref HydroTickEvent<Carcinogenic> args)
    {
        if (!CanMetabolizePlant(args.Args.TargetEntity, out var pcomp, mustHaveMutableSeed: true))
            return;

        pcomp.Toxins += 1.5f * ((float)args.Potency * 2f) * (float)args.Args.Quantity;
        pcomp.MutationLevel += 10 * ((float)args.Potency * 2) * ((float)args.Args.Quantity + pcomp.MutationMod);
    }













    private bool CanMetabolizePlant(EntityUid plantHolder, [NotNullWhen(true)] out PlantHolderComponent? plantHolderComponent,
        bool mustHaveAlivePlant = true, bool mustHaveMutableSeed = false)
    {
        plantHolderComponent = null;

        if (!TryComp(plantHolder, out plantHolderComponent))
            return false;

        if (mustHaveAlivePlant && (plantHolderComponent.Seed == null || plantHolderComponent.Dead))
            return false;

        if (mustHaveMutableSeed && (plantHolderComponent.Seed == null || plantHolderComponent.Seed.Immutable))
            return false;

        return true;
    }
}
