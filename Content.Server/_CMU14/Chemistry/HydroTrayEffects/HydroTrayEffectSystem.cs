using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects.Negative;
using Content.Shared._RMC14.Chemistry.Effects.Positive;
using Content.Shared._CMU14.Chemistry.Effects.Positive;
using Robust.Shared.Timing;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server._CMU14.Chemistry.HydroTrayEffects;

public sealed partial class HydroTrayEffectSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HydroTickEvent<Carcinogenic>>(Carcinogenic);
        SubscribeLocalEvent<HydroTickEvent<Antitoxic>>(Antitoxic);
        SubscribeLocalEvent<HydroTickEvent<Anticorrosive>>(Anticorrosive);
        SubscribeLocalEvent<HydroTickEvent<Hepatopeutic>>(Hepatopeutic);
        SubscribeLocalEvent<HydroTickEvent<Nephropeutic>>(Nephropeutic);
        SubscribeLocalEvent<HydroTickEvent<Pneumopeutic>>(Pneumopeutic);
        SubscribeLocalEvent<HydroTickEvent<Oculopeutic>>(Oculopeutic);
        SubscribeLocalEvent<HydroTickEvent<Cardiopeutic>>(Cardiopeutic);
        SubscribeLocalEvent<HydroTickEvent<Neuropeutic>>(Neuropeutic);
    }

    private void Antitoxic(ref HydroTickEvent<Antitoxic> args)
    {
        if (!CanMetabolizePlant(args.Args.TargetEntity, out var plant))
            return;
        plant.Toxins = MathF.Max(0f, plant.Toxins - HydroStrength(args.Potency, args.Args) * 10f);
        if (plant.Toxins > 0)
            plant.Toxins += -1.5f * ((float) args.Potency * 2f);
    }

    private void Anticorrosive(ref HydroTickEvent<Anticorrosive> args)
    {
        if (!CanMetabolizePlant(args.Args.TargetEntity, out var plant) || plant.Seed == null)
            return;
        plant.Health = MathF.Min(plant.Seed.Endurance, plant.Health + HydroStrength(args.Potency, args.Args) * 5f);
        if (plant.Toxins > 0)
            plant.Health += 0.75f * ((float) args.Potency * 2f);
    }

    private void Hepatopeutic(ref HydroTickEvent<Hepatopeutic> args)
        => EnableMutations(args.Args, "Plant Cancer", "Gluttony");

    private void Nephropeutic(ref HydroTickEvent<Nephropeutic> args)
        => EnableMutations(args.Args, "Light Tolerance", "Weed Tolerance", "Toxin Tolerance");

    private void Pneumopeutic(ref HydroTickEvent<Pneumopeutic> args)
        => EnableMutations(args.Args, "Endurance", "Lifespan", "Production", "Maturity");

    private void Oculopeutic(ref HydroTickEvent<Oculopeutic> args)
        => EnableMutations(args.Args, "Potency", "Bioluminescence", "Flowers");

    private void Neuropeutic(ref HydroTickEvent<Neuropeutic> args)
        => EnableMutations(args.Args, "Mutate Species");

    private void Cardiopeutic(ref HydroTickEvent<Cardiopeutic> args)
    {
        if (!CanMetabolizePlant(args.Args.TargetEntity, out _))
            return;
        var suppression = EnsureComp<CMUChemicalMutationSuppressionComponent>(args.Args.TargetEntity);
        var duration = TimeSpan.FromSeconds(60f * MathF.Max(1f, HydroStrength(args.Potency, args.Args)));
        suppression.ExpiresAt = Max(suppression.ExpiresAt, _timing.CurTime + duration);
    }

    private void EnableMutations(EntityEffectHydroArgs args, params string[] mutationNames)
    {
        if (!CanMetabolizePlant(args.TargetEntity, out var plant, mustHaveMutableSeed: true) || plant.Seed == null)
            return;

        foreach (var mutationName in mutationNames)
        {
            if (plant.MutationController.Fields[mutationName] < 1)
                plant.MutationController.Fields[mutationName] = 1;
        }
    }

    private static float HydroStrength(FixedPoint2 potency, EntityEffectHydroArgs args)
        => MathF.Max(0f, (float)potency * (float)args.Quantity);

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUChemicalMutationSuppressionComponent>();
        while (query.MoveNext(out var uid, out var suppression))
        {
            if (suppression.ExpiresAt <= now)
                RemCompDeferred<CMUChemicalMutationSuppressionComponent>(uid);
        }
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
