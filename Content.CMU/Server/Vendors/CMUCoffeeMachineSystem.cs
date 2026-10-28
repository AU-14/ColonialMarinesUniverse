using Content.Server.Popups;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Vendors;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Vendors;

/// <summary>
///     Takes a cup or mug into a <see cref="CMUCoffeeMachineComponent"/> and brews coffee into it, until someone takes it back out.
/// </summary>
public sealed class CMUCoffeeMachineSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCoffeeMachineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUCoffeeMachineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUCoffeeMachineComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CMUCoffeeMachineComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUCoffeeMachineComponent, EntRemovedFromContainerMessage>(OnCupRemoved);
    }

    private void OnMapInit(Entity<CMUCoffeeMachineComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
        UpdateAppearance(ent);
    }

    private void OnInteractHand(Entity<CMUCoffeeMachineComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.BrewEnd != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-still-brewing"), ent, args.User);
            return;
        }

        if (GetCup(ent) is not { } cup)
        {
            _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-hint"), ent, args.User);
            return;
        }

        _hands.PickupOrDrop(args.User, cup);
        _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-take", ("container", cup)), ent, args.User);
    }

    private void OnExamined(Entity<CMUCoffeeMachineComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-coffee-machine-examine"));
        if (GetCup(ent) is { } cup)
        {
            args.PushMarkup(Loc.GetString(ent.Comp.BrewEnd != null ? "cmu-coffee-machine-examine-brewing" : "cmu-coffee-machine-examine-ready",
                ("container", cup)));
        }
    }

    private void OnInteractUsing(Entity<CMUCoffeeMachineComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_solutions.TryGetFitsInDispenser(args.Used, out _, out var solution))
            return;

        args.Handled = true;
        if (GetCup(ent) is { } existing)
        {
            _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-occupied", ("container", existing)), ent, args.User);
            return;
        }

        if (solution.AvailableVolume <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-full", ("container", args.Used)), ent, args.User);
            return;
        }

        var slot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
        if (!_container.Insert(args.Used, slot))
            return;

        // The cup is ready as the brewing sound finishes.
        var brewTime = ent.Comp.BrewTime;
        if (ent.Comp.BrewSound is { } sound)
        {
            var resolved = _audio.ResolveSound(sound);
            brewTime = _audio.GetAudioLength(resolved);
            _audio.PlayPvs(resolved, ent, sound.Params);
        }

        ent.Comp.BrewEnd = _timing.CurTime + brewTime;
        ent.Comp.Brewer = args.User;
        UpdateAppearance(ent);
        _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-brewing", ("container", args.Used)), ent, args.User);
    }

    private void OnCupRemoved(Entity<CMUCoffeeMachineComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        ent.Comp.BrewEnd = null;
        ent.Comp.Brewer = null;
        UpdateAppearance(ent);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUCoffeeMachineComponent>();
        while (query.MoveNext(out var uid, out var machine))
        {
            if (machine.BrewEnd is not { } end || now < end)
                continue;

            Brew((uid, machine));
        }
    }

    private void Brew(Entity<CMUCoffeeMachineComponent> ent)
    {
        var brewer = ent.Comp.Brewer;
        ent.Comp.BrewEnd = null;
        ent.Comp.Brewer = null;

        if (GetCup(ent) is not { } cup)
            return;

        if (_solutions.TryGetFitsInDispenser(cup, out var solution, out var contents))
        {
            var amount = FixedPoint2.Min(ent.Comp.Amount, contents.AvailableVolume);
            if (amount > 0)
                _solutions.TryAddReagent(solution.Value, ent.Comp.Reagent.Id, amount, out _);
        }

        // The cup waits in the machine until someone takes it out.
        UpdateAppearance(ent);
        if (brewer is { } user && !TerminatingOrDeleted(user))
            _popup.PopupEntity(Loc.GetString("cmu-coffee-machine-done", ("container", cup)), ent, user);
    }

    private EntityUid? GetCup(Entity<CMUCoffeeMachineComponent> ent)
    {
        return _container.TryGetContainer(ent, ent.Comp.ContainerId, out var slot) && slot.ContainedEntities.Count > 0
            ? slot.ContainedEntities[0]
            : null;
    }

    private void UpdateAppearance(Entity<CMUCoffeeMachineComponent> ent)
    {
        var state = ent.Comp.EmptyState;
        if (GetCup(ent) is { } cup)
        {
            state = MetaData(cup).EntityPrototype is { } proto && ent.Comp.CupStates.TryGetValue(proto.ID, out var cupState)
                ? cupState
                : ent.Comp.GenericCupState;
        }

        _appearance.SetData(ent, CMUCoffeeMachineVisuals.State, state);
    }
}
