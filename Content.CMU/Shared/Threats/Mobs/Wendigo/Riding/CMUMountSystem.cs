using System.Numerics;
using Content.Shared._RMC14.Buckle;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Riding;

/// <summary>
///     Consent-based shoulder riding. Riders are buckled to the mount's strap at a per-seat offset, can keep using
///     their hands and weapons, and cannot hurt the mount. Explosions, crit/death and losing a leg throw them off.
/// </summary>
public sealed partial class CMUMountSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCBuckleSystem _rmcBuckle = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan DefaultDismountKnockdown = TimeSpan.FromSeconds(1.5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUMountComponent, StrapAttemptEvent>(OnMountStrapAttempt);
        SubscribeLocalEvent<CMUMountComponent, UnstrappedEvent>(OnMountUnstrapped);
        SubscribeLocalEvent<CMUMountComponent, MoveEvent>(OnMountMove);
        SubscribeLocalEvent<CMUMountComponent, PreventCollideEvent>(OnMountPreventCollide);
        SubscribeLocalEvent<CMUMountComponent, AttackAttemptEvent>(OnMountAttackAttempt);
        SubscribeLocalEvent<CMUMountComponent, MobStateChangedEvent>(OnMountMobStateChanged);
        SubscribeLocalEvent<CMUMountComponent, ExplosionReceivedEvent>(OnMountExplosionReceived,
            before: [typeof(SharedRMCExplosionSystem)]);

        SubscribeLocalEvent<CMURiderComponent, AttackAttemptEvent>(OnRiderAttackAttempt);
        SubscribeLocalEvent<CMURiderComponent, MobStateChangedEvent>(OnRiderMobStateChanged);
        // Before the explosion stun so the rider is already off and can actually be knocked down and back.
        SubscribeLocalEvent<CMURiderComponent, ExplosionReceivedEvent>(OnRiderExplosionReceived,
            before: [typeof(SharedRMCExplosionSystem)]);
    }

    #region Public API

    /// <summary>Whether <paramref name="rider"/> could climb onto <paramref name="mount"/> right now.</summary>
    public bool CanMount(Entity<CMUMountComponent?> mount, EntityUid rider, bool popup = false)
    {
        if (!Resolve(mount, ref mount.Comp, false) || rider == mount.Owner)
            return false;

        string? reason = null;
        if (HasComp<XenoComponent>(rider) ||
            !TryComp(rider, out HandsComponent? hands) || hands.Count < 1 ||
            !HasComp<BuckleComponent>(rider))
        {
            reason = "cmu-mount-fail-cant";
        }
        else if (CountLegs(rider) < 2)
        {
            reason = "cmu-mount-fail-legs";
        }
        else if (HasComp<CMURiderComponent>(rider) || _buckle.IsBuckled(rider))
        {
            reason = "cmu-mount-fail-already";
        }
        else if (!TryGetFreeSeat(mount.Comp, out _))
        {
            reason = "cmu-mount-fail-full";
        }
        else if (!_mobState.IsAlive(rider) || !_mobState.IsAlive(mount.Owner))
        {
            reason = "cmu-mount-fail-cant";
        }
        else if (!_interaction.InRangeUnobstructed(rider, mount.Owner, mount.Comp.Range))
        {
            reason = "cmu-mount-fail-range";
        }

        if (reason == null)
            return true;

        if (popup)
            _popup.PopupEntity(Loc.GetString(reason, ("mount", mount.Owner)), mount.Owner, rider, PopupType.SmallCaution);

        return false;
    }

    /// <summary>
    ///     The consented path: re-checks eligibility, sets the rider's seat offset, then buckles them on.
    ///     Called when the mount accepts; exposed for tests and admin tools.
    /// </summary>
    public bool TryMount(EntityUid mount, EntityUid rider, bool popup = true)
    {
        if (!TryComp(mount, out CMUMountComponent? comp) ||
            !CanMount((mount, comp), rider, popup) ||
            !TryGetFreeSeat(comp, out var seat))
        {
            return false;
        }

        Vector2? previous = HasComp<RMCBuckleOffsetComponent>(rider) ? _rmcBuckle.GetOffset(rider) : null;

        // The buckle system places the rider at strap offset + rider offset and unbuckles on drift: set it first.
        _rmcBuckle.CMUSetOffset(rider, GetLocalSeatOffset((mount, comp), seat));

        bool buckled;
        comp.ConsentGrant = rider;
        try
        {
            buckled = _buckle.TryBuckle(rider, rider, mount, popup: popup);
        }
        finally
        {
            comp.ConsentGrant = null;
        }

        if (!buckled)
        {
            _rmcBuckle.CMUSetOffset(rider, previous);
            return false;
        }

        SetSeatRider(comp, seat, rider);
        Dirty(mount, comp);

        var riderComp = EnsureComp<CMURiderComponent>(rider);
        riderComp.Mount = mount;
        riderComp.Seat = seat;
        riderComp.PreviousOffset = previous;
        Dirty(rider, riderComp);

        if (popup)
        {
            _popup.PopupEntity(Loc.GetString("cmu-mount-mounted", ("mount", mount)), rider, rider);
            _popup.PopupEntity(Loc.GetString("cmu-mount-mounted-mount", ("rider", rider)), mount, mount);
        }

        return true;
    }

    /// <summary>Force-unbuckles a rider, then optionally knocks them down (buckled entities cannot be downed).</summary>
    public void Dismount(EntityUid rider, bool knockdown = true)
    {
        if (!TryComp(rider, out CMURiderComponent? riderComp))
            return;

        var mount = riderComp.Mount;
        var knockdownTime = CompOrNull<CMUMountComponent>(mount)?.DismountKnockdown ?? DefaultDismountKnockdown;

        if (mount != null && TryComp(rider, out BuckleComponent? buckle) && buckle.BuckledTo == mount)
            _buckle.Unbuckle((rider, buckle), null);

        // A stale link with no matching buckle: clean up by hand.
        if (TryComp(rider, out riderComp))
        {
            if (TryComp(riderComp.Mount, out CMUMountComponent? mountComp))
                ReleaseRider((riderComp.Mount.Value, mountComp), rider);
            else
                ClearRider(rider, riderComp);
        }

        if (!knockdown || TerminatingOrDeleted(rider))
            return;

        _stun.TryKnockdown(rider, knockdownTime, true, drop: false);
        if (mount != null)
            _popup.PopupEntity(Loc.GetString("cmu-mount-thrown-off", ("mount", mount.Value)), rider, rider, PopupType.MediumCaution);
    }

    /// <summary>Throws every rider off the mount.</summary>
    public void DismountAll(Entity<CMUMountComponent?> mount, bool knockdown = true)
    {
        if (!Resolve(mount, ref mount.Comp, false))
            return;

        var left = mount.Comp.LeftRider;
        var right = mount.Comp.RightRider;
        if (left != null)
            Dismount(left.Value, knockdown);

        if (right != null)
            Dismount(right.Value, knockdown);
    }

    public bool IsRiding(EntityUid rider, EntityUid mount)
    {
        return TryComp(rider, out CMURiderComponent? comp) && comp.Mount == mount;
    }

    public bool TryGetFreeSeat(CMUMountComponent mount, out CMUMountSeat seat)
    {
        if (!IsOccupied(mount.LeftRider))
        {
            seat = CMUMountSeat.Left;
            return true;
        }

        if (!IsOccupied(mount.RightRider))
        {
            seat = CMUMountSeat.Right;
            return true;
        }

        seat = default;
        return false;
    }

    /// <summary>Starts waiting for the mount's answer. False while another request is still pending.</summary>
    public bool TryBeginRequest(Entity<CMUMountComponent> mount, EntityUid rider)
    {
        var now = _timing.CurTime;
        if (mount.Comp.PendingRequester is { } pending && Exists(pending) && now < mount.Comp.PendingUntil)
            return false;

        mount.Comp.PendingRequester = rider;
        // A little past the dialog timeout so the dialog's own cancel always clears it first.
        mount.Comp.PendingUntil = now + mount.Comp.ConsentTimeout + TimeSpan.FromSeconds(1);
        return true;
    }

    /// <summary>Consumes the pending request from <paramref name="rider"/>. False if it is not the pending one.</summary>
    public bool TryEndRequest(Entity<CMUMountComponent> mount, EntityUid rider)
    {
        if (mount.Comp.PendingRequester != rider)
            return false;

        mount.Comp.PendingRequester = null;
        mount.Comp.PendingUntil = TimeSpan.Zero;
        return true;
    }

    #endregion

    #region Consent and seats

    private void OnMountStrapAttempt(Entity<CMUMountComponent> ent, ref StrapAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // Only the consented buckle started by TryMount gets through, and never a third rider.
        if (ent.Comp.ConsentGrant == args.Buckle.Owner && TryGetFreeSeat(ent.Comp, out _))
            return;

        args.Cancelled = true;
        if (args.Popup && args.User is { } user)
        {
            _popup.PopupEntity(Loc.GetString("cmu-mount-needs-consent", ("mount", ent.Owner)),
                ent.Owner,
                user,
                PopupType.SmallCaution);
        }
    }

    private void OnMountUnstrapped(Entity<CMUMountComponent> ent, ref UnstrappedEvent args)
    {
        ReleaseRider(ent, args.Buckle.Owner);
    }

    private void OnMountMove(Entity<CMUMountComponent> ent, ref MoveEvent args)
    {
        if (_timing.ApplyingState || args.NewRotation == args.OldRotation)
            return;

        // Seats are local to the mount, which turns as it walks. Counter-rotate them so the riders stay on its
        // shoulders on screen. The offset is set before the position so the buckle drift check stays satisfied.
        RefreshSeat(ent, CMUMountSeat.Left, ent.Comp.LeftRider);
        RefreshSeat(ent, CMUMountSeat.Right, ent.Comp.RightRider);
    }

    private void RefreshSeat(Entity<CMUMountComponent> ent, CMUMountSeat seat, EntityUid? rider)
    {
        if (rider is not { } uid ||
            !TryComp(uid, out BuckleComponent? buckle) ||
            buckle.BuckledTo != ent.Owner ||
            !TryComp(ent, out StrapComponent? strap))
        {
            return;
        }

        var local = GetLocalSeatOffset(ent, seat);
        _rmcBuckle.CMUSetOffset(uid, local);
        _transform.SetLocalPosition(uid, strap.BuckleOffset + local);
    }

    private Vector2 GetLocalSeatOffset(Entity<CMUMountComponent> mount, CMUMountSeat seat)
    {
        var offset = seat == CMUMountSeat.Left ? mount.Comp.LeftSeat : mount.Comp.RightSeat;
        return (-Transform(mount).LocalRotation).RotateVec(offset);
    }

    private void ReleaseRider(Entity<CMUMountComponent> mount, EntityUid rider)
    {
        var changed = false;
        if (mount.Comp.LeftRider == rider)
        {
            mount.Comp.LeftRider = null;
            changed = true;
        }

        if (mount.Comp.RightRider == rider)
        {
            mount.Comp.RightRider = null;
            changed = true;
        }

        if (changed && !TerminatingOrDeleted(mount.Owner))
            Dirty(mount.Owner, mount.Comp);

        if (TryComp(rider, out CMURiderComponent? riderComp) && riderComp.Mount == mount.Owner)
            ClearRider(rider, riderComp);
    }

    private void ClearRider(EntityUid rider, CMURiderComponent riderComp)
    {
        if (TerminatingOrDeleted(rider))
            return;

        _rmcBuckle.CMUSetOffset(rider, riderComp.PreviousOffset);
        RemComp<CMURiderComponent>(rider);
    }

    private static void SetSeatRider(CMUMountComponent mount, CMUMountSeat seat, EntityUid? rider)
    {
        if (seat == CMUMountSeat.Left)
            mount.LeftRider = rider;
        else
            mount.RightRider = rider;
    }

    private bool IsOccupied(EntityUid? rider)
    {
        return rider != null && !TerminatingOrDeleted(rider.Value);
    }

    private static bool IsSeatedOn(CMUMountComponent mount, EntityUid uid)
    {
        return mount.LeftRider == uid || mount.RightRider == uid;
    }

    private int CountLegs(EntityUid uid)
    {
        var legs = 0;
        foreach (var _ in _body.GetBodyChildrenOfType(uid, BodyPartType.Leg))
        {
            legs++;
        }

        return legs;
    }

    #endregion

    #region Riders keep fighting, but not their mount

    private void OnRiderAttackAttempt(Entity<CMURiderComponent> ent, ref AttackAttemptEvent args)
    {
        // Raised per target for wide swings too, so this covers every melee path.
        if (args.Target != null && args.Target == ent.Comp.Mount)
            args.Cancel();
    }

    private void OnMountAttackAttempt(Entity<CMUMountComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Target is { } target && IsSeatedOn(ent.Comp, target))
            args.Cancel();
    }

    private void OnMountPreventCollide(Entity<CMUMountComponent> ent, ref PreventCollideEvent args)
    {
        // PreventCollideEvent is raised on both bodies, so handling the mount side covers either ordering.
        if (args.Cancelled)
            return;

        var other = args.OtherEntity;
        if (IsSeatedOn(ent.Comp, other))
        {
            args.Cancelled = true;
            return;
        }

        if (!TryComp(other, out ProjectileComponent? projectile))
            return;

        // A rider's own shots pass through the mount.
        if (projectile.Shooter is { } shooter && IsSeatedOn(ent.Comp, shooter))
        {
            args.Cancelled = true;
            return;
        }

        // Shots aimed at a rider are not soaked up by the mount under them.
        if (TryComp(other, out TargetedProjectileComponent? targeted) && IsSeatedOn(ent.Comp, targeted.Target))
            args.Cancelled = true;
    }

    #endregion

    #region Dismount triggers

    private void OnMountMobStateChanged(Entity<CMUMountComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            DismountAll(ent.AsNullable());
    }

    private void OnRiderMobStateChanged(Entity<CMURiderComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            Dismount(ent);
    }

    private void OnMountExplosionReceived(Entity<CMUMountComponent> ent, ref ExplosionReceivedEvent args)
    {
        DismountAll(ent.AsNullable());
    }

    private void OnRiderExplosionReceived(Entity<CMURiderComponent> ent, ref ExplosionReceivedEvent args)
    {
        Dismount(ent);
    }

    #endregion
}
