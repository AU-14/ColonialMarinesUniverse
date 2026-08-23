using Content.Server.Damage.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Medical.Examine;
using Content.Shared._RMC14.Tether;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaCombistickSystem : EntitySystem
{
    private static readonly TimeSpan NonTechUntangleDelay = TimeSpan.FromSeconds(3);
    private readonly HashSet<EntityUid> _pendingContainerRecalls = new();

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private ThrownItemSystem _thrown = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaPowerSystem _power = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCombistickComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<YautjaCombistickComponent, YautjaFoldCombistickActionEvent>(OnFoldAction);
        SubscribeLocalEvent<YautjaCombistickComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<YautjaCombistickComponent, ItemToggleDeactivateAttemptEvent>(OnDeactivateAttempt);
        SubscribeLocalEvent<YautjaCombistickComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, GotEquippedHandEvent>(OnGotEquippedHand);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, ThrowItemAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, ThrowDoHitEvent>(
            OnThrowHit,
            before: new[] { typeof(DamageOtherOnHitSystem), typeof(StaminaSystem) });
        SubscribeLocalEvent<YautjaChainedWeaponComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, MeleeHitEvent>(OnChainedMeleeHit);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, ComponentShutdown>(OnChainedShutdown);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, YautjaCallCombiActionEvent>(OnCallCombi);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, YautjaChainedWeaponUntangleDoAfterEvent>(OnUntangleDoAfter);
    }

    public override void Update(float frameTime)
    {
        if (_pendingContainerRecalls.Count == 0)
            return;

        var pending = new List<EntityUid>(_pendingContainerRecalls);
        _pendingContainerRecalls.Clear();

        foreach (var weapon in pending)
        {
            if (TerminatingOrDeleted(weapon) ||
                !TryComp(weapon, out YautjaChainedWeaponComponent? chained) ||
                chained.Recalling ||
                chained.LinkedTo == null ||
                !_containers.IsEntityInContainer(weapon))
            {
                continue;
            }

            _containers.TryRemoveFromContainer(weapon, force: true);
            Recall((weapon, chained));
        }
    }

    private void OnGetItemActions(Entity<YautjaCombistickComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands || !HasComp<YautjaComponent>(args.User))
            return;

        args.AddAction(ref ent.Comp.FoldAction, ent.Comp.FoldActionId);
    }

    private void OnFoldAction(Entity<YautjaCombistickComponent> ent, ref YautjaFoldCombistickActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        _toggle.Toggle((ent.Owner, null), args.Performer, predicted: false);
    }

    private void OnActivateAttempt(Entity<YautjaCombistickComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (args.User is not { } user)
            return;

        TryCancelInvalidToggle(ent, user, ref args.Cancelled, ref args.Popup);
    }

    private void OnDeactivateAttempt(Entity<YautjaCombistickComponent> ent, ref ItemToggleDeactivateAttemptEvent args)
    {
        if (args.User is not { } user)
            return;

        TryCancelInvalidToggle(ent, user, ref args.Cancelled, ref args.Popup);
    }

    private void TryCancelInvalidToggle(Entity<YautjaCombistickComponent> ent, EntityUid user, ref bool cancelled, ref string? popup)
    {
        if (_hands.GetActiveItem(user) == ent.Owner)
            return;

        cancelled = true;
        popup = Loc.GetString("cmu-yautja-combistick-active-hand", ("item", ent.Owner));
    }

    private void OnToggled(Entity<YautjaCombistickComponent> ent, ref ItemToggledEvent args)
    {
        if (args.User is not { } user)
            return;

        if (TryComp(ent.Owner, out YautjaSourceShieldBlockComponent? sourceBlock))
        {
            sourceBlock.ReadiedBlock = args.Activated
                ? YautjaSourceShieldChance.High
                : YautjaSourceShieldChance.None;
        }

        var message = args.Activated
            ? "cmu-yautja-combistick-extend"
            : "cmu-yautja-combistick-fold";

        _popup.PopupEntity(Loc.GetString(message, ("item", ent.Owner)), ent.Owner, user);
    }

    private void OnDropped(Entity<YautjaChainedWeaponComponent> ent, ref DroppedEvent args)
    {
        if (!HasComp<YautjaComponent>(args.User))
            return;

        if (ent.Comp.RequireActive &&
            TryComp(ent.Owner, out ItemToggleComponent? toggle) &&
            !toggle.Activated)
        {
            return;
        }

        SetupChain(ent, args.User);
    }

    private void OnGotEquippedHand(Entity<YautjaChainedWeaponComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.Recalling)
            return;

        if (ent.Comp.LinkedTo != null)
            CleanupChain(ent);
    }

    private void OnInteractHand(Entity<YautjaChainedWeaponComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || CanUseChainedWeapon(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-chained-weapon-untangle-start", ("user", args.User), ("item", ent.Owner)),
            ent.Owner,
            Filter.Pvs(ent.Owner),
            true,
            PopupType.MediumCaution);

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            NonTechUntangleDelay,
            new YautjaChainedWeaponUntangleDoAfterEvent(),
            ent.Owner,
            target: ent.Owner,
            used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnPickupAttempt(Entity<YautjaChainedWeaponComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.LinkedTo is not { } linked || args.User == linked)
            return;

        CleanupChain(ent);
    }

    private void OnUntangleDoAfter(Entity<YautjaChainedWeaponComponent> ent, ref YautjaChainedWeaponUntangleDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled)
            return;

        _hands.TryPickupAnyHand(args.User, ent.Owner, checkActionBlocker: false);
    }

    private void OnThrowAttempt(Entity<YautjaChainedWeaponComponent> ent, ref ThrowItemAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.Charged)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.Charged = false;
        Dirty(ent);
    }

    private void OnThrowHit(Entity<YautjaChainedWeaponComponent> ent, ref ThrowDoHitEvent args)
    {
        if (args.Handled || !HasComp<YautjaComponent>(args.Target))
            return;

        if (!_hands.TryPickupAnyHand(args.Target, ent.Owner, animate: false))
            return;

        args.Handled = true;
        CleanupChain(ent);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-chained-weapon-catch", ("item", ent.Owner)), args.Target, args.Target);

        if (TryComp(ent.Owner, out ThrownItemComponent? currentThrown))
            _thrown.StopThrow(ent.Owner, currentThrown);
        else
            _thrown.StopThrow(ent.Owner, args.Component);
    }

    private void OnInsertedIntoContainer(Entity<YautjaChainedWeaponComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (ent.Comp.Recalling || ent.Comp.LinkedTo == null)
            return;

        _pendingContainerRecalls.Add(ent.Owner);
    }

    private void OnChainedMeleeHit(Entity<YautjaChainedWeaponComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || ent.Comp.Charged || args.Weapon != ent.Owner)
            return;

        foreach (var target in args.HitEntities)
        {
            if (target == args.User || _mobState.IsDead(target) || IsSourceSimpleAnimal(target))
                continue;

            ent.Comp.Charged = true;
            Dirty(ent);
            return;
        }
    }

    private bool IsSourceSimpleAnimal(EntityUid target)
    {
        return TryComp<RMCMedicalExamineComponent>(target, out var medical) && medical.Simple;
    }

    private void OnChainedShutdown(Entity<YautjaChainedWeaponComponent> ent, ref ComponentShutdown args)
    {
        CleanupChain(ent);
    }

    private void OnCallCombi(Entity<YautjaChainedWeaponComponent> ent, ref YautjaCallCombiActionEvent args)
    {
        if (args.Handled || ent.Comp.LinkedTo != args.Performer)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        Recall(ent);
    }

    private void SetupChain(Entity<YautjaChainedWeaponComponent> ent, EntityUid user)
    {
        CleanupChain(ent);

        ent.Comp.LinkedTo = user;
        _actions.AddAction(user, ref ent.Comp.CallCombiAction, ent.Comp.CallCombiActionId, ent.Owner);
        var tether = EnsureComp<RMCTetherComponent>(ent.Owner);
        tether.TetherOrigin = user;
        tether.StaticTetherOrigin = _transform.GetMapCoordinates(user);
        tether.VisibleToOrigin = true;
        Dirty(ent.Owner, tether);
        Dirty(ent);
    }

    private void CleanupChain(Entity<YautjaChainedWeaponComponent> ent)
    {
        if (ent.Comp.LinkedTo is { } linked && ent.Comp.CallCombiAction is { } action)
            _actions.RemoveProvidedAction(linked, ent.Owner, action);

        ent.Comp.LinkedTo = null;
        RemComp<RMCTetherComponent>(ent.Owner);
        Dirty(ent);
    }

    private bool Recall(Entity<YautjaChainedWeaponComponent> ent)
    {
        if (ent.Comp.LinkedTo is not { } user || TerminatingOrDeleted(user))
        {
            CleanupChain(ent);
            return false;
        }

        if (_containers.IsEntityInContainer(ent.Owner))
        {
            CleanupChain(ent);
            return false;
        }

        if (!_power.TryGetWornBracer(user, out var bracer))
        {
            CleanupChain(ent);
            return false;
        }

        _transform.SetCoordinates(ent.Owner, Transform(user).Coordinates);

        ent.Comp.Recalling = true;
        try
        {
            _hands.TryPickupAnyHand(user, ent.Owner, checkActionBlocker: false);
        }
        finally
        {
            ent.Comp.Recalling = false;
        }

        if (!_power.TryDrainPower(bracer, user, ent.Comp.RecallPowerCost))
            return true;

        _audio.PlayPredicted(bracer.Comp.RecallSound, ent.Owner, user);
        CleanupChain(ent);
        return true;
    }

    private bool CanUseChainedWeapon(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user) ||
               HasComp<YautjaThrallComponent>(user);
    }
}
