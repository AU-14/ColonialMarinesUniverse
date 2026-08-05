using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

public abstract partial class SharedGunPredictionSystem : EntitySystem
{
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleRideSurfaceSystem _rideSurface = default!;

    public bool GunPrediction { get; private set; }

    public override void Initialize()
    {
        Subs.CVar(_config, RMCCVars.RMCGunPrediction, v => GunPrediction = v, true);
    }

    public bool ShouldPredict(EntityUid gun)
    {
        return GunPrediction && !HasComp<GunIgnorePredictionComponent>(gun);
    }

    public List<EntityUid>? ShootRequested(
        NetEntity netGun,
        NetCoordinates coordinates,
        NetEntity? target,
        ICommonSession session,
        bool continuous = false)
    {
        var user = session.AttachedEntity;

        if (user == null ||
            !_combatMode.IsInCombatMode(user) ||
            !_gun.TryGetGun(user.Value, out var ent, out var gun))
        {
            return null;
        }

        if (ent != GetEntity(netGun))
            return null;

        var shootCoordinates = GetCoordinates(coordinates);
        var shootMapCoordinates = _transform.ToMapCoordinates(shootCoordinates);
        if (!IsSameMap(ent, shootMapCoordinates))
            return null;

        var targetUid = GetEntity(target);
        if (targetUid is { } clickedTarget)
        {
            if (_rideSurface.TryGetRiderAtCoordinates(clickedTarget, shootMapCoordinates, out var rider))
                targetUid = rider;

            if (targetUid is { } resolvedTarget && !IsSameMap(resolvedTarget, shootMapCoordinates))
                targetUid = null;
        }

#pragma warning disable RA0002
        gun.ShootCoordinates = shootCoordinates;
        gun.Target = targetUid;
#pragma warning restore RA0002
        if (continuous)
            _gun.ResetShotCounter(ent, gun);

        return _gun.AttemptShootRequest(
            user.Value,
            (ent, gun),
            shootCoordinates,
            targetUid);
    }

    protected bool IsSameMap(EntityUid entity, EntityUid other)
    {
        return TryGetMapId(entity, out var mapId) &&
               TryGetMapId(other, out var otherMapId) &&
               mapId == otherMapId;
    }

    protected bool IsSameMap(EntityUid entity, MapCoordinates coordinates)
    {
        return coordinates.MapId != MapId.Nullspace &&
               TryGetMapId(entity, out var mapId) &&
               mapId == coordinates.MapId;
    }

    private bool TryGetMapId(EntityUid entity, out MapId mapId)
    {
        mapId = MapId.Nullspace;
        if (!TryComp(entity, out TransformComponent? transform))
            return false;

        mapId = transform.MapID;
        return mapId != MapId.Nullspace;
    }
}
