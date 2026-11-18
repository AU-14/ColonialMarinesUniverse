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
// earned by offering trophies, so gear never eats the clan SP that buys squads and Totems. The vendor asks
// through CMUVendorPooledPointsEvent before it charges anyone, and this answers for the armory: tier, Leader-only
// wares and the price all come from the rule, never from the buyer's own vendor points. The plasma caster is not
// sold here. Its lethal bolt and its eradicator are: both are clan unlocks the Leader pays for from their Honour.
// ARCH-02: the forge ware upgrades the buyer's bracer weapon in place.
// CMU14 STATE-602: the kennel wares upgrade the standing kennel's stock (BadBloodCampSystem.Kennel.cs).
// CMU14 ARCH-604: caste stations sell to their caste only, and CasteWares lock single wares of the clan's armory.
// CMU14 Playtest 5: STATE-701 (a caste-less grunt pays less for tier I), STATE-702 (the kennel is the Beast-Master's
// vendor: war-paint and thrall cages; the Beast-Master pays less for kennel wares and gets the first falcon free) and
// ARCH-707 (thrall cages, capped per Beast-Master and by the clan's threat budget). Prices: GetArmoryPrice.
// Dependency fields taken by this file: _armoryEradicator, _armoryForge, _armoryProto, _armoryThralls, _armoryVendor.
public sealed partial class BadBloodCampSystem
{
    [Dependency] private BadBloodEradicatorUnlockSystem _armoryEradicator = default!;
    [Dependency] private BadBloodForgeSystem _armoryForge = default!;
    [Dependency] private IPrototypeManager _armoryProto = default!;
    [Dependency] private BadBloodThrallBeastSystem _armoryThralls = default!;
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
        // tier IV still waits for the clan ship. CMU14 STATE-601: the lethal unlock is not discounted (D3), so the
        // clan never holds lethal bolts from the start
        var tier = GetArmoryTier(armory, ware);
        if (tier is 2 or 3 && _badBlood.IsBadBloodLeader(user) && !IsLethalUnlockWare(ware))
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
        if (armory.Comp.RequiredCaste is { } requiredCaste && !HoldsCaste(user, requiredCaste))
        {
            reason = "cmu-bad-blood-caste-armory-locked";
            return false;
        }

        // CMU14 ARCH-604: and a caste ware in the clan's armory only to its caste
        if (armory.Comp.CasteWares.TryGetValue(ware, out var wareCaste) && !HoldsCaste(user, wareCaste))
        {
            reason = "cmu-bad-blood-caste-ware-locked";
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

        // CMU14 STATE-602: a kennel upgrade needs a standing kennel that can still take it
        if (GetKennelUpgrade(ware) is { } upgrade)
        {
            if (!TryGetKennel(out var kennel))
            {
                reason = "cmu-bad-blood-armory-kennel-none";
                return false;
            }

            if (kennel.Comp.Upgrades.GetValueOrDefault(ware) >= upgrade.MaxBuys)
            {
                reason = "cmu-bad-blood-armory-kennel-maxed";
                return false;
            }
        }

        // CMU14 STATE-702: war-paint needs a standing kennel that is not painted yet
        if (IsArmoryWare<BadBloodWarPaintWareComponent>(ware))
        {
            if (!TryGetKennel(out var kennel))
            {
                reason = "cmu-bad-blood-armory-kennel-none";
                return false;
            }

            if (HasComp<BadBloodWarPaintComponent>(kennel))
            {
                reason = "cmu-bad-blood-armory-kennel-maxed";
                return false;
            }
        }

        // CMU14 ARCH-707: a thrall cage is the Beast-Master's, within their own thrall cap and the clan's threat budget
        if (IsArmoryWare<BadBloodThrallCageComponent>(ware))
        {
            if (!TryComp(user, out BadBloodBeastMasterComponent? beastMaster))
            {
                reason = "cmu-bad-blood-caste-armory-locked";
                return false;
            }

            if (_armoryThralls.CountThralls(user) >= beastMaster.MaxThralls)
            {
                reason = "cmu-bad-blood-thrall-cap";
                return false;
            }

            if (GetThrallBudgetRoom() <= 0)
            {
                reason = "cmu-bad-blood-thrall-budget";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// CMU14 ARCH-707 (D11): how many more thralls the clan's threat budget allows: the budget less the living Bad
    /// Bloods, the open squad seats and the thralls and cages already out. Counted here and not through
    /// <see cref="GetSquadBudgetRoom"/>, so it stays right whether or not that counts thralls too.
    /// </summary>
    public int GetThrallBudgetRoom()
    {
        if (!_rule.TryGetRule(out _))
            return 0;

        var bodies = _rule.CountLivingBadBloods() + GetPendingSquadSeats() + _armoryThralls.CountClanThralls();
        return Math.Max(0, _rule.GetThreatBudget() - bodies);
    }

    /// <summary>
    /// What <paramref name="user"/> pays for <paramref name="ware"/> listed at <paramref name="price"/>, by the
    /// armory's <see cref="BadBloodArmoryPriceComponent"/>: STATE-702, a Beast-Master's first falcon is free and the
    /// kennel wares cost them a share; STATE-701, a grunt (no caste, not the Hunt Leader) pays a share for tier I at
    /// the clan's armory. Without the component the listed price.
    /// </summary>
    public FixedPoint4 GetArmoryPrice(Entity<BadBloodCampArmoryComponent> armory, EntityUid user, EntProtoId ware, int price)
    {
        var listed = Math.Max(price, 0);
        if (!TryComp(armory, out BadBloodArmoryPriceComponent? rules))
            return FixedPoint4.New(listed);

        var multiplier = 1.0;
        if (TryComp(user, out BadBloodBeastMasterComponent? beastMaster))
        {
            if (!beastMaster.FreeFalconClaimed && rules.FirstFreeForBeastMaster.Contains(ware))
                return FixedPoint4.Zero;

            if (rules.KennelWares.Contains(ware))
                multiplier *= beastMaster.KennelWareMultiplier;
        }

        if (IsArmoryGrunt(armory, user) && GetArmoryTier(armory, ware) == 1)
            multiplier *= rules.GruntTierOneMultiplier;

        return FixedPoint4.New(listed * multiplier);
    }

    // CMU14 STATE-701: the grunt passive belongs to the clan's own armory only, and is lost with a caste
    private bool IsArmoryGrunt(Entity<BadBloodCampArmoryComponent> armory, EntityUid user)
    {
        return armory.Comp.RequiredCaste == null &&
               !HasComp<BadBloodCasteComponent>(user) &&
               !_badBlood.IsBadBloodLeader(user);
    }

    private bool IsArmoryWare<T>(EntProtoId ware) where T : IComponent, new()
    {
        return _armoryProto.TryIndex(ware, out var proto) &&
               proto.TryComp<T>(out _, EntityManager.ComponentFactory);
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
                Loc.GetString(reason, ("tier", GetArmoryTier(armory, ware)), ("caste", GetWareCasteName(armory, ware))),
                armory.Owner,
                user,
                PopupType.SmallCaution);
            return false;
        }

        // CMU14 STATE-701 / STATE-702: the buyer's own price
        var cost = GetArmoryPrice(armory, user, ware, price);
        if (!_rule.TrySpendHonour(user, cost))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-bad-blood-armory-not-enough", ("price", FormatSp(cost)), ("sp", FormatSp(_rule.GetHonour(user)))),
                armory.Owner,
                user,
                PopupType.SmallCaution);
            return false;
        }

