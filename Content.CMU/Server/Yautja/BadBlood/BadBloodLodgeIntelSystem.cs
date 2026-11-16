using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Intel;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-507 (I3): what marines take from a raided Lodge. When the clan ship is called a star chart turns up at the
/// Lodge armory; the first non-Bad Blood to pick it up learns the ship's ETA. And the trophy rack can be stripped,
/// one trophy at a time, each one retrieve intel. The offering itself is BadBloodCampSystem.Offering.cs and is left
/// alone: this only takes what already hangs there.
/// </summary>
public sealed class BadBloodLodgeIntelSystem : EntitySystem
{
    public static readonly EntProtoId StarChartPrototype = "CMUBadBloodClanStarChart";

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodCampSystem _camp = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private BadBloodBracerDataCoreSystem _dataCore = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private IntelSystem _intel = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodShipCalledEvent>(OnShipCalled);
        SubscribeLocalEvent<BadBloodStarChartComponent, GotEquippedHandEvent>(OnChartPickedUp);
        SubscribeLocalEvent<BadBloodCampOfferingComponent, GetVerbsEvent<AlternativeVerb>>(OnLodgeVerbs);
        SubscribeLocalEvent<BadBloodCampOfferingComponent, BadBloodStripTrophyDoAfterEvent>(OnStripDoAfter);
    }

    #region The star chart

    private void OnShipCalled(ref BadBloodShipCalledEvent args)
    {
        // a ship called again after turning back does not leave a second chart while the first is still there
        var charts = EntityQueryEnumerator<BadBloodStarChartComponent>();
        while (charts.MoveNext(out var uid, out var chart))
        {
            if (!chart.Recovered && !TerminatingOrDeleted(uid))
                return;
        }

        EntityCoordinates coords;
        if (_camp.TryGetPart(BadBloodCampPartKind.Armory, out var armory))
            coords = _transform.GetMoverCoordinates(armory);
        else if (_camp.TryGetLodge(out var lodge))
            coords = _transform.GetMoverCoordinates(lodge.Owner);
        else
            return;

        Spawn(StarChartPrototype, coords);
    }

    private void OnChartPickedUp(Entity<BadBloodStarChartComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.Recovered || _badBlood.IsBadBlood(args.User))
            return;

        ent.Comp.Recovered = true;
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-intel-star-chart-recovered", ("ship", _dataCore.DescribeShip())),
            args.User, args.User, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"BadBlood obj: {ToPrettyString(args.User):player} recovered the clan star chart {ToPrettyString(ent.Owner):chart}");

        var ev = new BadBloodStarChartRecoveredEvent(ent.Owner, args.User);
        RaiseLocalEvent(ref ev);
    }

    #endregion

    #region Stripping the trophy rack

    private void OnLodgeVerbs(Entity<BadBloodCampOfferingComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || _badBlood.IsBadBlood(args.User) || _rule.GetLodge() != ent.Owner)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-bad-blood-intel-strip-verb"),
            Message = Loc.GetString("cmu-bad-blood-intel-strip-verb-message"),
            Act = () => TryStartStripTrophy(user),
        });
    }

    /// <summary>
    /// Whether <paramref name="user"/> may strip a trophy from the standing Lodge now. <paramref name="reason"/> is
    /// the locale id of the refusal.
    /// </summary>
    public bool CanStripTrophy(EntityUid user, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (_badBlood.IsBadBlood(user))
        {
            reason = "cmu-bad-blood-intel-strip-clan";
            return false;
        }

        if (!TryGetRack(out _, out var rack) || rack.ContainedEntities.Count == 0)
        {
            reason = "cmu-bad-blood-intel-strip-empty";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Starts the strip do-after at the standing Lodge. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartStripTrophy(EntityUid user)
    {
        if (!CanStripTrophy(user, out var reason) || !_camp.TryGetLodge(out var lodge))
        {
            if (reason != null)
                _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);

            return false;
        }

        var raid = EnsureComp<BadBloodLodgeRaidComponent>(lodge.Owner);
        var doAfter = new DoAfterArgs(EntityManager, user, raid.StripTime, new BadBloodStripTrophyDoAfterEvent(),
            lodge.Owner, target: lodge.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-intel-strip-start"), user, user);
        return true;
    }

    private void OnStripDoAfter(Entity<BadBloodCampOfferingComponent> ent, ref BadBloodStripTrophyDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var user = args.User;

        // the Lodge can have fallen, or the rack been emptied, while the do-after ran
        if (_rule.GetLodge() != ent.Owner || !CanStripTrophy(user, out _) || !TryGetRack(out _, out var rack))
            return;

        var trophy = rack.ContainedEntities[rack.ContainedEntities.Count - 1];
        if (!_container.Remove(trophy, rack, destination: Transform(ent.Owner).Coordinates))
            return;

        var raid = EnsureComp<BadBloodLodgeRaidComponent>(ent.Owner);
        _intel.MakeRetrieveIntel(trophy, raid.TrophyRetrieveValue);

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-intel-strip-finished", ("trophy", trophy)), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"BadBlood obj: {ToPrettyString(user):player} stripped {ToPrettyString(trophy):trophy} from the Bad Blood Lodge {ToPrettyString(ent.Owner):lodge}");

        var ev = new BadBloodTrophyStrippedEvent(ent.Owner, trophy, user);
        RaiseLocalEvent(ref ev);
    }

    // the standing Lodge's trophy rack, the offering container
    private bool TryGetRack(out EntityUid lodge, [NotNullWhen(true)] out BaseContainer? rack)
    {
        lodge = default;
        rack = null;
        if (_rule.GetLodge() is not { } standing ||
            !TryComp(standing, out BadBloodCampOfferingComponent? offering) ||
            !_container.TryGetContainer(standing, offering.ContainerId, out rack))
        {
            return false;
        }

        lodge = standing;
        return true;
    }

    #endregion
}
