using Content.Server.CMU.Round;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU.Round;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    private readonly HashSet<EntityUid> _completedRoundStock = [];
    private readonly HashSet<EntityUid> _depletedRoundStock = [];

    /// <summary>
    /// Replaces a console's ordered catalog with a caller-owned detached projection and synchronizes it to clients.
    /// </summary>
    internal void ReplaceCatalog(
        Entity<RequisitionsComputerComponent> computer,
        List<RequisitionsCategory> categories)
    {
        computer.Comp.Categories = categories;
        ResetRoundStock(computer);
        Dirty(computer);
    }

    /// <summary>
    /// Projects the resolved round side onto the legacy server-side requisitions routing field.
    /// </summary>
    internal void SetRoundSide(Entity<RequisitionsComputerComponent> computer, RoundSide side)
    {
        computer.Comp.Faction = side switch
        {
            RoundSide.Govfor => "govfor",
            RoundSide.Opfor => "opfor",
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown round side."),
        };
    }

    private bool TryReserveRoundStock(
        Entity<RequisitionsComputerComponent> computer,
        int category,
        int order)
    {
        if (category < 0 ||
            category >= computer.Comp.Categories.Count ||
            order < 0 ||
            order >= computer.Comp.Categories[category].Entries.Count)
        {
            return false;
        }

        if (!TryComp(computer, out RoundAsrsConsoleCatalogComponent? catalog))
            return true;

        var offerIdsByCategory = catalog.OfferIdsByCategory;
        if (category >= offerIdsByCategory.Length || order >= offerIdsByCategory[category].Length)
        {
            Log.Error($"Bound ASRS console {ToPrettyString(computer)} has a catalog identity layout mismatch.");
            return false;
        }

        var offerId = offerIdsByCategory[category][order];
        var policies = catalog.StockPolicies;
        if (!policies.TryGetValue(offerId, out var policy))
            return true;

        if (!TryComp(computer, out RoundAsrsConsoleStockComponent? stock))
        {
            Log.Error($"Bound ASRS console {ToPrettyString(computer)} has no stock ledger.");
            return false;
        }

        var offers = stock.Offers;
        if (!offers.TryGetValue(offerId, out var state))
        {
            Log.Error($"Bound ASRS console {ToPrettyString(computer)} has no stock state for offer '{offerId}'.");
            return false;
        }

        state = ReplenishRoundOffer(state, policy, _timing.CurTime);
        if (state.Current <= 0)
        {
            offers[offerId] = state;
            return false;
        }

        var nextReplenish = state.NextReplenish;
        if (state.Current == policy.Maximum)
            nextReplenish = _timing.CurTime + policy.ReplenishDelay;

        offers[offerId] = state with
        {
            Current = state.Current - 1,
            NextReplenish = nextReplenish,
        };
        _depletedRoundStock.Add(computer);
        return true;
    }

    private void ResetRoundStock(Entity<RequisitionsComputerComponent> computer)
    {
        if (!TryComp(computer, out RoundAsrsConsoleCatalogComponent? catalog))
            throw new InvalidOperationException($"Cannot reset ASRS stock without a committed catalog binding on {ToPrettyString(computer)}.");

        var offers = EnsureComp<RoundAsrsConsoleStockComponent>(computer).Offers;
        offers.Clear();
        _depletedRoundStock.Remove(computer);
        var policies = catalog.StockPolicies;
        foreach (var (offerId, policy) in policies)
        {
            var current = policy.StartingStock < 0
                ? policy.Maximum
                : policy.StartingStock;
            var nextReplenish = current < policy.Maximum
                ? _timing.CurTime + policy.ReplenishDelay
                : default;
            offers.Add(offerId, new RoundAsrsOfferStockState(current, nextReplenish));
            if (current < policy.Maximum)
                _depletedRoundStock.Add(computer);
        }
    }

    private void ProcessRoundStock(TimeSpan now)
    {
        _completedRoundStock.Clear();
        foreach (var uid in _depletedRoundStock)
        {
            if (!TryComp(uid, out RoundAsrsConsoleCatalogComponent? catalog) ||
                !TryComp(uid, out RoundAsrsConsoleStockComponent? stock))
            {
                _completedRoundStock.Add(uid);
                continue;
            }

            var policies = catalog.StockPolicies;
            var offers = stock.Offers;
            var anyDepleted = false;
            var invalid = false;
            foreach (var (offerId, policy) in policies)
            {
                if (!offers.TryGetValue(offerId, out var state))
                {
                    Log.Error($"Bound ASRS console {ToPrettyString(uid)} has no stock state for offer '{offerId}'.");
                    invalid = true;
                    break;
                }

                state = ReplenishRoundOffer(state, policy, now);
                offers[offerId] = state;
                if (state.Current < policy.Maximum)
                    anyDepleted = true;
            }

            if (invalid || !anyDepleted)
                _completedRoundStock.Add(uid);
        }

        foreach (var uid in _completedRoundStock)
            _depletedRoundStock.Remove(uid);
    }

    private static RoundAsrsOfferStockState ReplenishRoundOffer(
        RoundAsrsOfferStockState state,
        RoundAsrsStockPolicy policy,
        TimeSpan now)
    {
        if (state.Current >= policy.Maximum || now < state.NextReplenish)
            return state;

        var elapsedPeriods = 1L + (now - state.NextReplenish).Ticks / policy.ReplenishDelay.Ticks;
        var missing = policy.Maximum - state.Current;
        var periodsToFull = (missing + (long) policy.ReplenishAmount - 1) / policy.ReplenishAmount;
        var appliedPeriods = Math.Min(elapsedPeriods, periodsToFull);
        var replenished = (int) Math.Min(missing, appliedPeriods * policy.ReplenishAmount);
        var current = state.Current + replenished;
        var nextReplenish = current >= policy.Maximum
            ? default
            : state.NextReplenish + TimeSpan.FromTicks(policy.ReplenishDelay.Ticks * appliedPeriods);
        return new RoundAsrsOfferStockState(current, nextReplenish);
    }
}
