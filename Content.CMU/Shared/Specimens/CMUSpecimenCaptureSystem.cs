using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.Threats.Mobs.Biomorph;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Pack;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Bed.Sleep;
using Content.Shared.Cuffs;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Speech.Components;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Specimens;

/// <summary>
///     Live specimen capture: Somnazine tranquilizer slugs sedate a specimen, handlers drag it onto an open secure
///     crate, and closing the lid seals it in so it can be hauled away.
///
///     Works on every hostile creature: xenonids and their variants (neomorphs, the abomination),
///     wendigos (natural and lab-made) and the flesh biomorphs. Biomorphs can't be knocked over, so they're held with
///     a plain stun instead of paralysis.
///
///     RMC cuts every stun on a xenonid to two thirds and won't re-stun one that's already down,
///     so a single long stun never holds. Sedation instead keeps refreshing a short hold for as long
///     as it lasts.
/// </summary>
public sealed partial class CMUSpecimenCaptureSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    /// <summary>
    ///     Sedation lost per second, so hits have to land close together.
    /// </summary>
    public const float SedationDecay = 0.1f;

    private static readonly TimeSpan HoldRefresh = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HoldParalysis = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     How close to the crate's centre a specimen has to lie to be sealed in: on the crate's own tile.
    /// </summary>
    private const float OnCrateRange = 0.45f;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSedateOnHitComponent, ProjectileHitEvent>(OnSedateHit);

        SubscribeLocalEvent<CMUSedationComponent, ExaminedEvent>(OnSedatedExamined);
        SubscribeLocalEvent<CMUSedationComponent, CanDragEvent>(OnSedatedCanDrag);

        SubscribeLocalEvent<ActionComponent, ActionAttemptEvent>(OnActionAttempt);

        SubscribeLocalEvent<CMUSpecimenCrateComponent, ComponentInit>(OnCrateInit);
        SubscribeLocalEvent<CMUSpecimenCrateComponent, MapInitEvent>(OnCrateMapInit);
        SubscribeLocalEvent<CMUSpecimenCrateComponent, EntityTerminatingEvent>(OnCrateTerminating);
        SubscribeLocalEvent<CMUSpecimenCrateComponent, ExaminedEvent>(OnCrateExamined);
        SubscribeLocalEvent<CMUSpecimenCrateComponent, ActivateInWorldEvent>(OnCrateActivate);
        SubscribeLocalEvent<CMUSpecimenCrateComponent, CMUCratePutInDoAfterEvent>(OnCratePutIn);
        SubscribeLocalEvent<CMUSpecimenCrateComponent, EntInsertedIntoContainerMessage>(OnCrateInserted);
        SubscribeLocalEvent<CMUSpecimenCrateComponent, EntRemovedFromContainerMessage>(OnCrateRemoved);
    }

    #region Sedation

    private void OnSedateHit(Entity<CMUSedateOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        if (_net.IsClient)
            return;

        Sedate(args.Target, ent.Comp.Amount, ent.Comp.SedationTime);
    }

    /// <summary>
    ///     Adds sedation to a specimen. Returns true if it is now sedated. Every hit on a sedated
    ///     specimen keeps it under for at least <paramref name="sedationTime"/> more.
    /// </summary>
    public bool Sedate(EntityUid target, float amount, TimeSpan sedationTime)
    {
        if (!IsSpecimen(target))
            return false;

        var sedation = EnsureComp<CMUSedationComponent>(target);
        var now = _timing.CurTime;
        var elapsed = (float) (now - sedation.LastUpdate).TotalSeconds;
        sedation.Level = MathF.Max(0, sedation.Level - elapsed * SedationDecay);
        sedation.LastUpdate = now;

        var threshold = GetSedationThreshold(target);
        var wasSedated = IsSedated(target);
        sedation.Level = MathF.Min(threshold, sedation.Level + amount);
        if (sedation.Level < threshold)
        {
            _popup.PopupEntity(Loc.GetString("cmu-specimen-sedation-building", ("target", target)), target, PopupType.SmallCaution);
            return false;
        }

        var until = now + sedationTime;
        if (sedation.SedatedUntil is not { } current || current < until)
            sedation.SedatedUntil = until;

        Dirty(target, sedation);
        SuspendPullParalysis((target, sedation));
        Hold(target, HoldParalysis);
        if (!wasSedated)
            _popup.PopupEntity(Loc.GetString("cmu-specimen-sedated", ("target", target)), target, PopupType.MediumCaution);

        return true;
    }

    /// <summary>
    ///     Whether <paramref name="uid"/> is a creature the capture kit works on.
    /// </summary>
    public bool IsSpecimen(EntityUid uid)
    {
        return HasComp<XenoComponent>(uid) || HasComp<BiomorphComponent>(uid) || HasComp<CMUWendigoPackComponent>(uid);
    }

    /// <summary>
    ///     Down, or for creatures that can't fall over, stunned.
    /// </summary>
    public bool IsSubdued(EntityUid uid)
    {
        return _standing.IsDown(uid) || HasComp<StunnedComponent>(uid);
    }

    public bool IsSedated(EntityUid uid)
    {
        return TryComp<CMUSedationComponent>(uid, out var sedation) &&
               sedation.SedatedUntil is { } until &&
               _timing.CurTime < until;
    }

    /// <summary>
    ///     Sedated, or shut in a specimen crate.
    /// </summary>
    public bool IsHeld(EntityUid uid)
    {
        if (IsSedated(uid))
            return true;

        return _container.TryGetContainingContainer(uid, out var container) &&
               HasComp<CMUSpecimenCrateComponent>(container.Owner);
    }

    /// <summary>
    ///     Holds a specimen in place for <paramref name="time"/>. Paralysis where it can fall over,
    ///     a stun where it can't.
    /// </summary>
    private void Hold(EntityUid uid, TimeSpan time)
    {
        if (!_stun.TryParalyze(uid, time, true))
            _stun.TryUpdateStunDuration(uid, time);
    }

    /// <summary>
    ///     Hits needed in quick succession to put a xenonid under. Bigger castes take more.
    /// </summary>
    public float GetSedationThreshold(EntityUid target)
    {
        if (!TryComp<RMCSizeComponent>(target, out var size))
            return 3;

        return size.Size switch
        {
            <= RMCSizes.SmallXeno => 2,
            RMCSizes.Xeno => 3,
            _ => 5,
        };
    }

    /// <summary>
    ///     A live xenonid knocks down whoever grabs it. Sedated, it can't, so handlers can drag it.
    /// </summary>
    private void SuspendPullParalysis(Entity<CMUSedationComponent> ent)
    {
        if (_net.IsClient || !TryComp<ParalyzeOnPullAttemptComponent>(ent, out var paralyze))
            return;

        ent.Comp.SuspendedPullParalysis = paralyze.Duration;
        RemComp<ParalyzeOnPullAttemptComponent>(ent);
    }

    private void RestorePullParalysis(Entity<CMUSedationComponent> ent)
    {
        if (_net.IsClient || ent.Comp.SuspendedPullParalysis is not { } duration)
            return;

        ent.Comp.SuspendedPullParalysis = null;
        var paralyze = EnsureComp<ParalyzeOnPullAttemptComponent>(ent);
        paralyze.Duration = duration;
        Dirty(ent, paralyze);
    }

    private void OnSedatedExamined(Entity<CMUSedationComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.SedatedUntil is not { } until || _timing.CurTime >= until || _container.IsEntityInContainer(ent))
            return;

        var seconds = (int) (until - _timing.CurTime).TotalSeconds;
        args.PushMarkup(Loc.GetString("cmu-specimen-examine-sedated", ("target", ent.Owner), ("seconds", seconds)));
    }

    private void OnSedatedCanDrag(Entity<CMUSedationComponent> ent, ref CanDragEvent args)
    {
        if (IsSedated(ent))
            args.Handled = true;
    }

    /// <summary>
    ///     A sedated or crated specimen can't use any of its abilities, acid included.
    /// </summary>
    private void OnActionAttempt(Entity<ActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled || !IsHeld(args.User))
            return;

        args.Cancelled = true;
        args.Reason = Loc.GetString("cmu-specimen-actions-blocked");
    }

    private void UpdateSedation()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUSedationComponent>();
        while (query.MoveNext(out var uid, out var sedation))
        {
            if (sedation.SedatedUntil is not { } until || now < sedation.NextHold)
                continue;

            sedation.NextHold = now + HoldRefresh;

            // A crated specimen's sedation waits until it's let out.
            if (_container.IsEntityInContainer(uid))
            {
                sedation.SedatedUntil = until + HoldRefresh;
                Dirty(uid, sedation);
                continue;
            }

            if (now >= until)
            {
                sedation.SedatedUntil = null;
                Dirty(uid, sedation);
                RestorePullParalysis((uid, sedation));
                _popup.PopupEntity(Loc.GetString("cmu-specimen-woke", ("target", uid)), uid, PopupType.LargeCaution);
                continue;
            }

            Hold(uid, HoldParalysis);
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        UpdateCrates();
        UpdateSedation();
    }

    #endregion

    #region Containment

    private void OnCrateInit(Entity<CMUSpecimenCrateComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
    }

    private void OnCrateMapInit(Entity<CMUSpecimenCrateComponent> ent, ref MapInitEvent args)
    {
        SetBlocking(ent, ent.Comp.State != CMUSpecimenCrateState.Open);
    }

    private void OnCrateTerminating(Entity<CMUSpecimenCrateComponent> ent, ref EntityTerminatingEvent args)
    {
        // Never take the specimen with the crate.
        if (_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            _container.EmptyContainer(container, true);
    }

    private void OnCrateExamined(Entity<CMUSpecimenCrateComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.State == CMUSpecimenCrateState.Closed ? "cmu-specimen-crate-examine-closed" : "cmu-specimen-crate-examine-open"));
        if (GetSpecimen(ent) is { } specimen)
            args.PushMarkup(Loc.GetString("cmu-specimen-crate-examine-full", ("target", specimen)));
        else
            args.PushMarkup(Loc.GetString("cmu-specimen-crate-examine-empty"));
    }

    private void OnCrateActivate(Entity<CMUSpecimenCrateComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || IsSpecimen(args.User))
            return;

        args.Handled = true;

        // There's no getting out: not even Corporate Security can open it from the inside.
        if (_container.TryGetContainingContainer(args.User, out var inside) && inside.Owner == ent.Owner)
        {
            _popup.PopupClient(Loc.GetString("cmu-specimen-crate-no-escape"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        TryToggle(ent, args.User);
    }

    /// <summary>
    ///     Opens a closed crate or closes an open one. Does nothing while the lid is moving. Opening
    ///     needs the crate's access; closing seals in a downed specimen lying on the crate.
    /// </summary>
    public bool TryToggle(Entity<CMUSpecimenCrateComponent> ent, EntityUid? user = null)
    {
        switch (ent.Comp.State)
        {
            case CMUSpecimenCrateState.Open:
                if (GetSpecimen(ent) == null && FindSpecimenOnCrate(ent) == null && FindPersonOnCrate(ent, user) is { } person)
                {
                    // A person has to be forced in, which takes a while; then the lid closes on them.
                    if (user != null)
                        return StartPutIn(ent, user.Value, person);

                    if (_net.IsServer)
                        TryContain(ent, person);
                }
                else if (_net.IsServer && GetSpecimen(ent) == null && FindSpecimenOnCrate(ent) is { } specimen)
                {
                    TryContain(ent, specimen);
                }

                SetBlocking(ent, true);
                return StartMoving(ent, false, user);
            case CMUSpecimenCrateState.Closed:
                if (user != null && !_access.IsAllowed(user.Value, ent))
                {
                    _popup.PopupClient(Loc.GetString("cmu-specimen-crate-locked"), ent, user.Value, PopupType.SmallCaution);
                    return false;
                }

                return StartMoving(ent, true, user);
            default:
                return false;
        }
    }

    /// <summary>
    ///     A downed specimen lying on the open crate, ready to be sealed in.
    /// </summary>
    private EntityUid? FindSpecimenOnCrate(Entity<CMUSpecimenCrateComponent> crate)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(crate, OnCrateRange))
        {
            if (uid != crate.Owner && IsSpecimen(uid) && IsSubdued(uid) && !_container.IsEntityInContainer(uid))
                return uid;
        }

        return null;
    }

    /// <summary>
    ///     Anyone the capture kit isn't for (people, synthetics, animals) lying or standing on the open crate,
    ///     other than whoever is closing it.
    /// </summary>
    private EntityUid? FindPersonOnCrate(Entity<CMUSpecimenCrateComponent> crate, EntityUid? closer = null)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(crate, OnCrateRange))
        {
            // Whoever is closing the lid isn't the one going in.
            if (uid != crate.Owner && uid != closer && !IsSpecimen(uid) && HasComp<MobStateComponent>(uid) && !_container.IsEntityInContainer(uid))
                return uid;
        }

        return null;
    }

    /// <summary>
    ///     Downed, cuffed, unconscious or asleep: quick to force into a crate.
    /// </summary>
    public bool IsRestrained(EntityUid uid)
    {
        return IsSubdued(uid) ||
               _cuffable.IsCuffed(uid) ||
               HasComp<RMCUnconsciousComponent>(uid) ||
               HasComp<SleepingComponent>(uid);
    }

    private bool StartPutIn(Entity<CMUSpecimenCrateComponent> crate, EntityUid user, EntityUid person)
    {
        var time = IsRestrained(person) ? crate.Comp.PutInTimeRestrained : crate.Comp.PutInTime;
        var doAfter = new DoAfterArgs(EntityManager, user, time, new CMUCratePutInDoAfterEvent(), crate, person, crate)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupPredicted(Loc.GetString("cmu-specimen-crate-putting-in", ("user", user), ("target", person)), crate, user, PopupType.MediumCaution);
        return true;
    }

    private void OnCratePutIn(Entity<CMUSpecimenCrateComponent> ent, ref CMUCratePutInDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } person)
            return;

        args.Handled = true;
        if (ent.Comp.State != CMUSpecimenCrateState.Open || GetSpecimen(ent) != null || FindPersonOnCrate(ent, args.User) != person)
            return;

        if (_net.IsServer && !TryContain(ent, person))
            return;

        SetBlocking(ent, true);
        StartMoving(ent, false, args.User);
    }

    /// <summary>
    ///     Whoever is shut in can still talk, but only muffled through the lid.
    /// </summary>
    private void OnCrateInserted(Entity<CMUSpecimenCrateComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_net.IsClient || args.Container.ID != ent.Comp.ContainerId || IsSpecimen(args.Entity))
            return;

        var crated = EnsureComp<CMUCratedPersonComponent>(args.Entity);
        if (!HasComp<MumbleAccentComponent>(args.Entity))
        {
            AddComp<MumbleAccentComponent>(args.Entity);
            crated.AddedMuffle = true;
        }
    }

    private void OnCrateRemoved(Entity<CMUSpecimenCrateComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_net.IsClient || args.Container.ID != ent.Comp.ContainerId ||
            !TryComp<CMUCratedPersonComponent>(args.Entity, out var crated))
        {
            return;
        }

        if (crated.AddedMuffle)
            RemComp<MumbleAccentComponent>(args.Entity);

        RemComp<CMUCratedPersonComponent>(args.Entity);
    }

    private bool StartMoving(Entity<CMUSpecimenCrateComponent> ent, bool open, EntityUid? user = null)
    {
        ent.Comp.State = open ? CMUSpecimenCrateState.Opening : CMUSpecimenCrateState.Closing;
        ent.Comp.MovingUntil = _timing.CurTime + ent.Comp.MoveTime;
        Dirty(ent);
        _audio.PlayPredicted(ent.Comp.MoveSound, ent, user);
        return true;
    }

    private void UpdateCrates()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUSpecimenCrateComponent>();
        while (query.MoveNext(out var uid, out var crate))
        {
            if (crate.MovingUntil is not { } until || now < until)
                continue;

            crate.MovingUntil = null;
            if (crate.State == CMUSpecimenCrateState.Opening)
            {
                crate.State = CMUSpecimenCrateState.Open;

                // The lid is up: whatever was inside is out, and the crate can be walked over again.
                if (_container.TryGetContainer(uid, crate.ContainerId, out var container))
                    _container.EmptyContainer(container, true);

                SetBlocking((uid, crate), false);
            }
            else
            {
                crate.State = CMUSpecimenCrateState.Closed;
            }

            Dirty(uid, crate);
        }
    }

    /// <summary>
    ///     An open crate doesn't block movement, so a body can be dragged onto it. A closed one does.
    /// </summary>
    private void SetBlocking(Entity<CMUSpecimenCrateComponent> crate, bool blocking)
    {
        if (!TryComp<FixturesComponent>(crate, out var fixtures))
            return;

        foreach (var fixture in fixtures.Fixtures.Values)
            _physics.SetHard(crate, fixture, blocking, fixtures);
    }

    public bool TryContain(Entity<CMUSpecimenCrateComponent> crate, EntityUid specimen)
    {
        if (GetSpecimen(crate) != null || !_container.TryGetContainer(crate, crate.Comp.ContainerId, out var container))
            return false;

        if (TryComp<PullableComponent>(specimen, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(specimen, pullable);

        if (!_container.Insert(specimen, container))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-specimen-contained", ("target", specimen)), crate);
        return true;
    }

    public EntityUid? GetSpecimen(Entity<CMUSpecimenCrateComponent> crate)
    {
        if (!_container.TryGetContainer(crate, crate.Comp.ContainerId, out var container) || container.ContainedEntities.Count == 0)
            return null;

        return container.ContainedEntities[0];
    }

    #endregion
}
