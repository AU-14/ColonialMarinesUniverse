using System.Numerics;
using Content.Server.Actions;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Humanoid;
using Content.Server.Humanoid.Systems;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Threats.Mobs.Threat;
using Content.Shared.CMU14.Medical.Injuries.Vision;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Atmos.Components;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._RMC14.Line;
using Content.Shared._RMC14.Input;
using Content.Shared._RMC14.Sprite;
using Content.Shared._RMC14.Tackle;
using Content.Shared._RMC14.Xenonids.Leap;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Movement.Events;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.CMU14.Threats.Mobs.Threat;

public sealed partial class CMUThreatSystem : EntitySystem
{
    private const float UpdateInterval = 0.25f;
    private const float MaintenanceUpdateInterval = 1f;
    private const string TargetKey = "Target";
    private const string TargetCoordinatesKey = "TargetCoordinates";
    private const string TargetPathfindKey = "TargetPathfind";
    private const string RallyCoordinatesKey = "RallyCoordinates";
    private const string RallyPathfindKey = "RallyPathfind";

    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private CMUTemporaryBlurryVisionSystem _blur = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private LineSystem _line = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private HumanoidOrganAppearanceSystem _appearance = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCSpriteSystem _rmcSprite = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _threatByTarget = new();
    private float _updateAccumulator;
    private float _maintenanceUpdateAccumulator;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUThreatComponent, MapInitEvent>(OnMapInit,
            after: [typeof(RandomHumanoidAppearanceSystem), typeof(SSDIndicatorSystem)]);
        SubscribeLocalEvent<CMUThreatComponent, PlayerDetachedEvent>(OnPlayerDetached,
            after: [typeof(SSDIndicatorSystem)]);
        SubscribeLocalEvent<CMUThreatComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CMUThreatComponent, DamageChangedEvent>(OnThreatDamaged,
            after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<CMUThreatComponent, MeleeHitEvent>(OnThreatMeleeHit);
        SubscribeLocalEvent<MobStateChangedEvent>(OnPotentialTargetStateChanged);
        SubscribeLocalEvent<CMUThreatRallyPointComponent, MapInitEvent>(OnRallyPointMapInit,
            after: [typeof(RandomHumanoidAppearanceSystem), typeof(SSDIndicatorSystem)]);
        SubscribeLocalEvent<CMUThreatComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUThreatPinComponent, ComponentShutdown>(OnPinShutdown);
        SubscribeLocalEvent<CMUThreatPinComponent, RefreshMovementSpeedModifiersEvent>(OnPinRefreshSpeed);
        SubscribeLocalEvent<CMUThreatPinComponent, UpdateCanMoveEvent>(OnPinUpdateCanMove);
        SubscribeLocalEvent<CMUHunterThreatComponent, UpdateCanMoveEvent>(OnHunterUpdateCanMove);
        SubscribeLocalEvent<CMUSmokerThreatComponent, UpdateCanMoveEvent>(OnSmokerUpdateCanMove);
        SubscribeLocalEvent<CMUThreatPinComponent, CMDisarmEvent>(OnPinnedVictimDisarmed);
        SubscribeLocalEvent<CMUThreatComponent, CMDisarmEvent>(OnThreatDisarmed);
        SubscribeLocalEvent<CMUThreatComponent, XenoLeapHitAttempt>(OnThreatLeapHitAttempt);
        SubscribeLocalEvent<CMUThreatTongueSegmentComponent, DamageChangedEvent>(OnTongueDamaged,
            after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<CMUWitchThreatComponent, DamageChangedEvent>(OnWitchDamaged,
            after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<CMUScreamerThreatComponent, DamageChangedEvent>(OnScreamerDamaged,
            after: [typeof(MobThresholdSystem)]);

        SubscribeLocalEvent<CMUHunterThreatComponent, XenoLeapHitEvent>(OnHunterLeapHit);
        SubscribeLocalEvent<CMUSmokerThreatComponent, CMUSmokerTongueActionEvent>(OnSmokerAction);
        SubscribeLocalEvent<CMUBoomerThreatComponent, CMUBoomerVomitActionEvent>(OnBoomerAction);
        SubscribeLocalEvent<CMUJockeyThreatComponent, CMUJockeyRideActionEvent>(OnJockeyAction);
        SubscribeLocalEvent<CMUSpitterThreatComponent, CMUSpitterAcidActionEvent>(OnSpitterAction);
        SubscribeLocalEvent<CMUHunterPouncingComponent, StartCollideEvent>(OnHunterCollide);
        SubscribeLocalEvent<CMUHunterPouncingComponent, PhysicsSleepEvent>(OnHunterSleep);
        SubscribeLocalEvent<CMUHunterPouncingComponent, ComponentRemove>(OnHunterPounceRemove);
        SubscribeLocalEvent<CMUJockeyLeapingComponent, StartCollideEvent>(OnJockeyCollide);
        SubscribeLocalEvent<CMUJockeyLeapingComponent, PhysicsSleepEvent>(OnJockeySleep);
        SubscribeLocalEvent<CMUJockeyLeapingComponent, ComponentRemove>(OnJockeyLeapRemove);
        SubscribeLocalEvent<CMUThreatSpitProjectileComponent, ProjectileHitEvent>(OnSpitProjectileHit);
    }

    public override void Update(float frameTime)
    {
        _updateAccumulator += frameTime;
        _maintenanceUpdateAccumulator += frameTime;

        if (_updateAccumulator >= UpdateInterval)
        {
            var elapsed = _updateAccumulator;
            _updateAccumulator = 0f;

            UpdatePins();
            UpdateHunterPounces();
            UpdateJockeyLeaps();
            UpdateScreamers();
            UpdateEmerging();
            UpdateAcidPools();
            UpdateSpitProjectiles();
            UpdateAi();
        }

        if (_maintenanceUpdateAccumulator >= MaintenanceUpdateInterval)
        {
            var elapsed = _maintenanceUpdateAccumulator;
            _maintenanceUpdateAccumulator = 0f;

            UpdateBile();
            UpdateBurningCorpses(elapsed);
            UpdateThreatSounds();
        }
    }

    private void OnMapInit(Entity<CMUThreatComponent> ent, ref MapInitEvent args)
    {
        RemoveSsdMarker(ent);
        ConfigureAiMover(ent);
        _appearance.TrySetEyeColor(ent.Owner, ent.Comp.EyeColor);
        ent.Comp.NextAiUpdate = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0f, ent.Comp.AiUpdateInterval));
        ScheduleNextIdleSound(ent.Comp);

        if (ent.Comp.CurrentTarget is { } target)
            TrackTarget(ent, target);

        if (TryComp(ent, out CMUWitchThreatComponent? witch))
        {
            witch.LastUpdateAt = _timing.CurTime;
            witch.NextUpdateAt = _timing.CurTime + WitchUpdateInterval;
            _npc.SleepNPC(ent);
        }

        var actions = EnsureComp<ActionsComponent>(ent);
        if (TryComp(ent, out CMUHunterThreatComponent? hunter))
            _actions.AddAction(ent, ref hunter.ActionEntity, hunter.Action, component: actions);
        if (TryComp(ent, out CMUSmokerThreatComponent? smoker))
            _actions.AddAction(ent, ref smoker.ActionEntity, smoker.Action, component: actions);
        if (TryComp(ent, out CMUBoomerThreatComponent? boomer))
            _actions.AddAction(ent, ref boomer.ActionEntity, boomer.Action, component: actions);
        if (TryComp(ent, out CMUJockeyThreatComponent? jockey))
            _actions.AddAction(ent, ref jockey.ActionEntity, jockey.Action, component: actions);
        if (TryComp(ent, out CMUSpitterThreatComponent? spitter))
            _actions.AddAction(ent, ref spitter.ActionEntity, spitter.Action, component: actions);
    }

