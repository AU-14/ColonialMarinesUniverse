using Content.Shared._RMC14.Armor.ThermalCloak;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Chat;
using Content.Shared.CMU14.Antags.GovForVeteran;
using Content.Shared.CMU14.Stealth;
using Content.Shared.Doors.Systems;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.Player;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.GovForVeteran;

public sealed partial class CMUVeteranCloakSystem : EntitySystem
{
    /// <summary>
    ///     What blocks line of sight for the "is anyone watching me" check: walls, doors, windows and tables.
    /// </summary>
    private const CollisionGroup SightMask =
        CollisionGroup.Impassable |
        CollisionGroup.MidImpassable |
        CollisionGroup.HighImpassable |
        CollisionGroup.InteractImpassable;

    /// <summary>
    ///     Collision bits he keeps while the field is up: solid world only. Doors, airlocks and windoors sit on
    ///     <see cref="CollisionGroup.MidImpassable"/> / <see cref="CollisionGroup.HighImpassable"/> and their masks
    ///     also cover Opaque/BulletImpassable/InteractImpassable, so this is a whitelist rather than a subtraction -
    ///     otherwise his bullet fixture would still rub against closed doors. Walls carry Impassable, so he still
    ///     cannot cross them or leave the map.
    /// </summary>
    private const CollisionGroup SolidMask =
        CollisionGroup.Impassable |
        CollisionGroup.GhostImpassable |
        CollisionGroup.BarricadeImpassable |
        CollisionGroup.BarbedBarricade;

    /// <summary>
    ///     The field controls are locked behind the antagonist skill; the veteran carries it, nobody else does.
    /// </summary>
    private static readonly EntProtoId<SkillDefinitionComponent> AntagonistSkill = "RMCSkillAntagonist";

    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // The cloak's own system owns ThermalCloakTurnInvisibleActionEvent, so the gate sits one step earlier:
        // the action is refused before it is even validated. Keyed on the action's own event type, which is a
        // reliable handle on the cloak toggle regardless of what the action considers its provider.
        SubscribeLocalEvent<ActionComponent, ActionAttemptEvent>(OnActionAttempt);

        SubscribeLocalEvent<CMUVeteranCloakComponent, CMUInvisibilityStartedEvent>(OnCloakStarted);
        SubscribeLocalEvent<CMUVeteranCloakComponent, CMUInvisibilityStoppedEvent>(OnCloakStopped);

