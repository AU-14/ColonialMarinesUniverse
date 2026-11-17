using System.Globalization;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Vendors;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

// FLOW-112, the Lodge armory: an automated vendor on the armory part. ARCH-01: it sells for the buyer's own Honour,
// earned by offering trophies, so gear never eats the clan SP that buys squads, hounds and Totems. The vendor asks
// through CMUVendorPooledPointsEvent before it charges anyone, and this answers for the armory: tier, Leader-only
// wares and the price all come from the rule, never from the buyer's own vendor points. The plasma caster is not
// sold here. Its lethal bolt and its eradicator are: both are clan unlocks the Leader pays for from their Honour.
// ARCH-02: the forge ware upgrades the buyer's bracer weapon in place.
// Dependency fields taken by this file: _armoryEradicator, _armoryForge, _armoryProto, _armoryVendor.
public sealed partial class BadBloodCampSystem
{
    [Dependency] private BadBloodEradicatorUnlockSystem _armoryEradicator = default!;
    [Dependency] private BadBloodForgeSystem _armoryForge = default!;
    [Dependency] private IPrototypeManager _armoryProto = default!;
    [Dependency] private SharedCMAutomatedVendorSystem _armoryVendor = default!;

    partial void InitializeArmory()
    {
        SubscribeLocalEvent<BadBloodCampArmoryComponent, CMUVendorPooledPointsEvent>(OnArmoryPooledPoints);
        SubscribeLocalEvent<BadBloodCampArmoryComponent, CMUVendorVendedItemEvent>(OnArmoryVended);
        SubscribeLocalEvent<BadBloodCampArmoryComponent, ActivatableUIOpenAttemptEvent>(OnArmoryOpenAttempt);
        SubscribeLocalEvent<BadBloodHonourChangedEvent>(OnArmoryHonourChanged);
    }

