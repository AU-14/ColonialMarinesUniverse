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
    [Dependency] private PopupSystem _popup = default!;

    private static readonly EntProtoId StampPrototype = "CMUCrayonStamp";

    public override void Initialize()
    {
        SubscribeLocalEvent<CrayonComponent, CMUCrayonSelectSymbolMessage>(OnSelectSymbol);
        SubscribeLocalEvent<CMUCrayonSymbolComponent, CrayonSelectMessage>(OnSelectStock);
        SubscribeLocalEvent<CMUCrayonSymbolComponent, AfterInteractEvent>(OnAfterInteract,
            after: [typeof(IngestionSystem)], before: [typeof(CrayonSystem)]);
    }

    private void OnSelectSymbol(Entity<CrayonComponent> ent, ref CMUCrayonSelectSymbolMessage args)
    {
        TrySelect(ent, args.Actor, args.Symbol);
    }

    private void OnSelectStock(Entity<CMUCrayonSymbolComponent> ent, ref CrayonSelectMessage args)
    {
        ent.Comp.Symbol = null;
        Dirty(ent);
    }

    public bool TrySelect(EntityUid crayon, EntityUid user, CMUCrayonSymbol symbol)
    {
        if (!HasComp<CrayonComponent>(crayon) || !symbol.IsValid())
            return false;

        var comp = EnsureComp<CMUCrayonSymbolComponent>(crayon);
        comp.Symbol = symbol;
        Dirty(crayon, comp);
        return true;
    }

    private void OnAfterInteract(Entity<CMUCrayonSymbolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || ent.Comp.Symbol is not { } symbol)
            return;

        args.Handled = true;
        if (!args.ClickLocation.IsValid(EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("crayon-interact-invalid-location"), ent, args.User);
            return;
        }

        if (!TrySpend(args.User, ent, symbol))
            return;

        var stamp = Spawn(StampPrototype, args.ClickLocation);
        var comp = EnsureComp<CMUCrayonStampComponent>(stamp);
        comp.Symbol = symbol;
        Dirty(stamp, comp);

        if (TryComp(ent, out CrayonComponent? crayon))
            _audio.PlayPvs(crayon.UseSound, ent);

        _adminLog.Add(LogType.CrayonDraw, LogImpact.Low,
            $"{ToPrettyString(args.User):user} drew a custom symbol at {args.ClickLocation}");
    }

    /// <summary>
    /// Spends a charge of a matching crayon for every colour the symbol uses, or nothing if one is missing.
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
    /// A crayon for each colour the symbol uses, once per charge it gives: the held one first, then
    /// those carried, then those within reach. A crayon of selectable colour gives any.
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

        foreach (var color in symbol.UsedColors())
        {
            if (!TryPick(candidates, left, c => !c.SelectableColor && c.Color == color, out var pick) &&
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
