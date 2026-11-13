using Content.Shared._RMC14.Projectiles;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Trigger.Systems;

namespace Content.Server.CMU14.Yautja;

/// <summary>
/// CMU14: gives yautja projectiles cmss13's do_at_max_range. RMCProjectileSystem deletes a projectile that reaches
/// ProjectileMaxRange without raising anything, so this catches the deletion and, only when the projectile actually
/// got to its range (not when it hit something first), raises the range event and applies the configured effect.
/// </summary>
public sealed partial class YautjaProjectileRangeSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    // RMCProjectileSystem stops a projectile once it is within 0.1 of its range; a hair more covers float error.
    private const float RangeTolerance = 0.15f;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaMaxRangeEffectComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnTerminating(Entity<YautjaMaxRangeEffectComponent> ent, ref EntityTerminatingEvent args)
    {
        var xform = Transform(ent);
        if (xform.MapUid is not { } map || TerminatingOrDeleted(map) || TerminatingOrDeleted(xform.ParentUid))
            return;

        if (!TryComp(ent, out ProjectileMaxRangeComponent? range) ||
            range.Origin is not { } origin ||
            !origin.TryDistance(EntityManager, _transform, _transform.GetMoverCoordinates(ent), out var distance) ||
            distance < range.Max - RangeTolerance)
        {
            return;
        }

        var ev = new YautjaProjectileReachedMaxRangeEvent();
        RaiseLocalEvent(ent, ref ev);

        if (ent.Comp.Trigger)
            _trigger.Trigger(ent.Owner);

        if (ent.Comp.Drop is { } drop)
            SpawnAtPosition(drop, xform.Coordinates);
    }
}