    private void OnPlayerDetached(Entity<CMUThreatComponent> ent, ref PlayerDetachedEvent args)
    {
        RemoveSsdMarker(ent);
        ConfigureAiMover(ent);
    }

    private void RemoveSsdMarker(EntityUid uid)
    {
        RemComp<SSDIndicatorComponent>(uid);
        _statusEffects.TryRemoveStatusEffect(uid, SSDIndicatorSystem.StatusEffectSSDSleeping);
    }

    private void ConfigureAiMover(EntityUid uid)
    {
        RemComp<RMCActiveInputMoverComponent>(uid);
        EnsureComp<InputMoverComponent>(uid);
        EnsureComp<MobMoverComponent>(uid);
    }

    private void OnMobStateChanged(Entity<CMUThreatComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        PlayThreatSound(ent, ent.Comp.DeathSound);

        ReleasePinsByAttacker(ent);
        if (HasComp<CMUThreatPinComponent>(ent))
            RemComp<CMUThreatPinComponent>(ent);
        if (TryComp(ent, out CMUBoomerThreatComponent? boomer))
            ExplodeBoomer(ent, boomer);
        if (TryComp(ent, out CMUSmokerThreatComponent? smoker))
            Spawn(smoker.SmokePrototype, Transform(ent).Coordinates);
        ent.Comp.CorpseBurnElapsed = 0f;
    }

