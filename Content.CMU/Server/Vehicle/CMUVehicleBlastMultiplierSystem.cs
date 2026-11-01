using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Explosion.Components;
using Content.Shared.GameTicking;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Vehicle;

/// <summary>
/// Rockets and IEDs carry an anti-vehicle multiplier, but the blast is credited to the explosive and it's
/// usually deleted by the time the blast lands, so the blast half of a rocket hit never saw it. Remember
/// where a multiplied explosive went off and scale that blast when it reaches a vehicle.
/// </summary>
public sealed partial class CMUVehicleBlastMultiplierSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // explosions are queued and processed over the next ticks, a few seconds covers big ones
    private static readonly TimeSpan BlastMemory = TimeSpan.FromSeconds(5);
    private const float EpicenterTolerance = 0.5f;

    private readonly List<(EntityUid Source, MapCoordinates Epicenter, float Multiplier, TimeSpan Expires)> _blasts = new();

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
        _blasts.Add((ent, _transform.GetMapCoordinates(ent), ent.Comp.Multiplier, _timing.CurTime + BlastMemory));
    }

    private void OnVehicleBlast(Entity<HardpointSlotsComponent> ent, ref ExplosionDamagePreparingEvent args)
    {
        Prune();

        // Tanks with a blast profile already scale every blast by explosion type and deliberately ignore the
        // source's multiplier; carrying it in here as well would stack the two and pin tank blasts to the cap.
        if (HasComp<CMUTankBlastProfileComponent>(ent))
            return;

        var multiplier = 1f;
        foreach (var blast in _blasts)
        {
            // the blast is credited to the explosive, so while it's still around the hardpoint damage
            // code reads the multiplier off it itself. scaling here too squared it and gutted the vehicle
            if (HasComp<VehicleDamageMultiplierComponent>(blast.Source))
                continue;

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
