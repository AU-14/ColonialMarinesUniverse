using System.Collections.Immutable;
using System.Linq;
using Content.Server._RMC14.Requisitions;
using Content.Server.AU14.Round;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU.Round;

namespace Content.Server.CMU.Round;

/// <summary>
/// Projects director-owned force catalogs onto side-specific ASRS console infrastructure.
/// </summary>
public sealed partial class RoundAsrsConsoleCatalogSystem : EntitySystem
{
    [Dependency] private CMURoundDirectorSystem _director = default!;
    [Dependency] private RequisitionsSystem _requisitions = default!;

    private readonly HashSet<EntityUid> _sideConsoles = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RequisitionsComputerComponent, ComponentStartup>(OnComputerStartup);
        SubscribeLocalEvent<RequisitionsComputerComponent, ComponentShutdown>(OnComputerShutdown);
        SubscribeLocalEvent<CMURoundPhaseChangedEvent>(OnRoundPhaseChanged);
    }

    private void OnComputerStartup(Entity<RequisitionsComputerComponent> ent, ref ComponentStartup args)
    {
        if (!TryGetSide(ent.Comp.Faction, out var side))
            return;

        _sideConsoles.Add(ent);
        TryBindCommittedCatalog(ent, side);
    }

    private void OnComputerShutdown(Entity<RequisitionsComputerComponent> ent, ref ComponentShutdown args)
    {
        _sideConsoles.Remove(ent);
    }

    private void OnRoundPhaseChanged(ref CMURoundPhaseChangedEvent args)
    {
        if (args.Phase != CMURoundPhase.WorldInitialized)
            return;

        foreach (var uid in _sideConsoles.ToArray())
        {
            if (!TryComp(uid, out RequisitionsComputerComponent? computer))
            {
                _sideConsoles.Remove(uid);
                continue;
            }

            if (TryGetSide(computer.Faction, out var side))
                TryBindCommittedCatalog((uid, computer), side);
        }
    }

    private bool TryBindCommittedCatalog(
        Entity<RequisitionsComputerComponent> computer,
        RoundSide side)
    {
        if (!WorldIsInitialized(_director.Phase))
            return false;

        if (!_director.TryGetCommittedAsrsCatalog(side, out var catalog))
        {
            var assignment = side switch
            {
                RoundSide.Govfor => _director.Selection?.GovforAssignment,
                RoundSide.Opfor => _director.Selection?.OpforAssignment,
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown round side."),
            };
            if (assignment != null)
            {
                throw new InvalidOperationException(
                    $"Committed {side} assignment '{assignment.Value.Force}' has no committed ASRS catalog.");
            }

            return false;
        }

        if (TryComp(computer, out RoundAsrsConsoleCatalogComponent? existing) &&
            existing.Generation == _director.Generation &&
            existing.Force == catalog.Force)
        {
            return true;
        }

        var categories = new List<RequisitionsCategory>(catalog.Categories.Length);
        var categoryIds = ImmutableArray.CreateBuilder<RoundAsrsCategoryId>(catalog.Categories.Length);
        var offerIds = ImmutableArray.CreateBuilder<ImmutableArray<RoundAsrsOfferId>>(catalog.Categories.Length);
        var stockPolicies = ImmutableDictionary.CreateBuilder<RoundAsrsOfferId, RoundAsrsStockPolicy>();

        foreach (var sourceCategory in catalog.Categories)
        {
            var entries = new List<RequisitionsEntry>(sourceCategory.Offers.Length);
            var categoryOfferIds = ImmutableArray.CreateBuilder<RoundAsrsOfferId>(sourceCategory.Offers.Length);
            foreach (var sourceOffer in sourceCategory.Offers)
            {
                entries.Add(new RequisitionsEntry
                {
                    Crate = sourceOffer.Crate,
                    Cost = sourceOffer.Cost,
                });
                categoryOfferIds.Add(sourceOffer.Id);
                if (sourceOffer.Stock is { } stock)
                    stockPolicies.Add(sourceOffer.Id, stock);
            }

            categories.Add(new RequisitionsCategory
            {
                Name = sourceCategory.Name,
                Entries = entries,
            });
            categoryIds.Add(sourceCategory.Id);
            offerIds.Add(categoryOfferIds.MoveToImmutable());
        }

        var binding = EnsureComp<RoundAsrsConsoleCatalogComponent>(computer);
        binding.Generation = _director.Generation;
        binding.Force = catalog.Force;
        binding.CategoryIds = categoryIds.MoveToImmutable();
        binding.OfferIdsByCategory = offerIds.MoveToImmutable();
        binding.StockPolicies = stockPolicies.ToImmutable();
        _requisitions.ReplaceCatalog(computer, categories);
        return true;
    }

    private static bool TryGetSide(string faction, out RoundSide side)
    {
        switch (faction)
        {
            case "govfor":
                side = RoundSide.Govfor;
                return true;
            case "opfor":
                side = RoundSide.Opfor;
                return true;
            default:
                side = default;
                return false;
        }
    }

    private static bool WorldIsInitialized(CMURoundPhase phase)
    {
        return phase is CMURoundPhase.WorldInitialized or
            CMURoundPhase.PlayersSpawned or
            CMURoundPhase.InRound;
    }
}
