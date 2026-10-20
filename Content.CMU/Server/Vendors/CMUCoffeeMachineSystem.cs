using Content.Server.Popups;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Vendors;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Robust.Server.Audio;

namespace Content.Server.CMU14.Vendors;

/// <summary>
///     Brews coffee into whatever drink container is used on a <see cref="CMUCoffeeMachineComponent"/>.
/// </summary>
public sealed class CMUCoffeeMachineSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCoffeeMachineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUCoffeeMachineComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CMUCoffeeMachineComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUCoffeeMachineComponent, CMUCoffeeBrewDoAfterEvent>(OnBrewed);
    }

    private void OnInteractHand(Entity<CMUCoffeeMachineComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-hint"), ent, args.User);
    }

    private void OnExamined(Entity<CMUCoffeeMachineComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-coffee-machine-examine"));
    }

    private void OnInteractUsing(Entity<CMUCoffeeMachineComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_solutions.TryGetFitsInDispenser(args.Used, out _, out var solution))
            return;

        args.Handled = true;
        if (solution.AvailableVolume <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-full", ("container", args.Used)), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.BrewTime, new CMUCoffeeBrewDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _audio.PlayPvs(ent.Comp.BrewSound, ent);
        _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-brewing"), ent, args.User);
    }

    private void OnBrewed(Entity<CMUCoffeeMachineComponent> ent, ref CMUCoffeeBrewDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } container)
            return;

        args.Handled = true;
        if (!_solutions.TryGetFitsInDispenser(container, out var solution, out var contents))
            return;

        var amount = FixedPoint2.Min(ent.Comp.Amount, contents.AvailableVolume);
        if (amount <= 0 || !_solutions.TryAddReagent(solution.Value, ent.Comp.Reagent.Id, amount, out _))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-done", ("container", container)), ent, args.User);
    }
}
