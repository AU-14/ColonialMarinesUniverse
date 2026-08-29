using Content.Shared.CMU14.Chemistry.Effects.Positive;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects.Negative;
using Content.Shared._RMC14.Chemistry.Effects.Positive;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Chemistry.HydroTrayEffects;

public sealed partial class HydroTrayEffectSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PlantHolderSystem _plantHolder = default!;
    [Dependency] private PlantTraySystem _plantTray = default!;

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
        SubscribeLocalEvent<PlantHolderComponent, BeforeRandomPlantMutationEvent>(OnBeforeRandomPlantMutation);
        SubscribeLocalEvent<PlantSpeciesChangedEvent>(OnPlantSpeciesChanged);
    }

    private void Antitoxic(ref HydroTickEvent<Antitoxic> args)
    {
        if (!TryGetAlivePlant(args.Target, out var tray, out _))
            return;

        _plantTray.AdjustToxin(tray, -HydroStrength(args.Potency, args.Quantity) * 10f);
        if (tray.Comp.ToxinLevel > 0)
            _plantTray.AdjustToxin(tray, -1.5f * ((float) args.Potency * 2f));
    }

    private void Anticorrosive(ref HydroTickEvent<Anticorrosive> args)
    {
        if (!TryGetAlivePlant(args.Target, out var tray, out var plant))
            return;

        var healing = HydroStrength(args.Potency, args.Quantity) * 5f;
        if (tray.Comp.ToxinLevel > 0)
            healing += 0.75f * ((float) args.Potency * 2f);

        _plantHolder.AdjustsHealth(plant, healing);
    }

    private void Hepatopeutic(ref HydroTickEvent<Hepatopeutic> args)
        => EnableMutations(args.Target,
            "ChangeLifespan",
            "ChangeEndurance",
            "ChangeWaterConsumption",
            "ChangeNutrientConsumption");

    private void Nephropeutic(ref HydroTickEvent<Nephropeutic> args)
        => EnableMutations(args.Target, "ChangeToxinsTolerance", "ChangeWeedTolerance");

    private void Pneumopeutic(ref HydroTickEvent<Pneumopeutic> args)
        => EnableMutations(args.Target,
            "ChangeEndurance",
            "ChangeLifespan",
            "ChangeProduction",
            "ChangeMaturation");

    private void Oculopeutic(ref HydroTickEvent<Oculopeutic> args)
        => EnableMutations(args.Target, "ChangePotency");

    private void Neuropeutic(ref HydroTickEvent<Neuropeutic> args)
        => EnableMutations(args.Target, "ChangeSpecies");

    private void Cardiopeutic(ref HydroTickEvent<Cardiopeutic> args)
    {
        if (!TryGetAlivePlant(args.Target, out _, out var plant))
            return;

        var suppression = EnsureComp<CMUChemicalMutationSuppressionComponent>(plant);
        var duration = TimeSpan.FromSeconds(60f * MathF.Max(1f, HydroStrength(args.Potency, args.Quantity)));
        suppression.ExpiresAt = Max(suppression.ExpiresAt, _timing.CurTime + duration);
    }

    private void EnableMutations(EntityUid target, params string[] mutationNames)
    {
        if (!TryGetAlivePlant(target, out _, out var plant))
            return;

        EnsureComp<CMUChemicalMutationWhitelistComponent>(plant).AllowedMutations.UnionWith(mutationNames);
    }

    private static float HydroStrength(FixedPoint2 potency, ReagentQuantity quantity)
        => MathF.Max(0f, (float) potency * (float) quantity.Quantity);

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
        if (!TryGetAlivePlant(args.Target, out var tray, out var plant))
            return;

        _plantTray.AdjustToxin(tray, 1.5f * ((float) args.Potency * 2f) * (float) args.Quantity.Quantity);
        plant.Comp.MutationLevel = MathHelper.Clamp(
            plant.Comp.MutationLevel + 10f * ((float) args.Potency * 2f) *
            ((float) args.Quantity.Quantity + plant.Comp.MutationMod),
            0f,
            plant.Comp.MaxMutationLevel);
        DirtyField(plant, nameof(plant.Comp.MutationLevel));
    }

    private void OnBeforeRandomPlantMutation(
        Entity<PlantHolderComponent> plant,
        ref BeforeRandomPlantMutationEvent args)
    {
        if (TryComp<CMUChemicalMutationWhitelistComponent>(plant, out var whitelist) &&
            whitelist.AllowedMutations.Count > 0 &&
            !whitelist.AllowedMutations.Contains(args.Mutation.Name))
        {
            args.Cancelled = true;
            return;
        }

        if (args.Mutation.Name == "ChangeChemicals" &&
            TryComp<CMUChemicalMutationSuppressionComponent>(plant, out var suppression) &&
            suppression.ExpiresAt > _timing.CurTime)
        {
            args.Cancelled = true;
        }
    }

    private void OnPlantSpeciesChanged(ref PlantSpeciesChangedEvent args)
    {
        if (TryComp<CMUChemicalMutationWhitelistComponent>(args.OldPlant, out var oldWhitelist) &&
            oldWhitelist.AllowedMutations.Count > 0)
        {
            EnsureComp<CMUChemicalMutationWhitelistComponent>(args.NewPlant)
                .AllowedMutations.UnionWith(oldWhitelist.AllowedMutations);
        }

        if (TryComp<CMUChemicalMutationSuppressionComponent>(args.OldPlant, out var oldSuppression) &&
            oldSuppression.ExpiresAt > _timing.CurTime)
        {
            var newSuppression = EnsureComp<CMUChemicalMutationSuppressionComponent>(args.NewPlant);
            newSuppression.ExpiresAt = Max(newSuppression.ExpiresAt, oldSuppression.ExpiresAt);
        }
    }

    private bool TryGetAlivePlant(
        EntityUid trayUid,
        out Entity<PlantTrayComponent> tray,
        out Entity<PlantHolderComponent> plant)
    {
        tray = default;
        plant = default;

        if (!TryComp<PlantTrayComponent>(trayUid, out var trayComp))
            return false;

        tray = (trayUid, trayComp);
        if (!_plantTray.TryGetAlivePlant(tray, out var plantUid) ||
            !TryComp<PlantHolderComponent>(plantUid, out var plantHolder))
        {
            return false;
        }

        plant = (plantUid.Value, plantHolder);
        return true;
    }
}
