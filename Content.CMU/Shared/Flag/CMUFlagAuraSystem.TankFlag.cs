using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Allegiance;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Flag;

public sealed partial class CMUFlagAuraSystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    /// <summary>
    ///     The upstream tank flag hardpoint. Its prototype stays untouched; the aura is attached in code.
    /// </summary>
    private static readonly EntProtoId TankMountedFlag = "VehicleTankMountedFlag";

    /// <summary>
    ///     The mounted flag is a United Americas flag.
    /// </summary>
    private static readonly ProtoId<AllegiancePrototype> TankMountedFlagAllegiance = "UA";

    private static readonly ProtoId<CMUFlagReinforcementsPrototype> TankMountedFlagReinforcements =
        "CMUFlagReinforcementsUA";

    /// <summary>
    ///     JIMA warbanner stats with a 10 tile reach.
    /// </summary>
    private static readonly CMUFlagAuraStats TankMountedFlagAura = new()
    {
        Range = 10f,
        ApplyMove = true,
        Duration = TimeSpan.FromSeconds(5),
    };

    private void InitializeTankFlag()
    {
        // The flag mounts on the turret, whose slot changes name the turret rather than the tank, so the aura is
        // attached up front and only applies while the flag sits in a vehicle (see GetActiveAura).
        SubscribeLocalEvent<HardpointItemComponent, MapInitEvent>(OnHardpointMapInit);
    }

    private void OnHardpointMapInit(Entity<HardpointItemComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || MetaData(ent).EntityPrototype?.ID != TankMountedFlag.Id)
            return;

        var aura = EnsureComp<CMUFlagAuraComponent>(ent);
        aura.Mounted ??= TankMountedFlagAura;
        aura.RequiresRaising = true;

        var allegiance = EnsureComp<CMUFlagAllegianceComponent>(ent);
        if (allegiance.Allegiances.Count == 0)
            allegiance.Allegiances.Add(TankMountedFlagAllegiance);

        allegiance.Reinforcements ??= TankMountedFlagReinforcements;
    }

    /// <summary>
    ///     A mounted flag flies at all times, driver or not. It only comes down while the flag or its vehicle is
    ///     badly damaged, and goes back up once both are repaired.
    /// </summary>
    private void UpdateRaisedFlag(Entity<CMUFlagAuraComponent, TransformComponent> flag)
    {
        var aura = flag.Comp1;
        if (GetMountedVehicle(flag) is not { } vehicle)
        {
            aura.Raised = false;
            aura.Factions.Clear();
            return;
        }

        var driver = CompOrNull<VehicleComponent>(vehicle)?.Operator;
        var damaged = IsTooDamaged((flag, aura), vehicle);
        if (aura.Raised == !damaged)
            return;

        aura.Raised = !damaged;
        if (aura.Raised)
            aura.NextOrder = _timing.CurTime;

        var message = Loc.GetString(damaged ? "cmu-tank-flag-damaged-lowered" : "cmu-tank-flag-raised",
            ("flag", flag.Owner));
        var type = damaged ? PopupType.MediumCaution : PopupType.Medium;
        _popup.PopupEntity(message, vehicle, type);
        if (driver is { } current)
            _popup.PopupEntity(message, current, current, type);
    }

    /// <summary>
    ///     Whether the flag or the vehicle carrying it has fallen to
    ///     <see cref="CMUFlagAuraComponent.LowerAtIntegrity"/> of its integrity.
    /// </summary>
    private bool IsTooDamaged(Entity<CMUFlagAuraComponent> flag, EntityUid vehicle)
    {
        return IsTooDamaged(flag.Owner, flag.Comp.LowerAtIntegrity) ||
               IsTooDamaged(vehicle, flag.Comp.LowerAtIntegrity);
    }

    private bool IsTooDamaged(EntityUid uid, float threshold)
    {
        if (!TryComp(uid, out HardpointIntegrityComponent? integrity))
            return false;

        if (integrity.DestroyedBeyondRepair)
            return true;

        return integrity.MaxIntegrity > 0 && integrity.Integrity / integrity.MaxIntegrity <= threshold;
    }

    /// <summary>
    ///     The vehicle a flag is installed on, if it sits in a hardpoint slot.
    /// </summary>
    private EntityUid? GetMountedVehicle(Entity<CMUFlagAuraComponent, TransformComponent> flag)
    {
        if (!_container.TryGetContainingContainer((flag.Owner, flag.Comp2, null), out var container) ||
            !HasComp<HardpointSlotsComponent>(container.Owner) ||
            !_topology.TryGetVehicle(flag.Owner, out var vehicle, includeSelf: false))
        {
            return null;
        }

        return vehicle;
    }
}
