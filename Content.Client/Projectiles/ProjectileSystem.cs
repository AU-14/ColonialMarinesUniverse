using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Xenonids.Damage;
using Content.Shared.Effects;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Client.Projectiles;

public sealed partial class ProjectileSystem : SharedProjectileSystem
{
    private static readonly TimeSpan ImpactCorrelationRetention = TimeSpan.FromSeconds(30);

    [Dependency] private AnimationPlayerSystem _player = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private SharedGunSystem _guns = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<(int Projectile, NetEntity? Target), TimeSpan> _authoritativeImpacts = new();
    private readonly Dictionary<(int Projectile, NetEntity Target), TimeSpan> _authoritativeFeedback = new();
    private readonly List<(int Projectile, NetEntity? Target)> _expiredImpactKeys = new();
    private readonly List<(int Projectile, NetEntity Target)> _expiredFeedbackKeys = new();
    private readonly Dictionary<(int Projectile, NetEntity? Target), TimeSpan> _predictedImpacts = new();
    private readonly Dictionary<(int Projectile, NetEntity Target), TimeSpan> _predictedFeedback = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImpactEffectEvent>(OnProjectileImpact);
        SubscribeNetworkEvent<PredictedProjectileImpactFeedbackEvent>(OnPredictedProjectileImpactFeedback);
    }

    private void OnProjectileImpact(ImpactEffectEvent ev)
    {
        var coords = GetCoordinates(ev.Coordinates);

        if (Deleted(coords.EntityId))
            return;

        if (ev.PredictedProjectile is { } predictedProjectile &&
            ev.Shooter is { } shooter &&
            _playerManager.LocalEntity is { } localPlayer &&
            GetNetEntity(localPlayer) == shooter)
        {
            var key = (predictedProjectile, ev.Target);
            if (_predictedImpacts.Remove(key))
                return;

            _authoritativeImpacts[key] =
                _timing.CurTime + ImpactCorrelationRetention;
        }

        SpawnImpactEffect(ev.Prototype, coords);
    }

    protected override void PlayImpactEffect(Entity<ProjectileComponent> projectile, EntityUid target)
    {
        if (!HasComp<PredictedProjectileClientComponent>(projectile) ||
            projectile.Comp.ImpactEffect is not { } impactEffect)
        {
            return;
        }

        var key = (projectile.Owner.Id, (NetEntity?) GetNetEntity(target));
        if (_authoritativeImpacts.Remove(key))
            return;

        _predictedImpacts[key] = _timing.CurTime + ImpactCorrelationRetention;
        SpawnImpactEffect(impactEffect, Transform(projectile).Coordinates);
    }

    protected override bool BeginPredictedImpactFeedback(EntityUid projectile, EntityUid target)
    {
        var key = (projectile.Id, GetNetEntity(target));
        if (_authoritativeFeedback.Remove(key))
            return false;

        _predictedFeedback[key] = _timing.CurTime + ImpactCorrelationRetention;
        return true;
    }

    private void OnPredictedProjectileImpactFeedback(PredictedProjectileImpactFeedbackEvent ev)
    {
        var key = (ev.Projectile, ev.Target);
        var predictedFeedbackPlayed = _predictedFeedback.Remove(key);
        if (!predictedFeedbackPlayed)
        {
            // Pre-seed authority-first correlation before locally applying any
            // persistent projectile outcome (embedding, grenades, thrown items).
            _authoritativeFeedback[key] = _timing.CurTime + ImpactCorrelationRetention;
        }

        ReconcilePredictedProjectile(ev, predictedFeedbackPlayed);
        if (predictedFeedbackPlayed)
            return;

        var coordinates = GetCoordinates(ev.Coordinates);
        var target = GetEntity(ev.Target);
        // A local persistent-outcome collision consumes the pre-seeded entry.
        _authoritativeFeedback[key] = _timing.CurTime + ImpactCorrelationRetention;
        _guns.PlayResolvedImpactSound(coordinates, ev.ImpactSound, ev.VaryPitch);

        if (!Deleted(target))
        {
            var damageEvent = new ProjectileDamageDealtEvent(
                _playerManager.LocalEntity,
                ev.Damage,
                ev.DamageTotal);
            RaiseLocalEvent(target, ref damageEvent);

            if (ev.FlashTarget)
                _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Local());
        }
    }

    private void ReconcilePredictedProjectile(
        PredictedProjectileImpactFeedbackEvent feedback,
        bool predictedFeedbackPlayed)
    {
        if (!feedback.ProjectileSpent)
        {
            var continuePrediction = new PredictedProjectileAuthorityReconcileEvent(
                feedback.Projectile,
                feedback.Target,
                false,
                false);
            RaiseLocalEvent(ref continuePrediction);
            return;
        }

        var persistent = !feedback.DeleteOnCollide;
        var projectile = new EntityUid(feedback.Projectile);
        if (Exists(projectile) &&
            HasComp<PredictedProjectileClientComponent>(projectile) &&
            TryComp(projectile, out ProjectileComponent? component))
        {
            if (persistent && !predictedFeedbackPlayed && !component.ProjectileSpent)
            {
                var impactCoordinates = GetCoordinates(feedback.Coordinates);
                if (!Deleted(impactCoordinates.EntityId))
                    _transform.SetCoordinates(projectile, impactCoordinates);

                // Until the matching authority is available for handoff, leave a
                // visible, stationary local representation at the confirmed impact.
                // Do not replay ProjectileHitEvent here: this network callback is
                // outside prediction, and gameplay handlers can mutate stacks,
                // knockback, status effects, and other state a second time.
                if (!TerminatingOrDeleted(projectile) &&
                    TryComp(projectile, out PhysicsComponent? physics))
                {
                    _physics.SetLinearVelocity(projectile, Vector2.Zero, body: physics);
                    _physics.SetBodyType(projectile, BodyType.Static, body: physics);
                }
            }

            component.ProjectileSpent = true;

            // A loaded physical projectile is predicted on its existing network
            // entity. The server owns deleting that entity; only retire a separate
            // client-side prediction copy here.
            if (!persistent && IsClientSide(projectile))
                QueueDel(projectile);
        }

        var reconcile = new PredictedProjectileAuthorityReconcileEvent(
            feedback.Projectile,
            feedback.Target,
            true,
            persistent);
        RaiseLocalEvent(ref reconcile);
    }

    /// <summary>
    /// Clears correlation state for a client-reported hit the server rejected, so
    /// the projectile's later authoritative impact is not mistaken for a duplicate.
    /// </summary>
    public void RejectPredictedImpact(int projectile)
    {
        RemoveProjectileCorrelations(_predictedImpacts, projectile);
        RemoveProjectileCorrelations(_authoritativeImpacts, projectile);
        RemoveProjectileCorrelations(_predictedFeedback, projectile);
        RemoveProjectileCorrelations(_authoritativeFeedback, projectile);
    }

    private void SpawnImpactEffect(string prototype, EntityCoordinates coordinates)
    {
        var ent = Spawn(prototype, coordinates);

        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            sprite[EffectLayers.Unshaded].AutoAnimated = false;
            _sprite.LayerMapTryGet((ent, sprite), EffectLayers.Unshaded, out var layer, false);
            var state = _sprite.LayerGetRsiState((ent, sprite), layer);
            var lifetime = 0.5f;

            if (TryComp<TimedDespawnComponent>(ent, out var despawn))
                lifetime = despawn.Lifetime;

            var anim = new Animation()
            {
                Length = TimeSpan.FromSeconds(lifetime),
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick()
                    {
                        LayerKey = EffectLayers.Unshaded,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(state.Name, 0f),
                        }
                    }
                }
            };

            _player.Play(ent, anim, "impact-effect");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        RemoveExpiredImpacts(_authoritativeImpacts, _expiredImpactKeys);
        RemoveExpiredImpacts(_predictedImpacts, _expiredImpactKeys);
        RemoveExpiredImpacts(_authoritativeFeedback, _expiredFeedbackKeys);
        RemoveExpiredImpacts(_predictedFeedback, _expiredFeedbackKeys);
    }

    private void RemoveExpiredImpacts<TKey>(Dictionary<TKey, TimeSpan> impacts, List<TKey> expired)
        where TKey : notnull
    {
        expired.Clear();
        foreach (var (key, expiry) in impacts)
        {
            if (expiry <= _timing.CurTime)
                expired.Add(key);
        }

        foreach (var key in expired)
        {
            impacts.Remove(key);
        }
    }

    private static void RemoveProjectileCorrelations<TTarget>(
        Dictionary<(int Projectile, TTarget Target), TimeSpan> correlations,
        int projectile)
    {
        foreach (var key in correlations.Keys.Where(key => key.Projectile == projectile).ToList())
        {
            correlations.Remove(key);
        }
    }
}
