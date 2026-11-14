using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Tools;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.Gauss;

/// <summary>
///     Shared half of the M60 gauss gun: gates the trigger on the gun being powered on, turns a trigger pull
///     during a charge into the charged release, scales recoil by firearms skill, and lets a multitool strip
///     the ID lock. Charging itself runs on the server (<c>CMUGaussChargeSystem</c>).
/// </summary>
public sealed class CMUGaussGunSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private CMGunSystem _cmGun = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGaussGunComponent, AttemptShootEvent>(OnAttemptShoot, after: [typeof(GunIDLockSystem)]);
        SubscribeLocalEvent<CMUGaussGunComponent, GunRefreshModifiersEvent>(OnRefreshModifiers);
        SubscribeLocalEvent<CMUGaussGunComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<CMUGaussGunComponent, CMUGaussToggleActionEvent>(OnToggleAction);
        SubscribeLocalEvent<CMUGaussGunComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<CMUGaussGunComponent, ItemToggleDeactivateAttemptEvent>(OnDeactivateAttempt);
        SubscribeLocalEvent<CMUGaussGunComponent, ContainerGettingRemovedAttemptEvent>(OnRemoveAttempt);
        SubscribeLocalEvent<CMUGaussGunComponent, HeldRelayedEvent<UnwieldAttemptEvent>>(OnUnwieldAttempt);
        SubscribeLocalEvent<CMUGaussGunComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUGaussGunComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUGaussGunComponent, CMUGaussHackDoAfterEvent>(OnHack);
    }

    public bool IsCharging(CMUGaussGunComponent comp) => comp.ChargeStart != null;

    public bool IsFullyCharged(CMUGaussGunComponent comp) =>
        comp.ChargeStart is { } start && _timing.CurTime - start >= comp.ChargeTime;

    /// <summary>
    ///     How much of the gun's recoil the shooter still feels after their firearms training.
    /// </summary>
    public float GetRecoilFraction(CMUGaussGunComponent comp, EntityUid? user)
    {
        if (user == null)
            return 1f;

        var level = _skills.GetSkill(user.Value, comp.RecoilSkill);
        return Math.Max(comp.MinRecoilFraction, 1f - level * comp.RecoilReductionPerSkill);
    }

    private void OnAttemptShoot(Entity<CMUGaussGunComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_itemToggle.IsActivated(ent.Owner))
        {
            args.Cancelled = true;
            _popup.PopupEntity(Loc.GetString("cmu-gauss-not-active", ("gun", ent)), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!IsCharging(ent.Comp))
        {
            // A primary shot: remember where it's aimed so it can hit someone lying down there.
            if (_net.IsServer)
                ent.Comp.ShotAim = args.ToCoordinates is { } aim ? _transform.ToMapCoordinates(aim) : null;

            return;
        }

        // While a charge is spinning up the trigger only ever releases the charged shot.
        args.Cancelled = true;

        if (!IsFullyCharged(ent.Comp))
        {
            _popup.PopupEntity(Loc.GetString("cmu-gauss-still-charging"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (_net.IsClient || args.ToCoordinates is not { } target)
            return;

        var ev = new CMUGaussReleaseChargeEvent(args.User, target);
        RaiseLocalEvent(ent, ref ev);
    }

    private void OnRefreshModifiers(Entity<CMUGaussGunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!_cmGun.TryGetGunUser(ent, out var user))
            return;

        args.CameraRecoilScalar *= GetRecoilFraction(ent.Comp, user.Owner);
    }

    private void OnGetActions(Entity<CMUGaussGunComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands)
            return;

        args.AddAction(ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        Dirty(ent);
    }

    private void OnToggleAction(Entity<CMUGaussGunComponent> ent, ref CMUGaussToggleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _itemToggle.Toggle(ent.Owner, args.Performer);
    }

    // Once a charge is building it has to be fired: the gun can't be powered off, let go of or unwielded
    // until then.
    private void OnDeactivateAttempt(Entity<CMUGaussGunComponent> ent, ref ItemToggleDeactivateAttemptEvent args)
    {
        if (!IsCharging(ent.Comp))
            return;

        args.Cancelled = true;
        args.Popup = Loc.GetString("cmu-gauss-must-fire");
    }

    private void OnRemoveAttempt(Entity<CMUGaussGunComponent> ent, ref ContainerGettingRemovedAttemptEvent args)
    {
        if (IsCharging(ent.Comp) && !_timing.ApplyingState)
            args.Cancel();
    }

    private void OnUnwieldAttempt(Entity<CMUGaussGunComponent> ent, ref HeldRelayedEvent<UnwieldAttemptEvent> args)
    {
        if (args.Args.Wielded != ent.Owner || !IsCharging(ent.Comp))
            return;

        args.Args.Cancelled = true;
        args.Args.Message = Loc.GetString("cmu-gauss-must-fire");
    }

    private void OnToggled(Entity<CMUGaussGunComponent> ent, ref ItemToggledEvent args)
    {
        if (ent.Comp.ToggleActionEntity is { } action)
            _actions.SetToggled(action, args.Activated);
    }

    private void OnExamined(Entity<CMUGaussGunComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(CMUGaussGunComponent)))
        {
            args.PushMarkup(Loc.GetString(_itemToggle.IsActivated(ent.Owner)
                ? "cmu-gauss-examine-active"
                : "cmu-gauss-examine-inactive"));

            if (IsCharging(ent.Comp))
            {
                args.PushMarkup(Loc.GetString(IsFullyCharged(ent.Comp)
                    ? "cmu-gauss-examine-charged"
                    : "cmu-gauss-examine-charging"));
            }
        }
    }

    private void OnInteractUsing(Entity<CMUGaussGunComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<MultitoolComponent>(args.Used))
            return;

        args.Handled = true;

        if (!HasComp<GunIDLockComponent>(ent))
        {
            _popup.PopupEntity(Loc.GetString("cmu-gauss-hack-no-lock", ("gun", ent)), ent, args.User);
            return;
        }

        var level = _skills.GetSkill(args.User, ent.Comp.HackSkill);
        var delay = ent.Comp.HackTime - ent.Comp.HackReductionPerSkill * level;
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;

        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new CMUGaussHackDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            BreakOnDropItem = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-gauss-hack-start", ("gun", ent)), ent, args.User);
    }

    private void OnHack(Entity<CMUGaussGunComponent> ent, ref CMUGaussHackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!TryComp(ent, out GunIDLockComponent? idLock))
            return;

        if (idLock.Action is { } action)
            _actions.RemoveAction(action);

        RemComp<GunIDLockComponent>(ent);
        _popup.PopupEntity(Loc.GetString("cmu-gauss-hack-finish", ("gun", ent)), ent, args.User);
    }
}
