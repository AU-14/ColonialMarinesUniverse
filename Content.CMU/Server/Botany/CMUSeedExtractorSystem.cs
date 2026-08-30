using System.Linq;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Botany;

/// <summary>
/// Preserves CMU's bulk plant-bag interaction on the shared Nubotany seed extractor.
/// </summary>
public sealed class CMUSeedExtractorSystem : EntitySystem
{
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeedExtractorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnGetAlternativeVerbs(Entity<SeedExtractorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            !_power.IsPowered(ent.Owner) ||
            args.Using is not { } plantBag ||
            !HasComp<CMUPlantBagComponent>(plantBag) ||
            !HasComp<StorageComponent>(plantBag))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("seed-extractor-component-convert-plant-bag"),
            IconEntity = GetNetEntity(plantBag),
            Act = () => ConvertPlantBag(ent, plantBag, user),
        });
    }

    private void ConvertPlantBag(Entity<SeedExtractorComponent> ent, EntityUid plantBag, EntityUid user)
    {
        if (!Exists(plantBag) ||
            !_power.IsPowered(ent.Owner) ||
            !HasComp<CMUPlantBagComponent>(plantBag) ||
            !TryComp(plantBag, out StorageComponent? storage))
        {
            return;
        }

        var produceConverted = 0;
        var seedsExtracted = 0;
        foreach (var item in storage.Container.ContainedEntities.ToList())
        {
            if (!TryComp(item, out ProduceComponent? produce) ||
                produce.PlantProtoId is not { } plantProtoId ||
                _botany.TryGetPlantComponent<PlantTraitSeedlessComponent>(produce.PlantData, plantProtoId, out _) ||
                !_botany.TryGetPlantComponent<PlantDataComponent>(produce.PlantData, plantProtoId, out var plantData))
            {
                continue;
            }

            var baseSeeds = ent.Comp.BaseSeeds;
            var amount = baseSeeds.Next(_random);
            var coords = Transform(ent).Coordinates;
            for (var i = 0; i < amount; i++)
            {
                _botany.SpawnSeedPacket(plantData, plantProtoId, produce.PlantData, coords, user);
                seedsExtracted++;
            }

            QueueDel(item);
            produceConverted++;
        }

        if (produceConverted == 0)
        {
            _popup.PopupEntity(
                Loc.GetString("seed-extractor-component-plant-bag-no-seeds"),
                ent,
                user,
                PopupType.MediumCaution);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("seed-extractor-component-plant-bag-converted",
                ("produce", produceConverted),
                ("seeds", seedsExtracted)),
            ent,
            user,
            PopupType.Medium);
    }
}