    private void OnThreatDamaged(Entity<CMUThreatComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased ||
            _mobState.IsDead(ent) ||
            _timing.CurTime < ent.Comp.NextHurtSound ||
            !_random.Prob(ent.Comp.HurtSoundChance))
        {
            return;
        }

        ent.Comp.NextHurtSound = _timing.CurTime + ent.Comp.HurtSoundCooldown;
        PlayThreatSound(ent, ent.Comp.HurtSound);
    }

    private void OnThreatMeleeHit(Entity<CMUThreatComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit ||
            args.User != ent.Owner ||
            args.HitEntities.Count == 0)
        {
            return;
        }

        foreach (var target in args.HitEntities)
            TryDelimb(ent, target, ent.Comp.DelimbChance);

        if (_timing.CurTime < ent.Comp.NextAttackSound)
            return;

        ent.Comp.NextAttackSound = _timing.CurTime + ent.Comp.AttackSoundCooldown;
        PlayThreatSound(ent, ent.Comp.AttackSound);
    }

    private void TryDelimb(EntityUid attacker, EntityUid target, float chance)
    {
        if (chance <= 0f ||
            !IsValidVictim(attacker, target) ||
            !_random.Prob(Math.Clamp(chance, 0f, 1f)) ||
            !TryComp<BodyComponent>(target, out _))
        {
            return;
        }

        var limbs = new List<(EntityUid Id, BodyPartComponent Part)>();
        foreach (var limb in _body.GetBodyChildren(target))
        {
            if (limb.Component.PartType is BodyPartType.Arm or BodyPartType.Leg)
                limbs.Add(limb);
        }

        if (limbs.Count == 0)
            return;

        var severed = _random.Pick(limbs);
        var ev = new BodyPartSeverAttemptEvent(target, severed.Id, severed.Part.PartType);
        RaiseLocalEvent(severed.Id, ref ev, broadcast: true);
    }

    private void UpdateThreatSounds()
    {
        var query = EntityQueryEnumerator<CMUThreatComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var threat, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive ||
                threat.State != CMUThreatAiState.Idle ||
                threat.CurrentTarget != null ||
                _timing.CurTime < threat.NextIdleSound)
            {
                continue;
            }

            PlayThreatSound(uid, threat.IdleSound);
            ScheduleNextIdleSound(threat);
        }
    }

    private void ScheduleNextIdleSound(CMUThreatComponent threat)
    {
        var min = (float) threat.IdleSoundMinInterval.TotalSeconds;
        var max = (float) threat.IdleSoundMaxInterval.TotalSeconds;
        threat.NextIdleSound = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(min, Math.Max(min, max)));
    }

    private void PlayThreatSound(EntityUid uid, Robust.Shared.Audio.SoundSpecifier? sound)
    {
        if (sound != null)
            _audio.PlayPvs(sound, uid);
    }

    private void UpdateBurningCorpses(float elapsed)
    {
        var query = EntityQueryEnumerator<CMUThreatComponent, MobStateComponent, FlammableComponent>();
        while (query.MoveNext(out var uid, out var threat, out var mobState, out var flammable))
        {
            if (mobState.CurrentState != MobState.Dead || !flammable.OnFire)
            {
                threat.CorpseBurnElapsed = 0f;
                continue;
            }

            threat.CorpseBurnElapsed += elapsed;
            if (threat.CorpseBurnElapsed < threat.CorpseBurnDuration)
                continue;

            PlayThreatSound(uid, threat.AshSound);
            Spawn("Ash", Transform(uid).Coordinates);
            QueueDel(uid);
        }
    }

    private void OnShutdown(Entity<CMUThreatComponent> ent, ref ComponentShutdown args)
    {
        UntrackTarget(ent, ent.Comp.CurrentTarget);
        ReleasePinsByAttacker(ent);
        if (HasComp<CMUThreatPinComponent>(ent))
            RemComp<CMUThreatPinComponent>(ent);
    }

    private bool IsValidVictim(EntityUid attacker, EntityUid victim)
    {
        return victim != attacker &&
               Exists(victim) &&
               !HasComp<CMUThreatComponent>(victim) &&
               HasComp<MobStateComponent>(victim) &&
               HasComp<DamageableComponent>(victim) &&
               _mobState.IsAlive(victim);
    }

    private bool IsValidBarricadeTarget(EntityUid target)
    {
        return Exists(target) &&
               HasComp<CMUThreatBarricadeTargetComponent>(target) &&
               HasComp<DamageableComponent>(target);
    }

    private bool IsValidLightTarget(EntityUid target)
    {
        return Exists(target) &&
               HasComp<CMUThreatLightTargetComponent>(target) &&
               HasComp<DamageableComponent>(target) &&
               TryComp(target, out PointLightComponent? light) &&
               light.Enabled;
    }

    private bool IsValidCombatTarget(EntityUid attacker, EntityUid target)
    {
        return IsValidVictim(attacker, target) ||
               IsValidBarricadeTarget(target) ||
               IsValidLightTarget(target);
    }

    private void OnPotentialTargetStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState is not (MobState.Critical or MobState.Dead))
            return;

        if (HasComp<CMUThreatPinComponent>(args.Target))
            RemComp<CMUThreatPinComponent>(args.Target);

        if (!_threatByTarget.Remove(args.Target, out var attackers))
            return;

        foreach (var uid in attackers)
        {
            if (TryComp(uid, out CMUThreatComponent? threat) && threat.CurrentTarget == args.Target)
                ClearNpcTarget(uid, threat);
        }
    }

    private void TrackTarget(EntityUid threat, EntityUid target)
    {
        if (!_threatByTarget.TryGetValue(target, out var attackers))
        {
            attackers = new HashSet<EntityUid>();
            _threatByTarget[target] = attackers;
        }

        attackers.Add(threat);
    }

    private void UntrackTarget(EntityUid threat, EntityUid? target)
    {
        if (target is not { } targetUid || !_threatByTarget.TryGetValue(targetUid, out var attackers))
            return;

        attackers.Remove(threat);
        if (attackers.Count == 0)
            _threatByTarget.Remove(targetUid);
    }

    private void OnRallyPointMapInit(Entity<CMUThreatRallyPointComponent> ent, ref MapInitEvent args)
    {
        var coordinates = _transform.GetMapCoordinates(ent);
        foreach (var threat in _lookup.GetEntitiesInRange<CMUThreatComponent>(coordinates, ent.Comp.Radius))
            SetRallyPoint(threat, ent);
    }

    private void OnThreatLeapHitAttempt(Entity<CMUThreatComponent> ent, ref XenoLeapHitAttempt args)
    {
        if (HasComp<CMUThreatComponent>(args.Leaper))
            args.Cancelled = true;
    }

    private bool TryDistance(EntityUid source, EntityUid target, out float distance)
    {
        distance = 0f;
        if (!Exists(source) || !Exists(target))
            return false;

        var from = _transform.GetMapCoordinates(source);
        var to = _transform.GetMapCoordinates(target);
        if (from.MapId != to.MapId)
            return false;

        distance = Vector2.Distance(from.Position, to.Position);
        return true;
    }

    private bool CanTarget(EntityUid attacker, EntityUid target, float range, bool requireSight = true)
    {
        return IsValidVictim(attacker, target) &&
               TryDistance(attacker, target, out var distance) &&
               distance <= range &&
               (!requireSight || _interaction.InRangeUnobstructed(attacker, target, range));
    }

    private void SetState(EntityUid uid, CMUThreatAiState state)
    {
        if (!TryComp(uid, out CMUThreatComponent? threat) || threat.State == state)
            return;
        threat.State = state;
        Dirty(uid, threat);
    }

    private void PlayAbilitySound(EntityUid uid, Robust.Shared.Audio.SoundSpecifier? sound)
    {
        if (sound != null)
            _audio.PlayPvs(sound, uid);
    }

    private void StopMover(EntityUid uid)
    {
        if (TryComp(uid, out PhysicsComponent? physics))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
    }
}
