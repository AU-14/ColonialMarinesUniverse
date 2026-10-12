using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Explosion.Components;
using Content.Shared.GameTicking;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Vehicle;

/// <summary>
/// Rockets and IEDs carry an anti-vehicle multiplier, but explosions are credited to whoever fired them,
/// so the blast half of a rocket hit never saw it. Remember where a multiplied explosive went off and
/// scale that blast when it reaches a vehicle.
/// </summary>
public sealed partial class CMUVehicleBlastMultiplierSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // explosions are queued and processed over the next ticks, a few seconds covers big ones
    private static readonly TimeSpan BlastMemory = TimeSpan.FromSeconds(5);
    private const float EpicenterTolerance = 0.5f;

    private readonly List<(MapCoordinates Epicenter, float Multiplier, TimeSpan Expires)> _blasts = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleDamageMultiplierComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<HardpointSlotsComponent, ExplosionDamagePreparingEvent>(OnVehicleBlast);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _blasts.Clear());
    }

    private void OnTrigger(Entity<VehicleDamageMultiplierComponent> ent, ref TriggerEvent args)
    {
        if (!HasComp<ExplosiveComponent>(ent) || ent.Comp.Multiplier <= 1f)
            return;

        Prune();
        _blasts.Add((_transform.GetMapCoordinates(ent), ent.Comp.Multiplier, _timing.CurTime + BlastMemory));
    }

    private void OnVehicleBlast(Entity<HardpointSlotsComponent> ent, ref ExplosionDamagePreparingEvent args)
    {
        Prune();
        var multiplier = 1f;
        foreach (var blast in _blasts)
        {
            if (blast.Epicenter.MapId == args.Epicenter.MapId &&
                (blast.Epicenter.Position - args.Epicenter.Position).LengthSquared() <= EpicenterTolerance * EpicenterTolerance)
            {
                multiplier = MathF.Max(multiplier, blast.Multiplier);
            }
        }

        if (multiplier > 1f)
            args.Damage *= multiplier;
    }

    private void Prune()
    {
        var now = _timing.CurTime;
        _blasts.RemoveAll(b => b.Expires <= now);
    }
}
