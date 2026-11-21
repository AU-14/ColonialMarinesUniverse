using System.Linq;
using Content.Shared.CMU14.CorporateRelations;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.CorporateRelations;

public sealed partial class CMUCorporateRelationsSystem
{
    public const int MaxWorkOrders = 3;
    public const int MinPremiumPercent = 5;
    public const int MaxPremiumPercent = 40;

    /// <summary>
    ///     WY resells work order deliveries off-world for this share of their value. It is above
    ///     <see cref="MaxPremiumPercent"/>, so every order leaves WY some profit.
    /// </summary>
    public const float ResaleMargin = 0.5f;

    private readonly List<WorkOrder> _workOrders = new();
    private int _nextWorkOrderId;
    private bool _workOrdersFrozen;

    private sealed class WorkOrder
    {
        public required int Id;
        public required ProtoId<CMUWorkOrderGoodPrototype> Good;
        public required int Quantity;
        public required int PremiumPercent;
        public required TimeSpan ExpiresAt;
        public int Delivered;

        /// <summary>
        ///     Premium WY set aside when posting and hasn't paid out yet.
        /// </summary>
        public float Escrow;
    }

    private void InitializeWorkOrders()
    {
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsPostWorkOrderBuiMsg>(OnPostWorkOrder);
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsCancelWorkOrderBuiMsg>(OnCancelWorkOrder);
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsFreezeWorkOrdersBuiMsg>(OnFreezeWorkOrders);
    }

    private void UpdateWorkOrders(TimeSpan now)
    {
        var expired = _workOrders.Where(o => o.ExpiresAt <= now).ToList();
        foreach (var order in expired)
        {
            CloseWorkOrder(order);
            Announce("cmu-contracts-announce-order-expired", ("good", GoodName(order)), ("delivered", order.Delivered), ("quantity", order.Quantity));
        }

        if (expired.Count > 0)
            UpdateAllUi();
    }

    /// <summary>
    ///     Called by submission storage for every delivery. Fills the first open work order for the
    ///     item: the colony gets the premium on top of the normal price, WY gets the resale margin.
    /// </summary>
    /// <param name="units">How many units were delivered, e.g. the stack size.</param>
    /// <param name="value">What the delivery is worth before the tariff split.</param>
    public void OnDelivery(EntityUid item, int units, float value)
    {
        if (_withdrawn || units <= 0)
            return;

        foreach (var order in _workOrders)
        {
            var good = _prototypes.Index(order.Good);
            if (!_tag.HasTag(item, good.Tag))
                continue;

            var filled = Math.Min(units, order.Quantity - order.Delivered);
            if (filled <= 0)
                continue;

            var premium = Math.Min(order.Escrow, filled * good.UnitValue * order.PremiumPercent / 100f);
            order.Escrow -= premium;
            order.Delivered += filled;
            _colonyBudget.AddToBudget(premium);
            _corporate.AddToCorporateBudget(value / units * filled * ResaleMargin);

            if (order.Delivered >= order.Quantity)
            {
                CloseWorkOrder(order);
                Announce("cmu-contracts-announce-order-filled", ("good", Loc.GetString(good.Name)), ("quantity", order.Quantity));
            }

            UpdateAllUi();
            return;
        }
    }

    private void OnPostWorkOrder(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsPostWorkOrderBuiMsg args)
    {
        if (ent.Comp.Side != CMUContractsSide.Corporate)
            return;

        if (_withdrawn)
        {
            Refuse(ent, args.Actor, "cmu-contracts-refuse-withdrawn");
            return;
        }

        if (_workOrdersFrozen)
        {
            Refuse(ent, args.Actor, "cmu-contracts-refuse-orders-frozen");
            return;
        }

        if (_workOrders.Count >= MaxWorkOrders)
        {
            Refuse(ent, args.Actor, "cmu-contracts-refuse-orders-full", ("max", MaxWorkOrders));
            return;
        }

        if (!_prototypes.TryIndex<CMUWorkOrderGoodPrototype>(args.Good, out var good))
            return;

        var quantity = Math.Clamp(args.Quantity, 1, good.MaxQuantity);
        var premiumPercent = Math.Clamp(args.PremiumPercent, MinPremiumPercent, MaxPremiumPercent);
        var escrow = MathF.Ceiling(quantity * good.UnitValue * premiumPercent / 100f);
        if (_corporate.GetCorporateBudget() < escrow)
        {
            Refuse(ent, args.Actor, "cmu-contracts-refuse-corporate-funds");
            return;
        }

        _corporate.AddToCorporateBudget(-escrow);
        _workOrders.Add(new WorkOrder
        {
            Id = _nextWorkOrderId++,
            Good = good.ID,
            Quantity = quantity,
            PremiumPercent = premiumPercent,
            ExpiresAt = _timing.CurTime + Cycle,
            Escrow = escrow,
        });

        Announce("cmu-contracts-announce-order-posted", ("good", Loc.GetString(good.Name)), ("quantity", quantity), ("premium", premiumPercent));
        UpdateAllUi();
    }

    private void OnCancelWorkOrder(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsCancelWorkOrderBuiMsg args)
    {
        if (ent.Comp.Side != CMUContractsSide.Corporate)
            return;

        var id = args.Id;
        if (_workOrders.FirstOrDefault(o => o.Id == id) is not { } order)
            return;

        CloseWorkOrder(order);
        Announce("cmu-contracts-announce-order-cancelled", ("good", GoodName(order)));
        UpdateAllUi();
    }

    private void OnFreezeWorkOrders(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsFreezeWorkOrdersBuiMsg args)
    {
        if (ent.Comp.Side != CMUContractsSide.Corporate || _workOrdersFrozen)
            return;

        CloseAllWorkOrders();
        _workOrdersFrozen = true;
        Announce("cmu-contracts-announce-orders-frozen");
        UpdateAllUi();
    }

    private void CloseAllWorkOrders()
    {
        foreach (var order in _workOrders.ToList())
        {
            CloseWorkOrder(order);
        }
    }

    /// <summary>
    ///     Removes an order and returns its unpaid premium to WY.
    /// </summary>
    private void CloseWorkOrder(WorkOrder order)
    {
        _workOrders.Remove(order);
        if (order.Escrow > 0)
        {
            // WY's own money coming back, not earnings.
            _corporate.AddToCorporateBudget(order.Escrow);
            _markets.IgnoreCorporateIncome(order.Escrow);
        }
    }

    private string GoodName(WorkOrder order)
    {
        return Loc.GetString(_prototypes.Index(order.Good).Name);
    }
}
