using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._RMC14.Requisitions;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.Requisitions;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-508: the field uplink relay. GOVFOR requisitions sell a relay kit on Bad Blood rounds only; a marine plants it
/// under open sky and it becomes an uplink site (<see cref="BadBloodUplinkSiteComponent"/>, kind Relay) that runs on
/// its own battery. <see cref="BadBloodObjectiveChainSystem.IsSiteWorking"/> asks <see cref="IsRelayWorking"/>.
/// </summary>
public sealed class BadBloodFieldRelaySystem : EntitySystem
{
    private const string GovforRequisitions = "govfor";
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RequisitionsSystem _requisitions = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodFieldRelayKitComponent, UseInHandEvent>(OnKitUseInHand);
        SubscribeLocalEvent<BadBloodFieldRelayKitComponent, BadBloodRelayPlantDoAfterEvent>(OnPlantDoAfter);
        SubscribeLocalEvent<BadBloodFieldRelayComponent, ExaminedEvent>(OnRelayExamined);

        // the broadcast copy: the directed one on RequisitionsComputerComponent is taken
        SubscribeLocalEvent<CMURequisitionsCatalogBuiltEvent>(OnCatalogBuilt);
        SubscribeLocalEvent<BadBloodRuleStartedEvent>(OnRuleStarted);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateEvery;

