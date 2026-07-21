using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Client.Projectiles;

public sealed partial class ProjectileSystem : SharedProjectileSystem
{
    private static readonly TimeSpan ImpactCorrelationRetention = TimeSpan.FromSeconds(30);

    [Dependency] private AnimationPlayerSystem _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<int, TimeSpan> _authoritativeImpacts = new();
    private readonly List<int> _expiredImpacts = new();
    private readonly Dictionary<int, TimeSpan> _predictedImpacts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImpactEffectEvent>(OnProjectileImpact);
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
            if (_predictedImpacts.Remove(predictedProjectile))
                return;

            _authoritativeImpacts[predictedProjectile] =
                _timing.CurTime + ImpactCorrelationRetention;
        }

        SpawnImpactEffect(ev.Prototype, coords);
    }

    protected override void PlayImpactEffect(Entity<ProjectileComponent> projectile)
    {
        if (!HasComp<PredictedProjectileClientComponent>(projectile) ||
            projectile.Comp.ImpactEffect is not { } impactEffect)
        {
            return;
        }

        if (_authoritativeImpacts.Remove(projectile.Owner.Id))
            return;

        _predictedImpacts[projectile.Owner.Id] = _timing.CurTime + ImpactCorrelationRetention;
        SpawnImpactEffect(impactEffect, Transform(projectile).Coordinates);
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

        RemoveExpiredImpacts(_authoritativeImpacts);
        RemoveExpiredImpacts(_predictedImpacts);
    }

    private void RemoveExpiredImpacts(Dictionary<int, TimeSpan> impacts)
    {
        _expiredImpacts.Clear();
        foreach (var (projectile, expiry) in impacts)
        {
            if (expiry <= _timing.CurTime)
                _expiredImpacts.Add(projectile);
        }

        foreach (var projectile in _expiredImpacts)
        {
            impacts.Remove(projectile);
        }
    }
}
