using System.Diagnostics.CodeAnalysis;
using Content.Server.Power.Components; // CMU14
using Content.Server.Power.EntitySystems; // CMU14
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.CMU14.Atmos; // CMU14
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server.Atmos.EntitySystems;

[UsedImplicitly]
public sealed partial class GasMinerSystem : SharedGasMinerSystem
{
    [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private PowerReceiverSystem _power = default!; // CMU14
    [Dependency] private TransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasMinerComponent, AtmosDeviceUpdateEvent>(OnMinerUpdated);
    }

    private void OnMinerUpdated(Entity<GasMinerComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var miner = ent.Comp;
        var oldState = miner.MinerState;
        float toSpawn;

        // CMU14: powered miners (portable colony tier) idle without power.
        // mapper placed miners carry no receiver and are unaffected
        if (TryComp<ApcPowerReceiverComponent>(ent, out var receiver) && !_power.IsPowered(ent, receiver))
        {
            if (miner.MinerState != GasMinerState.Disabled)
            {
                miner.MinerState = GasMinerState.Disabled;
                Dirty(ent);
            }
            return;
        }

        if (!GetValidEnvironment(ent, out var environment))
        {
            miner.MinerState = GasMinerState.Disabled;
        }
        // SpawnAmount is declared in mol/s so to get the amount of gas we hope to mine, we have to multiply this by
        // how long we have been waiting to spawn it and further cap the number according to the miner's state.
        else if ((toSpawn = CapSpawnAmount(ent, miner.SpawnAmount * args.dt, environment)) == 0)
        {
            miner.MinerState = GasMinerState.Idle;
        }
        else
        {
            // Time to mine some gas.
            if (TryCreateOutputMixture(ent, toSpawn, out var merger)) // CMU14
            {
                miner.MinerState = GasMinerState.Working;
                _atmosphereSystem.Merge(environment, merger);
            }
            else
            {
                miner.MinerState = GasMinerState.Disabled;
            }
        }

        if (miner.MinerState != oldState)
        {
            Dirty(ent);
        }
    }

    // CMU14: allow special-purpose miners to emit a true gas mixture while keeping
    // GasMinerComponent.SpawnAmount as the total mol/s output.
    private bool TryCreateOutputMixture(
        Entity<GasMinerComponent> ent,
        float toSpawn,
        [NotNullWhen(true)] out GasMixture? output)
    {
        var miner = ent.Comp;
        output = new GasMixture(1) { Temperature = miner.SpawnTemperature };

        if (!TryComp<CMUGasMinerMixtureComponent>(ent, out var mixture))
        {
            output.SetMoles(miner.SpawnGas, toSpawn);
            return true;
        }

        var totalWeight = 0f;
        foreach (var weight in mixture.Gases.Values)
        {
            if (weight > 0f && float.IsFinite(weight))
                totalWeight += weight;
        }

        if (totalWeight <= 0f || !float.IsFinite(totalWeight))
        {
            output = null;
            return false;
        }

        foreach (var (gas, weight) in mixture.Gases)
        {
            if (weight > 0f && float.IsFinite(weight))
                output.SetMoles(gas, toSpawn * weight / totalWeight);
        }

        return true;
    }

    private bool GetValidEnvironment(Entity<GasMinerComponent> ent, [NotNullWhen(true)] out GasMixture? environment)
    {
        var (uid, miner) = ent;
        var transform = Transform(uid);
        var position = _transformSystem.GetGridOrMapTilePosition(uid, transform);

        // Treat space as an invalid environment
        if (_atmosphereSystem.IsTileSpace(transform.GridUid, transform.MapUid, position))
        {
            environment = null;
            return false;
        }

        environment = _atmosphereSystem.GetContainingMixture((uid, transform), true, true);
        return environment != null;
    }

    private float CapSpawnAmount(Entity<GasMinerComponent> ent, float toSpawnTarget, GasMixture environment)
    {
        var (uid, miner) = ent;

        // How many moles could we theoretically spawn. Cap by pressure and amount.
        var allowableMoles = Math.Min(
            (miner.MaxExternalPressure - environment.Pressure) * environment.Volume / (miner.SpawnTemperature * Atmospherics.R),
            miner.MaxExternalAmount - environment.TotalMoles);

        var toSpawnReal = Math.Clamp(allowableMoles, 0f, toSpawnTarget);

        if (toSpawnReal < Atmospherics.GasMinMoles) {
            return 0f;
        }

        return toSpawnReal;
    }
}
