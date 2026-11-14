using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.Yautja.BadBlood;

// FLOW-115: the kennel. Any Bad Blood buys a sleeping hound here, reserved for them for a while. The hound itself
// (who wakes it, who drives it, going back to sleep, the landing hounds) is BadBloodHoundSystem.
public sealed partial class BadBloodCampSystem
{
    [Dependency] private BadBloodHoundSystem _kennelHounds = default!;

    partial void InitializeKennel()
    {
        SubscribeLocalEvent<BadBloodKennelComponent, GetVerbsEvent<Verb>>(OnKennelVerbs);
    }

    /// <summary>
    /// <paramref name="buyer"/> pays <see cref="BadBloodCampRuleComponent.HoundCost"/> and a sleeping hound appears
    /// at the kennel, theirs alone to wake for the kennel's reservation time. Refused, with a popup and nothing
    /// spent, for anyone but a Bad Blood, without a kennel, over the hound caps or without the SP.
    /// </summary>
    public bool TryBuyHound(EntityUid buyer)
    {
        if (!_rule.TryGetRule(out var rule))
            return false;

        var cost = rule.Comp.HoundCost;
        string? reason = null;
        EntityUid kennel = default;
        BadBloodKennelComponent? kennelComp = null;

        if (!_badBlood.IsBadBlood(buyer))
            reason = "cmu-bad-blood-hound-sleeper-not-clan";
        else if (!TryGetPart(BadBloodCampPartKind.Kennel, out kennel) || !TryComp(kennel, out kennelComp))
            reason = "cmu-bad-blood-hound-no-kennel";
        else if (_kennelHounds.GetHoundRoom() < 1)
            reason = "cmu-bad-blood-hound-cap";
        else if (!_rule.TrySpendSp(cost))
            reason = "cmu-bad-blood-hound-cannot-afford";

        if (reason != null || kennelComp == null)
        {
            if (reason != null)
                _popup.PopupEntity(Loc.GetString(reason, ("cost", cost.ToString())), buyer, buyer, PopupType.SmallCaution);

            return false;
        }

        var coords = Transform(kennel).Coordinates;
        _kennelHounds.SpawnSleepingHound(coords, buyer, kennelComp.BuyerReservation);

        if (kennelComp.PadPrototype is { } pad)
            _hands.PickupOrDrop(buyer, Spawn(pad, Transform(buyer).Coordinates));

        _popup.PopupEntity(
            Loc.GetString("cmu-bad-blood-hound-bought", ("seconds", (int) kennelComp.BuyerReservation.TotalSeconds)),
            buyer,
            buyer);
        return true;
    }

    private void OnKennelVerbs(Entity<BadBloodKennelComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            !_badBlood.IsBadBlood(args.User) ||
            !_rule.TryGetRule(out var rule))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-bad-blood-hound-verb", ("cost", rule.Comp.HoundCost.ToString())),
            Message = Loc.GetString("cmu-bad-blood-hound-verb-message"),
            Act = () => TryBuyHound(user),
        });
    }
}