        var query = EntityQueryEnumerator<BadBloodFieldRelayComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var relay, out var xform))
        {
            if (!xform.Anchored || relay.ChargeUsed >= relay.ChargeDuration)
                continue;

            relay.ChargeUsed += UpdateEvery;
            if (relay.ChargeUsed < relay.ChargeDuration || relay.EmptyReported)
                continue;

            relay.EmptyReported = true;
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-relay-empty"), uid, PopupType.MediumCaution);
            _adminLog.Add(LogType.Action, LogImpact.Low, $"BadBlood obj: field relay {ToPrettyString(uid):relay} ran out of charge");
        }
    }

    /// <summary>
    /// An anchored relay with charge left.
    /// </summary>
    public bool IsRelayWorking(EntityUid uid)
    {
        return TryComp(uid, out BadBloodFieldRelayComponent? relay) &&
               !TerminatingOrDeleted(uid) &&
               Transform(uid).Anchored &&
               relay.ChargeUsed < relay.ChargeDuration;
    }

    private void OnRelayExamined(Entity<BadBloodFieldRelayComponent> ent, ref ExaminedEvent args)
    {
        var left = ent.Comp.ChargeDuration - ent.Comp.ChargeUsed;
        args.PushMarkup(left > TimeSpan.Zero
            ? Loc.GetString("cmu-bad-blood-obj-relay-examine-charge", ("minutes", (int) Math.Ceiling(left.TotalMinutes)))
            : Loc.GetString("cmu-bad-blood-obj-relay-examine-empty"));
    }

    #region Planting

    private void OnKitUseInHand(Entity<BadBloodFieldRelayKitComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryStartPlant(args.User, ent.Owner);
    }

    /// <summary>
    /// Whether <paramref name="user"/> may plant <paramref name="kit"/> where they stand: not a Bad Blood, on the
    /// ground, under open sky. <paramref name="reason"/> is the locale id of the refusal.
    /// </summary>
    public bool CanPlant(EntityUid user, EntityUid kit, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (!HasComp<BadBloodFieldRelayKitComponent>(kit) || TerminatingOrDeleted(kit))
        {
            reason = "cmu-bad-blood-obj-relay-not-kit";
            return false;
        }

        if (_badBlood.IsBadBlood(user) || _mobState.IsIncapacitated(user))
        {
            reason = "cmu-bad-blood-obj-marines-only";
            return false;
        }

        var xform = Transform(user);
        if (xform.GridUid == null)
        {
            reason = "cmu-bad-blood-obj-relay-no-ground";
            return false;
        }

        // open sky: where a supply drop could land (the area allows it and no roofing entity covers it). A tile with
        // no area at all (unmapped ground) only has the roofing-entity check left, which the area lookup skips, so it
        // falls back to the Lodge kit's test
        // Grid-relative lookups only: CanSupplyDrop(MapCoordinates) converts to map-relative coordinates, which find no
        // area unless the map entity is itself the grid. After CMU ceiling tiers, Fulton and SupplyDrop are the same
        // tier, and CanFulton also applies the roofing-entity check
        if (_area.TryGetArea(xform.Coordinates, out var area, out _))
        {
            if (!area.Value.Comp.SupplyDrop || !_area.CanFulton(xform.Coordinates))
            {
                reason = "cmu-bad-blood-obj-relay-roofed";
                return false;
            }
        }
        else
        {
            _area.CanOrbitalBombard(xform.Coordinates, out var roofed);
            if (roofed)
            {
                reason = "cmu-bad-blood-obj-relay-roofed";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Starts planting the relay kit. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartPlant(EntityUid user, EntityUid kit)
    {
        if (!CanPlant(user, kit, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);
            return false;
        }

        var comp = Comp<BadBloodFieldRelayKitComponent>(kit);
        var doAfter = new DoAfterArgs(EntityManager, user, comp.PlantTime, new BadBloodRelayPlantDoAfterEvent(), kit,
            used: kit)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-relay-plant-start"), user, user);
        return true;
    }

    private void OnPlantDoAfter(Entity<BadBloodFieldRelayKitComponent> ent, ref BadBloodRelayPlantDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var user = args.User;
        if (!CanPlant(user, ent.Owner, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);
            return;
        }

        var coords = Transform(user).Coordinates.SnapToGrid(EntityManager);
        var relay = Spawn(ent.Comp.Relay, coords);
        QueueDel(ent.Owner);

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-obj-relay-planted"), user, user, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"BadBlood obj: {ToPrettyString(user):player} planted the field relay {ToPrettyString(relay):relay}");
    }

    #endregion

    #region Requisitions

    private void OnCatalogBuilt(ref CMURequisitionsCatalogBuiltEvent args)
    {
        if (!TryGetChainConfig(out var chain) || !TryComp(args.Computer, out RequisitionsComputerComponent? computer))
            return;

        TryAddCatalogEntry((args.Computer, computer), chain);
    }

    private void OnRuleStarted(ref BadBloodRuleStartedEvent ev)
    {
        if (!TryComp(ev.Rule.Owner, out BadBloodObjectiveChainComponent? chain))
            return;

        var query = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (query.MoveNext(out var uid, out var computer))
        {
            TryAddCatalogEntry((uid, computer), chain);
        }
    }

    /// <summary>
    /// Whether a requisitions console sells the field relay crate.
    /// </summary>
    public bool HasCatalogEntry(Entity<RequisitionsComputerComponent> computer, string crateId)
    {
        return computer.Comp.Categories.Any(c => c.Entries.Any(e => e.Crate.Id == crateId));
    }

    // only GOVFOR consoles, only once per console
    private void TryAddCatalogEntry(Entity<RequisitionsComputerComponent> computer, BadBloodObjectiveChainComponent chain)
    {
        if (!string.Equals(computer.Comp.Faction, GovforRequisitions, StringComparison.OrdinalIgnoreCase))
            return;

        var category = computer.Comp.Categories.FirstOrDefault(c => c.Name == chain.FieldRelayCategory);
        if (category == null)
        {
            Log.Warning($"ASRS console {ToPrettyString(computer.Owner)} has no {chain.FieldRelayCategory} category; the field relay was not added.");
            return;
        }

        if (category.Entries.Any(e => e.Crate.Id == chain.FieldRelayCrate.Id))
            return;

        _requisitions.AddEntryToCategory(computer.Owner, computer.Comp, chain.FieldRelayCategory, new RequisitionsEntry
        {
            Cost = chain.FieldRelayCost,
            Crate = chain.FieldRelayCrate,
        });
        Dirty(computer);

        // a zero budget change re-sends the ASRS UI state so open windows show the new entry
        _requisitions.ChangeBudget(0, computer.Comp.Faction);
    }

    private bool TryGetChainConfig([NotNullWhen(true)] out BadBloodObjectiveChainComponent? chain)
    {
        chain = null;
        return _rule.TryGetRule(out var rule) && TryComp(rule.Owner, out chain);
    }

    #endregion
}
