using System.Linq;
using System.Text;
using Content.Server._RMC14.Dropship;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Ambassador;
using Content.Server.CMU14.Chat;
using Content.Server.CMU14.Comms;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Dropship;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.ColonyShift;

public sealed partial class ColonyRotationRuleSystem : GameRuleSystem<ColonyRotationRuleComponent>
{
    [Dependency] private AmbassadorConsoleSystem _ambassador = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ColonyCommsConsoleSystem _comms = default!;
    [Dependency] private DropshipSystem _dropship = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void Started(EntityUid uid, ColonyRotationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var now = Timing.CurTime;
        component.Status = ColonyRotationTransportStatus.Scheduled;
        component.AutoDispatchAt = now + component.AutoDispatchDelay;
        component.EarliestCallAt = now + component.EarliestCall;
        _comms.RefreshTransportUi();
    }

    protected override void ActiveTick(EntityUid uid, ColonyRotationRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var now = Timing.CurTime;
        switch (component.Status)
        {
            case ColonyRotationTransportStatus.Scheduled:
                if (now >= component.AutoDispatchAt)
                    Dispatch(component, false);
                break;
            case ColonyRotationTransportStatus.Inbound:
                // load and launch early enough that it touches down on the announced ETA
                if (now >= component.ArriveAt - LaunchLead(component) && now >= component.NextAttempt)
                    TryLaunchInbound(component);
                break;
            case ColonyRotationTransportStatus.Approaching:
                UpdateApproach(component);
                break;
            case ColonyRotationTransportStatus.Docked:
                UpdateDocked(component);
                break;
            case ColonyRotationTransportStatus.Departing:
                UpdateDeparture(component);
                break;
        }
    }

    public bool TryGetTransport(out ColonyRotationTransportStatus status, out TimeSpan? at, out bool canCall, out bool canRecall)
    {
        status = ColonyRotationTransportStatus.None;
        at = null;
        canCall = false;
        canRecall = false;
        if (!TryGetActive(out var comp))
            return false;

        var now = Timing.CurTime;
        status = comp.Status;
        at = comp.Status switch
        {
            ColonyRotationTransportStatus.Scheduled => comp.AutoDispatchAt,
            ColonyRotationTransportStatus.Inbound or ColonyRotationTransportStatus.Approaching => comp.ArriveAt,
            ColonyRotationTransportStatus.Docked => comp.DepartAt,
            _ => null,
        };
        canCall = comp.Status == ColonyRotationTransportStatus.Scheduled &&
                  now >= comp.EarliestCallAt && now >= comp.NextCallAllowed;
        canRecall = CanRecall(comp, now);
        return true;
    }

    public bool TryCall(out string? reason)
    {
        reason = null;
        if (!TryGetActive(out var comp) || comp.Status != ColonyRotationTransportStatus.Scheduled)
            return false;

        var now = Timing.CurTime;
        if (now < comp.EarliestCallAt)
        {
            reason = Loc.GetString("colony-rotation-call-too-early",
                ("minutes", Minutes(comp.EarliestCallAt - now)));
            return false;
        }

        if (now < comp.NextCallAllowed)
        {
            reason = Loc.GetString("colony-rotation-call-cooldown");
            return false;
        }

        Dispatch(comp, true);
        return true;
    }

    public bool TryRecall(out string? reason)
    {
        reason = null;
        if (!TryGetActive(out var comp))
            return false;

        var now = Timing.CurTime;
        if (!CanRecall(comp, now))
        {
            reason = Loc.GetString(CanRecall(comp, now, true)
                ? "colony-rotation-recall-cooldown"
                : "colony-rotation-recall-committed");
            return false;
        }

        Recall(comp);
        return true;
    }

    private void Recall(ColonyRotationRuleComponent comp)
    {
        var now = Timing.CurTime;

        // the auto one can be recalled too, it just comes back later
        comp.Status = ColonyRotationTransportStatus.Scheduled;
        comp.AutoDispatchAt = now + comp.RecallExtension;
        comp.NextCallAllowed = now + comp.CallCooldown;
        comp.Pad = null;
        Announce(comp, Loc.GetString("colony-rotation-recalled",
            ("minutes", Minutes(comp.RecallExtension))));
        _comms.RefreshTransportUi();
    }

    private bool CanRecall(ColonyRotationRuleComponent comp, TimeSpan now, bool ignoreCooldown = false)
    {
        // once the shuttle's loaded and flying there's no turning it round
        return comp.Status == ColonyRotationTransportStatus.Inbound &&
               comp.Shuttle == null &&
               now < comp.ArriveAt - LaunchLead(comp) &&
               (ignoreCooldown || now >= comp.NextCallAllowed);
    }