    /// <summary>
    /// Superiority Points as players read them: two decimals, cut and never rounded up, so nobody is shown a
    /// point the clan does not have yet.
    /// </summary>
    public static string FormatSp(FixedPoint4 sp)
    {
        var hundredths = sp.Value / 100;
        return (hundredths / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }

    // every change to anyone's Honour reaches the open vendor windows at once: an offering pays in without buying
    private void OnArmoryHonourChanged(ref BadBloodHonourChangedEvent args)
    {
        var armories = EntityQueryEnumerator<BadBloodCampArmoryComponent>();
        while (armories.MoveNext(out _, out var armory))
        {
            SyncArmoryBalance(armory.PointsType);
        }
    }

    // keeps the balance in the vendor window current, and turns the armory over to tier IV when the ship arrives
    partial void TickArmory(Entity<BadBloodCampComponent> lodge, Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var armories = EntityQueryEnumerator<BadBloodCampArmoryComponent>();
        while (armories.MoveNext(out var uid, out var armory))
        {
            if (rule.Comp.TierFourUnlocked)
                ApplyArmoryTierFour((uid, armory));

            SyncArmoryBalance(armory.PointsType);
        }
    }

    #region Buying

    /// <summary>
    /// The armory tier <paramref name="ware"/> needs: 1 unless the armory lists it higher.
    /// </summary>
    public int GetArmoryTier(Entity<BadBloodCampArmoryComponent> armory, EntProtoId ware)
    {
        return armory.Comp.Tiers.TryGetValue(ware, out var tier) ? Math.Max(tier, 1) : 1;
    }

    /// <summary>
    /// Whether <paramref name="user"/> may buy <paramref name="ware"/> at this armory right now, the price aside.
    /// <paramref name="reason"/> is the loc key of the refusal.
    /// </summary>
    public bool CanBuyFromArmory(Entity<BadBloodCampArmoryComponent> armory, EntityUid user, EntProtoId ware, out string reason)
    {
        reason = string.Empty;

        if (!_badBlood.IsBadBlood(user) || !_rule.IsRuleActive())
        {
            reason = "cmu-bad-blood-armory-not-clan";
            return false;
        }

        // the Leader is armed a step ahead of the clan: tier II and III wares open to them one tier early.
        // tier IV still waits for the clan ship.
        var tier = GetArmoryTier(armory, ware);
        if (tier is 2 or 3 && _badBlood.IsBadBloodLeader(user))
            tier--;

        if (!_rule.IsTierUnlocked(tier))
        {
            reason = "cmu-bad-blood-armory-tier-locked";
            return false;
        }

        if (armory.Comp.LeaderOnly.Contains(ware) && !_badBlood.IsBadBloodLeader(user))
        {
            reason = "cmu-bad-blood-armory-leader-only";
            return false;
        }

        // once the ship has arrived every bracer already holds the lethal bolt and the eradicator: never take SP
        // for either
        if (IsLethalUnlockWare(ware) && (_armoryEradicator.IsLethalUnlocked() || _rule.IsShipArrived()))
        {
            reason = "cmu-bad-blood-armory-lethal-already-unlocked";
            return false;
        }

        // ARCH-06: a caste station sells only to its caste
        if (armory.Comp.RequiredCaste is { } requiredCaste &&
            !(TryComp(user, out BadBloodCasteComponent? holder) && holder.Caste == requiredCaste))
        {
            reason = "cmu-bad-blood-caste-station-locked";
            return false;
        }

        // ARCH-02: the forge needs a chosen bracer weapon that is not forged yet
        if (IsForgeWare(ware) && GetForgeableWeapons(user).Count == 0)
        {
            reason = "cmu-bad-blood-armory-forge-nothing";
            return false;
        }

        if (IsEradicatorUnlockWare(ware))
        {
            if (_armoryEradicator.IsEradicatorUnlocked() || _rule.IsShipArrived())
            {
                reason = "cmu-bad-blood-armory-already-unlocked";
                return false;
            }

            if (!_armoryEradicator.IsLethalUnlocked())
            {
                reason = "cmu-bad-blood-armory-needs-lethal";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Pays <paramref name="price"/> of the buyer's own Honour for <paramref name="ware"/>, or nothing if the ware is
    /// locked for this buyer or they cannot afford it. The buyer is told why.
    /// </summary>
    public bool TryPayArmory(Entity<BadBloodCampArmoryComponent> armory, EntityUid user, EntProtoId ware, int price)
    {
        if (!CanBuyFromArmory(armory, user, ware, out var reason))
        {
            _popup.PopupEntity(
                Loc.GetString(reason, ("tier", GetArmoryTier(armory, ware))),
                armory.Owner,
                user,
                PopupType.SmallCaution);
            return false;
        }

        if (!_rule.TrySpendHonour(user, FixedPoint4.New(Math.Max(price, 0))))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-bad-blood-armory-not-enough", ("price", price), ("sp", FormatSp(_rule.GetHonour(user)))),
                armory.Owner,
                user,
                PopupType.SmallCaution);
            return false;
        }

        SyncArmoryBalance(armory.Comp.PointsType);
        return true;
    }

    private void OnArmoryPooledPoints(Entity<BadBloodCampArmoryComponent> ent, ref CMUVendorPooledPointsEvent args)
    {
        // the armory never touches anyone's own vendor points, paid or refused
        args.Handled = true;
        args.Paid = TryPayArmory(ent, args.User, args.Entry.Id, args.Points);
    }

    // a ware that is an unlock and not a thing to carry is applied here and the placeholder is deleted
    private void OnArmoryVended(Entity<BadBloodCampArmoryComponent> ent, ref CMUVendorVendedItemEvent args)
    {
        if (TryComp<BadBloodForgeUpgradeComponent>(args.Item, out var forge))
        {
            var multiplier = forge.Multiplier;
            QueueDel(args.Item);
            foreach (var weapon in GetForgeableWeapons(args.User))
            {
                _armoryForge.TryForge(weapon, multiplier);
            }

            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-armory-forged"), args.User, args.User);
            return;
        }

        if (HasComp<BadBloodLethalUnlockComponent>(args.Item))
        {
            QueueDel(args.Item);
            if (_armoryEradicator.UnlockLethal())
                _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-armory-lethal-unlocked"));

            return;
        }

        if (!HasComp<BadBloodEradicatorUnlockComponent>(args.Item))
            return;

        QueueDel(args.Item);
        if (!_armoryEradicator.UnlockEradicator())
            return;

        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-armory-eradicator-unlocked"));
    }

    private bool IsForgeWare(EntProtoId ware)
    {
        return _armoryProto.TryIndex(ware, out var proto) &&
               proto.TryComp<BadBloodForgeUpgradeComponent>(out _, EntityManager.ComponentFactory);
    }

    /// <summary>
    /// The weapon the forge would upgrade for <paramref name="user"/>: the starter weapon they picked on landing, in
    /// the bracer they wear, if it is not forged yet. Empty otherwise.
    /// </summary>
    public List<EntityUid> GetForgeableWeapons(EntityUid user)
    {
        var weapons = new List<EntityUid>();
        if (!_spitInventory.TryGetSlotEntity(user, "gloves", out var bracer) ||
            !TryComp(bracer, out YautjaBadBloodGearChoiceComponent? choice) ||
            !choice.Chosen ||
            !TryComp(bracer, out YautjaGearContainerComponent? gear))
        {
            return weapons;
        }

        if (choice.ChosenKind is { } kind &&
            gear.Gear.TryGetValue(kind, out var weapon) &&
            !TerminatingOrDeleted(weapon) &&
            !HasComp<BadBloodForgedComponent>(weapon))
        {
            weapons.Add(weapon);
        }

        return weapons;
    }

    private bool IsLethalUnlockWare(EntProtoId ware)
    {
        return _armoryProto.TryIndex(ware, out var proto) &&
               proto.TryComp<BadBloodLethalUnlockComponent>(out _, EntityManager.ComponentFactory);
    }

    private bool IsEradicatorUnlockWare(EntProtoId ware)
    {
        return _armoryProto.TryIndex(ware, out var proto) &&
               proto.TryComp<BadBloodEradicatorUnlockComponent>(out _, EntityManager.ComponentFactory);
    }

    #endregion

    #region The vendor window

    private void OnArmoryOpenAttempt(Entity<BadBloodCampArmoryComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_badBlood.IsBadBlood(args.User) || !_rule.IsRuleActive())
        {
            if (!args.Silent)
            {
                _popup.PopupEntity(Loc.GetString("cmu-bad-blood-armory-not-clan"), ent.Owner, args.User, PopupType.SmallCaution);
            }

            args.Cancel();
            return;
        }

        // ARCH-06: a caste station opens only for its caste. The others are told how to take it up
        if (ent.Comp.RequiredCaste is { } caste &&
            !(TryComp(args.User, out BadBloodCasteComponent? held) && held.Caste == caste))
        {
            if (!args.Silent)
                _popup.PopupEntity(Loc.GetString("cmu-bad-blood-caste-station-locked"), ent.Owner, args.User, PopupType.SmallCaution);

            args.Cancel();
            return;
        }

        PrepareArmoryBuyer(ent, args.User);
    }

    /// <summary>
    /// Gives <paramref name="user"/> what the vendor needs before it sells anything for points, and a current copy
    /// of the clan's balance. Opening the armory does this by itself.
    /// </summary>
    public void PrepareArmoryBuyer(Entity<BadBloodCampArmoryComponent> armory, EntityUid user)
    {
        var buyer = EnsureComp<CMVendorUserComponent>(user);
        _armoryVendor.SetExtraPoints((user, buyer), armory.Comp.PointsType, _rule.GetHonour(user).Int());
    }

    // each buyer sees their own Honour, whole points only: the window has no room for fractions. the real balance
    // stays on the rule
    private void SyncArmoryBalance(string pointsType)
    {
        var buyers = EntityQueryEnumerator<YautjaBadBloodComponent, CMVendorUserComponent>();
        while (buyers.MoveNext(out var uid, out _, out var buyer))
        {
            var shown = _rule.GetHonour(uid).Int();
#pragma warning disable RA0002 // read-only lookup on the vendor user's points
            if (buyer.ExtraPoints != null &&
                buyer.ExtraPoints.TryGetValue(pointsType, out var current) &&
                current == shown)
#pragma warning restore RA0002
            {
                continue;
            }

            _armoryVendor.SetExtraPoints((uid, buyer), pointsType, shown);
        }
    }

    #endregion

    #region Tier IV

    /// <summary>
    /// Turns the armory over to tier IV: the listed wares drop to their tier IV price and nothing stays Leader
    /// only. Runs by itself once the clan ship has arrived. Doing it twice changes nothing.
    /// </summary>
    public void ApplyArmoryTierFour(Entity<BadBloodCampArmoryComponent> armory)
    {
        if (armory.Comp.TierFourApplied)
            return;

        armory.Comp.TierFourApplied = true;
        armory.Comp.LeaderOnly.Clear();

        if (armory.Comp.TierFourPrices.Count == 0 || !TryComp(armory.Owner, out CMAutomatedVendorComponent? vendor))
            return;

        // rebuilt, not edited in place: the vendor system is the only writer of its wares, and SetSections also
        // sends them to clients
        var sections = new List<CMVendorSection>(vendor.Sections.Count);
        foreach (var section in vendor.Sections)
        {
            var entries = new List<CMVendorEntry>(section.Entries.Count);
            foreach (var entry in section.Entries)
            {
                entries.Add(entry.Points != null && armory.Comp.TierFourPrices.TryGetValue(entry.Id, out var price)
                    ? entry with { Points = price }
                    : entry);
            }

            sections.Add(new CMVendorSection
            {
                Name = section.Name,
                Choices = section.Choices,
                TakeAll = section.TakeAll,
                TakeOne = section.TakeOne,
                Entries = entries,
                SharedSpecLimit = section.SharedSpecLimit,
                SharedJOLimit = section.SharedJOLimit,
                Jobs = section.Jobs,
                Ranks = section.Ranks,
                Holidays = section.Holidays,
                HasBoxes = section.HasBoxes,
            });
        }

        _armoryVendor.SetSections((armory.Owner, vendor), sections);
    }

    #endregion
}
