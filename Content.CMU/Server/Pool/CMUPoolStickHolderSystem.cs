using Content.Shared.CMU14.Pool;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Pool;

public sealed partial class CMUPoolStickHolderSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPoolStickHolderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUPoolStickHolderComponent, InteractHandEvent>(OnTakeStick);
        SubscribeLocalEvent<CMUPoolStickHolderComponent, InteractUsingEvent>(OnReturnStick);
        SubscribeLocalEvent<CMUPoolStickHolderComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<CMUPoolStickHolderComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CMUPoolStickHolderComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnMapInit(Entity<CMUPoolStickHolderComponent> holder, ref MapInitEvent args)
    {
        if (_containers.TryGetContainer(holder.Owner, CMUPoolStickHolderComponent.ContainerId, out var sticks))
            UpdateAppearance(holder.Owner, sticks);
    }

    private void OnTakeStick(Entity<CMUPoolStickHolderComponent> holder, ref InteractHandEvent args)
    {
        if (args.Handled || !_containers.TryGetContainer(holder.Owner, CMUPoolStickHolderComponent.ContainerId, out var sticks))
            return;

        args.Handled = true;
        if (sticks.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-pool-stick-holder-empty"), holder.Owner, args.User);
            return;
        }

        _hands.TryPickup(args.User, sticks.ContainedEntities[0], animate: false);
    }

    private void OnReturnStick(Entity<CMUPoolStickHolderComponent> holder, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CMUPoolStickComponent>(args.Used)
            || !_containers.TryGetContainer(holder.Owner, CMUPoolStickHolderComponent.ContainerId, out var sticks))
            return;

        args.Handled = true;
        if (sticks.ContainedEntities.Count >= CMUPoolStickHolderComponent.Capacity)
        {
            _popup.PopupEntity(Loc.GetString("cmu-pool-stick-holder-full"), holder.Owner, args.User);
            return;
        }

        _hands.TryDropIntoContainer(args.User, args.Used, sticks);
    }

    private void OnInsertAttempt(Entity<CMUPoolStickHolderComponent> holder, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == CMUPoolStickHolderComponent.ContainerId
            && (!HasComp<CMUPoolStickComponent>(args.EntityUid)
                || args.Container.ContainedEntities.Count >= CMUPoolStickHolderComponent.Capacity))
            args.Cancel();
    }

    private void OnInserted(Entity<CMUPoolStickHolderComponent> holder, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == CMUPoolStickHolderComponent.ContainerId)
            UpdateAppearance(holder.Owner, args.Container);
    }

    private void OnRemoved(Entity<CMUPoolStickHolderComponent> holder, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == CMUPoolStickHolderComponent.ContainerId && !TerminatingOrDeleted(holder.Owner))
            UpdateAppearance(holder.Owner, args.Container);
    }

    private void UpdateAppearance(EntityUid holder, BaseContainer container)
    {
        var count = Math.Min(container.ContainedEntities.Count, CMUPoolStickHolderComponent.Capacity);
        _appearance.SetData(holder, CMUPoolStickHolderVisuals.Fill, (CMUPoolStickHolderFill) count);
    }
}
