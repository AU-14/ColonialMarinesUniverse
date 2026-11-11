using System.Linq;
using Content.Server.Popups;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Kitchen;
using Content.Shared.CMU14.Vendors;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Vendors;

/// <summary>
///     Runs <see cref="CMUAutoDrinkComponent"/>: pours drinks into the container in the bay, drops a disposable cup
///     into an empty bay, and hands the container back.
/// </summary>
public sealed class CMUAutoDrinkSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private CMUFoodlineSystem _foodline = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUAutoDrinkComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CMUAutoDrinkComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CMUAutoDrinkComponent, EntInsertedIntoContainerMessage>((uid, comp, _) => UpdateUi((uid, comp)));
        SubscribeLocalEvent<CMUAutoDrinkComponent, EntRemovedFromContainerMessage>((uid, comp, _) => UpdateUi((uid, comp)));
        SubscribeLocalEvent<CMUAutoDrinkComponent, BoundUIOpenedEvent>((uid, comp, _) => UpdateUi((uid, comp)));

        Subs.BuiEvents<CMUAutoDrinkComponent>(CMUAutoDrinkUiKey.Key, subs =>
        {
            subs.Event<CMUAutoDrinkPourBuiMsg>(OnPour);
            subs.Event<CMUAutoDrinkCupBuiMsg>(OnCup);
            subs.Event<CMUAutoDrinkEjectBuiMsg>(OnEject);
        });
    }

    private void OnInit(Entity<CMUAutoDrinkComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent.Owner, CMUAutoDrinkComponent.BaySlotId, ent.Comp.Bay);
    }

    private void OnRemove(Entity<CMUAutoDrinkComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.Bay);
    }

    private void OnPour(Entity<CMUAutoDrinkComponent> ent, ref CMUAutoDrinkPourBuiMsg args)
    {
        if (_foodline.IsClosedForFoodline(ent, args.Actor))
            return;

        var wanted = args.Drink;
        var drink = ent.Comp.Drinks.FirstOrDefault(d => d.Reagent == wanted);
        if (drink == null || !ent.Comp.PourAmounts.Contains(args.Amount))
            return;

        if (ent.Comp.Bay.Item is not { } container ||
            !_solutions.TryGetFitsInDispenser(container, out var solution, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-autodrink-no-container"), ent, args.Actor);
            return;
        }

        if (!_solutions.TryAddReagent(solution.Value, drink.Reagent.Id, args.Amount, out var poured) || poured <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-autodrink-full"), ent, args.Actor);
            return;
        }

        _audio.PlayPvs(ent.Comp.PourSound, ent);
        UpdateUi(ent);
    }

    private void OnCup(Entity<CMUAutoDrinkComponent> ent, ref CMUAutoDrinkCupBuiMsg args)
    {
        if (_foodline.IsClosedForFoodline(ent, args.Actor))
            return;

        if (ent.Comp.Bay.HasItem)
        {
            _popup.PopupEntity(Loc.GetString("cmu-autodrink-bay-occupied"), ent, args.Actor);
            return;
        }

        var now = _timing.CurTime;
        if (now < ent.Comp.NextCup)
            return;

        ent.Comp.NextCup = now + ent.Comp.CupCooldown;
        var cup = Spawn(ent.Comp.Cup, Transform(ent).Coordinates);
        if (!_itemSlots.TryInsert(ent.Owner, ent.Comp.Bay, cup, null))
        {
            Del(cup);
            return;
        }

        _audio.PlayPvs(ent.Comp.CupSound, ent);
        UpdateUi(ent);
    }

    private void OnEject(Entity<CMUAutoDrinkComponent> ent, ref CMUAutoDrinkEjectBuiMsg args)
    {
        _itemSlots.TryEjectToHands(ent.Owner, ent.Comp.Bay, args.Actor, true);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<CMUAutoDrinkComponent> ent)
    {
        string? name = null;
        FixedPoint2 volume = 0;
        FixedPoint2 max = 0;
        var contents = new List<CMUAutoDrinkContent>();

        if (ent.Comp.Bay.Item is { } container)
        {
            name = Name(container);
            if (_solutions.TryGetFitsInDispenser(container, out _, out var solution))
            {
                volume = solution.Volume;
                max = solution.MaxVolume;
                foreach (var reagent in solution.Contents.OrderByDescending(r => r.Quantity))
                {
                    if (!_reagents.TryIndex(reagent.Reagent, out var proto))
                        continue;

                    // Show what the menu calls it: the lemon-lime drink's reagent is named after a soda brand.
                    var menu = ent.Comp.Drinks.FirstOrDefault(d => d.Reagent.Id == reagent.Reagent.Prototype);
                    var label = menu?.Name is { } loc ? Loc.GetString(loc) : proto.LocalizedName;
                    contents.Add(new CMUAutoDrinkContent(label, reagent.Quantity, proto.SubstanceColor));
                }
            }
        }

        _ui.SetUiState(ent.Owner, CMUAutoDrinkUiKey.Key, new CMUAutoDrinkBuiState(name, volume, max, contents));
    }

    public override void Update(float frameTime)
    {
        // Contents can change while the screen is open (someone drinks from the cup in the bay), so keep it current.
        var query = EntityQueryEnumerator<CMUAutoDrinkComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Bay.Item != null && _ui.IsUiOpen(uid, CMUAutoDrinkUiKey.Key) && _timing.CurTick.Value % 30 == 0)
                UpdateUi((uid, comp));
        }
    }
}
