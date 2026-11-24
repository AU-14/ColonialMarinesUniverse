using System.Linq;
using Content.Server.Popups;
using Content.Shared.CMU14.CorporateWork;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     Supplier Negotiation. See <see cref="CMUNegotiationTerminalComponent"/>.
/// </summary>
public sealed class CMUNegotiationTerminalSystem : EntitySystem
{
    [Dependency] private CMUCorporateWorkSystem _work = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string Task = "negotiation";
    private const int Suppliers = 8;
    private const int Goods = 10;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUNegotiationTerminalComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMUNegotiationTerminalComponent, CMUNegotiationStartBuiMsg>(OnStart);
        SubscribeLocalEvent<CMUNegotiationTerminalComponent, CMUNegotiationOfferBuiMsg>(OnOffer);
        SubscribeLocalEvent<CMUNegotiationTerminalComponent, CMUNegotiationAcceptBuiMsg>(OnAccept);
    }

    private void OnUiOpened(Entity<CMUNegotiationTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnStart(Entity<CMUNegotiationTerminalComponent> ent, ref CMUNegotiationStartBuiMsg args)
    {
        TryStartCase(ent, args.Tier, args.Actor);
    }

    private void OnOffer(Entity<CMUNegotiationTerminalComponent> ent, ref CMUNegotiationOfferBuiMsg args)
    {
        Offer(ent, args.Offer, args.Actor);
    }

    private void OnAccept(Entity<CMUNegotiationTerminalComponent> ent, ref CMUNegotiationAcceptBuiMsg args)
    {
        if (ent.Comp.Case is { } deal)
            Close(ent, deal.Ask, args.Actor);
    }

    /// <summary>
    ///     Opens talks with a new supplier at the given tier, replacing any open ones.
    /// </summary>
    public bool TryStartCase(Entity<CMUNegotiationTerminalComponent> ent, int tier, EntityUid user)
    {
        if (tier < 0 || tier >= ent.Comp.Tiers.Count)
            return false;

        if (_timing.CurTime < ent.Comp.NextCaseAt)
        {
            _popup.PopupEntity(Loc.GetString("cmu-negotiation-cooldown"), ent, user);
            return false;
        }

        var info = ent.Comp.Tiers[tier];
        var list = _random.Next(20, 120) * 100;
        var floor = (int) MathF.Round(list * _random.NextFloat(info.MinFloor, info.MaxFloor) / 10f) * 10;
        ent.Comp.Case = new CMUNegotiationCase
        {
            Tier = tier,
            Supplier = Loc.GetString($"cmu-negotiation-supplier-{_random.Next(1, Suppliers + 1)}"),
            Goods = Loc.GetString($"cmu-negotiation-goods-{_random.Next(1, Goods + 1)}"),
            ListPrice = list,
            Floor = floor,
            Ask = list,
            Patience = info.Patience,
        };

        ent.Comp.LastResult = null;
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    ///     Makes an offer. At or above the ask closes the deal; a fair offer gets a counter; a lowball
    ///     offer costs extra patience. Out of patience, the supplier walks away.
    /// </summary>
    public CMUNegotiationReply? Offer(Entity<CMUNegotiationTerminalComponent> ent, int offer, EntityUid user)
    {
        if (ent.Comp.Case is not { } deal || offer <= 0)
            return null;

        if (offer >= deal.Ask)
        {
            deal.Log.Add(new CMUNegotiationExchange(offer, CMUNegotiationReply.Accepted, offer));
            Close(ent, offer, user);
            return CMUNegotiationReply.Accepted;
        }

        var tier = ent.Comp.Tiers[deal.Tier];
        CMUNegotiationReply reply;
        if (offer < deal.Floor * ent.Comp.InsultFraction)
        {
            deal.Patience -= 2;
            reply = CMUNegotiationReply.Insulted;
        }
        else
        {
            deal.Patience -= 1;
            var moved = deal.Ask - (int) MathF.Round((deal.Ask - offer) * tier.Concession);
            deal.Ask = Math.Max(deal.Floor, moved);
            reply = CMUNegotiationReply.Counter;
        }

        if (deal.Patience <= 0)
        {
            deal.Log.Add(new CMUNegotiationExchange(offer, CMUNegotiationReply.WalkedAway, deal.Ask));
            Close(ent, null, user);
            return CMUNegotiationReply.WalkedAway;
        }

        deal.Log.Add(new CMUNegotiationExchange(offer, reply, deal.Ask));
        UpdateUi(ent);
        return reply;
    }

    /// <summary>
    ///     Ends the talks. Pays by how much of the gap between list price and walk-away price was won.
    /// </summary>
    public void Close(Entity<CMUNegotiationTerminalComponent> ent, int? price, EntityUid user)
    {
        if (ent.Comp.Case is not { } deal)
            return;

        var payout = 0;
        if (price is { } closed)
        {
            var possible = Math.Max(1, deal.ListPrice - deal.Floor);
            var saved = Math.Clamp((deal.ListPrice - closed) / (float) possible, 0f, 1f);
            payout = _work.Pay(user, Task, ent.Comp.Tiers[deal.Tier].Reward * saved);
        }

        ent.Comp.LastResult = new CMUNegotiationResult(price, deal.ListPrice, payout);
        ent.Comp.Case = null;
        ent.Comp.NextCaseAt = _timing.CurTime + ent.Comp.Cooldown;
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<CMUNegotiationTerminalComponent> ent)
    {
        var deal = ent.Comp.Case;
        var state = new CMUNegotiationTerminalBuiState(
            ent.Comp.Tiers.Select(t => new CMUNegotiationTierInfo(t.Name, t.Patience, t.Reward)).ToList(),
            deal?.Tier,
            deal?.Supplier ?? string.Empty,
            deal?.Goods ?? string.Empty,
            deal?.ListPrice ?? 0,
            deal?.Ask ?? 0,
            deal?.Patience ?? 0,
            deal?.Log.ToList() ?? new List<CMUNegotiationExchange>(),
            ent.Comp.NextCaseAt,
            ent.Comp.LastResult);

        _ui.SetUiState(ent.Owner, CMUNegotiationTerminalUi.Key, state);
    }
}
