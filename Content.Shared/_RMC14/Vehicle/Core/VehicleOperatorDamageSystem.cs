using Content.Shared._RMC14.Explosion;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Vehicle.Components;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleOperatorDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleOperatorDamageComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<VehicleOperatorDamageComponent, ExplosionReceivedEvent>(OnExplosionReceived);
    }

    private void OnBeforeDamageChanged(Entity<VehicleOperatorDamageComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!args.Damage.AnyPositive() ||
            !TryGetOperator(ent.Owner, out var operatorUid) ||
            !IsOtherVehicle(ent.Owner, args.Origin) && !IsOtherVehicle(ent.Owner, args.Source))
        {
            return;
        }

        TransferDamage(operatorUid, args.Damage, ent.Comp.RammingDamageMultiplier, args.Origin);
    }

    private void OnExplosionReceived(Entity<VehicleOperatorDamageComponent> ent, ref ExplosionReceivedEvent args)
    {
        if (!args.Damage.AnyPositive() || !TryGetOperator(ent.Owner, out var operatorUid))
            return;

        var coordinates = _transform.GetMapCoordinates(ent.Owner);
        var directRange = MathF.Max(0f, ent.Comp.DirectExplosionRange);
        var directHit = coordinates.MapId == args.Epicenter.MapId &&
            (coordinates.Position - args.Epicenter.Position).LengthSquared() <= directRange * directRange;
        var multiplier = directHit
            ? ent.Comp.DirectExplosionDamageMultiplier
            : ent.Comp.NearbyExplosionDamageMultiplier;

        TransferDamage(operatorUid, args.Damage, multiplier);
    }

    private bool TryGetOperator(EntityUid vehicleUid, out EntityUid operatorUid)
    {
        operatorUid = default;
        if (!TryComp(vehicleUid, out VehicleComponent? vehicle) || vehicle.Operator is not { } found)
            return false;

        operatorUid = found;
        return true;
    }

    private bool IsOtherVehicle(EntityUid vehicleUid, EntityUid? source)
    {
        return source is { } uid && uid != vehicleUid && HasComp<VehicleComponent>(uid);
    }

    private void TransferDamage(EntityUid operatorUid, DamageSpecifier damage, float multiplier, EntityUid? origin = null)
    {
        multiplier = MathF.Max(0f, multiplier);
        if (multiplier <= 0f)
            return;

        var transferred = DamageSpecifier.GetPositive(damage) * multiplier;
        if (!transferred.Empty)
            _damageable.TryChangeDamage(operatorUid, transferred, origin: origin);
    }
}
