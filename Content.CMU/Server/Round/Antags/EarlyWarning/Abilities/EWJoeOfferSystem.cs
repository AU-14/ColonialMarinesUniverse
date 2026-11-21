using Content.Server.Administration.Logs;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Threats;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Offer: the Joe takes in a loose item an unfinished Offer directive asks for (a firearm), the way Scour
/// takes in gear, and the item leaves play. Each offering ticks every unfinished Offer directive it fits.
/// ARCH-11: a Meld directive's offering (a camera, a telecomms server) is not taken in but melded into the
/// river where it stands (<see cref="EWJoeMeldedStructureSystem"/>); a structure directive with the default
/// filter off (Purge the Nest) consumes anchored threat structures like items. RB-31: a structure takes
/// <see cref="StructureOfferDelay"/> to offer, an item the Joe's own <see cref="EWJoeComponent.OfferDelay"/>.
/// Also rolls, titles and scores <see cref="EWJoeOfferConditionComponent"/> objectives.
/// </summary>
public sealed partial class EWJoeOfferSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDestructibleSystem _destructible = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EWJoeCollectConditionSystem _collect = default!;
    [Dependency] private EWJoeMeldedStructureSystem _melded = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    /// <summary>RB-31: the Offer do-after on a structure (items keep the Joe's own, shorter delay).</summary>
    public static readonly TimeSpan StructureOfferDelay = TimeSpan.FromSeconds(6);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeOfferActionEvent>(OnOffer);
        SubscribeLocalEvent<EWJoeComponent, EWJoeOfferDoAfterEvent>(OnOfferDoAfter);

        SubscribeLocalEvent<EWJoeOfferConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<EWJoeOfferConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<EWJoeOfferConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<EWJoeOfferConditionComponent, EWJoeObjectiveStillPossibleEvent>(OnStillPossible);
    }

    #region Action

    private void OnOffer(Entity<EWJoeComponent> ent, ref EWJoeOfferActionEvent args)
    {
        if (args.Handled)
            return;

        var item = args.Target;
        if (!CanOffer(ent, item))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, ent, OfferDelayFor(ent.Comp, item), new EWJoeOfferDoAfterEvent(), ent, item)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-offer-start", ("item", item)), ent, ent);
    }

    /// <summary>
    /// RB-31: how long offering <paramref name="item"/> takes. A loose item takes the Joe's
    /// <see cref="EWJoeComponent.OfferDelay"/>; a structure (anchored, or no item at all: a wall camera, a
    /// server, a resin wall) takes <see cref="StructureOfferDelay"/>, so one Offer cannot strip a nest in passing.
    /// </summary>
    public TimeSpan OfferDelayFor(EWJoeComponent joe, EntityUid item)
    {
        if (!IsStructure(item))
            return joe.OfferDelay;

        return joe.OfferDelay > StructureOfferDelay ? joe.OfferDelay : StructureOfferDelay;
    }

    /// <summary>Anchored down, or not a carried thing at all.</summary>
    public bool IsStructure(EntityUid item)
    {
        return Transform(item).Anchored || !HasComp<ItemComponent>(item);
    }

    private void OnOfferDoAfter(Entity<EWJoeComponent> ent, ref EWJoeOfferDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } item)
            return;

        args.Handled = true;

        if (!Exists(item) || !CanOffer(ent, item))
            return;

        Offer(ent, item);
    }

    /// <summary>
    /// An item on the ground (not in a hand, bag or holster) that an unfinished Offer directive wants.
    /// Refusals say why, so the Joe is never left guessing.
    /// </summary>
    private bool CanOffer(Entity<EWJoeComponent> joe, EntityUid item)
    {
        if (!IsOfferable(item) && !AnyOfferIgnoresDefaultFilter(joe, item))
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-offer-invalid"), joe, joe, PopupType.SmallCaution);
            return false;
        }

        if (OpenOffers(joe, item).Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-offer-unwanted"), joe, joe, PopupType.SmallCaution);
            return false;
        }

        if (_container.IsEntityInContainer(item))
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-offer-taken", ("item", item)), joe, joe, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    /// <summary>
    /// COUPLING-02, the default offering filter: a carried thing, not machinery or a body. An item that is
    /// not anchored down, not a sentry or FOB gun, not a drone or other mob, not an attachment and not a
    /// vehicle hardpoint. What kind of item an Offer directive wants is its own whitelist
    /// (<see cref="IsOfferable(EntityUid, EWJoeOfferConditionComponent)"/>); firearms are one such whitelist.
    /// </summary>
    public bool IsOfferable(EntityUid item)
    {
        return Exists(item)
            && HasComp<ItemComponent>(item)
            && !Transform(item).Anchored
            && !HasComp<SentryComponent>(item)
            && !HasComp<MobStateComponent>(item)
            && !HasComp<AttachableComponent>(item)
            && !HasComp<HardpointItemComponent>(item);
    }

    /// <summary>
    /// Whether <paramref name="item"/> is something <paramref name="offer"/> wants: it passes the directive's
    /// whitelist, fails its blacklist, and (unless the directive opts out) the default filter.
    /// </summary>
    public bool IsOfferable(EntityUid item, EWJoeOfferConditionComponent offer)
    {
        // ARCH-11: a melded structure has been given already
        if (!Exists(item) || HasComp<EWJoeMeldedStructureComponent>(item))
            return false;

        // a repaired structure is fair game for a new directive, never again for the one it counted for
        if (offer.Meld && offer.MeldCounted.Contains(item))
            return false;

        if (UsesDefaultFilter(offer) && !IsOfferable(item))
            return false;

        if (Weight(item, offer) <= 0)
            return false;

        return _whitelist.IsWhitelistPass(offer.Whitelist, item)
            && (offer.Blacklist == null || !_whitelist.IsWhitelistPass(offer.Blacklist, item));
    }

    /// <summary>The Joe's unfinished Offer directives that <paramref name="item"/> fits.</summary>
    public List<EWJoeOfferConditionComponent> OpenOffers(EntityUid joe, EntityUid item)
    {
        var open = new List<EWJoeOfferConditionComponent>();
        if (!Exists(item) || !_mind.TryGetMind(joe, out _, out var mind))
            return open;

        var itemMap = Transform(item).MapID;
        foreach (var objective in mind.Objectives)
        {
            if (TryComp(objective, out EWJoeOfferConditionComponent? offer)
                && offer.Offered < offer.Target
                && OnOfferPlanet(offer, itemMap)
                && IsOfferable(item, offer))
            {
                open.Add(offer);
            }
        }

        return open;
    }

    /// <summary>Whether <paramref name="map"/> belongs to the planet <paramref name="offer"/> was given on.</summary>
    private bool OnOfferPlanet(EWJoeOfferConditionComponent offer, MapId map)
    {
        if (offer.OfferMap is not { } offerMap)
            return true;

        return _zLevels.GetAllNetworkMapIds(offerMap).Contains(map);
    }

    /// <summary>An open Offer directive that opted out of the default filter and wants this item.</summary>
    private bool AnyOfferIgnoresDefaultFilter(EntityUid joe, EntityUid item)
    {
        foreach (var offer in OpenOffers(joe, item))
        {
            if (!UsesDefaultFilter(offer))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="offer"/> wants carried items only. Meld directives want structures, and a
    /// directive may opt out (<see cref="EWJoeOfferConditionComponent.DefaultFilter"/>).
    /// </summary>
    private static bool UsesDefaultFilter(EWJoeOfferConditionComponent offer)
    {
        return offer.DefaultFilter && !offer.Meld;
    }

    /// <summary>
    /// What <paramref name="item"/> is worth to <paramref name="offer"/>: 1, or its threat structure weight
    /// (a flesh nest 3, spreading growth 0) when the directive is weighted. RB-31: hive, overmind and blight
    /// cores are not on this scale at all; Purge the Nest blacklists them (ew_joe.yml).
    /// </summary>
    public int Weight(EntityUid item, EWJoeOfferConditionComponent offer)
    {
        if (!offer.Weighted)
            return 1;

        return TryComp(item, out CMUThreatStructureComponent? structure) ? structure.Weight : 1;
    }

    /// <summary>
    /// Consumes the item (or melds it, for a Meld directive) and ticks every Offer directive it fits, by
    /// weight where weighted. Public so tests can skip the do-after.
    /// </summary>
    public void Offer(Entity<EWJoeComponent> joe, EntityUid item)
    {
        var open = OpenOffers(joe, item);
        if (open.Count == 0)
            return;

        var meld = false;
        foreach (var offer in open)
        {
            offer.Offered += Weight(item, offer);
            meld |= offer.Meld;
            if (offer.Meld)
                offer.MeldCounted.Add(item);
        }

        var itemName = Name(item);
        var how = meld ? "melded" : "offered";
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} {how} {ToPrettyString(item):item}");

        // ARCH-11: a structure stays where it stands, melded into the river; the marker keeps it from counting twice
        if (meld)
        {
            _melded.Meld(item, joe);
            _messages.Notify(joe, Loc.GetString("cmu-ew-joe-offer-melded", ("item", itemName)));
            return;
        }

        // the gun leaves play with its magazine and attachments; anything merely stored in it drops, as with Scour
        if (TryComp(item, out StorageComponent? storage))
            _container.EmptyContainer(storage.Container, true);

        // a consumed threat structure is destroyed, not just removed, so its destruction hooks still run
        // (a hive core's replant cooldown, an egg's remains)
        if (!HasComp<CMUThreatStructureComponent>(item) || !_destructible.DestroyEntity(item))
            QueueDel(item);

        _messages.Notify(joe, Loc.GetString("cmu-ew-joe-offer-done", ("item", itemName)));
    }

    #endregion

    #region Objective

    private void OnAssigned(Entity<EWJoeOfferConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        var min = Math.Max(1, ent.Comp.Min);
        var max = Math.Max(min, ent.Comp.Max);

        // only what lies on the Joe's planet (its z-level network of maps) can be offered
        if (args.Mind.OwnedEntity is not { } owner)
        {
            args.Cancelled = true;
            return;
        }

        var mapId = Transform(owner).MapID;
        if (mapId == MapId.Nullspace)
        {
            args.Cancelled = true;
            return;
        }

        var offer = ent.Comp;
        var available = _collect.CountAvailable(_zLevels.GetAllNetworkMapIds(mapId), ent.Comp.Whitelist, max,
            filter: uid => IsOfferable(uid, offer), weight: uid => Weight(uid, offer));
        if (available < min)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.Target = _random.Next(min, Math.Min(max, available) + 1);
        ent.Comp.Offered = 0;
        ent.Comp.OfferMap = mapId;
    }

    private void OnAfterAssign(Entity<EWJoeOfferConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent, Loc.GetString(ent.Comp.Title, ("count", ent.Comp.Target)), args.Meta);
        _metaData.SetEntityDescription(ent, Loc.GetString(ent.Comp.Description, ("count", ent.Comp.Target)), args.Meta);
    }

    private void OnGetProgress(Entity<EWJoeOfferConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Target <= 0 ? 1f : Math.Clamp(ent.Comp.Offered / (float) ent.Comp.Target, 0f, 1f);
    }

    /// <summary>
    /// ARCH-02: still possible while enough offerable things for the rest of the count (by weight) are on the
    /// planet. Melded structures are never offerable again, so they do not count.
    /// </summary>
    private void OnStillPossible(Entity<EWJoeOfferConditionComponent> ent, ref EWJoeObjectiveStillPossibleEvent args)
    {
        var remaining = ent.Comp.Target - ent.Comp.Offered;
        if (remaining <= 0)
            return;

        var offer = ent.Comp;
        if (_collect.CountAvailable(args.Maps, offer.Whitelist, remaining, filter: uid => IsOfferable(uid, offer),
                weight: uid => Weight(uid, offer)) < remaining)
            args.Possible = false;
    }

    #endregion
}
