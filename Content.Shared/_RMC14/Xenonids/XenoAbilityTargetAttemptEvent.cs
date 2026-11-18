using Content.Shared.Projectiles;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids;

// CMU14 file: a fork hook for xeno abilities, xeno projectiles and tackles. RMC spares only the same hive there, so a
// fork whose xenos fight beside another faction (the Bad Blood clan's beasts) cancels the hit on its allies. Nothing
// is raised unless the source carries XenoTargetFilterComponent, so every other xeno pays one component lookup and
// behaves as upstream.

/// <summary>
/// CMU14: on a xeno whose ability, projectile and tackle targets a fork system filters through
/// <see cref="XenoAbilityTargetAttemptEvent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenoTargetFilterComponent : Component;

/// <summary>
/// CMU14: raised on a filtered xeno (<see cref="XenoTargetFilterComponent"/>) before one of its abilities, its
/// projectiles or its tackle hits <paramref name="Target"/>. Set <see cref="Cancelled"/> to spare the target.
/// </summary>
[ByRefEvent]
public record struct XenoAbilityTargetAttemptEvent(EntityUid Target, bool Cancelled = false);

/// <summary>
/// CMU14: the one-line checks the RMC hit paths call.
/// </summary>
public static class XenoTargetFilter
{
    /// <summary>
    /// Whether a fork system spares <paramref name="target"/> from <paramref name="source"/>'s ability or tackle.
    /// </summary>
    public static bool IsBlocked(IEntityManager entMan, EntityUid source, EntityUid target)
    {
        if (!entMan.HasComponent<XenoTargetFilterComponent>(source))
            return false;

        var ev = new XenoAbilityTargetAttemptEvent(target);
        entMan.EventBus.RaiseLocalEvent(source, ref ev);
        return ev.Cancelled;
    }

    /// <summary>
    /// <see cref="IsBlocked"/> for a projectile: the projectile's shooter is the source.
    /// </summary>
    public static bool IsProjectileBlocked(IEntityManager entMan, EntityUid projectile, EntityUid target)
    {
        return entMan.TryGetComponent(projectile, out ProjectileComponent? shot) &&
               shot.Shooter is { } shooter &&
               IsBlocked(entMan, shooter, target);
    }
}
