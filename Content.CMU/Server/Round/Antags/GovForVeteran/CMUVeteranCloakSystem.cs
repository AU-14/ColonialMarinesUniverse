using Content.Server.Chat.Managers;
using Content.Shared._RMC14.Armor.ThermalCloak;
using Content.Shared.Actions.Events;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Chat;
using Content.Shared.CMU14.Antags.GovForVeteran;
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
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
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
        CollisionGroup.LowImpassable |
        CollisionGroup.GhostImpassable |
        CollisionGroup.BarricadeImpassable |
        CollisionGroup.BarbedBarricade;

    /// <summary>
    ///     Danger red used for the reveal line, matching the game's span_danger styling.
    /// </summary>
    private const string RevealDangerColor = "#c40000";

    /// <summary>
    ///     The field controls are locked behind the antagonist skill; the veteran carries it, nobody else does.
    /// </summary>
    private static readonly EntProtoId<SkillDefinitionComponent> AntagonistSkill = "RMCSkillAntagonist";

    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // The cloak's own system owns ThermalCloakTurnInvisibleActionEvent, so the gate sits one step earlier:
        // its action is simply invalid while a watched veteran presses it.
        SubscribeLocalEvent<ActionValidateEvent>(OnActionValidate);

        SubscribeLocalEvent<EntityActiveInvisibleComponent, ComponentStartup>(OnCloakStarted);
        SubscribeLocalEvent<EntityActiveInvisibleComponent, ComponentShutdown>(OnCloakStopped);

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

        // These are raised on the storage / the gun rather than the user, so they need a targeted lookup.
        SubscribeLocalEvent<StorageInteractAttemptEvent>(OnStorageInteract);
        SubscribeLocalEvent<ShotAttemptedEvent>(OnShotAttempted);
    }

    /// <summary>
    ///     Only lets the field go up or down while nobody can see him: a watched attempt invalidates the cloak
    ///     toggle before it runs, with feedback.
    /// </summary>
    private void OnActionValidate(ref ActionValidateEvent args)
    {
        if (args.Invalid)
            return;

        if (!HasComp<ThermalCloakComponent>(args.Provider))
            return;

        // Locked behind the antagonist skill: without it the field controls do not respond at all.
        if (_skills.GetSkill(args.User, AntagonistSkill) < 1)
        {
            args.Invalid = true;
            _popups.PopupEntity(Loc.GetString("cmu-veteran-cloak-unskilled"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        // Past this point only the veteran answers to the watching rule.
        if (!TryComp<CMUVeteranCloakComponent>(args.User, out var cloak))
            return;

        if (!IsWatched((args.User, cloak)))
            return;

        args.Invalid = true;

        var message = IsCloaked(args.User)
            ? "cmu-veteran-cloak-watched-uncloak"
            : "cmu-veteran-cloak-watched";
        _popups.PopupEntity(Loc.GetString(message), args.User, args.User, PopupType.MediumCaution);
    }

    private void OnCloakStarted(Entity<EntityActiveInvisibleComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<CMUVeteranCloakComponent>(ent.Owner, out var cloak))
            return;

        MakePhaseable((ent.Owner, cloak));
        HideHeldItems(ent.Owner);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnCloakStopped(Entity<EntityActiveInvisibleComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<CMUVeteranCloakComponent>(ent.Owner, out var cloak))
            return;

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
        if (!IsCloaked(ent.Owner) || !_mobState.IsIncapacitated(ent.Owner))
            return;

        // Going down drops the field: never leave an invisible body behind.
        RemComp<EntityActiveInvisibleComponent>(ent.Owner);
    }

    private void OnShutdown(Entity<CMUVeteranCloakComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Cleaning = true;
        try
        {
            if (IsCloaked(ent.Owner) && !TerminatingOrDeleted(ent.Owner))
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
        if (!IsCloaked(ent.Owner))
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

            if (_interaction.InRangeUnobstructed(observer,
                    veteran,
                    ent.Comp.SightRange,
                    SightMask,
                    predicate: uid => uid == veteran,
                    lagCompensate: false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells everyone nearby that the field dropped, once per drop.
    /// </summary>
    private void NotifyReveal(Entity<CMUVeteranCloakComponent> ent)
    {
        var message = Loc.GetString("cmu-veteran-cloak-reveal");
        var wrapped = $"[bold][color={RevealDangerColor}]{message}[/color][/bold]";
        var filter = Filter.Empty().AddInRange(_transform.GetMapCoordinates(ent.Owner), ent.Comp.RevealRange);
        _chat.ChatMessageToManyFiltered(filter,
            ChatChannel.Notifications,
            message,
            wrapped,
            EntityUid.Invalid,
            false,
            true,
            Color.FromHex(RevealDangerColor));
    }

    /// <summary>
    ///     Drops the collision bits that make doors solid while the field is up, and takes the door-bump tag off
    ///     him so a closed airlock neither blocks him, opens for him, nor buzzes at him. Walls keep their
    ///     Impassable bit, so he still cannot walk through them.
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

    private bool IsCloaked(EntityUid uid)
    {
        return HasComp<EntityActiveInvisibleComponent>(uid);
    }

    private bool IsLocked(Entity<CMUVeteranCloakComponent> ent)
    {
        return IsCloaked(ent.Owner) || _timing.CurTime < ent.Comp.UncloakLockUntil;
    }

    private void OnInteractionAttempt(Entity<CMUVeteranCloakComponent> ent, ref InteractionAttemptEvent args)
    {
        if (!IsLocked(ent))
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
