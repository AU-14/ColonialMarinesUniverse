using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Weapons.Ranged.Chamber;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Expeditions;

/// <summary>
/// Sight-limited infantry decisions. Native steering, weapons and medical do-afters execute actions.
/// Only expedition guards without a controlling player participate.
/// </summary>
public sealed partial class CMUExpeditionAgentSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private GunSystem _guns = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private NPCSystem _npcs = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedWieldableSystem _wield = default!;

    private const float ArrivalRange = 0.25f;
    private static readonly TimeSpan ThinkInterval = TimeSpan.FromSeconds(0.15);

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUExpeditionAgentComponent, MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<CMUExpeditionAgentComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<CMUExpeditionAgentComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUExpeditionAgentComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<CMUExpeditionWeaponComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<CMUExpeditionWeaponComponent, TakeAmmoEvent>(OnTakeAmmo,
            before: new[] { typeof(RMCGunChamberSystem), typeof(SharedGunSystem) });
        InitializeMedicine();
        InitializeTactics();
        InitializeRadio();
        InitializeEquipment();
        InitializeLearning();
    }

    private void OnMobState(Entity<CMUExpeditionAgentComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            Stop(ent);
    }

    private void OnPlayerAttached(Entity<CMUExpeditionAgentComponent> ent, ref PlayerAttachedEvent args) => Stop(ent);
    private void OnShutdown(Entity<CMUExpeditionAgentComponent> ent, ref ComponentShutdown args) => Stop(ent);

    private void Stop(Entity<CMUExpeditionAgentComponent> ent)
    {
        CancelWork(ent, ent.Comp);
        CancelPlan(ent, ent.Comp, false);
        CancelTreatment(ent.Comp);
        RemComp<NPCRangedCombatComponent>(ent);
        _steering.Unregister(ent);
        RemComp<ActiveNPCComponent>(ent);
        ent.Comp.Target = null;
        ent.Comp.LastSeen = null;
        ClearCover(ent.Comp);
        ent.Comp.State = CMUExpeditionAgentState.Disabled;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        _bodyClearCache.Clear();
        _grenadeHazards.RemoveAll(hazard => hazard.Until <= now);
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var agent, out var transform))
        {
            if (!_npcs.Enabled || HasComp<ActorComponent>(uid) || !_mobs.IsAlive(uid))
            {
                if (agent.State != CMUExpeditionAgentState.Disabled)
                    Stop((uid, agent));
                continue;
            }
            if (agent.NextThink <= now)
            {
                agent.NextThink = now + ThinkInterval;
                agent.Home ??= transform.Coordinates;
                LoadExperience(uid, agent);
                // RMC removes this at map init from humans without a player. This body is AI-owned.
                EnsureComp<InputMoverComponent>(uid);
                EnsureComp<ActiveNPCComponent>(uid);
                // Expedition fire has its own lead and trigger discipline; never run two gun controllers.
                RemComp<NPCRangedCombatComponent>(uid);
                Think(uid, agent, transform, now);
            }
            UpdateFire(uid, agent, now);
        }
    }

    private void Think(EntityUid uid, CMUExpeditionAgentComponent agent, TransformComponent transform, TimeSpan now)
    {
        if (agent.Home is not { } home || agent.OrderedDestination == null && !_transform.InRange(transform.Coordinates, home, agent.LeashRange))
        {
            CancelTreatment(agent);
            agent.Target = null;
            agent.LastSeen = null;
            ClearCover(agent);
            agent.State = CMUExpeditionAgentState.Guard;
            if (agent.Home is { } returnTo && Exists(returnTo.EntityId))
                Move(uid, returnTo);
            return;
        }

        ReceiveContact(uid, agent, now);
        var seen = Observe(uid, agent, transform, now);
        var damage = _damage.GetTotalDamage(uid).Float();
        var hit = damage > agent.LastDamage + 0.1f;
        agent.LastDamage = damage;
        if (seen != null || hit || now < agent.SuppressedUntil)
            CancelWork(uid, agent);
        if (hit)
        {
            agent.LastHit = now;
            agent.Stress = Math.Min(1, agent.Stress + 0.3f);
            agent.SuppressedUntil = now + TimeSpan.FromSeconds(1.5);
            if (agent.CoverAnchor != null && agent.State is CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.HoldAngle)
            {
                agent.RepeatedPeekHits = now - agent.LastPeekHit < TimeSpan.FromSeconds(15) ? agent.RepeatedPeekHits + 1 : 1;
                agent.LastPeekHit = now;
                RecordTactic(uid, agent, false, false);
                if (agent.RepeatedPeekHits >= 2 && agent.PeekPosition is { } exposed)
                {
                    agent.FailedPosition = exposed;
                    agent.AvoidPositionUntil = now + TimeSpan.FromSeconds(12);
                }
            }
        }
        if (damage >= agent.HealDamage)
            agent.WoundedSince ??= now;
        else
            agent.WoundedSince = null;
        UpdateEmotions(uid, agent, damage, now);

        if (agent.State == CMUExpeditionAgentState.Healing)
        {
            if (!hit && TreatmentSafe(uid, agent))
                return;
            CancelTreatment(agent);
            agent.State = CMUExpeditionAgentState.Guard;
            agent.NextRetreat = now;
        }

        var hasAmmo = ReadyRifle(uid, agent, damage);
        if (RunPlan(uid, agent, hasAmmo, damage, hit, now))
            return;
        if (agent.State != CMUExpeditionAgentState.Retreat && agent.State != CMUExpeditionAgentState.OutOfAmmo &&
            (!hasAmmo || damage >= agent.RetreatDamage && now >= agent.NextRetreat &&
                HasMedicine(uid) && ShouldTreat(agent, damage, now)))
        {
            BeginRetreat(uid, agent, transform, hasAmmo, now);
        }

        if (agent.State is CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.OutOfAmmo)
        {
            if (ContinueMove(uid, agent, transform, now))
                return;
            if (TryTreat(uid, agent, damage, now))
                return;
            if (!hasAmmo || now < agent.HoldUntil)
                return;
            agent.State = agent.CoverAnchor != null ? CMUExpeditionAgentState.Recover : CMUExpeditionAgentState.Guard;
            agent.FireAt = now;
        }

        // A fresh hit interrupts a peek immediately; the return position is fixed, not another random move.
        if ((hit || now < agent.SuppressedUntil) && agent.CoverAnchor is { } shelter &&
            agent.State is CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.HoldAngle)
        {
            BeginMove(uid, agent, shelter, CMUExpeditionAgentState.Withdraw, now);
            return;
        }

        if (agent.State is CMUExpeditionAgentState.Reposition or CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.Withdraw)
        {
            if (ContinueMove(uid, agent, transform, now))
                return;
            if (agent.State == CMUExpeditionAgentState.Peeking)
                Aim(agent, now, true);
            else if (agent.CoverAnchor == null)
                agent.State = CMUExpeditionAgentState.Guard;
            else
            {
                agent.FollowupBursts = 0;
                agent.State = CMUExpeditionAgentState.Recover;
                agent.FireAt = now + RecoveryDelay(agent);
            }
        }

        if (agent.State == CMUExpeditionAgentState.HoldAngle)
        {
            if (seen != null)
                return;
            EndBurst(uid, agent, now, false);
            return;
        }

        if (agent.State == CMUExpeditionAgentState.Recover && agent.CoverAnchor != null)
        {
            _steering.Unregister(uid);
            if (TryTreat(uid, agent, damage, now))
                return;
            if (agent.LastSeen is { } threat && now < agent.ForgetAt && agent.PeekPosition is { } peek &&
                Sheltered(uid, transform.Coordinates, threat) &&
                !(agent.FailedPosition is { } failed && now < agent.AvoidPositionUntil && _transform.InRange(peek, failed, 1.4f)))
            {
                // Ready the rifle while hidden, then evaluate the same cone the trigger will use.
                if (now < agent.FireAt || now < agent.SuppressedUntil || !CanLeaveCover(uid, agent))
                    return;
                if (!GrenadeDanger(peek) && FiringLaneClear(uid, peek, threat))
                {
                    BeginMove(uid, agent, peek, CMUExpeditionAgentState.Peeking, now);
                    return;
                }
            }
            ClearCover(agent); // Destroyed cover or a changed attack angle requires a new solution.
            agent.NextReposition = now;
            agent.State = CMUExpeditionAgentState.Guard;
        }

        if (seen == null)
        {
            if (TryTreat(uid, agent, damage, now))
                return;
            if (agent.LastSeen is { } lastSeen && now < agent.ForgetAt)
            {
                if (now < agent.LastContact + agent.LostSightDelay)
                {
                    agent.State = CMUExpeditionAgentState.Watch;
                    _steering.Unregister(uid);
                }
                else
                {
                    agent.State = CMUExpeditionAgentState.Investigate;
                    Move(uid, lastSeen);
                }
            }
            else
            {
                agent.Target = null;
                agent.LastSeen = null;
                ClearCover(agent);
                agent.State = CMUExpeditionAgentState.Guard;
                if (agent.OrderedDestination is { } order)
                {
                    if (_transform.InRange(transform.Coordinates, order, 0.5f))
                    {
                        agent.Home = order;
                        agent.OrderedDestination = null;
                        _steering.Unregister(uid);
                    }
                    else
                        Move(uid, order);
                }
                else if (!TryFortify(uid, agent, now))
                    Move(uid, home);
            }
            return;
        }

        if (now < agent.SuppressedUntil && agent.CoverAnchor == null && now >= agent.NextSuppressionResponse)
        {
            agent.NextSuppressionResponse = now + TimeSpan.FromSeconds(2);
            if (FindPosition(uid, agent, transform, true) is { } refuge)
            {
                BeginMove(uid, agent, refuge.Anchor, CMUExpeditionAgentState.Retreat, now);
                agent.HoldUntil = agent.SuppressedUntil;
                return;
            }
        }

        var separation = Vector2.Distance(_transform.GetWorldPosition(uid), _transform.GetWorldPosition(seen.Value));
        if (separation > agent.FireRange)
        {
            ClearCover(agent);
            agent.State = CMUExpeditionAgentState.Investigate;
            Move(uid, agent.LastSeen!.Value);
            return;
        }

        if (agent.CoverAnchor == null && now >= agent.NextReposition)
        {
            agent.NextReposition = now + agent.RepositionCooldown;
            if (FindPosition(uid, agent, transform, false) is { } position)
            {
                agent.CoverAnchor = position.Anchor;
                agent.PeekPosition = position.Peek;
                BeginMove(uid, agent, position.Anchor, CMUExpeditionAgentState.Reposition, now);
                return;
            }
        }

        _steering.Unregister(uid);
        if (agent.State is not (CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.Recover))
            Aim(agent, now);
    }

    private EntityUid? Observe(EntityUid uid, CMUExpeditionAgentComponent agent, TransformComponent transform, TimeSpan now)
    {
        EntityUid? seen = null;
        var nearest = agent.DetectionRange * agent.DetectionRange;
        agent.VisibleThreats.Clear();
        foreach (var hostile in ExpeditionHostiles(uid, agent))
        {
            if (!_mobs.IsAlive(hostile) || !TryComp<TransformComponent>(hostile, out var targetTransform) ||
                targetTransform.MapID != transform.MapID || !Visible(uid, hostile, agent.DetectionRange))
                continue;
            agent.VisibleThreats.Add(targetTransform.Coordinates);
            var distance = Vector2.DistanceSquared(_transform.GetWorldPosition(transform), _transform.GetWorldPosition(targetTransform));
            if (distance >= nearest && hostile != agent.Target || seen != null && seen == agent.Target)
                continue;
            nearest = distance;
            seen = hostile;
        }
        if (seen is { } target)
        {
            if (agent.LastSeen == null || now - agent.LastContact > TimeSpan.FromSeconds(30))
                agent.FirstContact = now;
            if (agent.Target != target)
            {
                if (agent.Action != null)
                    CancelPlan(uid, agent, false);
                agent.RepeatedPeekHits = 0;
                ClearCover(agent);
                if (agent.State is CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.Recover)
                    agent.State = CMUExpeditionAgentState.Guard;
            }
            agent.Target = target;
            agent.LastSeen = Transform(target).Coordinates;
            agent.ForgetAt = now + agent.MemoryDuration;
            agent.LastContact = now;
            agent.ContactFromRadio = false;
            ShareContact(uid, agent, target, now);
        }
        return seen;
    }

    private bool ReadyRifle(EntityUid uid, CMUExpeditionAgentComponent agent, float damage)
    {
        if (!_guns.TryGetGun(uid, out var gun))
            return false;
        EnsureComp<CMUExpeditionWeaponComponent>(gun);
        var moving = agent.State is CMUExpeditionAgentState.Reposition or CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.OutOfAmmo or
            CMUExpeditionAgentState.PlanMove or CMUExpeditionAgentState.Reloading or CMUExpeditionAgentState.Rescuing or CMUExpeditionAgentState.Throwing;
        if (moving || agent.WorkItem != null || agent.Entrench && agent.State == CMUExpeditionAgentState.Guard && agent.LastSeen == null ||
            HasMedicine(uid) && ShouldTreat(agent, damage, _timing.CurTime) && TreatmentSafe(uid, agent))
            _wield.TryUnwield(gun.Owner, uid);
        else if (TryComp<WieldableComponent>(gun, out var wieldable) && !wieldable.Wielded)
            _wield.TryWield((gun.Owner, wieldable), uid);
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(gun, ref ammo);
        return ammo.Count > 0;
    }

    private static void Aim(CMUExpeditionAgentComponent agent, TimeSpan now, bool peek = false)
    {
        agent.State = CMUExpeditionAgentState.Aim;
        agent.FireAt = now + (peek ? agent.PeekAimDuration : agent.AimDuration);
    }

    private void BeginRetreat(EntityUid uid, CMUExpeditionAgentComponent agent, TransformComponent transform, bool hasAmmo, TimeSpan now)
    {
        ClearCover(agent);
        var position = FindPosition(uid, agent, transform, true);
        BeginMove(uid, agent, position?.Anchor ?? agent.Home!.Value,
            hasAmmo ? CMUExpeditionAgentState.Retreat : CMUExpeditionAgentState.OutOfAmmo, now);
        agent.HoldUntil = now + TimeSpan.FromSeconds(2);
        agent.NextRetreat = now + TimeSpan.FromSeconds(6);
    }

    private static void ClearCover(CMUExpeditionAgentComponent agent)
    {
        agent.CoverDestination = null;
        agent.CoverAnchor = null;
        agent.PeekPosition = null;
        agent.Route.Clear();
        agent.RouteDestination = null;
    }

    private void BeginMove(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates destination, CMUExpeditionAgentState state, TimeSpan now)
    {
        agent.State = state;
        agent.LastMoveFailed = false;
        agent.CoverDestination = destination;
        agent.MoveUntil = now + agent.RepositionTimeout;
        if (state == CMUExpeditionAgentState.Peeking)
            agent.PeekInitialDamage = agent.LastDamage;
        if (state is CMUExpeditionAgentState.Reposition or CMUExpeditionAgentState.Retreat &&
            agent.LastSeen != null && !_transform.InRange(Transform(uid).Coordinates, destination, 2) && !BuildTacticalRoute(uid, agent, destination))
        {
            agent.MoveUntil = now;
            _steering.Unregister(uid);
            return;
        }
        Move(uid, destination, state == CMUExpeditionAgentState.Peeking);
    }

    private bool ContinueMove(EntityUid uid, CMUExpeditionAgentComponent agent, TransformComponent transform, TimeSpan now)
    {
        if (agent.CoverDestination is not { } destination)
            return false;
        var precise = agent.State == CMUExpeditionAgentState.Peeking;
        // Native steering can oscillate around a tiny sub-tile radius. Stop as soon as the actual
        // stance near the destination has the required firing cone, not at an arbitrary tile centre.
        var clearStance = precise && agent.LastSeen is { } threat &&
            _transform.InRange(transform.Coordinates, destination, 0.7f) &&
            FiringLaneClear(uid, transform.Coordinates, threat);
        if (clearStance || _transform.InRange(transform.Coordinates, destination, precise ? 0.12f : ArrivalRange))
        {
            _steering.Unregister(uid);
            agent.CoverDestination = null;
            agent.Route.Clear();
            agent.RouteDestination = null;
            if (clearStance)
                agent.PeekPosition = transform.Coordinates;
            return false;
        }
        if (now >= agent.MoveUntil || TryComp<NPCSteeringComponent>(uid, out var steering) && steering.Status == SteeringStatus.NoPath)
        {
            _steering.Unregister(uid);
            agent.FailedPosition = destination;
            agent.LastMoveFailed = true;
            agent.AvoidPositionUntil = now + TimeSpan.FromSeconds(8);
            if (agent.State == CMUExpeditionAgentState.Peeking)
                agent.State = CMUExpeditionAgentState.Guard;
            ClearCover(agent);
            agent.NextReposition = now + TimeSpan.FromSeconds(1);
            return false;
        }
        Move(uid, destination, precise);
        return true;
    }

    private bool Visible(EntityUid observer, EntityUid target, float range) =>
        _interaction.InRangeUnobstructed(observer, target, range,
            CollisionGroup.Impassable | CollisionGroup.InteractImpassable,
            predicate: entity => entity == observer || entity == target || HasComp<NpcFactionMemberComponent>(entity));

    private void Move(EntityUid uid, EntityCoordinates destination, bool precise = false)
    {
        if (TryComp<CMUExpeditionAgentComponent>(uid, out var moving) &&
            moving.RouteDestination != destination && moving.State == CMUExpeditionAgentState.Investigate &&
            !BuildTacticalRoute(uid, moving, destination))
        {
            _steering.Unregister(uid);
            moving.State = CMUExpeditionAgentState.Watch;
            return;
        }
        if (TryComp<CMUExpeditionAgentComponent>(uid, out var agent) && agent.RouteDestination == destination)
        {
            while (agent.Route.TryPeek(out var point) && _transform.InRange(Transform(uid).Coordinates, point, 0.2f))
                agent.Route.Dequeue();
            if (agent.Route.TryPeek(out var waypoint))
                destination = waypoint;
        }
        if (_transform.InRange(Transform(uid).Coordinates, destination, precise ? 0.1f : ArrivalRange))
        {
            _steering.Unregister(uid);
            return;
        }
        if (TryComp<NPCSteeringComponent>(uid, out var existing) && existing.Status != SteeringStatus.NoPath &&
            _transform.InRange(existing.Coordinates, destination, 0.1f))
            return;
        if (existing?.Status == SteeringStatus.NoPath)
            _steering.Unregister(uid, existing);
        var steering = _steering.Register(uid, destination);
        steering.Range = precise ? 0.1f : 0.18f;
        steering.ArriveOnLineOfSight = false;
    }
}
