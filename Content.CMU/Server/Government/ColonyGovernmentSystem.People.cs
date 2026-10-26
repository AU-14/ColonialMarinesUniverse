// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;
using Content.Shared.Paper;
using Content.Shared.Stacks;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Government;

// The mayor and the players: the rewards that the mayor gives, and the orders that he pays the players for. The townsfolk are real players, so a reward goes to the one who stands by the console, and the
// money of an order goes to the account of the one who brought the thing. The appeals and the votes are in .Appeals.cs, the notary in .Notary.cs, and the state of the console of the people in .Citizens.cs.
public sealed partial class ColonyGovernmentSystem
{
    public const string PaperProto = "Paper";
    public const int MaxReasonLength = 80;
    public const int MaxOrders = 5;
    public const int OrderLifetimeMinutes = 30;
    public const int MaxHistory = 40;

    #region Helpers

    // A line of text that a player has written: the marks of the markup, the back-slashes and the line breaks are taken out, it is trimmed and cut.
    private static string Clean(string text, int max)
    {
        var clean = new string(text.Where(c => c != '[' && c != ']' && c != '\n' && c != '\r' && c != '\\').ToArray()).Trim();
        return clean.Length > max ? clean[..max].TrimEnd() : clean;
    }

    // Whether a person stands near a console (the range that the licences have).
    private bool Near(EntityUid console, EntityUid person)
    {
        return Exists(person) && Transform(person).MapID == Transform(console).MapID
               && _transform.InRange(Transform(console).Coordinates, Transform(person).Coordinates, LicenseRange);
    }

    private NetUserId? UserOf(EntityUid person)
    {
        return TryComp(person, out ActorComponent? actor) ? actor.PlayerSession.UserId : null;
    }

    // Money onto the account of a person (false if he has no account).
    private bool Pay(EntityUid person, int amount)
    {
        if (_bank.FindOwnedCard(person) is not { } owned)
            return false;

        owned.card.AccountBalance += amount;
        Dirty(owned.uid, owned.card);
        return true;
    }

    private int BalanceOf(EntityUid person)
    {
        return _bank.FindOwnedCard(person) is { } owned ? (int) owned.card.AccountBalance : 0;
    }

    // A paper with a text on it (the charters, the contracts), put into the hands of a person if there is a free hand, and on the floor by him if not.
    private EntityUid PrintPaper(string name, string text, EntityUid to)
    {
        var paper = Spawn(PaperProto, Transform(to).Coordinates);
        _meta.SetEntityName(paper, name);
        if (TryComp(paper, out PaperComponent? paperComp))
            _paper.SetContent((paper, paperComp), text);

        _hands.TryPickupAnyHand(to, paper);
        return paper;
    }

    private string CategoryName(AwardCategory category)
    {
        return Loc.GetString("rumc-award-category-" + category.ToString().ToLowerInvariant());
    }

    #endregion

    #region Rewards