    private bool TryGetActive(out ColonyRotationRuleComponent comp)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var rule, out _))
        {
            comp = rule;
            return true;
        }

        comp = default!;
        return false;
    }

    private TimeSpan LaunchLead(ColonyRotationRuleComponent comp)
    {
        return TimeSpan.FromSeconds(comp.FlightStartup + comp.FlightTime);
    }

    private void Dispatch(ColonyRotationRuleComponent comp, bool called, TimeSpan? eta = null)
    {
        var now = Timing.CurTime;
        // can't land sooner than the flight itself takes
        var inbound = eta ?? comp.InboundTime;
        if (inbound < LaunchLead(comp))
            inbound = LaunchLead(comp);

        comp.Status = ColonyRotationTransportStatus.Inbound;
        comp.ArriveAt = now + inbound;
        comp.NextCallAllowed = now + comp.CallCooldown;
        comp.NextAttempt = now;
        comp.Pad = PickPad();

        var where = comp.Pad is { } pad ? PadName(pad) : Loc.GetString("colony-rotation-pad-unknown");
        Announce(comp, Loc.GetString(called ? "colony-rotation-called" : "colony-rotation-dispatched",
            ("minutes", Minutes(inbound)),
            ("pad", where)));
        _comms.RefreshTransportUi();
    }

    private void TryLaunchInbound(ColonyRotationRuleComponent comp)
    {
        comp.NextAttempt = Timing.CurTime + comp.RetryDelay;
        if (comp.Shuttle == null && !TryLoadShuttle(comp))
        {
            // no shuttle means no ending, so don't strand the round on a broken grid file
            Log.Error($"Couldn't load the colony rotation transport from {comp.ShuttlePath}, ending the shift without it.");
            EndShift(comp);
            return;
        }

        if (!IsValidPad(comp.Pad))
            comp.Pad = PickPad();

        if (comp.Pad is not { } pad)
        {
            Log.Warning("Colony rotation transport has nowhere to land, retrying.");
            return;
        }

        if (!TryFly(comp, pad))
        {
            comp.Pad = null;
            return;
        }

        comp.Status = ColonyRotationTransportStatus.Approaching;
        _comms.RefreshTransportUi();
    }

    private void UpdateApproach(ColonyRotationRuleComponent comp)
    {
        if (comp.Shuttle is not { } ship || TerminatingOrDeleted(ship) || comp.Pad is not { } pad)
        {
            EndShift(comp);
            return;
        }

        if (TryComp(ship, out FTLComponent? ftl) && ftl.State is not (FTLState.Cooldown or FTLState.Available))
            return;

        if (Transform(ship).MapUid != Transform(pad).MapUid)
        {
            // FTL setup got vetoed somewhere, it's still sitting in deep space
            comp.Status = ColonyRotationTransportStatus.Inbound;
            comp.NextAttempt = Timing.CurTime + comp.RetryDelay;
            return;
        }

        var now = Timing.CurTime;
        comp.Status = ColonyRotationTransportStatus.Docked;
        comp.DepartAt = now + comp.DockTime;
        comp.NextDockWarning = 0;
        Announce(comp, Loc.GetString("colony-rotation-landed",
            ("pad", PadName(pad)),
            ("minutes", Minutes(comp.DockTime))));
        _comms.RefreshTransportUi();
    }

    private void UpdateDocked(ColonyRotationRuleComponent comp)
    {
        if (comp.Shuttle is not { } ship || TerminatingOrDeleted(ship))
        {
            EndShift(comp);
            return;
        }

        var now = Timing.CurTime;
        var left = comp.DepartAt - now;
        while (comp.NextDockWarning < comp.DockWarnings.Count && left <= comp.DockWarnings[comp.NextDockWarning])
        {
            var warning = comp.DockWarnings[comp.NextDockWarning];
            comp.NextDockWarning++;

            // a short dock time skips the warnings it's already past
            if (left < warning - TimeSpan.FromSeconds(5))
                continue;

            Announce(comp, warning >= TimeSpan.FromMinutes(1)
                ? Loc.GetString("colony-rotation-departing-minutes", ("minutes", Minutes(warning)))
                : Loc.GetString("colony-rotation-departing-seconds", ("seconds", (int) warning.TotalSeconds)));
        }

        if (now < comp.DepartAt || now < comp.NextAttempt)
            return;

        comp.NextAttempt = now + comp.RetryDelay;
        if (comp.ReturnPad is not { } home || TerminatingOrDeleted(home) || !TryFly(comp, home))
        {
            // can't get airborne, the shift's over either way
            Log.Warning("Colony rotation transport couldn't take off, ending the shift where it stands.");
            EndShift(comp);
            return;
        }

        comp.Status = ColonyRotationTransportStatus.Departing;
        comp.DepartAt = now + comp.DepartureTimeout;
        Announce(comp, Loc.GetString("colony-rotation-departed"));
        _comms.RefreshTransportUi();
    }

    private void UpdateDeparture(ColonyRotationRuleComponent comp)
    {
        if (comp.Shuttle is { } ship && !TerminatingOrDeleted(ship) &&
            TryComp(ship, out FTLComponent? ftl) && ftl.State == FTLState.Starting &&
            Timing.CurTime < comp.DepartAt)
        {
            return;
        }

        EndShift(comp);
    }

    private bool TryLoadShuttle(ColonyRotationRuleComponent comp)
    {
        var options = new MapLoadOptions
        {
            DeserializationOptions = DeserializationOptions.Default with
            {
                InitializeMaps = true,
                LogOrphanedGrids = false,
            },
        };

        if (!_mapLoader.TryLoadGeneric(comp.ShuttlePath, out var result, options))
            return false;

        var ship = result.Grids.FirstOrDefault();
        if (ship == default || !TryGetNavigation(ship, out _))
        {
            _mapLoader.Delete(result);
            return false;
        }

        // same deep space return setup third parties and the hospital transport use
        var home = Spawn(comp.ReturnDestination, _transform.ToMapCoordinates(Transform(ship).Coordinates));
        EnsureComp<ThirdPartyDropshipReturnDestinationComponent>(home).Shuttle = ship;
        EnsureComp<DropshipDestinationComponent>(home);
        _dropship.SetDestinationShip(home, ship);
        _dropship.SetDestinationHome(home, true);
        EnsureComp<DropshipComponent>(ship);
        _dropship.SetDropshipDestination(ship, home);

        comp.Shuttle = ship;
        comp.ReturnPad = home;
        return true;
    }

    private bool TryFly(ColonyRotationRuleComponent comp, EntityUid destination)
    {
        if (comp.Shuttle is not { } ship || !TryGetNavigation(ship, out var nav) || HasComp<FTLComponent>(ship))
            return false;

        // FlyTo's bool isn't the whole story, FTL setup can still bail inside it
        _dropship.FlyTo(nav, destination, null, startupTime: comp.FlightStartup, hyperspaceTime: comp.FlightTime);
        return TryComp(ship, out FTLComponent? ftl) && ftl.State == FTLState.Starting;
    }

    private bool TryGetNavigation(EntityUid ship, out Entity<DropshipNavigationComputerComponent> nav)
    {
        var children = Transform(ship).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (TryComp(child, out DropshipNavigationComputerComponent? computer))
            {
                nav = (child, computer);
                return true;
            }
        }

        nav = default;
        return false;
    }

    private EntityUid? PickPad()
    {
        // plain colony pads first, third party pads only if that's all the map has
        EntityUid? fallback = null;
        var query = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var destination, out _))
        {
            if (!IsValidPad(uid))
                continue;

            if (string.IsNullOrEmpty(destination.FactionController))
                return uid;

            if (string.Equals(destination.FactionController, "thirdparty", StringComparison.OrdinalIgnoreCase))
                fallback ??= uid;
        }

        return fallback;
    }

    private bool IsValidPad(EntityUid? pad)
    {
        if (pad is not { } uid || TerminatingOrDeleted(uid) ||
            !TryComp(uid, out DropshipDestinationComponent? destination) ||
            destination.Home ||
            destination.Ship is { } occupant && !TerminatingOrDeleted(occupant) ||
            HasComp<ThirdPartyDropshipReturnDestinationComponent>(uid) ||
            HasComp<CMUSummonedThirdPartyDestinationComponent>(uid))
        {
            return false;
        }

        var xform = Transform(uid);
        return HasComp<RMCPlanetComponent>(xform.MapUid) || HasComp<RMCPlanetComponent>(xform.GridUid);
    }

    private string PadName(EntityUid pad)
    {
        return _area.TryGetArea(pad, out _, out var area)
            ? area.Name
            : Loc.GetString("colony-rotation-pad-unknown");
    }

    private void Announce(ColonyRotationRuleComponent comp, string message)
    {
        _chat.DispatchFilteredAnnouncement(ColonyAnnouncements.Recipients(EntityManager),
            message,
            sender: Loc.GetString("colony-rotation-announcement-sender"),
            announcementSound: comp.AnnouncementSound);
    }

    private static int Minutes(TimeSpan time)
    {
        return Math.Max(1, (int) Math.Ceiling(time.TotalMinutes));
    }

    private void EndShift(ColonyRotationRuleComponent comp)
    {
        if (comp.Status == ColonyRotationTransportStatus.None)
            return;

        comp.Status = ColonyRotationTransportStatus.None;
        TallyShift(comp);
        _comms.RefreshTransportUi();
        GameTicker.EndRound(Loc.GetString("colony-rotation-round-end"));
    }

    private void TallyShift(ColonyRotationRuleComponent comp)
    {
        comp.RotatedOut.Clear();
        comp.ShipmentCounts.Clear();
        comp.ShipmentValue = 0;
        comp.StayedBehind = 0;

        var aboard = new HashSet<EntityUid>();
        if (comp.Shuttle is { } ship && !TerminatingOrDeleted(ship))
        {
            var prices = GetPrices();
            var pending = new Stack<EntityUid>();
            pending.Push(ship);
            while (pending.TryPop(out var uid))
            {
                var children = Transform(uid).ChildEnumerator;
                while (children.MoveNext(out var child))
                    pending.Push(child);

                if (uid == ship)
                    continue;

                if (IsLivePlayer(uid))
                {
                    aboard.Add(uid);
                    comp.RotatedOut.Add(Name(uid));
                    continue;
                }

                var price = GetPrice(uid, prices);
                if (price <= 0)
                    continue;

                var count = TryComp(uid, out StackComponent? stack) ? stack.Count : 1;
                var name = MetaData(uid).EntityPrototype?.Name ?? Name(uid);
                comp.ShipmentCounts[name] = comp.ShipmentCounts.GetValueOrDefault(name) + count;
                comp.ShipmentValue += price * count;
            }
        }

        comp.ShipmentValue *= _ambassador.GetSubmissionMultiplier();
        comp.RotatedOut.Sort(StringComparer.CurrentCultureIgnoreCase);

        var mobs = EntityQueryEnumerator<MindContainerComponent, MobStateComponent>();
        while (mobs.MoveNext(out var uid, out _, out _))
        {
            if (!aboard.Contains(uid) && IsLivePlayer(uid))
                comp.StayedBehind++;
        }
    }

    private bool IsLivePlayer(EntityUid uid)
    {
        return TryComp(uid, out MindContainerComponent? mind) && mind.HasMind &&
               HasComp<MobStateComponent>(uid) && !_mobState.IsDead(uid);
    }

    // the submission points mapped on the colony are the price list, best price per tag wins
    private Dictionary<ProtoId<TagPrototype>, float> GetPrices()
    {
        var prices = new Dictionary<ProtoId<TagPrototype>, float>();
        var query = EntityQueryEnumerator<SubmissionStorageComponent>();
        while (query.MoveNext(out var storage))
        {
            if (storage.Rewards == null)
                continue;

            foreach (var (tag, reward) in storage.Rewards)
            {
                if (!prices.TryGetValue(tag, out var best) || reward > best)
                    prices[tag] = reward;
            }
        }

        return prices;
    }

    private float GetPrice(EntityUid uid, Dictionary<ProtoId<TagPrototype>, float> prices)
    {
        if (prices.Count == 0 || !TryComp(uid, out TagComponent? tags))
            return 0;

        // same averaging as SubmissionStorageSystem so the numbers match what the chutes would've paid
        var sum = 0f;
        var matched = 0;
        foreach (var tag in tags.Tags)
        {
            if (!prices.TryGetValue(tag, out var reward))
                continue;

            sum += reward;
            matched++;
        }

        return matched == 0 ? 0 : sum / matched;
    }

    protected override void AppendRoundEndText(EntityUid uid, ColonyRotationRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        args.AddLine(Loc.GetString("colony-rotation-summary-rotated", ("count", component.RotatedOut.Count)));
        if (component.RotatedOut.Count > 0)
            args.AddLine(string.Join(", ", component.RotatedOut));

        args.AddLine(Loc.GetString("colony-rotation-summary-stayed", ("count", component.StayedBehind)));

        if (component.ShipmentCounts.Count == 0)
        {
            args.AddLine(Loc.GetString("colony-rotation-summary-no-shipment"));
            return;
        }

        args.AddLine(Loc.GetString("colony-rotation-summary-shipment",
            ("value", (int) Math.Round(component.ShipmentValue))));

        var lines = new StringBuilder();
        foreach (var (name, count) in component.ShipmentCounts.OrderByDescending(pair => pair.Value))
        {
            if (lines.Length > 0)
                lines.Append(", ");

            lines.Append(Loc.GetString("colony-rotation-summary-shipment-line", ("count", count), ("item", name)));
        }

        args.AddLine(lines.ToString());
    }
}
