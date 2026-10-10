using System.Linq;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Stack;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Repairable;
using Content.Shared.CMU14.Construction;
using Content.Shared.CMU14.Radio;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Construction;

/// <summary>
///     Everything about the field antenna mast that its construction graph cannot say for itself: who may
///     work on it, where it may stand, and which side's nets it carries.
///     <list type="bullet">
///         <item>Graph steps never see the user, so training is enforced one level up: an interaction that
///         looks like construction work is vetoed here before <see cref="ConstructionSystem"/> validates it.</item>
///         <item>RMC construction has no veto before a build, so a footing is checked the moment it lands and
///         taken back down, metal returned, when the site is wrong.</item>
///         <item>The mast only relays the nets of the factions keyed into it. Keys go in and come out only
///         while the feed is open, which is the half-way point both of raising a mast and of taking one over.</item>
///     </list>
/// </summary>
public sealed partial class AU14MastAssemblySystem : EntitySystem
{
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private StackSystem _stack = default!;

    private static readonly ProtoId<TagPrototype> AntennaHeadTag = "AU14MastAntennaHead";
    private static readonly ProtoId<TagPrototype> CircuitboardTag = "RMCCircuitboard";
    private static readonly ProtoId<ToolQualityPrototype> PulsingQuality = "Pulsing";

    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();

        // After repair so welding a damaged mast is still a repair rather than a training complaint,
        // before construction so an untrained user never reaches a graph step.
        SubscribeLocalEvent<AU14MastAssemblyComponent, InteractUsingEvent>(OnInteractUsing,
            before: [typeof(ConstructionSystem)],
            after: [typeof(RMCRepairableSystem)]);
        SubscribeLocalEvent<AU14MastAssemblyComponent, ExaminedEvent>(OnAssemblyExamined);

        SubscribeLocalEvent<AU14MastKeyComponent, MapInitEvent>(OnKeyMapInit);
        SubscribeLocalEvent<AU14MastKeyComponent, ConstructionChangeEntityEvent>(OnKeyEntityChanged);
        SubscribeLocalEvent<AU14MastKeyComponent, AU14MastKeyDoAfterEvent>(OnKeyDoAfter);
        SubscribeLocalEvent<AU14MastKeyComponent, AU14MastZeroizeDoAfterEvent>(OnZeroizeDoAfter);
        SubscribeLocalEvent<AU14MastKeyComponent, ExaminedEvent>(OnKeyExamined);