    // A reward: the one who is rewarded stands by the console. The cost and the cash come out of the budget, the cash goes to his account, the medal and the charter are put into his hands.
    private void OnGrantAward(Entity<ColonyGovernmentConsoleComponent> console, ref GrantAwardMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var target = GetEntity(msg.Target);
        if (!_proto.TryIndex<ColonyAwardPrototype>(msg.Award, out var proto) || target == msg.Actor || !Near(console.Owner, target))
            return;

        var total = proto.Cost + proto.Cash;
        if (_budget.GetBudget() < total || (proto.Cash > 0 && _bank.FindOwnedCard(target) == null))
            return;

        var reason = Clean(msg.Reason, MaxReasonLength);
        if (reason.Length < 3)
            return;

        _budget.AddToBudget(-total);
        if (proto.Cash > 0)
            Pay(target, proto.Cash);

        var name = MetaData(target).EntityName;
        var awardName = Loc.GetString(proto.Name);
        var category = CategoryName(proto.Category);

        if (proto.Medal is { } medalProto)
        {
            var medal = Spawn(medalProto, Transform(target).Coordinates);
            _hands.TryPickupAnyHand(target, medal);
        }

        if (proto.Charter)
        {
            var text = string.Join("\n", new[]
            {
                "[color=#a07000][bold]" + Loc.GetString("rumc-award-charter-title") + "[/bold][/color]",
                string.Empty,
                Loc.GetString("rumc-award-charter-name", ("name", name)),
                Loc.GetString("rumc-award-charter-award", ("award", awardName)),
                Loc.GetString("rumc-award-charter-reason", ("category", category), ("reason", reason)),
                string.Empty,
                "[color=#aa0000][bold]" + Loc.GetString("rumc-award-charter-seal") + "[/bold][/color]",
            });

            PrintPaper(Loc.GetString("rumc-award-charter-paper", ("name", name)), text, target);
        }

        comp.Awards.Insert(0, new AwardRecord(proto.ID, name, proto.Category, reason, _timing.CurTime));
        if (comp.Awards.Count > MaxHistory)
            comp.Awards.RemoveRange(MaxHistory, comp.Awards.Count - MaxHistory);

        Refresh();
    }

    #endregion

    #region Orders

    // The mayor orders a thing: the money for all of it is taken out of the budget at once and kept, so that a player who brings the thing is always paid.
    private void OnCreateOrder(Entity<ColonyGovernmentConsoleComponent> console, ref CreateOrderMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        if (!_proto.TryIndex<ColonyOrderKindPrototype>(msg.Kind, out var kind) || comp.Orders.Count >= MaxOrders)
            return;

        if (msg.Amount < 1 || msg.Amount > kind.MaxAmount || msg.Price < Math.Max(1, kind.UnitPrice / 2) || msg.Price > kind.UnitPrice * 3)
            return;

        var escrow = msg.Amount * msg.Price;
        if (_budget.GetBudget() < escrow)
            return;

        _budget.AddToBudget(-escrow);
        comp.Orders.Add(new MunicipalOrder(comp.NextOrderId++, kind.ID, msg.Amount, msg.Price, _timing.CurTime + TimeSpan.FromMinutes(OrderLifetimeMinutes)));

        Refresh();
    }

    private void OnCancelOrder(Entity<ColonyGovernmentConsoleComponent> console, ref CancelOrderMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var orderId = msg.Order;
        if (government.Comp.Orders.FirstOrDefault(o => o.Id == orderId) is not { } order)
            return;

        CloseOrder(government.Comp, order);
        Refresh();
    }

    // An order is over: what was not brought is given back to the budget.
    private void CloseOrder(ColonyGovernmentComponent comp, MunicipalOrder order)
    {
        comp.Orders.Remove(order);
        if (order.Left > 0)
            _budget.AddToBudget(order.Left * order.Price);
    }

    private void UpdateOrders(ColonyGovernmentComponent comp, TimeSpan now)
    {
        foreach (var order in comp.Orders.Where(o => o.Until <= now).ToList())
            CloseOrder(comp, order);
    }

    private static bool MatchesKind(MetaDataComponent meta, StackComponent? stack, ColonyOrderKindPrototype kind)
    {
        if (kind.Item is { } entProto && meta.EntityPrototype?.ID == entProto.Id)
            return true;

        return kind.Stack is { } stackId && stack != null && stack.StackTypeId == stackId;
    }

