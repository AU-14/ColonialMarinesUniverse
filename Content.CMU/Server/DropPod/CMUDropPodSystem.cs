using System.Numerics;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.CrashLand;
using Content.Shared.CMU14.DropPod;
using Content.Shared.Climbing.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Parallax;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.DropPod;

public sealed partial class CMUDropPodSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedCrashLandSystem _crashLand = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private CMUDropPodLandingSystem _landing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private CMUSharedDropPodSystem _pod = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUDropPodComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CMUDropPodComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);
        SubscribeLocalEvent<CMUDropPodComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<CMUDropPodComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<CMUDropPodComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<CMUDropPodComponent, CMUDropPodInsertDoAfterEvent>(OnInsertDoAfter);
        SubscribeLocalEvent<CMUDropPodComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<CMUDropPodComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUDropPodComponent, CrashLandedEvent>(OnCrashLanded);

        SubscribeLocalEvent<CMUDropPodFlightComponent, ComponentShutdown>(OnFlightShutdown);
    }

    private void OnInit(Entity<CMUDropPodComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Container = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
        UpdateAppearance(ent);
    }

    private void OnRelayMovement(Entity<CMUDropPodComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        if (ent.Comp.State == CMUDropPodState.InFlight)
            return;

        Eject(ent);
    }

    private void OnRemoved(Entity<CMUDropPodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        RemCompDeferred<CMUDropPodLandedComponent>(ent);
        UpdateAppearance(ent);
    }

    private void OnGetAlternativeVerbs(Entity<CMUDropPodComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.State == CMUDropPodState.InFlight)
            return;

        if (ent.Comp.Container.ContainedEntity != null)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Act = () => Eject(ent),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("cmu-drop-pod-eject-verb"),
                Priority = 1,
            });
            return;
        }

        if (!CanInsert(ent, args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => Insert(ent, user),
            Category = VerbCategory.Insert,
            Text = Loc.GetString("cmu-drop-pod-enter-verb"),
        });
    }

    private void OnDragDrop(Entity<CMUDropPodComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !CanInsert(ent, args.Dragged))
            return;

        if (args.User == args.Dragged)
        {
            args.Handled = Insert(ent, args.Dragged);
            return;
        }

        var ev = new CMUDropPodInsertDoAfterEvent(GetNetEntity(args.Dragged));
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.InsertDelay, ev, ent, args.Dragged)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnInsertDoAfter(Entity<CMUDropPodComponent> ent, ref CMUDropPodInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        Insert(ent, GetEntity(args.Inserted));
    }

    private void OnDestroyed(Entity<CMUDropPodComponent> ent, ref DestructionEventArgs args)
    {
        Eject(ent);
    }

    private void OnExamined(Entity<CMUDropPodComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(CMUDropPodComponent)))
        {
            args.PushMarkup(Loc.GetString("cmu-drop-pod-examine-state", ("state", ent.Comp.State.ToString())));
        }
    }

    public bool CanInsert(Entity<CMUDropPodComponent> pod, EntityUid target)
    {
        return !TerminatingOrDeleted(target)
            && pod.Comp.Container.ContainedEntity == null
            && _pod.CanAccept(pod, target)
            && _container.CanInsert(target, pod.Comp.Container);
    }

    private bool Insert(Entity<CMUDropPodComponent> pod, EntityUid target)
    {
        if (!CanInsert(pod, target) || !_container.Insert(target, pod.Comp.Container))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-drop-pod-enter", ("target", target)), pod);
        UpdateAppearance(pod);
        return true;
    }

    private void Eject(Entity<CMUDropPodComponent> pod, bool climb = true)
    {
        if (pod.Comp.Container.ContainedEntity is not { } contained)
            return;

        _container.Remove(contained, pod.Comp.Container);
        if (climb)
            _climb.ForciblySetClimbing(contained, pod);

        if (pod.Comp.ExitStun > TimeSpan.Zero)
            _stun.TryStun(contained, pod.Comp.ExitStun, true);

        UpdateAppearance(pod);
    }

    /// <summary>
    /// Checks whether a pod could fire right now, without side effects.
    /// </summary>
    public bool CanLaunch(Entity<CMUDropPodComponent> pod, out string? reason)
    {
        reason = null;
        if (pod.Comp.State != CMUDropPodState.Ready || HasComp<CMUDropPodFlightComponent>(pod))
        {
            reason = Loc.GetString("cmu-drop-pod-not-ready", ("pod", pod.Owner));
            return false;
        }

        if (pod.Comp.RequireOccupant && pod.Comp.Container.ContainedEntity == null)
        {
            reason = Loc.GetString("cmu-drop-pod-no-occupant", ("pod", pod.Owner));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Queues a pod to leave after <paramref name="delay"/> and land near <paramref name="target"/> on <paramref name="planetMap"/>.
    /// </summary>
    public bool TryLaunch(Entity<CMUDropPodComponent> pod, EntityUid planetMap, Vector2i target, TimeSpan delay, out string? reason)
    {
        if (!CanLaunch(pod, out reason))
            return false;

        if (!TryFindLanding(pod, planetMap, target, pod.Comp.ScatterRadius, out var landing))
        {
            reason = Loc.GetString("cmu-drop-pod-no-landing", ("pod", pod.Owner));
            return false;
        }

        var flight = AddComp<CMUDropPodFlightComponent>(pod);
        flight.PlanetMap = planetMap;
        flight.Target = target;
        flight.Landing = landing;
        flight.NextPhaseAt = _timing.CurTime + delay;

        pod.Comp.State = CMUDropPodState.InFlight;
        Dirty(pod);
        SetWeaponLocks(pod, true);
        return true;
    }

    private bool TryFindLanding(Entity<CMUDropPodComponent> pod, EntityUid planetMap, Vector2i target, float radius, out EntityCoordinates landing)
    {
        var comp = pod.Comp;
        return _landing.TryFindLanding(planetMap,
            target,
            radius,
            comp.LandingAttempts,
            comp.TargetDepth,
            comp.LandingBlockers,
            comp.BlockersMustBeIndestructible,
            comp.RespectParadropAreas,
            out landing);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var flights = EntityQueryEnumerator<CMUDropPodComponent, CMUDropPodFlightComponent>();
        while (flights.MoveNext(out var uid, out var pod, out var flight))
        {
            if (flight.Falling || time < flight.NextPhaseAt)
                continue;

            if (flight.Phase < 0)
                Depart((uid, pod), flight);

            AdvancePhase((uid, pod), flight, time);
        }

        var landed = EntityQueryEnumerator<CMUDropPodComponent, CMUDropPodLandedComponent>();
        while (landed.MoveNext(out var uid, out var pod, out var state))
        {
            if (time < state.NextActionAt)
                continue;

            ProcessLanded((uid, pod), state, time);
        }
    }

    private void Depart(Entity<CMUDropPodComponent> pod, CMUDropPodFlightComponent flight)
    {
        _audio.PlayPvs(pod.Comp.LaunchSound, pod);

        var transit = _map.CreateMap(out _);
        _metaData.SetEntityName(transit, Loc.GetString("cmu-drop-pod-transit-map"));
        EnsureComp<ParallaxComponent>(transit);
        flight.TransitMap = transit;

        var xform = Transform(pod);
        if (xform.Anchored)
            _transform.Unanchor(pod, xform);

        _transform.SetCoordinates(pod, new EntityCoordinates(transit, Vector2.Zero));
    }

    private void AdvancePhase(Entity<CMUDropPodComponent> pod, CMUDropPodFlightComponent flight, TimeSpan time)
    {
        flight.Phase++;
        if (flight.Phase >= pod.Comp.Phases.Count)
        {
            flight.Falling = true;
            _crashLand.DoCrashLand(pod,
                flight.Landing,
                pod.Comp.SkyFallDuration,
                pod.Comp.CrashDuration,
                false,
                pod.Comp.DropSound,
                pod.Comp.CrashSound);
            return;
        }

        var phase = pod.Comp.Phases[flight.Phase];
        flight.NextPhaseAt = time + phase.Duration;

        if (flight.TransitMap is { } transit && TryComp(transit, out ParallaxComponent? parallax))
        {
            parallax.Parallax = phase.Parallax;
            parallax.TravelVelocity = phase.Velocity;
            Dirty(transit, parallax);
        }

        var occupant = pod.Comp.Container.ContainedEntity;
        if (occupant != null)
        {
            if (phase.Message is { } message)
                _popup.PopupEntity(Loc.GetString(message), occupant.Value, occupant.Value, PopupType.Medium);

            if (phase.Sound != null)
                _audio.PlayGlobal(phase.Sound, occupant.Value);

            if (phase.Shakes > 0)
                _cameraShake.ShakeCamera(occupant.Value, phase.Shakes, phase.ShakeStrength);
        }

        if (!phase.Atmospheric || flight.BrokenApart || !_random.Prob(pod.Comp.AtmosphereBreakupChance))
            return;

        flight.BrokenApart = true;
        if (occupant != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-drop-pod-breakup-atmosphere"), occupant.Value, occupant.Value, PopupType.LargeCaution);
            if (pod.Comp.BreakupSound != null)
                _audio.PlayGlobal(pod.Comp.BreakupSound, occupant.Value);
        }

        var radius = pod.Comp.ScatterRadius + pod.Comp.BreakupExtraScatter;
        if (TryFindLanding(pod, flight.PlanetMap, flight.Target, radius, out var scattered))
            flight.Landing = scattered;
    }

    private void OnCrashLanded(Entity<CMUDropPodComponent> ent, ref CrashLandedEvent args)
    {
        if (!TryComp(ent, out CMUDropPodFlightComponent? flight))
            return;

        var broken = flight.BrokenApart || _random.Prob(ent.Comp.ImpactBreakupChance);
        RemComp<CMUDropPodFlightComponent>(ent);
        RemComp<CrashLandableComponent>(ent);

        var xform = Transform(ent);
        if (xform.GridUid != null)
            _transform.AnchorEntity((ent, xform));

        var coords = _transform.GetMapCoordinates(ent, xform);
        if (ent.Comp.LandingShakes > 0)
            _cameraShake.ShakeCamera(Filter.Empty().AddInRange(coords, ent.Comp.LandingShakeRange), ent.Comp.LandingShakes, ent.Comp.LandingShakeStrength);

        if (ent.Comp.LandingDamage is { } landingDamage)
        {
            _mobs.Clear();
            _lookup.GetEntitiesInRange(coords, ent.Comp.LandingDamageRadius, _mobs);
            foreach (var mob in _mobs)
            {
                if (_container.IsEntityInContainer(mob))
                    continue;

                _damageable.TryChangeDamage(mob, landingDamage, origin: ent);
            }
        }

        if (broken)
        {
            BreakApart(ent);
            return;
        }

        ent.Comp.State = CMUDropPodState.Landed;
        Dirty(ent);
        SetWeaponLocks(ent, false);
        UpdateAppearance(ent);

        if (ent.Comp.Container.ContainedEntity is not { } occupant)
            return;

        var landed = EnsureComp<CMUDropPodLandedComponent>(ent);
        landed.NextActionAt = _timing.CurTime + ent.Comp.OpenDelay;
        if (ent.Comp.InjuryDamage is not { } injury || !_random.Prob(ent.Comp.InjuryChance))
            return;

        _damageable.TryChangeDamage(occupant, injury, true, false, origin: ent);
        if (ent.Comp.InjuryStun > TimeSpan.Zero)
            _stun.TryStun(occupant, ent.Comp.InjuryStun, true);

        if (ent.Comp.TreatmentDamage == null || ent.Comp.TreatmentTicks <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-drop-pod-injured"), occupant, occupant, PopupType.MediumCaution);
            return;
        }

        landed.Injured = true;
        landed.TreatmentRemaining = ent.Comp.TreatmentTicks;
        landed.NextActionAt = _timing.CurTime + ent.Comp.TreatmentInterval;
        _popup.PopupEntity(Loc.GetString("cmu-drop-pod-injured-treatment"), occupant, occupant, PopupType.MediumCaution);
    }

    private void ProcessLanded(Entity<CMUDropPodComponent> pod, CMUDropPodLandedComponent landed, TimeSpan time)
    {
        if (pod.Comp.Container.ContainedEntity is not { } occupant)
        {
            RemCompDeferred<CMUDropPodLandedComponent>(pod);
            return;
        }

        if (!landed.Injured || pod.Comp.TreatmentDamage is not { } treatment)
        {
            Eject(pod);
            return;
        }

        _damageable.TryChangeDamage(occupant, treatment, true, false, origin: pod);
        landed.TreatmentRemaining--;
        landed.NextActionAt = time + pod.Comp.TreatmentInterval;
        if (landed.TreatmentRemaining > 0)
            return;

        _popup.PopupEntity(Loc.GetString("cmu-drop-pod-treatment-complete"), occupant, occupant);
        Eject(pod);
    }

    private void BreakApart(Entity<CMUDropPodComponent> pod)
    {
        var coords = Transform(pod).Coordinates;
        _audio.PlayPvs(pod.Comp.BreakupSound, coords);
        _popup.PopupCoordinates(Loc.GetString("cmu-drop-pod-breakup-impact", ("pod", pod.Owner)), coords, PopupType.MediumCaution);

        if (pod.Comp.BreakupDebris is { } debris)
        {
            for (var i = 0; i < pod.Comp.BreakupDebrisCount; i++)
                Spawn(debris, coords.Offset(_random.NextVector2(0.5f)));
        }

        SetWeaponLocks(pod, false);
        foreach (var slot in pod.Comp.WeaponSlots)
            _itemSlots.TryEject(pod.Owner, slot, null, out _);

        if (pod.Comp.Container.ContainedEntity is { } occupant)
        {
            Eject(pod, false);
            if (pod.Comp.BreakupDamage is { } damage)
                _damageable.TryChangeDamage(occupant, damage, true, false, origin: pod);

            if (pod.Comp.BreakupStun > TimeSpan.Zero)
                _stun.TryStun(occupant, pod.Comp.BreakupStun, true);
        }

        QueueDel(pod);
    }

    private void OnFlightShutdown(Entity<CMUDropPodFlightComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.TransitMap is { } transit && !TerminatingOrDeleted(transit))
            QueueDel(transit);
    }

    private void SetWeaponLocks(Entity<CMUDropPodComponent> pod, bool locked)
    {
        if (!TryComp(pod, out ItemSlotsComponent? slots))
            return;

        foreach (var slot in pod.Comp.WeaponSlots)
            _itemSlots.SetLock((pod, slots), slot, locked);
    }

    private void UpdateAppearance(Entity<CMUDropPodComponent> pod)
    {
        var occupied = pod.Comp.Container.ContainedEntity != null;
        if (pod.Comp.Occupied != occupied)
        {
            pod.Comp.Occupied = occupied;
            Dirty(pod);
        }

        var status = pod.Comp.State == CMUDropPodState.Landed
            ? CMUDropPodStatus.Spent
            : occupied
                ? CMUDropPodStatus.Occupied
                : CMUDropPodStatus.Empty;

        _appearance.SetData(pod, CMUDropPodVisuals.Status, status);
    }
}