        // CMU14 STATE-702: the Beast-Master's free falcon is spent
        if (TryComp(armory, out BadBloodArmoryPriceComponent? rules) &&
            rules.FirstFreeForBeastMaster.Contains(ware) &&
            TryComp(user, out BadBloodBeastMasterComponent? beastMaster))
        {
            beastMaster.FreeFalconClaimed = true;
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

        // CMU14 STATE-602
        if (TryComp<BadBloodKennelUpgradeComponent>(args.Item, out var kennelUpgrade))
        {
            var ware = MetaData(args.Item).EntityPrototype?.ID;
            QueueDel(args.Item);
            if (ware != null && TryGetKennel(out var kennel))
            {
                ApplyKennelUpgrade(kennel, ware, kennelUpgrade);
                _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-armory-kennel-improved",
                    ("seconds", (int) kennel.Comp.Interval.TotalSeconds),
                    ("stock", kennel.Comp.MinStock)));
            }

            return;
        }

        // CMU14 STATE-702: war-paint for the standing kennel
        if (TryComp<BadBloodWarPaintWareComponent>(args.Item, out var warPaint))
        {
            var multiplier = warPaint.Multiplier;
            QueueDel(args.Item);
            if (TryGetKennel(out var kennel))
            {
                ApplyKennelWarPaint(kennel, multiplier);
                _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-beast-war-paint",
                    ("percent", (int) MathF.Round((multiplier - 1f) * 100f))));
            }

            return;
        }

        // CMU14 ARCH-707: a thrall cage stands at the kennel, the buyer's, until a ghost takes the thrall
        if (TryComp<BadBloodThrallCageComponent>(args.Item, out var cage))
        {
            cage.Master = args.User;
            cage.StockedAt = _timing.CurTime;
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-thrall-cage-stocked"), args.User, args.User, PopupType.Medium);
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

    // CMU14 ARCH-604
    private bool HoldsCaste(EntityUid user, ProtoId<BadBloodCastePrototype> caste)
    {
        return TryComp(user, out BadBloodCasteComponent? held) && held.Caste == caste;
    }

    // CMU14 ARCH-604: the caste a locked ware is for, as players read it. Empty for a ware open to every Bad Blood
    private string GetWareCasteName(Entity<BadBloodCampArmoryComponent> armory, EntProtoId ware)
    {
        var caste = armory.Comp.CasteWares.TryGetValue(ware, out var wareCaste) ? wareCaste : armory.Comp.RequiredCaste;
        return caste is { } id && _armoryProto.TryIndex(id, out var proto) ? Loc.GetString(proto.Name) : string.Empty;
    }

    // CMU14 STATE-602
    private BadBloodKennelUpgradeComponent? GetKennelUpgrade(EntProtoId ware)
    {
        return _armoryProto.TryIndex(ware, out var proto) &&
               proto.TryComp<BadBloodKennelUpgradeComponent>(out var upgrade, EntityManager.ComponentFactory)
            ? upgrade
            : null;
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

        // ARCH-06: a caste station opens only for its caste. The others are told where to take it up
        if (ent.Comp.RequiredCaste is { } caste && !HoldsCaste(args.User, caste))
        {
            if (!args.Silent)
                _popup.PopupEntity(Loc.GetString("cmu-bad-blood-caste-armory-locked"), ent.Owner, args.User, PopupType.SmallCaution);

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
