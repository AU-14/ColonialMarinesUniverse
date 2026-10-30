using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Visuals;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

/// <summary>
///     Runs <see cref="CMUAirFillerComponent"/> vents: once a second, each one below its target pressure adds enough
///     breathable air to close the gap, up to its per-second limit. It needs no pipes or power.
/// </summary>
public sealed partial class CMUAirFillerSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<CMUAirFillerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var filler, out var xform))
        {
            var filling = xform.Anchored && TryFill(uid, filler);
            if (filling == filler.Filling)
                continue;

            filler.Filling = filling;
            _appearance.SetData(uid, VentPumpVisuals.State, filling ? VentPumpState.Out : VentPumpState.Off);
        }
    }

    private bool TryFill(EntityUid uid, CMUAirFillerComponent filler)
    {
        if (_atmosphere.GetContainingMixture(uid, excite: true) is not { } air ||
            air.Pressure >= filler.TargetPressure)
        {
            return false;
        }

        // Moles needed to bring this tile up to the target: n = PV / RT.
        var needed = (filler.TargetPressure - air.Pressure) * air.Volume / (Atmospherics.R * filler.Temperature);
        var moles = MathF.Min(needed, filler.MaxMolesPerSecond * (float) CheckInterval.TotalSeconds);
        if (moles <= 0)
            return false;

        var added = new GasMixture(air.Volume) { Temperature = filler.Temperature };
        added.AdjustMoles(Gas.Oxygen, moles * filler.OxygenFraction);
        added.AdjustMoles(Gas.Nitrogen, moles * (1 - filler.OxygenFraction));
        _atmosphere.Merge(air, added);
        return true;
    }
}