        SubscribeLocalEvent<CMUVeteranCloakComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUVeteranCloakComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CMUVeteranCloakComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);

        // Gates: while the field is up (and for the uncloak window) he cannot fight, reach, carry or use anything.
        SubscribeLocalEvent<CMUVeteranCloakComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<CMUVeteranCloakComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<CMUVeteranCloakComponent, DropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<CMUVeteranCloakComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<CMUVeteranCloakComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<CMUVeteranCloakComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<CMUVeteranCloakComponent, StartPullAttemptEvent>(OnStartPullAttempt);
        SubscribeLocalEvent<CMUVeteranCloakComponent, SpeakAttemptEvent>(OnSpeakAttempt);

        // These are raised on the storage / the gun rather than the user, so they need a targeted lookup.
        SubscribeLocalEvent<StorageInteractAttemptEvent>(OnStorageInteract);
        SubscribeLocalEvent<ShotAttemptedEvent>(OnShotAttempted);
    }

    /// <summary>
    ///     Only lets the field go up or down while nobody can see him, and refuses the controls entirely without
    ///     the antagonist skill.
    /// </summary>
    private void OnActionAttempt(Entity<ActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<InstantActionComponent>(ent.Owner, out var instant) ||
            instant.Event is not ThermalCloakTurnInvisibleActionEvent)
        {
            return;
        }

        // Locked behind the antagonist skill: without it the field controls do not respond at all.
        if (_skills.GetSkill(args.User, AntagonistSkill) < 1)
        {
            args.Cancelled = true;
            args.Reason = Loc.GetString("cmu-veteran-cloak-unskilled");
            return;
        }

        // Past this point only the veteran answers to the watching rule.
        if (!TryComp<CMUVeteranCloakComponent>(args.User, out var cloak) ||
            !IsWatched((args.User, cloak)))
        {
            return;
        }

        args.Cancelled = true;
        args.Reason = Loc.GetString(cloak.Cloaked
            ? "cmu-veteran-cloak-watched-uncloak"
            : "cmu-veteran-cloak-watched");
        args.Type = PopupType.MediumCaution;
    }

    private void OnCloakStarted(Entity<CMUVeteranCloakComponent> ent, ref CMUInvisibilityStartedEvent args)
    {
        var cloak = ent.Comp;
        cloak.Cloaked = true;

        // Walk through people as well as doors; bullets are unaffected.
        var active = EnsureComp<EntityActiveInvisibleComponent>(ent.Owner);
        active.DisableMobCollision = true;
        Dirty(ent.Owner, active);

        MakePhaseable((ent.Owner, cloak));
        HideHeldItems(ent.Owner);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnCloakStopped(Entity<CMUVeteranCloakComponent> ent, ref CMUInvisibilityStoppedEvent args)
    {
        var cloak = ent.Comp;
        cloak.Cloaked = false;

        if (TryComp<EntityActiveInvisibleComponent>(ent.Owner, out var active))
        {
            active.DisableMobCollision = false;
            Dirty(ent.Owner, active);
        }

        cloak.UncloakLockUntil = _timing.CurTime + cloak.UncloakWeaponLock;
        RestorePhase((ent.Owner, cloak));
        RestoreHeldItems((ent.Owner, cloak));

        if (TerminatingOrDeleted(ent.Owner) || cloak.Cleaning)
            return;

        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        NotifyReveal((ent.Owner, cloak));
    }

    private void OnMobStateChanged(Entity<CMUVeteranCloakComponent> ent, ref MobStateChangedEvent args)
    {
        if (!ent.Comp.Cloaked || !_mobState.IsIncapacitated(ent.Owner))
            return;

        // Going down drops the field: never leave an invisible body behind.
        RemComp<EntityActiveInvisibleComponent>(ent.Owner);
    }

    private void OnShutdown(Entity<CMUVeteranCloakComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Cleaning = true;
        try
        {
            ent.Comp.Cloaked = false;

            if (HasComp<EntityActiveInvisibleComponent>(ent.Owner) && !TerminatingOrDeleted(ent.Owner))
                RemComp<EntityActiveInvisibleComponent>(ent.Owner);

            RestorePhase(ent);
            RestoreHeldItems(ent);

            if (!TerminatingOrDeleted(ent.Owner))
                _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        }
        finally
        {
            ent.Comp.Cleaning = false;
        }
    }

    private void OnRefreshSpeed(Entity<CMUVeteranCloakComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Cloaked)
            return;

        args.ModifySpeed(ent.Comp.CloakSpeedMultiplier);
    }

    /// <summary>
    ///     Whether any mob can currently see him: same map, conscious, not blind, and unobstructed line of sight
    ///     within <see cref="CMUVeteranCloakComponent.SightRange"/>.
    /// </summary>
    private bool IsWatched(Entity<CMUVeteranCloakComponent> ent)
    {
        var veteran = ent.Owner;
        var veteranCoords = _transform.GetMapCoordinates(veteran);

        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var observer, out var mobState, out var observerXform))
        {
            if (observer == veteran)
                continue;

            // The dead and the unconscious are not watching anything.
            if (_mobState.IsIncapacitated(observer, mobState))
                continue;

            if (observerXform.MapID != veteranCoords.MapId)
                continue;

            // Blindfolds, eye damage, closed eyes and blindness effects all cancel this event.
            var seeAttempt = new CanSeeAttemptEvent();
            RaiseLocalEvent(observer, seeAttempt);
            if (seeAttempt.Blind)
                continue;

            var observerCoords = _transform.GetMapCoordinates(observer);
            if (_interaction.InRangeUnobstructed(observerCoords,
                    veteranCoords,
                    ent.Comp.SightRange,
                    SightMask,
                    predicate: uid => uid == veteran || uid == observer,
                    checkAccess: false))
            {
                return true;
            }
        }

        return false;
    }

    private void NotifyReveal(Entity<CMUVeteranCloakComponent> ent)
    {
        var message = Loc.GetString("cmu-veteran-cloak-reveal");
        var veteranCoords = _transform.GetMapCoordinates(ent.Owner);

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } viewer || viewer == ent.Owner)
                continue;

            var viewerCoords = _transform.GetMapCoordinates(viewer);
            if (viewerCoords.MapId != veteranCoords.MapId ||
                (viewerCoords.Position - veteranCoords.Position).Length() > ent.Comp.RevealRange)
            {
                continue;
            }

            _popups.PopupEntity(message, viewer, viewer, PopupType.MediumCaution);
        }
    }

    /// <summary>
    ///     Drops the collision bits that make doors and low obstacles solid while the field is up, and takes the
    ///     door-bump tag off him so a closed airlock neither blocks him, opens for him, nor buzzes at him. Walls keep
    ///     their Impassable bit, so he still cannot walk through them.
    /// </summary>
    private void MakePhaseable(Entity<CMUVeteranCloakComponent> ent)
    {
        ent.Comp.OriginalMasks.Clear();

        if (TryComp<FixturesComponent>(ent.Owner, out var fixtures))
        {
            var keep = (int) SolidMask;
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                var mask = fixture.CollisionMask & keep;
                if (mask == fixture.CollisionMask)
                    continue;

                ent.Comp.OriginalMasks[id] = fixture.CollisionMask;
                _physics.SetCollisionMask(ent.Owner, id, fixture, mask, fixtures);
            }
        }

        if (_tags.HasTag(ent.Owner, SharedDoorSystem.DoorBumpTag))
        {
            _tags.RemoveTag(ent.Owner, SharedDoorSystem.DoorBumpTag);
            ent.Comp.RemovedDoorBumpTag = true;
        }
    }

    private void RestorePhase(Entity<CMUVeteranCloakComponent> ent)
    {
        if (ent.Comp.OriginalMasks.Count > 0 &&
            !TerminatingOrDeleted(ent.Owner) &&
            TryComp<FixturesComponent>(ent.Owner, out var fixtures))
        {
            foreach (var (id, mask) in ent.Comp.OriginalMasks)
            {
                if (!fixtures.Fixtures.TryGetValue(id, out var fixture))
                    continue;

                _physics.SetCollisionMask(ent.Owner, id, fixture, mask, fixtures);
            }
        }

        ent.Comp.OriginalMasks.Clear();

        if (ent.Comp.RemovedDoorBumpTag && !TerminatingOrDeleted(ent.Owner))
        {
            _tags.AddTag(ent.Owner, SharedDoorSystem.DoorBumpTag);
            ent.Comp.RemovedDoorBumpTag = false;
        }
    }

    /// <summary>
    ///     Anything he is holding vanishes with him, otherwise a floating machete would give the field away.
    /// </summary>
    private void HideHeldItems(EntityUid uid)
    {
        if (!TryComp<CMUVeteranCloakComponent>(uid, out var cloak) ||
            !TryComp<HandsComponent>(uid, out var hands))
        {
            return;
        }

        RestoreHeldItems((uid, cloak));

        foreach (var held in _hands.EnumerateHeld((uid, hands)))
        {
            if (TerminatingOrDeleted(held) || HasComp<EntityActiveInvisibleComponent>(held))
                continue;

            var active = EnsureComp<EntityActiveInvisibleComponent>(held);
            active.Opacity = 0f;
            active.DisableMobCollision = true;
            Dirty(held, active);
            EnsureComp<EntityTurnInvisibleComponent>(held);
            cloak.HiddenItems.Add(held);
        }
    }

    private void RestoreHeldItems(Entity<CMUVeteranCloakComponent> ent)
    {
        foreach (var held in ent.Comp.HiddenItems)
        {
            if (TerminatingOrDeleted(held))
                continue;

            RemComp<EntityActiveInvisibleComponent>(held);
            RemComp<EntityTurnInvisibleComponent>(held);
        }

        ent.Comp.HiddenItems.Clear();
    }

    #region Interaction gates

    private bool IsLocked(Entity<CMUVeteranCloakComponent> ent)
    {
        return ent.Comp.Cloaked || _timing.CurTime < ent.Comp.UncloakLockUntil;
    }

    private void OnInteractionAttempt(Entity<CMUVeteranCloakComponent> ent, ref InteractionAttemptEvent args)
    {
        if (!IsLocked(ent))
            return;

        // Target-less checks are the action system validating a button press (and the UI gates). Blocking those
        // would also block the cloak's own toggle, which is the only way back out. Everything with a real target
        // is refused, and use / drop / pickup / attack / throw / shooting / storage each have their own gate.
        if (args.Target == null)
            return;

        args.Cancelled = true;
        DenyPopup(ent);
    }

    private void OnUseAttempt(Entity<CMUVeteranCloakComponent> ent, ref UseAttemptEvent args)
    {
        if (!IsLocked(ent))
            return;

        args.Cancel();
        DenyPopup(ent);
    }

    private void OnDropAttempt(Entity<CMUVeteranCloakComponent> ent, ref DropAttemptEvent args)
    {
        if (!IsLocked(ent))
            return;

        args.Cancel();
    }

    private void OnPickupAttempt(Entity<CMUVeteranCloakComponent> ent, ref PickupAttemptEvent args)
    {
        if (!IsLocked(ent))
            return;

        args.Cancel();
        DenyPopup(ent);
    }

    private void OnAttackAttempt(Entity<CMUVeteranCloakComponent> ent, ref AttackAttemptEvent args)
    {
        if (!IsLocked(ent))
            return;

        args.Cancel();
        DenyPopup(ent);
    }

    private void OnBeforeThrow(Entity<CMUVeteranCloakComponent> ent, ref BeforeThrowEvent args)
    {
        if (!IsLocked(ent))
            return;

        args.Cancelled = true;
        DenyPopup(ent);
    }

    private void OnStartPullAttempt(Entity<CMUVeteranCloakComponent> ent, ref StartPullAttemptEvent args)
    {
        if (!IsLocked(ent))
            return;

        args.Cancel();
        DenyPopup(ent);
    }

    private void OnSpeakAttempt(Entity<CMUVeteranCloakComponent> ent, ref SpeakAttemptEvent args)
    {
        // Silent while the field is up: no talking, no shouting, nothing that gives the position away.
        if (!ent.Comp.Cloaked)
            return;

        args.Cancel();
    }

    private void OnStorageInteract(ref StorageInteractAttemptEvent args)
    {
        if (!TryComp<CMUVeteranCloakComponent>(args.User, out var cloak) || !IsLocked((args.User, cloak)))
            return;

        args.Cancelled = true;
        DenyPopup((args.User, cloak));
    }

    private void OnShotAttempted(ref ShotAttemptedEvent args)
    {
        if (args.Cancelled ||
            !TryComp<CMUVeteranCloakComponent>(args.User, out var cloak) ||
            !IsLocked((args.User, cloak)))
        {
            return;
        }

        args.Cancel();
        DenyPopup((args.User, cloak));
    }

    private void DenyPopup(Entity<CMUVeteranCloakComponent> ent)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextDeniedPopup)
            return;

        ent.Comp.NextDeniedPopup = now + ent.Comp.DeniedPopupCooldown;
        _popups.PopupEntity(Loc.GetString("cmu-veteran-cloak-denied"), ent, ent, PopupType.SmallCaution);
    }

    #endregion
}
