using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Atmos; // CMU14
using Content.Shared.Examine;
using Content.Shared.Temperature;

namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedGasMinerSystem : EntitySystem
{
    [Dependency] private SharedAtmosphereSystem _sharedAtmosphereSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasMinerComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<GasMinerComponent, AnchorStateChangedEvent>(OnMinerAnchorChanged);
    }

    private void OnExamine(Entity<GasMinerComponent> ent, ref ExaminedEvent args)
    {
        var component = ent.Comp;

        if (!component.ShowExamineText)
            return;

        using (args.PushGroup(nameof(GasMinerComponent)))
        {
            // CMU14: mixture miners should describe every gas they emit.
            var gasName = Loc.GetString(_sharedAtmosphereSystem.GetGas(component.SpawnGas).Name);
            if (TryComp<CMUGasMinerMixtureComponent>(ent, out var mixture))
            {
                var gasNames = new List<string>();
                foreach (var (gas, weight) in mixture.Gases)
                {
                    if (weight > 0f && float.IsFinite(weight))
                        gasNames.Add(Loc.GetString(_sharedAtmosphereSystem.GetGas(gas).Name));
                }

                if (gasNames.Count > 0)
                    gasName = string.Join(", ", gasNames);
            }

            args.PushMarkup(Loc.GetString("gas-miner-mines-text",
                ("gas", gasName)));

            args.PushText(Loc.GetString("gas-miner-amount-text",
                ("moles", $"{component.SpawnAmount:0.#}")));

            args.PushText(Loc.GetString("gas-miner-temperature-text",
                ("tempK", $"{component.SpawnTemperature:0.#}"),
                ("tempC", $"{TemperatureHelpers.KelvinToCelsius(component.SpawnTemperature):0.#}")));

            if (component.MaxExternalAmount < float.PositiveInfinity)
            {
                args.PushText(Loc.GetString("gas-miner-moles-cutoff-text",
                    ("moles", $"{component.MaxExternalAmount:0.#}")));
            }

            if (component.MaxExternalPressure < float.PositiveInfinity)
            {
                args.PushText(Loc.GetString("gas-miner-pressure-cutoff-text",
                    ("pressure", $"{component.MaxExternalPressure:0.#}")));
            }

            args.AddMarkup(component.MinerState switch
            {
                GasMinerState.Disabled => Loc.GetString("gas-miner-state-disabled-text"),
                GasMinerState.Idle => Loc.GetString("gas-miner-state-idle-text"),
                GasMinerState.Working => Loc.GetString("gas-miner-state-working-text"),
                // C# pattern matching is not exhaustive for enums
                _ => throw new IndexOutOfRangeException(nameof(component.MinerState)),
            });
        }
    }

    // This is merely to update the examine text.
    // when a gas miner with RequireAnchored (in the atmos device comp) set to true is unanchored,
    // it leaves the atmosphere and stops receiving AtmosDeviceUpdateEvent which spawns the gas
    private void OnMinerAnchorChanged(Entity<GasMinerComponent> entity, ref AnchorStateChangedEvent args)
    {
        if (!TryComp<AtmosDeviceComponent>(entity, out var atmosDevice))
            return;

        if (!args.Anchored && atmosDevice.RequireAnchored)
            entity.Comp.MinerState = GasMinerState.Disabled;
    }
}