    // A player brings the thing that he holds in his hand: as much as the order still wants is taken (the whole stack, or a part of it), and the price for each goes to his account.
    private void OnDeliverOrder(Entity<CitizenConsoleComponent> console, ref DeliverOrderMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var actor = msg.Actor;
        var orderId = msg.Order;
        if (comp.Orders.FirstOrDefault(o => o.Id == orderId) is not { } order || !_proto.TryIndex<ColonyOrderKindPrototype>(order.Kind, out var kind))
            return;

        if (order.Contractor != null && MetaData(actor).EntityName != order.Contractor)
        {
            _popup.PopupEntity(Loc.GetString("rumc-order-not-yours", ("name", order.Contractor)), console.Owner, actor);
            return;
        }

        if (!_hands.TryGetActiveItem(actor, out var held))
        {
            _popup.PopupEntity(Loc.GetString("rumc-order-nothing", ("kind", Loc.GetString(kind.Name))), console.Owner, actor);
            return;
        }

        TryComp(held.Value, out StackComponent? stack);
        if (!MatchesKind(MetaData(held.Value), stack, kind))
        {
            _popup.PopupEntity(Loc.GetString("rumc-order-nothing", ("kind", Loc.GetString(kind.Name))), console.Owner, actor);
            return;
        }

        if (_bank.FindOwnedCard(actor) == null)
        {
            _popup.PopupEntity(Loc.GetString("rumc-order-no-account"), console.Owner, actor);
            return;
        }

        var have = stack?.Count ?? 1;
        var take = Math.Min(have, order.Left);
        if (stack != null)
        {
            if (!_stack.TryUse((held.Value, stack), take))
                return;
        }
        else
        {
            QueueDel(held.Value);
        }

        var pay = take * order.Price;
        Pay(actor, pay);
        order.Left -= take;
        AddProgress(comp, actor, PlanKind.Deliver, take);
        if (order.Left <= 0)
        {
            comp.Orders.Remove(order);
        }

        _popup.PopupEntity(Loc.GetString("rumc-order-taken", ("amount", take), ("sum", pay)), console.Owner, actor);
        Refresh();
    }

    #endregion

    #region The state for the windows

    private PeopleState BuildPeople(ColonyGovernmentComponent comp, EntityUid console)
    {
        var now = _timing.CurTime;

        var awards = _proto.EnumeratePrototypes<ColonyAwardPrototype>()
            .OrderBy(a => a.Category).ThenBy(a => a.Cost + a.Cash)
            .Select(a => new AwardTypeEntry(a.ID, a.Category, Loc.GetString(a.Name), Loc.GetString(a.Description), a.Cost, a.Cash, a.Medal != null))
            .ToList();

        var history = comp.Awards
            .Select(r => new AwardEntry(_proto.TryIndex<ColonyAwardPrototype>(r.Award, out var proto) ? Loc.GetString(proto.Name) : r.Award, r.Holder, CategoryName(r.Category), r.Reason))
            .ToList();

        var kinds = _proto.EnumeratePrototypes<ColonyOrderKindPrototype>()
            .OrderBy(k => k.UnitPrice).ThenBy(k => Loc.GetString(k.Name))
            .Select(k => new OrderKindEntry(k.ID, Loc.GetString(k.Name), Loc.GetString(k.Description), k.UnitPrice, k.MaxAmount))
            .ToList();

        return new PeopleState(awards, history, NearbyPeople(console), kinds, OrderEntries(comp, now), AppealEntries(comp, 40), PollEntryOrNull(comp.Poll, now, false),
            comp.PastPolls.Select(p => PollEntryOf(p, now, true)).ToList(), RegistryEntries(comp), comp.NotaryTotal);
    }

    private List<OrderEntry> OrderEntries(ColonyGovernmentComponent comp, TimeSpan now)
    {
        var entries = new List<OrderEntry>();
        foreach (var order in comp.Orders)
        {
            if (!_proto.TryIndex<ColonyOrderKindPrototype>(order.Kind, out var kind))
                continue;

            entries.Add(new OrderEntry(order.Id, Loc.GetString(kind.Name), Loc.GetString(kind.Description), order.Total, order.Left, order.Price,
                Math.Max(0, (int) Math.Ceiling((order.Until - now).TotalSeconds)), order.Contractor ?? string.Empty));
        }

        return entries;
    }

    #endregion
}
