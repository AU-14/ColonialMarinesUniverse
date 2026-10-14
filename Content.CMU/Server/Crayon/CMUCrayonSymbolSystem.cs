using Content.Server.Administration.Logs;
using Content.Server.Crayon;
using Content.Server.Popups;
using Content.Shared.Charges.Systems;
using Content.Shared.CMU14.Crayon;
using Content.Shared.Crayon;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Crayon;

public sealed class CMUCrayonSymbolSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private CMUCrayonPaletteSystem _palette = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private CMUCrayonStencilSystem _stencil = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly EntProtoId StampPrototype = "CMUCrayonStamp";

    public override void Initialize()
    {
        SubscribeLocalEvent<CrayonComponent, CMUCrayonSelectSymbolMessage>(OnSelectSymbol);
        SubscribeLocalEvent<CMUCrayonSymbolComponent, CrayonSelectMessage>(OnSelectStock);
        SubscribeLocalEvent<CMUCrayonSymbolComponent, AfterInteractEvent>(OnAfterInteract,
            after: [typeof(IngestionSystem)], before: [typeof(CrayonSystem)]);
        SubscribeLocalEvent<CMUCrayonSymbolComponent, CMUCrayonDrawImageEvent>(OnDrawImage);
    }

    private void OnSelectSymbol(Entity<CrayonComponent> ent, ref CMUCrayonSelectSymbolMessage args)
    {
        if (args.Symbol is { } symbol)
            TrySelect(ent, args.Actor, symbol);
    }

    // Every stock pick lands here, from a button, a search or the comma queue moving on.
    private void OnSelectStock(Entity<CMUCrayonSymbolComponent> ent, ref CrayonSelectMessage args)
    {
        _stencil.ClearStencil(args.Actor);
    }

    /// <summary>
    /// Picks a custom symbol in a crayon's window: the user stamps it with every crayon until they pick a stock decal.
    /// </summary>
    public bool TrySelect(EntityUid crayon, EntityUid user, CMUCrayonSymbol symbol)
    {
        return HasComp<CrayonComponent>(crayon) && _stencil.TrySetStencil(user, symbol);
    }

    private void OnAfterInteract(Entity<CMUCrayonSymbolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_stencil.TryGetStencil(args.User, out var symbol))
            return;

        args.Handled = true;
        if (!TryGetFloor(args.ClickLocation, out var floor))
        {
            _popup.PopupEntity(Loc.GetString("crayon-interact-invalid-location"), ent, args.User);
            return;
        }

        if (!TrySpend(args.User, ent, symbol))
            return;

        var stamp = Spawn(StampPrototype, floor);
        var comp = EnsureComp<CMUCrayonStampComponent>(stamp);
        comp.Symbol = symbol;
        Dirty(stamp, comp);

        if (TryComp(ent, out CrayonComponent? crayon))
            _audio.PlayPvs(crayon.UseSound, ent);

        _adminLog.Add(LogType.CrayonDraw, LogImpact.Low,
            $"{ToPrettyString(args.User):user} drew a custom symbol at {args.ClickLocation}");
    }

    /// <summary>
    /// The click on a grid's floor tile, in that grid's coordinates.
    /// </summary>
    private bool TryGetFloor(EntityCoordinates click, out EntityCoordinates floor)
    {
        floor = default;
        if (!click.IsValid(EntityManager))
            return false;

        var map = _transform.ToMapCoordinates(click);
        if (!_map.TryFindGridAt(map, out var gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, map.Position, out var tile) ||
            tile.Tile.IsEmpty)
        {
            return false;
        }

        floor = _transform.ToCoordinates(gridUid, map);
        return true;
    }

    // Drawn on something other than the floor, the stencil goes on instead of the decal.
    private void OnDrawImage(Entity<CMUCrayonSymbolComponent> ent, ref CMUCrayonDrawImageEvent args)
    {
        if (args.Cancelled || args.Pixels != null || !_stencil.TryGetStencil(args.User, out var symbol))
            return;

        if (!TrySpend(args.User, ent, symbol))
        {
            args.Cancelled = true;
            return;
        }

        args.Size = symbol.Size;
        args.Pixels = (byte[]) symbol.Pixels.Clone();
        args.Palette = new List<Color>(symbol.Palette);
    }

    /// <summary>
    /// Spends a charge of a matching crayon for every crayon colour the symbol uses, whatever its tones,
    /// or nothing if one is missing.
    /// </summary>
    public bool TrySpend(EntityUid user, EntityUid held, CMUCrayonSymbol symbol)
    {
        if (!TryGatherCrayons(user, held, symbol, out var crayons))
        {
            _popup.PopupEntity(Loc.GetString("cmu-crayon-symbol-missing-colors"), user, user);
            return false;
        }

        foreach (var crayon in crayons)
        {
            _charges.TryUseCharge(crayon);
            if (TryComp(crayon, out CrayonComponent? comp) && comp.DeleteEmpty && _charges.IsEmpty(crayon))
            {
                _popup.PopupEntity(Loc.GetString("crayon-interact-used-up-text", ("owner", crayon)), user, user);
                QueueDel(crayon);
            }
        }

        return true;
    }

    /// <summary>
    /// A crayon for each box crayon colour the symbol uses in any of its tones, and for each other
    /// colour, once per charge it gives: the held one first, then those carried, then those within
    /// reach. A crayon draws as the box crayon nearest its colour; one of selectable colour gives any.
    /// </summary>
    public bool TryGatherCrayons(EntityUid user, EntityUid held, CMUCrayonSymbol symbol, out List<EntityUid> crayons)
    {
        crayons = new List<EntityUid>();
        var left = new Dictionary<EntityUid, int>();
        var candidates = new List<(EntityUid Uid, CrayonComponent Crayon)>();
        foreach (var uid in Candidates(user, held))
        {
            if (left.ContainsKey(uid) || !TryComp(uid, out CrayonComponent? crayon))
                continue;

            left[uid] = _charges.GetCurrentCharges(uid);
            candidates.Add((uid, crayon));
        }

        var families = new HashSet<int>();
        var others = new List<Color>();
        foreach (var color in symbol.UsedColors())
        {
            if (_palette.TryGetFamily(color, out var family, out _))
                families.Add(family);
            else
                others.Add(color);
        }

        foreach (var family in families)
        {
            if (!TryPick(candidates, left, c => !c.SelectableColor && _palette.NearestFamily(c.Color) == family, out var pick) &&
                !TryPick(candidates, left, c => c.SelectableColor, out pick))
            {
                return false;
            }

            crayons.Add(pick);
        }

        foreach (var color in others)
        {
            if (!TryPick(candidates, left, c => !c.SelectableColor && CMUCrayonPaletteSystem.Same(c.Color, color), out var pick) &&
                !TryPick(candidates, left, c => c.SelectableColor, out pick))
            {
                return false;
            }

            crayons.Add(pick);
        }

        return true;
    }

    private static bool TryPick(
        List<(EntityUid Uid, CrayonComponent Crayon)> candidates,
        Dictionary<EntityUid, int> left,
        Func<CrayonComponent, bool> match,
        out EntityUid pick)
    {
        foreach (var (uid, crayon) in candidates)
        {
            if (left[uid] <= 0 || !match(crayon))
                continue;

            left[uid]--;
            pick = uid;
            return true;
        }

        pick = default;
        return false;
    }

    private IEnumerable<EntityUid> Candidates(EntityUid user, EntityUid held)
    {
        yield return held;

        foreach (var item in _inventory.GetHandOrInventoryEntities(user))
        {
            yield return item;
            foreach (var inner in Contained(item))
            {
                yield return inner;
            }
        }

        foreach (var nearby in _lookup.GetEntitiesInRange<CrayonComponent>(Transform(user).Coordinates,
                     SharedInteractionSystem.InteractionRange, LookupFlags.Uncontained))
        {
            if (_interaction.InRangeUnobstructed(user, nearby.Owner))
                yield return nearby;
        }
    }

    private IEnumerable<EntityUid> Contained(EntityUid uid)
    {
        if (!TryComp(uid, out ContainerManagerComponent? manager))
            yield break;

        foreach (var container in _container.GetAllContainers(uid, manager))
        {
            foreach (var inner in container.ContainedEntities)
            {
                yield return inner;
                foreach (var deeper in Contained(inner))
                {
                    yield return deeper;
                }
            }
        }
    }
}
