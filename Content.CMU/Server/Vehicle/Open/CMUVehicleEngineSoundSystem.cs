using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Vehicle.Open;

/// <summary>
/// Runs an open vehicle's engine from its switch, key, fuel, engine and speed: plays the start, rev,
/// stall and switching off, and tells clients when to play the running loops. A new driver at the
/// wheel switches it on; it runs on without one. See <see cref="CMUVehicleEngineSoundComponent"/>.
/// </summary>
public sealed class CMUVehicleEngineSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CMUVehicleEngineSwitchSystem _engine = default!;
    [Dependency] private CMUVehicleFuelSystem _fuel = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUVehicleEngineSoundComponent, VehicleComponent, GridVehicleMoverComponent>();
        while (query.MoveNext(out var uid, out var engine, out var vehicle, out var mover))
        {
            if (vehicle.Operator != engine.Driver)
            {
                engine.Driver = vehicle.Operator;
                if (vehicle.Operator != null)
                    _engine.SetSwitched((uid, engine), true);
            }

            var canRun = engine.Switched && CanRun((uid, vehicle));
            if (!engine.On)
            {
                // Switched on without a key, fuel or a working engine, it doesn't catch.
                if (!canRun)
                {
                    _engine.SetSwitched((uid, engine), false);
                    continue;
                }

                engine.On = true;
                engine.IdleAt = now + engine.IdleDelay;
                engine.WasMoving = false;
                engine.StartStream = _audio.PlayPvs(engine.StartSound, uid)?.Entity;
                continue;
            }

            if (!canRun)
            {
                // Switched off or the key pulled, it winds down; a dry tank or a dead engine stalls it.
                var switchedOff = !engine.Switched;
                engine.On = false;
                engine.IdleAt = null;
                engine.StartStream = _audio.Stop(engine.StartStream);
                SetRunning((uid, engine), false, engine.Rough);
                _engine.SetSwitched((uid, engine), false);
                _audio.PlayPvs(!switchedOff && IsStalled(uid) ? engine.StallSound : engine.OffSound, uid);
                continue;
            }

            if (engine.IdleAt is { } idleAt)
            {
                if (now < idleAt)
                    continue;

                engine.IdleAt = null;
            }

            SetRunning((uid, engine), true, IsRough(uid));

            var moving = MathF.Abs(mover.CurrentSpeed) > engine.MovingSpeed;
            if (moving && !engine.WasMoving && now >= engine.NextRev)
            {
                engine.NextRev = now + engine.RevCooldown;
                _audio.PlayPvs(engine.RevSound, uid);
            }

            engine.WasMoving = moving;
        }
    }

    private bool CanRun(Entity<VehicleComponent> vehicle)
    {
        var ev = new VehicleCanRunEvent(vehicle);
        RaiseLocalEvent(vehicle, ref ev);
        return ev.CanRun;
    }

    /// <summary>
    /// The engine itself gave out: a dry tank, a dead engine, or a hull at zero (which kills it).
    /// </summary>
    private bool IsStalled(EntityUid uid)
    {
        return HasComp<CMUVehicleFuelComponent>(uid) && !_fuel.HasFuel(uid) ||
               TryComp(uid, out CMUOpenVehicleComponent? jeep) && jeep.EngineIntegrity <= 0f ||
               TryComp(uid, out HardpointIntegrityComponent? hull) && hull.MaxIntegrity > 0f && hull.Integrity <= 0f;
    }

    private bool IsRough(EntityUid uid)
    {
        return TryComp(uid, out CMUOpenVehicleComponent? jeep) &&
               jeep.EngineMaxIntegrity > 0f &&
               jeep.EngineIntegrity / jeep.EngineMaxIntegrity < jeep.EngineSmokeFraction;
    }

    private void SetRunning(Entity<CMUVehicleEngineSoundComponent> ent, bool running, bool rough)
    {
        if (ent.Comp.EngineRunning == running && ent.Comp.Rough == rough)
            return;

        ent.Comp.EngineRunning = running;
        ent.Comp.Rough = rough;
        Dirty(ent);
    }
}
