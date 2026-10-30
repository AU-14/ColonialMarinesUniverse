using Content.Shared.Vehicle;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// An open vehicle's engine switch: off, the vehicle won't drive. The driver flips it with a button;
/// the server's engine sound system starts and stops the engine from it.
/// </summary>
public sealed class CMUVehicleEngineSwitchSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleEngineSoundComponent, VehicleCanRunEvent>(OnCanRun);
    }

    private void OnCanRun(Entity<CMUVehicleEngineSoundComponent> ent, ref VehicleCanRunEvent args)
    {
        if (!ent.Comp.Switched)
            args.CanRun = false;
    }

    public void SetSwitched(Entity<CMUVehicleEngineSoundComponent> ent, bool switched)
    {
        if (ent.Comp.Switched == switched)
            return;

        ent.Comp.Switched = switched;
        Dirty(ent);
        var ev = new CMUVehicleEngineSwitchedEvent(switched);
        RaiseLocalEvent(ent, ref ev);
    }
}

/// <summary>
/// An open vehicle's engine was switched on or off.
/// </summary>
[ByRefEvent]
public readonly record struct CMUVehicleEngineSwitchedEvent(bool Switched);
