using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Systems;

namespace Content.Server.CMU14.Medical;

/// <summary>
/// Breathing from an oxygen tank through internals clears suffocation damage three times as fast as normal breathing.
/// </summary>
public sealed class CMUOxygenTankRecoverySystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;

    /// <summary>
    /// Normal breathing already heals once per breath; this adds the rest.
    /// </summary>
    private const float ExtraRecoveryMultiplier = 2f;

    public override void Initialize()
    {
        SubscribeLocalEvent<InternalsComponent, StopSuffocatingEvent>(OnBreathed);
    }

    private void OnBreathed(Entity<InternalsComponent> ent, ref StopSuffocatingEvent args)
    {
        if (!_internals.AreInternalsWorking(ent.Comp) ||
            ent.Comp.GasTankEntity is not { } tank ||
            !TryComp<GasTankComponent>(tank, out var gasTank) ||
            !TryComp<RespiratorComponent>(ent, out var respirator))
        {
            return;
        }

        var air = gasTank.Air;
        if (air.GetMoles(Gas.Oxygen) <= 0f)
            return;

        var recovery = respirator.DamageRecovery;
        _damageable.ChangeDamage(ent.Owner, recovery * ExtraRecoveryMultiplier);
    }
}
