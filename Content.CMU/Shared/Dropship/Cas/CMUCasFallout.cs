using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Dropship.Cas;

public enum CMUCasFalloutType : byte
{
    /// <summary>Lingering zone where weeds cannot spread or be planted.</summary>
    WeedKiller,

    /// <summary>Lingering smoke that blocks M90 AI acquisition and laser designation.</summary>
    Obscurant,

    /// <summary>Burning flare that marks itself as a CAS target.</summary>
    Flare,

    /// <summary>One-shot damage to resin structures.</summary>
    Resin,

    /// <summary>One-shot bonus damage to vehicles and heavily armored targets.</summary>
    Armor,
}

/// <summary>
/// Spawned by a special rocket's impact to apply its lasting or bonus effect.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUCasFalloutComponent : Component
{
    [DataField("fallout", required: true), AutoNetworkedField]
    public CMUCasFalloutType Type;

    [DataField, AutoNetworkedField]
    public float Radius = 3f;

    [DataField]
    public DamageSpecifier? Damage;

    /// <summary>Hull damage dealt to vehicles, for <see cref="CMUCasFalloutType.Armor"/>.</summary>
    [DataField]
    public float VehicleDamage;

    /// <summary>Minimum xeno armor for a mob to count as heavily armored.</summary>
    [DataField]
    public int HeavyArmor = 30;

    [DataField]
    public string Abbreviation = "FLR";
}

public sealed class CMUCasFalloutSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedDropshipWeaponSystem _weapons = default!;

    private readonly HashSet<Entity<XenoConstructComponent>> _constructs = new();
    private readonly HashSet<Entity<VehicleComponent>> _vehicles = new();
    private readonly HashSet<Entity<CMArmorComponent>> _armored = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCasFalloutComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CMUCasFalloutComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var coordinates = _transform.GetMapCoordinates(ent);
        switch (ent.Comp.Type)
        {
            case CMUCasFalloutType.Flare:
                _weapons.MakeDropshipTarget(ent, ent.Comp.Abbreviation);
                break;
            case CMUCasFalloutType.Resin:
                if (ent.Comp.Damage == null)
                    break;
                _constructs.Clear();
                _lookup.GetEntitiesInRange(coordinates, ent.Comp.Radius, _constructs);
                foreach (var construct in _constructs)
                    _damage.TryChangeDamage(construct, ent.Comp.Damage, ignoreResistances: true, origin: ent);
                break;
            case CMUCasFalloutType.Armor:
                _vehicles.Clear();
                _lookup.GetEntitiesInRange(coordinates, ent.Comp.Radius, _vehicles);
                foreach (var vehicle in _vehicles)
                {
                    if (ent.Comp.VehicleDamage > 0)
                        _hardpoints.DamageVehicleHull(vehicle, ent.Comp.VehicleDamage);
                }

                if (ent.Comp.Damage == null)
                    break;
                _armored.Clear();
                _lookup.GetEntitiesInRange(coordinates, ent.Comp.Radius, _armored);
                foreach (var armored in _armored)
                {
                    if (armored.Comp.XenoArmor >= ent.Comp.HeavyArmor)
                        _damage.TryChangeDamage(armored, ent.Comp.Damage, ignoreResistances: true, origin: ent);
                }
                break;
        }
    }

    public bool IsWeedKilled(MapCoordinates coordinates)
    {
        return InZone(CMUCasFalloutType.WeedKiller, coordinates);
    }

    public bool IsObscured(MapCoordinates coordinates)
    {
        return InZone(CMUCasFalloutType.Obscurant, coordinates);
    }

    private bool InZone(CMUCasFalloutType type, MapCoordinates coordinates)
    {
        if (coordinates.MapId == MapId.Nullspace)
            return false;

        var query = EntityQueryEnumerator<CMUCasFalloutComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fallout, out var xform))
        {
            if (fallout.Type != type || xform.MapID != coordinates.MapId)
                continue;
            var position = _transform.GetWorldPosition(xform);
            if ((position - coordinates.Position).LengthSquared() <= fallout.Radius * fallout.Radius)
                return true;
        }

        return false;
    }
}
