using Content.Server.Explosion.EntitySystems;
using Content.Shared._AU14.Insurgency.Sapper;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Slow;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Kitchen.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._AU14.Insurgency.Sapper;

/// <summary>
///     Runs the snare trap's non-lethal payload. When a snare goes off it binds the tripper: they are
///     rooted in place (cannot walk) and their hands are bound (cannot use items, attack, or fire), and
///     their view and sprite are flipped upside down. They break out on their own after a long struggle,
///     or a friend cuts them loose fast with a knife.
///
///     The upside-down view and sprite are done entirely client-side from the networked
///     <see cref="SapperSnaredComponent"/> (see the client SapperSnareVisualsSystem); the eye flip cannot
///     be set once server-side because the client's eye-lerping resets it every frame.
/// </summary>
public sealed class SapperSnareSystem : EntitySystem
{
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SapperSnareComponent, TriggerEvent>(OnSnareTriggered);

        SubscribeLocalEvent<SapperSnaredComponent, ComponentShutdown>(OnSnaredShutdown);
        SubscribeLocalEvent<SapperSnaredComponent, InteractUsingEvent>(OnSnaredInteractUsing);
        SubscribeLocalEvent<SapperSnaredComponent, SapperStruggleDoAfterEvent>(OnStruggleComplete);
        SubscribeLocalEvent<SapperSnaredComponent, SapperCutFreeDoAfterEvent>(OnCutFreeComplete);

        // While snared their hands are bound: block every generic interaction and every attack (which is
        // what gun fire and melee both route through), so a caught victim really is helpless.
        SubscribeLocalEvent<SapperSnaredComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<SapperSnaredComponent, AttackAttemptEvent>(OnAttackAttempt);
        // Gun fire is gated on the GUN, not the shooter, and GunComponent+AttemptShootEvent already has a
        // subscriber (ThermalCloak), so the block runs through a marker put on the victim's held guns.
        SubscribeLocalEvent<SapperSnaredGunComponent, AttemptShootEvent>(OnShootAttempt);
    }

    private void OnShootAttempt(Entity<SapperSnaredGunComponent> ent, ref AttemptShootEvent args)
    {
        if (HasComp<SapperSnaredComponent>(args.User))
            args.Cancelled = true;
    }

    private void OnSnareTriggered(EntityUid uid, SapperSnareComponent comp, TriggerEvent args)
    {
        // The step gate already spared friendlies, but re-check in case something else set it off.
        if (args.User is not { } tripper || HasComp<CLFMemberComponent>(tripper))
            return;

        if (HasComp<SapperSnaredComponent>(tripper))
            return;

        var snared = EnsureComp<SapperSnaredComponent>(tripper);
        snared.StruggleTime = comp.StruggleTime;
        snared.CutFreeTime = comp.CutFreeTime;
        snared.FlipAngle = comp.FlipAngle;
        Dirty(tripper, snared);

        // Root them for the whole struggle so they cannot move. Refresh the speed modifiers a second time
        // right after: the root's own refresh can land a hair before the component reports Running, so we
        // make sure the zero-speed modifier is actually in effect.
        _slow.TryRoot(tripper, comp.StruggleTime, true);
        _speed.RefreshMovementSpeedModifiers(tripper);

        // Mark every gun they're holding so its shots are blocked while they're snared (they can't pick up
        // anything new: the interaction block covers that).
        foreach (var held in _hands.EnumerateHeld(tripper))
        {
            if (HasComp<GunComponent>(held))
                EnsureComp<SapperSnaredGunComponent>(held);
        }

        _popup.PopupEntity(Loc.GetString("insfor-sapper-snare-caught"), tripper, tripper, PopupType.LargeCaution);

        // Begin the self-struggle. It cannot be moved or attacked out of (they are rooted and bound) and
        // does not break on damage, so the only ways out are finishing it or being cut free. RequireCanInteract
        // is off so the hand-binding above does not instantly cancel their own struggle.
        var doAfter = new DoAfterArgs(EntityManager, tripper, comp.StruggleTime, new SapperStruggleDoAfterEvent(), tripper)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = false,
            RequireCanInteract = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnSnaredShutdown(Entity<SapperSnaredComponent> ent, ref ComponentShutdown args)
    {
        // Free their movement immediately (in case they were cut loose early, before the root expired).
        RemComp<RMCRootedComponent>(ent);
        _speed.RefreshMovementSpeedModifiers(ent);

        // Unmark whatever guns they still hold (guns dropped while snared keep a harmless stale marker).
        foreach (var held in _hands.EnumerateHeld(ent.Owner))
            RemComp<SapperSnaredGunComponent>(held);
    }

    private void OnSnaredInteractUsing(Entity<SapperSnaredComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Only a friend with a knife can cut someone loose, and not the victim themselves.
        if (args.User == ent.Owner || !HasComp<SharpComponent>(args.Used))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.CutFreeTime, new SapperCutFreeDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("insfor-sapper-snare-cutting"), ent, args.User);
    }

    private void OnStruggleComplete(Entity<SapperSnaredComponent> ent, ref SapperStruggleDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        _popup.PopupEntity(Loc.GetString("insfor-sapper-snare-struggled-free"), ent, ent);
        RemComp<SapperSnaredComponent>(ent);
    }

    private void OnCutFreeComplete(Entity<SapperSnaredComponent> ent, ref SapperCutFreeDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        _popup.PopupEntity(Loc.GetString("insfor-sapper-snare-cut-free"), ent, ent);
        RemComp<SapperSnaredComponent>(ent);
    }

    // ----- hand-binding: nothing the victim tries with their hands gets through while snared ----------

    private void OnInteractionAttempt(Entity<SapperSnaredComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnAttackAttempt(Entity<SapperSnaredComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }
}
