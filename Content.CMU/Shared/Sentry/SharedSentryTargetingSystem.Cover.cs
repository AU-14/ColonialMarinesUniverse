using Content.Shared.BarricadeBlock;
using Content.Shared._RMC14.Barricade; // CMU14: DirectionalBulletBlockerComponent

namespace Content.Shared._RMC14.Sentry;

public abstract partial class SharedSentryTargetingSystem
{
    private const float SentryCoverDistance = 4f;


    public bool CanTargetAcrossBarricade(EntityUid shooter, EntityUid blocker)
    {
        return TryComp<BarricadeBlockComponent>(blocker, out var cover)
            // && (cover.Blocking < 100 || ClearsBarricade(shooter, blocker, cover)); // CMU14: replaced by EffectiveBlocking
            && (EffectiveBlocking(blocker, cover) < 1f || ClearsBarricade(shooter, blocker, cover)); // CMU14: uses EffectiveBlocking to account for DirectionalBulletBlocker
    }
    
    // CMU14 method
    /// <summary>
    ///     Chance (0-1) that the barricade blocks a shot
    ///     Takes into account DirectionalBulletBlocker if the component exists in a barricade.
    /// </summary>
    private float EffectiveBlocking(EntityUid blocker, BarricadeBlockComponent cover)
    {
        var chance = cover.Blocking / 100f;
        if (TryComp<DirectionalBulletBlockerComponent>(blocker, out var directional))
            chance *= directional.BlockChance;

        return chance;
    }

    public bool ClearsBarricade(EntityUid shooter, EntityUid blocker, BarricadeBlockComponent cover)
    {
        var distance = HasComp<SentryTargetingComponent>(shooter) ? SentryCoverDistance : cover.Distance;
        var origin = Transform(shooter).Coordinates;
        if (!origin.TryDistance(EntityManager, _xform, Transform(blocker).Coordinates, out var separation)
            || separation > distance)
            return false;

        var facing = _xform.GetWorldRotation(shooter).GetCardinalDir();
        var barrier = _xform.GetWorldRotation(blocker).GetCardinalDir();
        return facing == barrier || cover.Bidirectional && facing == barrier.GetOpposite();
    }
}
