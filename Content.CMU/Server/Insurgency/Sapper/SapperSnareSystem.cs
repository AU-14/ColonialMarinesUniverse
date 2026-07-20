using Content.Shared._AU14.Insurgency.Sapper;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Slow;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;

namespace Content.Server._AU14.Insurgency.Sapper;

/// <summary>
///     Runs the snare trap's non-lethal payload. When a snare goes off it binds the tripper: they are
///     rooted in place (cannot walk) and cuffed (dropping whatever they held and unable to use items,
///     attack, or fire), and their view and sprite are flipped upside down. They break out on their own
///     after a long struggle, or a friend cuts them loose fast with a knife.
///
///     Hand-binding is done with real cuffs (see <see cref="SharedCuffableSystem"/>) rather than by
///     server-only interaction/attack blocks: the cuffable state is networked and shared, so the client
///     predicts the victim's helplessness instead of mispredicting and rubber-banding. The root is also
///     driven by the networked <see cref="RMCRootedComponent"/> (predicted the same way).
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
    [Dependency] private SharedCuffableSystem _cuffable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SapperSnareComponent, TriggerEvent>(OnSnareTriggered);

        SubscribeLocalEvent<SapperSnaredComponent, ComponentShutdown>(OnSnaredShutdown);
        SubscribeLocalEvent<SapperSnaredComponent, InteractUsingEvent>(OnSnaredInteractUsing);
        SubscribeLocalEvent<SapperSnaredComponent, SapperStruggleDoAfterEvent>(OnStruggleComplete);
        SubscribeLocalEvent<SapperSnaredComponent, SapperCutFreeDoAfterEvent>(OnCutFreeComplete);

        // The cuffs cannot be wormed out of while snared: block any uncuff attempt (self-breakout or a
        // helper's) so the only ways out stay the struggle timer or a knife cut-free.
        SubscribeLocalEvent<SapperSnaredComponent, UncuffAttemptEvent>(OnUncuffAttempt);
    }

    private void OnUncuffAttempt(Entity<SapperSnaredComponent> ent, ref UncuffAttemptEvent args)
    {
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

        // Cuff them: this drops what they held and blocks item use, attacks, and gun fire in a predicted,
        // shared way. Track the cuffs so they come off again when the snare ends.
        snared.Cuffs = _cuffable.TryAddCuffsInstant(tripper, comp.CuffPrototype);
        Dirty(tripper, snared);

        // Root them for the whole struggle so they cannot move. Refresh the speed modifiers a second time
        // right after: the root's own refresh can land a hair before the component reports Running, so we
        // make sure the zero-speed modifier is actually in effect.
        _slow.TryRoot(tripper, comp.StruggleTime, true);
        _speed.RefreshMovementSpeedModifiers(tripper);

        _popup.PopupEntity(Loc.GetString("insfor-sapper-snare-caught"), tripper, tripper, PopupType.LargeCaution);

        // Begin the self-struggle. It cannot be moved or attacked out of (they are rooted and cuffed) and
        // does NOT break on damage, so being shot while snared does not free the victim: the only ways out
        // are finishing this struggle or being cut free. RequireCanInteract is off so the cuffs above do not
        // instantly cancel their own struggle.
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

        // Take the cuffs off by deleting them - the snare's cuffs are conjured by the trap, so they vanish
        // with it instead of leaving a free pair on the floor. Removal from the container cleans up the
        // hand-blocking virtual items and refreshes the cuffed state.
        if (ent.Comp.Cuffs is { } cuffs && !TerminatingOrDeleted(cuffs))
            QueueDel(cuffs);
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
}
