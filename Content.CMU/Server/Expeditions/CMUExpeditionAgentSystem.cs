using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Weapons.Ranged.Chamber;
using Content.Shared.CMU14.Expeditions;
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
using Robust.Shared.Physics.Components;
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
        InitializeMedics();
        InitializeTactics();
        InitializeRadio();
        InitializeEquipment();
        InitializeRecovery();
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
        ClearScavenging(ent, ent.Comp);
        ReleaseManeuver(ent, ent.Comp);
        ClearTraffic(ent.Comp);
        ent.Comp.CoveringFor = null;
        ent.Comp.CoveringUntil = TimeSpan.Zero;
        ent.Comp.ContactDestination = null;
        ent.Comp.FlankAssignment = null;
        ent.Comp.FlankAssignmentUntil = TimeSpan.Zero;
        CancelWork(ent, ent.Comp);
        CancelPlan(ent, ent.Comp, false);
        CancelTreatment(ent.Comp);
        RemComp<NPCRangedCombatComponent>(ent);
        _steering.Unregister(ent);
        RemComp<ActiveNPCComponent>(ent);
        ent.Comp.Target = null;
        ent.Comp.LastSeen = null;
        ent.Comp.PendingWeapon = null;
        ent.Comp.MovingFire = false;
        ent.Comp.ContactMoveUntil = TimeSpan.Zero;
        ClearCover(ent.Comp);
        StopSpacing(ent, ent.Comp);
        ClearThreatAssessment(ent.Comp);
        ent.Comp.State = CMUExpeditionAgentState.Disabled;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        _orderRouteSearched = false;
        _bodyClearCache.Clear();
        _groundCache.Clear();
        _smokeTiles.Clear();
        _smokeScreens.RemoveAll(screen => screen.Until <= now);
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
            if (PauseForKnockdown(uid, agent))
                continue;
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
            CancelMedical(uid, agent, "outside-leash");
            CancelTreatment(agent);
            agent.Target = null;
            agent.LastSeen = null;
            ClearCover(agent);
            StopSpacing(uid, agent);
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
        ValidateCover(uid, agent, hit, now);

        if (RunMedic(uid, agent, damage, hit, now))
            return;
        if (RecoverWeapon(uid, agent, now) || ChooseWeapon(uid, agent, now))
            return;
        if (KeepCombatSpacing(uid, agent, now))
            return;

        if (agent.State == CMUExpeditionAgentState.Healing)
        {
            if (!hit && TreatmentSafe(uid, agent))
                return;
            CancelTreatment(agent);
            agent.State = CMUExpeditionAgentState.Guard;
            agent.NextRetreat = now;
        }

        var hasAmmo = ReadyRifle(uid, agent);
        if (hasAmmo)
            ClearScavenging(uid, agent);
        if (hasAmmo && HoldCoveringFire(uid, agent, now))
            return;
        if (hasAmmo && ContinueContactMovement(uid, agent, now))
            return;
        if (RunPlan(uid, agent, hasAmmo, damage, hit, now))
            return;
        if (!hasAmmo && RunAmmoFallback(uid, agent, now))
            return;
        // An exhausted weapon should not prevent an otherwise safe relocation or patrol.
        if (!hasAmmo && seen == null && (agent.LastSeen == null || now >= agent.ForgetAt) && agent.OrderedDestination != null)
        {
            agent.Target = null;
            agent.LastSeen = null;
            ClearCover(agent);
            agent.State = CMUExpeditionAgentState.Guard;
            FollowOrders(uid, agent, now);
            return;
        }
        if (agent.State != CMUExpeditionAgentState.Retreat && agent.State != CMUExpeditionAgentState.OutOfAmmo &&
            (!hasAmmo || damage >= agent.RetreatDamage && now >= agent.NextRetreat &&
                HasMedicine(uid) && ShouldTreat(agent, damage, now)))
        {
            BeginRetreat(uid, agent, transform, hasAmmo, now);
        }

        if (agent.State is CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.OutOfAmmo)
        {
            if (!hasAmmo && (hit || GrenadeDanger(transform.Coordinates)) && now >= agent.NextRetreat)
                BeginRetreat(uid, agent, transform, false, now);
            if (ContinueMove(uid, agent, transform, now))
                return;
            if (TryTreat(uid, agent, damage, now))
                return;
            if (!hasAmmo || now < agent.HoldUntil)
                return;
            agent.State = agent.CoverAnchor != null ? CMUExpeditionAgentState.Recover : CMUExpeditionAgentState.Guard;
            agent.FireAt = now;
        }

        // Suppression must not cancel every peek before its first shot. Ordinary pressure
        // can cut a volley short after firing; life-threatening damage can interrupt at once.
        if ((hit && damage >= agent.EmergencyHealDamage ||
                (hit || now < agent.SuppressedUntil) && now >= agent.NextSuppressionResponse &&
                agent.State == CMUExpeditionAgentState.Engage && agent.ShotsFired >= Math.Min(2, VolleySize(agent))) && agent.CoverAnchor is { } shelter &&
            agent.State is CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.HoldAngle)
        {
            agent.NextSuppressionResponse = now + TimeSpan.FromSeconds(3);
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
                ValidateCover(uid, agent, hit, now);
            }
        }

        if (agent.State == CMUExpeditionAgentState.HoldAngle)
        {
            if (seen != null)
            {
                if (now >= agent.PositionCommittedUntil && now >= agent.NextReposition)
                {
                    agent.NextReposition = now + agent.RepositionCooldown;
                    if (FindPosition(uid, agent, transform, false) is { } better && TryReserveManeuver(uid, agent, now))
                    {
                        agent.CoverAnchor = better.Anchor;
                        agent.PeekPosition = better.Peek;
                        agent.FightingPosition = null;
                        BeginMove(uid, agent, better.Anchor, CMUExpeditionAgentState.Reposition, now);
                    }
                }
                return;
            }
            EndBurst(uid, agent, now, false);
            return;
        }

        if (agent.State == CMUExpeditionAgentState.Recover && agent.CoverAnchor != null)
        {
            _steering.Unregister(uid);
            if (TryTreat(uid, agent, damage, now))
                return;
            if (agent.LastSeen is { } threat && now < agent.ForgetAt && agent.PeekPosition is { } peek &&
                ShelteredFromKnownThreats(uid, agent, transform.Coordinates) &&
                !(agent.FailedPosition is { } failed && now < agent.AvoidPositionUntil && _transform.InRange(peek, failed, 1.4f)))
            {
                // Ready the rifle while hidden, then evaluate the same cone the trigger will use.
                if (now < agent.FireAt || !CanLeaveCover(uid, agent))
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
            if (agent.State is CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage)
            {
                if (now - agent.LastContact >= TimeSpan.FromSeconds(0.35))
                    EndBurst(uid, agent, now, false);
                return;
            }
            if (TryTreat(uid, agent, damage, now))
                return;
            if (agent.LastSeen is { } lastSeen && now < agent.ForgetAt)
            {
                if (!agent.ContactFromRadio && now < agent.LastContact +
                    (agent.LastContactWasMelee ? TimeSpan.FromSeconds(3) : agent.LostSightDelay))
                {
                    agent.State = CMUExpeditionAgentState.Watch;
                    _steering.Unregister(uid);
                }
                else
                {
                    InvestigateContact(uid, agent, lastSeen, now);
                }
            }
            else
            {
                if (agent.ContactFromRadio)
                    agent.RadioDecision = "report-expired";
                agent.ContactFromRadio = false;
                agent.Target = null;
                agent.LastSeen = null;
                ClearCover(agent);
                agent.State = CMUExpeditionAgentState.Guard;
                if (!FollowOrders(uid, agent, now) && !TryFortify(uid, agent, now))
                    Move(uid, home);
            }
            return;
        }

        if (now < agent.SuppressedUntil && agent.CoverAnchor == null && now >= agent.NextSuppressionResponse &&
            agent.State == CMUExpeditionAgentState.Recover && now - agent.LastShotAt < TimeSpan.FromSeconds(1))
        {
            agent.NextSuppressionResponse = now + TimeSpan.FromSeconds(2);
            if (FindPosition(uid, agent, transform, true) is { } refuge)
            {
                agent.CoverAnchor = refuge.Anchor;
                BeginMove(uid, agent, refuge.Anchor, CMUExpeditionAgentState.Retreat, now);
                agent.HoldUntil = agent.SuppressedUntil;
                return;
            }
        }

        var separation = Vector2.Distance(_transform.GetWorldPosition(uid), _transform.GetWorldPosition(seen.Value));
        var engagementRange = WeaponFireRange(uid, agent);
        if (separation > engagementRange || agent.State == CMUExpeditionAgentState.Investigate && separation > engagementRange - 1.25f)
        {
            if (agent.State != CMUExpeditionAgentState.Investigate)
                ClearCover(agent);
            InvestigateContact(uid, agent, agent.LastSeen!.Value, now, visible: true);
            return;
        }

        // Finish a committed volley before searching for another position. Damage, suppression,
        // lost sight and urgent utility actions have already had their chance to interrupt it.
        if (agent.State is CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage)
            return;

        if (TryDisperse(uid, agent, now))
            return;

        // React from the current firing stance before starting a longer cover move. Hits and
        // suppression still take priority above; otherwise dense cover must not delay every opening shot.
        if (agent.CoverAnchor == null && now >= agent.NextReposition && now >= agent.FirstContact + agent.BurstDuration)
        {
            agent.NextReposition = now + agent.RepositionCooldown;
            if (FindPosition(uid, agent, transform, false) is { } position && TryReserveManeuver(uid, agent, now))
            {
                agent.CoverAnchor = position.Anchor;
                agent.PeekPosition = position.Peek;
                BeginMove(uid, agent, position.Anchor, CMUExpeditionAgentState.Reposition, now);
                return;
            }
        }

        _steering.Unregister(uid);
        // Reacquiring a target or losing cover must not bypass the squad's attack slots.
        if (agent.State is not (CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.Recover) &&
            CanLeaveCover(uid, agent))
            Aim(agent, now);
    }

    private EntityUid? Observe(EntityUid uid, CMUExpeditionAgentComponent agent, TransformComponent transform, TimeSpan now)
    {
        EntityUid? seen = null;
        var candidates = new List<(EntityUid Target, float Distance)>();
        agent.VisibleThreats.Clear();
        agent.MeleeThreats.Clear();
        agent.RushTarget = null;
        foreach (var hostile in ExpeditionHostiles(uid, agent))
        {
            if (!_mobs.IsAlive(hostile) || !TryComp<TransformComponent>(hostile, out var targetTransform) ||
                targetTransform.MapID != transform.MapID || !Visible(uid, hostile, agent.DetectionRange))
                continue;
            agent.VisibleThreats.Add(targetTransform.Coordinates);
            var distance = Vector2.Distance(_transform.GetWorldPosition(transform), _transform.GetWorldPosition(targetTransform));
            candidates.Add((hostile, distance));
        }
        // Include representatives of other attack directions, not only the nearest crowd.
        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        UpdateThreatSectors(uid, agent, candidates, now);
        var nearestRush = agent.MeleeStandoffRange;
        foreach (var (hostile, distance) in candidates)
        {
            if (!IsMeleeThreat(hostile))
                continue;
            // Only nearby, currently visible bodies contribute to escape prediction.
            if (agent.MeleeThreats.Count < 6)
                agent.MeleeThreats.Add((Transform(hostile).Coordinates,
                    TryComp<PhysicsComponent>(hostile, out var body) ? body.LinearVelocity : Vector2.Zero));
            var rushDistance = RushDistance(uid, agent, hostile, distance);
            if (hostile == agent.Target)
                rushDistance -= 0.5f;
            if (rushDistance >= nearestRush)
                continue;
            nearestRush = rushDistance;
            agent.RushTarget = hostile;
        }
        var best = float.MaxValue;
        var stance = agent.State == CMUExpeditionAgentState.Recover && agent.PeekPosition is { } peek
            ? peek : transform.Coordinates;
        var armed = _guns.TryGetGun(uid, out var rifle);
        var shotNeighbors = new HashSet<EntityUid>();
        if (armed)
            _lookup.GetEntitiesInRange(uid, agent.FireRange + 3, shotNeighbors);
        var currentVisible = false;
        EntityUid? flankTarget = null;
        var flankScore = float.MaxValue;
        for (var index = 0; index < candidates.Count; index++)
        {
            var (hostile, distance) = candidates[index];
            if (index >= 4 && hostile != agent.Target && !SectorTarget(agent, hostile))
                continue;
            var point = Transform(hostile).Coordinates;
            var usable = armed && _transform.InRange(stance, point, agent.FireRange) &&
                SafeShot(uid, agent, rifle, point, stance, shotNeighbors);
            var score = distance + (usable ? 0 : agent.DetectionRange + 4);
            // Spread ordinary fire onto unengaged opponents. Rushers override this below,
            // and the current volley/target commitment still prevents score-driven jitter.
            if (agent.TargetAssignments.TryGetValue(hostile, out var assigned))
                score += Math.Min(2, assigned) * 4;
            if (agent.RecentShooters.ContainsKey(hostile))
                score -= 3;
            if (hostile != agent.Target && now >= agent.NextFlankResponse && usable &&
                agent.LastSeen is { } previousContact &&
                FlankingContact(uid, previousContact, point) && score < flankScore &&
                SafeShot(uid, agent, rifle, point, transform.Coordinates, shotNeighbors))
            {
                flankTarget = hostile;
                flankScore = score;
            }
            if (hostile == agent.Target)
            {
                currentVisible = true;
                // Do not restart aim because two equally exposed players exchange places.
                score -= 2;
                if (usable && agent.State is CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage)
                    score -= agent.DetectionRange;
            }
            if (score >= best)
                continue;
            best = score;
            seen = hostile;
        }
        // Keep a visible contact while completing a move or utility action. A tree briefly
        // hiding that contact must not erase the destination halfway through a step-out.
        if (agent.RushTarget is { } rusher)
            seen = rusher;
        else if (currentVisible && HasCoverCommitment(uid, agent, now))
        {
            seen = agent.Target;
            flankTarget = null;
        }
        else if (flankTarget is { } flanker)
        {
            if (AssignFlankResponse(uid, agent, flanker, now))
                seen = flanker;
            else
            {
                flankTarget = null;
                if (currentVisible)
                    seen = agent.Target;
            }
        }
        if (agent.RushTarget == null && flankTarget == null && agent.Target != null && seen != agent.Target)
        {
            if (currentVisible && (CommittedMovement(agent) || agent.Action != null ||
                agent.State == CMUExpeditionAgentState.Healing || now < agent.NextTargetSwitch))
                seen = agent.Target;
            else if (!currentVisible && !agent.ContactFromRadio && _mobs.IsAlive(agent.Target.Value) && now - agent.LastContact < TimeSpan.FromSeconds(0.45))
                return null;
        }
        if (seen is { } target)
        {
            if (agent.LastSeen == null || now - agent.LastContact > TimeSpan.FromSeconds(30))
                agent.FirstContact = now;
            if (agent.Target != target)
            {
                agent.ImmediateFireUntil = now + TimeSpan.FromSeconds(0.75);
                agent.NextMovingBurst = now;
                agent.MovingBurstEnd = TimeSpan.Zero;
                if (agent.Action == null && agent.Plan.Count == 0)
                    agent.NextPlan = now + TimeSpan.FromSeconds(0.35);
                if (CanFireWhileMoving(uid, agent) &&
                    agent.State is CMUExpeditionAgentState.Guard or CMUExpeditionAgentState.Investigate &&
                    TryComp<NPCSteeringComponent>(uid, out var travelling))
                {
                    var start = transform.Coordinates;
                    var end = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(travelling.Coordinates));
                    var delta = end.Position - start.Position;
                    agent.ContactDestination = delta.LengthSquared() > 4 ? start.Offset(Vector2.Normalize(delta) * 2) : end;
                    agent.ContactMoveUntil = now + TimeSpan.FromSeconds(1.25);
                }
                agent.RepeatedPeekHits = 0;
                agent.NextTargetSwitch = now + TimeSpan.FromSeconds(1.5);
                if (target == flankTarget && agent.RushTarget == null)
                {
                    agent.NextFlankResponse = now + TimeSpan.FromSeconds(2);
                    agent.FlankResponses++;
                    // Answer a witnessed flank before choosing another optional move.
                    // An ongoing reload/treatment keeps its own interruption rules.
                    if (agent.Action == null && agent.State != CMUExpeditionAgentState.Healing)
                    {
                        agent.Plan.Clear();
                        ReleaseManeuver(uid, agent);
                        agent.ContactDestination = null;
                        ClearCover(agent);
                        if (agent.SpacingDestination == null)
                            _steering.Unregister(uid);
                        agent.State = CMUExpeditionAgentState.Guard;
                        agent.NextPlan = now + agent.BurstDuration;
                        agent.NextReposition = now + agent.BurstDuration;
                        agent.NextSuppressionResponse = now + agent.BurstDuration;
                    }
                }
                if (!CommittedMovement(agent) && agent.Action == null && agent.State != CMUExpeditionAgentState.Healing)
                {
                    // Target selection does not invalidate a usable shelter or a safe travel leg.
                    // ValidateCover evaluates the new bearing on this same think.
                    agent.State = CMUExpeditionAgentState.Guard;
                }
            }
            agent.Target = target;
            agent.LastContactWasMelee = IsMeleeThreat(target);
            agent.LastSeen = Transform(target).Coordinates;
            agent.ForgetAt = now + agent.MemoryDuration;
            agent.LastContact = now;
            agent.ContactFromRadio = false;
            ShareContact(uid, agent, target, now);
        }
        return seen;
    }

    private static bool CommittedMovement(CMUExpeditionAgentComponent agent) => agent.State is
        CMUExpeditionAgentState.Reposition or CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.Withdraw or
        CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.OutOfAmmo or CMUExpeditionAgentState.PlanMove or
        CMUExpeditionAgentState.RecoverWeapon or CMUExpeditionAgentState.Scavenge || agent.SpacingDestination != null;

    private bool ReadyRifle(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (!_guns.TryGetGun(uid, out var gun))
            return false;
        agent.Rifle = gun;
        agent.WeaponBurstLimit = TryComp<CMUExpeditionWeaponRoleComponent>(gun, out var role) ? role.BurstLimit : int.MaxValue;
        EnsureComp<CMUExpeditionWeaponComponent>(gun);
        // Moving between firing positions does not require a free hand. Keep the rifle ready
        // through peeks, withdrawals and flanks instead of restarting its native wield delay.
        var usingHands = agent.ScavengeTarget != null || agent.State is CMUExpeditionAgentState.Reloading or CMUExpeditionAgentState.Rescuing or CMUExpeditionAgentState.Throwing or CMUExpeditionAgentState.Healing ||
            agent.Action is CMUTacticalAction.GrabCasualty or CMUTacticalAction.DragCasualty;
        if (usingHands || agent.WorkItem != null || agent.PreparingWork || _timing.CurTime < agent.RifleLoweredUntil)
            _wield.TryUnwield(gun.Owner, uid);
        else if (TryComp<WieldableComponent>(gun, out var wieldable) && !wieldable.Wielded)
            _wield.TryWield((gun.Owner, wieldable), uid);
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(gun, ref ammo);
        return ammo.Count > 0;
    }

    private static void Aim(CMUExpeditionAgentComponent agent, TimeSpan now, bool peek = false)
    {
        agent.InvestigationDestination = null;
        agent.InvestigationContact = null;
        agent.Route.Clear();
        agent.RouteDestination = null;
        agent.LostAimSince = null;
        agent.State = CMUExpeditionAgentState.Aim;
        agent.FireAt = UrgentFire(agent, now) ? now : now + (peek ? agent.PeekAimDuration : agent.AimDuration);
    }

    private void BeginRetreat(EntityUid uid, CMUExpeditionAgentComponent agent, TransformComponent transform, bool hasAmmo, TimeSpan now)
    {
        ReleaseManeuver(uid, agent);
        agent.ContactDestination = null;
        ClearCover(agent);
        var position = FindPosition(uid, agent, transform, true);
        if (hasAmmo && (position == null || agent.LastDamage < agent.EmergencyHealDamage &&
                !TryReserveManeuver(uid, agent, now)))
        {
            // No real shelter exists: keep the gun in the fight instead of walking home
            // or waiting out a treatment hold in the open.
            _steering.Unregister(uid);
            agent.State = CMUExpeditionAgentState.Recover;
            agent.FireAt = now;
            agent.NextRetreat = now + TimeSpan.FromSeconds(6);
            return;
        }
        agent.CoverAnchor = position?.Anchor;
        BeginMove(uid, agent, position?.Anchor ?? agent.Home!.Value,
            hasAmmo ? CMUExpeditionAgentState.Retreat : CMUExpeditionAgentState.OutOfAmmo, now);
        agent.HoldUntil = now + TimeSpan.FromSeconds(2);
        agent.NextRetreat = now + TimeSpan.FromSeconds(6);
    }

    private static void ClearCover(CMUExpeditionAgentComponent agent)
    {
        agent.InvestigationDestination = null;
        agent.InvestigationContact = null;
        agent.ResumeVolley = false;
        agent.CoverDestination = null;
        agent.CoverAnchor = null;
        agent.PeekPosition = null;
        agent.Route.Clear();
        agent.RouteDestination = null;
    }

    private void BeginMove(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates destination, CMUExpeditionAgentState state, TimeSpan now)
    {
        agent.ResumeVolley = state == CMUExpeditionAgentState.Peeking &&
            agent.State == CMUExpeditionAgentState.Engage && agent.ShotsFired > 0;
        agent.State = state;
        agent.LastMoveFailed = false;
        agent.CoverDestination = destination;
        agent.MoveUntil = now + agent.RepositionTimeout;
        agent.MoveProgressDestination = null;
        agent.MoveProgressAt = now;
        if (state == CMUExpeditionAgentState.Peeking)
            agent.PeekInitialDamage = agent.LastDamage;
        if (state is CMUExpeditionAgentState.Reposition or CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.Withdraw &&
            agent.LastSeen != null && !_transform.InRange(Transform(uid).Coordinates, destination, 2) && !BuildTacticalRoute(uid, agent, destination))
        {
            agent.MoveUntil = now;
            ReleaseManeuver(uid, agent);
            _steering.Unregister(uid);
            return;
        }
        Move(uid, destination, state == CMUExpeditionAgentState.Peeking);
    }

    private bool ContinueMove(EntityUid uid, CMUExpeditionAgentComponent agent, TransformComponent transform, TimeSpan now)
    {
        if (agent.CoverDestination is not { } destination)
            return false;
        if (!ManeuverSupported(uid, agent, now) || GrenadeDanger(destination))
        {
            _steering.Unregister(uid);
            ReleaseManeuver(uid, agent);
            agent.LastMoveFailed = true;
            ClearCover(agent);
            agent.State = CMUExpeditionAgentState.Guard;
            agent.NextReposition = now + TimeSpan.FromSeconds(2);
            return false;
        }
        var precise = agent.State == CMUExpeditionAgentState.Peeking;
        // Native steering can oscillate around a tiny sub-tile radius. Stop as soon as the actual
        // stance near the destination has the required firing cone, not at an arbitrary tile centre.
        var clearStance = precise && agent.LastSeen is { } threat &&
            _transform.InRange(transform.Coordinates, destination, 0.7f) &&
            _transform.InRange(transform.Coordinates, threat, agent.FireRange - 0.25f) &&
            FiringLaneClear(uid, transform.Coordinates, threat);
        var shelteredStop = agent.State is CMUExpeditionAgentState.Reposition or CMUExpeditionAgentState.Withdraw &&
            _transform.InRange(transform.Coordinates, destination, 0.55f) && BodyFits(uid, transform.Coordinates) &&
            ShelteredFromKnownThreats(uid, agent, transform.Coordinates);
        if (clearStance || shelteredStop || _transform.InRange(transform.Coordinates, destination, precise ? 0.12f : ArrivalRange))
        {
            _steering.Unregister(uid);
            // Cancel travel momentum at a deliberate cover stop; otherwise a short arrival radius
            // can leave the NPC coasting out of the shelter after its steering has been removed.
            _physics.SetLinearVelocity(uid, Vector2.Zero);
            ReleaseManeuver(uid, agent);
            agent.CoverDestination = null;
            agent.Route.Clear();
            agent.RouteDestination = null;
            if (clearStance)
                agent.PeekPosition = transform.Coordinates;
            if (shelteredStop)
                agent.CoverAnchor = transform.Coordinates;
            return false;
        }
        if (UpdateMoveProgress(agent, transform.Coordinates,
                agent.RouteDestination == destination && agent.Route.TryPeek(out var waypoint) ? waypoint : destination, now))
        {
            // Wading is slower than a dry-ground reposition. Progress, not water contact,
            // earns more time; stalled bodies still fail below and utility actions stay bounded.
            if (TryComp<MovementSpeedModifierComponent>(uid, out var speed) && speed.CurrentSprintSpeed < 1.5f &&
                agent.State != CMUExpeditionAgentState.PlanMove)
                agent.MoveUntil = now + agent.RepositionTimeout;
        }
        if (now >= agent.MoveUntil || now - agent.MoveProgressAt >= TimeSpan.FromSeconds(1.5) ||
            TryComp<NPCSteeringComponent>(uid, out var steering) && steering.Status == SteeringStatus.NoPath)
        {
            _steering.Unregister(uid);
            ReleaseManeuver(uid, agent);
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
            predicate: entity => entity == observer || entity == target || HasComp<NpcFactionMemberComponent>(entity)) &&
        !SmokeOccludes(Transform(observer).Coordinates, Transform(target).Coordinates);

    private void Move(EntityUid uid, EntityCoordinates destination, bool precise = false,
        bool routeWaypoint = false, bool validated = false)
    {
        if (TryComp<CMUExpeditionAgentComponent>(uid, out var blocked) && blocked.State == CMUExpeditionAgentState.Investigate &&
            blocked.FailedPosition is { } failed && _timing.CurTime < blocked.AvoidPositionUntil &&
            _transform.InRange(destination, failed, 0.75f))
        {
            _steering.Unregister(uid);
            blocked.State = CMUExpeditionAgentState.Watch;
            return;
        }
        if (TryComp<CMUExpeditionAgentComponent>(uid, out var moving) &&
            moving.RouteDestination != destination && moving.State == CMUExpeditionAgentState.Investigate &&
            !BuildTacticalRoute(uid, moving, destination))
        {
            _steering.Unregister(uid);
            moving.FailedPosition = destination;
            moving.AvoidPositionUntil = _timing.CurTime + TimeSpan.FromSeconds(1);
            moving.State = CMUExpeditionAgentState.Watch;
            return;
        }
        if (TryComp<CMUExpeditionAgentComponent>(uid, out var agent) && agent.RouteDestination == destination)
        {
            var start = Transform(uid).Coordinates;
            AdvanceRoute(uid, agent.Route, start);
            // Steering avoidance or a moving obstacle can displace us from a valid segment.
            // Reconnect from the actual body position instead of pushing through its corner.
            if (agent.Route.TryPeek(out var next) && !TraversablePassage(uid, start, next) &&
                !BuildTacticalRoute(uid, agent, destination))
            {
                _steering.Unregister(uid);
                agent.MoveUntil = _timing.CurTime;
                agent.LastMoveFailed = true;
                if (agent.State == CMUExpeditionAgentState.Investigate)
                {
                    agent.FailedPosition = destination;
                    agent.AvoidPositionUntil = _timing.CurTime + TimeSpan.FromSeconds(1);
                    ClearCover(agent);
                    agent.State = CMUExpeditionAgentState.Watch;
                }
                return;
            }
            if (agent.Route.TryPeek(out var waypoint))
                destination = waypoint;
            routeWaypoint = agent.Route.Count > 1;
            validated = true;
            if (agent.State == CMUExpeditionAgentState.Investigate)
            {
                var now = _timing.CurTime;
                UpdateMoveProgress(agent, start, destination, now);
                if (now - agent.MoveProgressAt >= TimeSpan.FromSeconds(1.5) ||
                    TryComp<NPCSteeringComponent>(uid, out var pursuit) && pursuit.Status == SteeringStatus.NoPath)
                {
                    _steering.Unregister(uid);
                    agent.FailedPosition = agent.RouteDestination;
                    agent.AvoidPositionUntil = now + TimeSpan.FromSeconds(1);
                    ClearCover(agent);
                    agent.State = CMUExpeditionAgentState.Watch;
                    return;
                }
            }
        }
        if (_transform.InRange(Transform(uid).Coordinates, destination,
                routeWaypoint ? CornerArrivalRange : precise ? 0.1f : ArrivalRange))
        {
            _steering.Unregister(uid);
            return;
        }
        if (TryComp<CMUExpeditionAgentComponent>(uid, out var traveller) &&
            !QueueMovement(uid, traveller, ref destination))
            return;
        TryComp<NPCSteeringComponent>(uid, out var existing);
        if (existing?.Status == SteeringStatus.NoPath)
        {
            _steering.Unregister(uid, existing);
            existing = null;
        }
        var changed = existing == null || existing.Coordinates != destination;
        var steering = _steering.Register(uid, destination, existing);
        steering.Range = routeWaypoint ? CornerArrivalRange : precise ? 0.1f : 0.18f;
        steering.ArriveOnLineOfSight = false;
        // Our body-safe segments already provide a path. A second navmesh route can prune
        // the corner or seek a different polygon centre; retain native local avoidance only.
        steering.RepathRange = validated ? float.MaxValue : 1.5f;
        if (validated)
        {
            steering.PathfindToken?.Cancel();
            steering.PathfindToken = null;
            steering.CurrentPath.Clear();
            if (changed)
                Array.Clear(steering.Interest);
        }
    }
}
