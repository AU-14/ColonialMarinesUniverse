using System.Linq;
using Content.Shared._RMC14.Stun;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Vehicle.Jeep;

/// <summary>
/// Blacks the riders of a vehicle out on a hard head on crash and knocks some out for longer, each by
/// their own roll, and blares the horn while its driver is out on the wheel.
/// </summary>
public sealed class CMUVehicleCrashKnockoutSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleCrashKnockoutComponent, CMUVehicleCrashedEvent>(OnCrashed);
        SubscribeLocalEvent<CMUVehicleCrashKnockoutComponent, ComponentShutdown>(OnShutdown);
    }

    /// <summary>
    /// The chance each rider is knocked out by a crash at a share of top speed: none below the
    /// minimum, the minimum chance there, rising evenly to certain at top speed. Any crash from the
    /// minimum on blacks every rider out for a moment.
    /// </summary>
    public static float Chance(CMUVehicleCrashKnockoutComponent comp, bool frontal, float speed)
    {
        if (!frontal || speed < comp.MinSpeed)
            return 0f;

        if (comp.MinSpeed >= 1f)
            return 1f;

        var t = Math.Clamp((speed - comp.MinSpeed) / (1f - comp.MinSpeed), 0f, 1f);
        return comp.MinChance + (1f - comp.MinChance) * t;
    }

    private void OnCrashed(Entity<CMUVehicleCrashKnockoutComponent> ent, ref CMUVehicleCrashedEvent args)
    {
        var chance = Chance(ent.Comp, args.Frontal, args.Speed);
        if (chance <= 0f || !TryComp(ent, out CMUVehicleSeatsComponent? seats))
            return;

        foreach (var seat in seats.SeatEntities)
        {
            if (!TryComp(seat, out StrapComponent? strap) || strap.BuckledEntities.Count == 0)
                continue;

            var driver = CompOrNull<CMUVehicleSeatComponent>(seat)?.Driver == true;
            // Knocked out, a rider may slump out of the seat, which changes the strap's riders.
            foreach (var rider in strap.BuckledEntities.ToList())
            {
                if (HasComp<RMCUnconsciousComponent>(rider))
                    continue;

                // Everyone blacks out for a moment; whoever loses the roll stays out.
                var knockedOut = _random.Prob(chance);
                var duration = knockedOut ? _random.Next(ent.Comp.MinDuration, ent.Comp.MaxDuration) : ent.Comp.Blackout;
                if (!_sizeStun.TryKnockOut(rider, duration, false))
                    continue;

                if (!knockedOut)
                {
                    _popup.PopupEntity(Loc.GetString("cmu-vehicle-crash-blackout"), rider, rider, PopupType.MediumCaution);
                    continue;
                }

                _popup.PopupEntity(Loc.GetString("cmu-vehicle-crash-knocked-out", ("rider", rider)), rider, PopupType.MediumCaution);
                if (driver)
                    BlareHorn(ent, rider, duration);
            }
        }
    }

    private void BlareHorn(Entity<CMUVehicleCrashKnockoutComponent> ent, EntityUid driver, TimeSpan duration)
    {
        ent.Comp.Driver = driver;
        ent.Comp.HornUntil = _timing.CurTime + duration;
        if (ent.Comp.Horn != null || ent.Comp.HornSound == null)
            return;

        ent.Comp.Horn = _audio.PlayPvs(ent.Comp.HornSound, ent)?.Entity;
    }

    private void OnShutdown(Entity<CMUVehicleCrashKnockoutComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Horn = _audio.Stop(ent.Comp.Horn);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUVehicleCrashKnockoutComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Horn == null)
                continue;

            // The horn stops when the driver comes to, or is no longer slumped in the vehicle.
            if (now < comp.HornUntil &&
                comp.Driver is { } driver &&
                HasComp<RMCUnconsciousComponent>(driver) &&
                CompOrNull<BuckleComponent>(driver)?.BuckledTo is { } seat &&
                CompOrNull<CMUVehicleSeatComponent>(seat)?.Vehicle == uid)
            {
                continue;
            }

            comp.Horn = _audio.Stop(comp.Horn);
            comp.Driver = null;
        }
    }
}
