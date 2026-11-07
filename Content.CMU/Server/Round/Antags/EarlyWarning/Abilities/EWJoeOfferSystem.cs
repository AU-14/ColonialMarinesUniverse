using Content.Server.Administration.Logs;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Database;
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
/// Also rolls, titles and scores <see cref="EWJoeOfferConditionComponent"/> objectives.
/// </summary>
public sealed partial class EWJoeOfferSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EWJoeCollectConditionSystem _collect = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeOfferActionEvent>(OnOffer);
        SubscribeLocalEvent<EWJoeComponent, EWJoeOfferDoAfterEvent>(OnOfferDoAfter);

        SubscribeLocalEvent<EWJoeOfferConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<EWJoeOfferConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<EWJoeOfferConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
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

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.OfferDelay, new EWJoeOfferDoAfterEvent(), ent, item)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-offer-start", ("item", item)), ent, ent);
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
        if (!IsOfferable(item))
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
    /// A carried weapon, not machinery or a body: an item with a gun that is not anchored down, not a
    /// sentry or FOB gun, not a drone or other mob, not an attachment and not a vehicle hardpoint.
    /// </summary>
    public bool IsOfferable(EntityUid item)
    {
        return Exists(item)
            && HasComp<GunComponent>(item)
            && HasComp<ItemComponent>(item)
            && !Transform(item).Anchored
            && !HasComp<SentryComponent>(item)
            && !HasComp<MobStateComponent>(item)
            && !HasComp<AttachableComponent>(item)
            && !HasComp<HardpointItemComponent>(item);
    }

    /// <summary>The Joe's unfinished Offer directives that <paramref name="item"/> fits.</summary>
    public List<EWJoeOfferConditionComponent> OpenOffers(EntityUid joe, EntityUid item)
    {
        var open = new List<EWJoeOfferConditionComponent>();
        if (!IsOfferable(item) || !_mind.TryGetMind(joe, out _, out var mind))
            return open;

        foreach (var objective in mind.Objectives)
        {
            if (TryComp(objective, out EWJoeOfferConditionComponent? offer)
                && offer.Offered < offer.Target
                && _whitelist.IsWhitelistPass(offer.Whitelist, item))
            {
                open.Add(offer);
            }
        }

        return open;
    }

    /// <summary>Consumes the item and ticks every Offer directive it fits. Public so tests can skip the do-after.</summary>
    public void Offer(Entity<EWJoeComponent> joe, EntityUid item)
    {
        var open = OpenOffers(joe, item);
        if (open.Count == 0)
            return;

        foreach (var offer in open)
        {
            offer.Offered++;
        }

        var itemName = Name(item);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} offered {ToPrettyString(item):item}");

        // the gun leaves play with its magazine and attachments; anything merely stored in it drops, as with Scour
        if (TryComp(item, out StorageComponent? storage))
            _container.EmptyContainer(storage.Container, true);
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

        var available = _collect.CountAvailable(_zLevels.GetAllNetworkMapIds(mapId), ent.Comp.Whitelist, max, filter: IsOfferable);
        if (available < min)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.Target = _random.Next(min, Math.Min(max, available) + 1);
        ent.Comp.Offered = 0;
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

    #endregion
}