        SubscribeLocalEvent<RMCConstructionTransactionCompletedEvent>(OnConstructionCompleted);
    }

    #region Training gate and feed work

    private void OnInteractUsing(Entity<AU14MastAssemblyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var feedWork = IsFeedWork(ent.Owner, args.Used, out var key);

        // Only stand in the way of things that could actually be a construction step or feed work. A finished
        // mast is also a splice target and a repair target, and swallowing every interaction here would break both.
        if (!feedWork && !IsConstructionInput(args.Used))
            return;

        if (!HasMastTraining(args.User, ent.Comp))
        {
            // Handled, so construction never sees the interaction and the untrained user is told why rather than
            // being left clicking at a structure that silently does nothing.
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("au14-mast-untrained"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!feedWork || key == null)
            return;

        args.Handled = true;
        StartFeedWork((ent.Owner, key), args.User, args.Used);
    }

    /// <summary>
    ///     Whether this item is the sort of thing the mast and antenna head graphs consume: a tool, a material
    ///     stack, a circuit board or the antenna head.
    /// </summary>
    private bool IsConstructionInput(EntityUid used)
    {
        return HasComp<ToolComponent>(used) ||
               HasComp<StackComponent>(used) ||
               _tags.HasTag(used, AntennaHeadTag) ||
               _tags.HasTag(used, CircuitboardTag);
    }

    /// <summary>
    ///     A fill card or a multitool used on a mast whose feed is open. The multitool is a graph step one node
    ///     earlier (tuning the feed after the head goes on), so the node has to be checked, not just the tool.
    /// </summary>
    private bool IsFeedWork(EntityUid mast, EntityUid used, out AU14MastKeyComponent? key)
    {
        key = null;

        if (!TryComp(mast, out key) || !IsFeedOpen((mast, key)))
            return false;

        return HasComp<ANPRCFillCardComponent>(used) || _tools.HasQuality(used, PulsingQuality);
    }

    private bool IsFeedOpen(Entity<AU14MastKeyComponent> mast)
    {
        return TryComp(mast, out ConstructionComponent? construction) &&
               construction.Node == mast.Comp.FeedOpenNode;
    }

    private void StartFeedWork(Entity<AU14MastKeyComponent> mast, EntityUid user, EntityUid used)
    {
        if (TryComp(used, out ANPRCFillCardComponent? card))
        {
            if (string.IsNullOrEmpty(card.Faction) || !mast.Comp.FactionChannels.ContainsKey(card.Faction))
            {
                _popup.PopupEntity(Loc.GetString("au14-mast-key-blank"), mast, user, PopupType.SmallCaution);
                return;
            }

            if (mast.Comp.Keys.Contains(card.Faction))
            {
                _popup.PopupEntity(Loc.GetString("au14-mast-key-already", ("faction", FactionName(card.Faction))),
                    mast, user);
                return;
            }

            StartDoAfter(mast, user, used, mast.Comp.KeyDelay, new AU14MastKeyDoAfterEvent());
            _popup.PopupEntity(Loc.GetString("au14-mast-key-start"), mast, user);
            return;
        }

        if (mast.Comp.Keys.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("au14-mast-zeroize-empty"), mast, user);
            return;
        }

        StartDoAfter(mast, user, used, mast.Comp.ZeroizeDelay, new AU14MastZeroizeDoAfterEvent());
        _popup.PopupEntity(Loc.GetString("au14-mast-zeroize-start"), mast, user);
    }

    private void StartDoAfter(EntityUid mast, EntityUid user, EntityUid used, TimeSpan delay, DoAfterEvent ev)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, ev, mast, mast, used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
        });
    }

    private void OnKeyDoAfter(Entity<AU14MastKeyComponent> ent, ref AU14MastKeyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used)
            return;

        args.Handled = true;

        // the feed can have been sealed or torn down while the card was going in
        if (!IsFeedOpen(ent) ||
            !TryComp(used, out ANPRCFillCardComponent? card) ||
            string.IsNullOrEmpty(card.Faction) ||
            !ent.Comp.FactionChannels.ContainsKey(card.Faction))
        {
            return;
        }

        ent.Comp.Keys.Add(card.Faction);
        Dirty(ent);
        ApplyKeys(ent);

        _popup.PopupEntity(Loc.GetString("au14-mast-key-done", ("faction", FactionName(card.Faction))),
            ent, args.User);
    }

    private void OnZeroizeDoAfter(Entity<AU14MastKeyComponent> ent, ref AU14MastZeroizeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!IsFeedOpen(ent))
            return;

        ent.Comp.Keys.Clear();
        Dirty(ent);
        ApplyKeys(ent);

        _popup.PopupEntity(Loc.GetString("au14-mast-zeroize-done"), ent, args.User);
    }

    #endregion

    #region Keys

    private void OnKeyMapInit(Entity<AU14MastKeyComponent> ent, ref MapInitEvent args)
    {
        ApplyKeys(ent);
    }

    // the untuned and the finished mast are separate prototypes, so opening or sealing the feed swaps the
    // entity. carry the keys across before the new one initialises and applies them
    private void OnKeyEntityChanged(Entity<AU14MastKeyComponent> ent, ref ConstructionChangeEntityEvent args)
    {
        if (args.Old != ent.Owner || args.New == ent.Owner)
            return;

        if (!TryComp(args.New, out AU14MastKeyComponent? newKey))
            return;

        newKey.Keys.Clear();
        newKey.Keys.UnionWith(ent.Comp.Keys);
    }

    /// <summary>Points the mast's relay at exactly the nets of the factions keyed into it.</summary>
    private void ApplyKeys(Entity<AU14MastKeyComponent> ent)
    {
        if (!TryComp(ent, out ANPRCRelayAnchorComponent? anchor))
            return;

        anchor.Channels.Clear();

        foreach (var faction in ent.Comp.Keys)
        {
            if (!ent.Comp.FactionChannels.TryGetValue(faction, out var channels))
                continue;

            foreach (var channel in channels)
            {
                anchor.Channels.Add(channel);
            }
        }
    }

    private void OnKeyExamined(Entity<AU14MastKeyComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(AU14MastKeyComponent)))
        {
            if (ent.Comp.Keys.Count == 0)
            {
                args.PushMarkup(Loc.GetString("au14-mast-examine-unkeyed"));
            }
            else
            {
                var factions = string.Join(", ", ent.Comp.Keys.Order().Select(FactionName));
                args.PushMarkup(Loc.GetString("au14-mast-examine-keyed", ("factions", factions)));
            }

            if (IsFeedOpen(ent))
                args.PushMarkup(Loc.GetString("au14-mast-examine-feed-open"));
        }
    }

    private static string FactionName(string faction)
    {
        return faction.ToUpperInvariant();
    }

    #endregion

    private void OnAssemblyExamined(Entity<AU14MastAssemblyComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.ExamineHint)
            return;

        if (!HasMastTraining(args.Examiner, ent.Comp))
            args.PushMarkup(Loc.GetString("au14-mast-examine-untrained"));
    }

    #region Siting

    private void OnConstructionCompleted(ref RMCConstructionTransactionCompletedEvent args)
    {
        if (!TryComp(args.Built, out AU14MastSiteComponent? site))
            return;

        if (CheckSite((args.Built, site)) is not { } reason)
            return;

        var coords = Transform(args.Built).Coordinates;
        var refund = site.Refund - args.MaterialShortfall;

        if (refund > 0 && !string.IsNullOrEmpty(args.StackType))
            _stack.SpawnMultipleAtPosition(new ProtoId<StackPrototype>(args.StackType), refund, coords);

        _popup.PopupEntity(Loc.GetString(reason), args.User, args.User, PopupType.MediumCaution);
        QueueDel(args.Built);
    }

    /// <summary>The loc id of the first siting rule this footing breaks, or null when the site is good.</summary>
    private string? CheckSite(Entity<AU14MastSiteComponent> footing)
    {
        var xform = Transform(footing);

        if (xform.MapUid is { } mapUid &&
            TryComp(mapUid, out CMUZLevelMapComponent? zLevel) &&
            zLevel.Depth < 0)
        {
            return "au14-mast-site-underground";
        }

        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return "au14-mast-site-no-ground";

        var indices = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var gridEnt = new Entity<MapGridComponent>(gridUid, grid);

        // a map without areas has nothing to say about roofs, so only an area that exists can refuse
        if (_areas.TryGetArea(xform.Coordinates, out _, out _) && !_areas.IsWeatherEnabled(gridEnt, indices))
            return "au14-mast-site-roofed";

        var radius = footing.Comp.ClearRadius;

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var tile = _map.GetTileRef(gridEnt, indices + new Vector2i(x, y));

                if (tile.Tile.IsEmpty || _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                    return "au14-mast-site-cramped";
            }
        }

        _nearby.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, footing.Comp.MinSpacing, _nearby, LookupFlags.Static | LookupFlags.Approximate);

        foreach (var other in _nearby)
        {
            if (other == footing.Owner)
                continue;

            if (HasComp<AU14MastAssemblyComponent>(other) || HasComp<AU14CommsStructureVisualsComponent>(other))
                return "au14-mast-site-crowded";
        }

        return null;
    }

    #endregion

    private bool HasMastTraining(EntityUid user, AU14MastAssemblyComponent mast) =>
        _skills.HasSkill(user, mast.Skill, mast.RequiredSkillLevel) ||
        _skills.HasSkill(user, mast.AlternativeSkill, mast.AlternativeSkillLevel);
}
