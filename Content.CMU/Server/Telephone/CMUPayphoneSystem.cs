using System.Linq;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Telephone;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Telephone;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Telephone;

public sealed partial class CMUPayphoneSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> Currency = "Currency";

    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private ColonyBankSystem _bank = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private TagSystem _tags = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPayphoneComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUPayphoneComponent, CMUPhoneCallAttemptEvent>(OnCallAttempt);
    }

    private void OnMapInit(Entity<CMUPayphoneComponent> ent, ref MapInitEvent args)
    {
        if (!_areas.TryGetArea(ent.Owner, out _, out var area))
            return;

        _metaData.SetEntityName(ent, Loc.GetString("cmu-payphone-name", ("area", area.Name)));
    }

    /// <summary>
    ///     A call costs <see cref="CMUPayphoneComponent.CallCost"/>: cash in hand first, then the caller's ID card
    ///     account. 911 doesn't come through here, so it stays free.
    /// </summary>
    private void OnCallAttempt(Entity<CMUPayphoneComponent> ent, ref CMUPhoneCallAttemptEvent args)
    {
        var cost = TryComp(args.Target, out RotaryPhoneComponent? target)
            ? ent.Comp.CostFor(target.Category)
            : ent.Comp.CallCost;
        if (cost <= 0)
            return;

        var user = args.User;
        var cash = _hands.EnumerateHeld(user)
            .Where(held => _tags.HasTag(held, Currency) && TryComp(held, out StackComponent? stack) && stack.Count >= cost)
            .Select(held => (EntityUid?) held)
            .FirstOrDefault();

        if (cash is { } paid)
        {
            _stack.TryUse(paid, cost);
            _popup.PopupEntity(Loc.GetString("cmu-payphone-paid-cash", ("amount", cost)), ent, user);
            return;
        }

        if (_idCard.TryFindIdCard(user, out var idCard) &&
            !_bank.IsLocked(idCard.Comp, out _) &&
            idCard.Comp.AccountBalance >= cost)
        {
            idCard.Comp.AccountBalance -= cost;
            Dirty(idCard);
            _bank.RecordTransaction(idCard, AtmHistoryKind.Purchase, cost);
            _popup.PopupEntity(Loc.GetString("cmu-payphone-paid-card", ("amount", cost)), ent, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-payphone-cant-pay", ("amount", cost)), ent, user, PopupType.SmallCaution);
        args.Cancelled = true;
    }
}
