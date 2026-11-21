using System.Linq;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.CMU14.Botany;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Botany;

/// <summary>
/// Preserves CMU's bulk plant-bag interaction on the seed extractor.
/// CMU14: lives on <see cref="CMUBatchSeedExtractorComponent"/> (which replaced the upstream SeedExtractor component on the
/// prototype) and uses the shared <see cref="CMUSeedYield"/> rules, so bag and batch give the same yield.
/// </summary>
public sealed partial class CMUSeedExtractorSystem : EntitySystem
{
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUBatchSeedExtractorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnGetAlternativeVerbs(Entity<CMUBatchSeedExtractorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
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

    private void ConvertPlantBag(Entity<CMUBatchSeedExtractorComponent> ent, EntityUid plantBag, EntityUid user)
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
                !CMUSeedYield.TryGetSeedSource(_botany, produce, out var plantProtoId, out var plantData))
            {
                continue;
            }

            var amount = CMUSeedYield.RollPacketCount(ent.Comp.BaseSeeds, _random);
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
