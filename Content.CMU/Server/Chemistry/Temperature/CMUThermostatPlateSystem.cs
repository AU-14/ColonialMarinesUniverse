using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Chemistry.Temperature;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Placeable;
using Content.Shared.Power;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Chemistry.Temperature;

/// <summary>
/// Replaces the upstream <c>SolutionHeaterSystem</c> on the hotplate and the old CMU chiller: a plate the player
/// switches on and gives a target temperature. While it is on, powered and has something on its
/// <see cref="ItemPlacerComponent"/>, it moves every placed solution towards the target, never past it and only
/// in the direction its <see cref="CMUThermostatMode"/> allows.
/// </summary>
public sealed class CMUThermostatPlateSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    /// <summary>
    /// How close to a target callers such as the synthesizer treat a solution as having arrived. The plate itself steps
    /// all the way to the target, since the clamp already prevents overshoot.
    /// </summary>
    public const float TargetTolerance = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUThermostatPlateComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<CMUThermostatPlateComponent, ItemPlacedEvent>(OnItemPlaced);
        SubscribeLocalEvent<CMUThermostatPlateComponent, ItemRemovedEvent>(OnItemRemoved);
        SubscribeLocalEvent<CMUThermostatPlateComponent, CMUThermostatSetTargetMessage>(OnSetTargetMessage);
        SubscribeLocalEvent<CMUThermostatPlateComponent, CMUThermostatToggleMessage>(OnToggleMessage);
        SubscribeLocalEvent<CMUThermostatPlateComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUThermostatPlateComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// Moves one solution towards <paramref name="target"/> by at most <paramref name="energy"/> thermal energy,
    /// stopping exactly at the target. Empty solutions and solutions already at the target are skipped; <see cref="CMUThermostatMode.HeatOnly"/> never cools and
    /// <see cref="CMUThermostatMode.CoolOnly"/> never heats.
    /// </summary>
    /// <param name="energy">Magnitude of the thermal energy for this step (e.g. energy per second times frame time).</param>
    /// <returns>True if the solution's temperature changed.</returns>
    public bool StepToward(Entity<SolutionComponent> soln, float target, float energy, CMUThermostatMode mode)
    {
        var solution = soln.Comp.Solution;

        if (energy <= 0f || float.IsNaN(target) || solution.Volume <= FixedPoint2.Zero)
            return false;

        // no heat capacity means AddThermalEnergyClamped would divide by zero
        if (solution.GetHeatCapacity(_proto) <= 0f)
            return false;

        var temperature = solution.Temperature;
        var difference = target - temperature;
        // the clamp lands exactly on the target, so after arriving this is exactly zero
        if (difference == 0f)
            return false;

        if (difference > 0f)
        {
            if (mode == CMUThermostatMode.CoolOnly)
                return false;

            _solutionContainer.AddThermalEnergyClamped(soln, energy, Math.Max(temperature, 0f), target);
        }
        else
        {
            if (mode == CMUThermostatMode.HeatOnly)
                return false;

            _solutionContainer.AddThermalEnergyClamped(soln, -energy, target, float.PositiveInfinity);
        }

        return solution.Temperature != temperature;
    }

    /// <summary>
    /// Switches the plate on or off, as the window and the verb do.
    /// </summary>
    public void SetEnabled(EntityUid uid, bool enabled)
    {
        if (!TryComp(uid, out CMUThermostatPlateComponent? comp))
            return;

        var ent = new Entity<CMUThermostatPlateComponent>(uid, comp);
        if (comp.Enabled != enabled)
        {
            comp.Enabled = enabled;
            Dirty(ent);
        }

        UpdateActive(ent);
    }

    /// <summary>
    /// Sets the target temperature, clamped to the plate's range.
    /// </summary>
    public void SetTarget(EntityUid uid, float target)
    {
        if (!TryComp(uid, out CMUThermostatPlateComponent? comp) || float.IsNaN(target))
            return;

        comp.TargetTemperature = Math.Clamp(target, comp.MinTarget, comp.MaxTarget);
        Dirty(uid, comp);
    }

    private void UpdateActive(Entity<CMUThermostatPlateComponent> ent, bool? powered = null)
    {
        var uid = ent.Owner;
        var on = ent.Comp.Enabled
                 && (powered ?? _powerReceiver.IsPowered(uid))
                 && TryComp(uid, out ItemPlacerComponent? placer)
                 && placer.PlacedEntities.Count > 0;

        // the hotplate's "on" layer (recoloured on the chiller prototype)
        _appearance.SetData(uid, SolutionHeaterVisuals.IsOn, on);

        if (on)
            EnsureComp<CMUActiveThermostatPlateComponent>(uid);
        else
            RemComp<CMUActiveThermostatPlateComponent>(uid);
    }

    private void OnPowerChanged(Entity<CMUThermostatPlateComponent> ent, ref PowerChangedEvent args)
    {
        UpdateActive(ent, args.Powered);
    }

    private void OnItemPlaced(Entity<CMUThermostatPlateComponent> ent, ref ItemPlacedEvent args)
    {
        UpdateActive(ent);
    }

    private void OnItemRemoved(Entity<CMUThermostatPlateComponent> ent, ref ItemRemovedEvent args)
    {
        UpdateActive(ent);
    }

    private void OnSetTargetMessage(Entity<CMUThermostatPlateComponent> ent, ref CMUThermostatSetTargetMessage args)
    {
        SetTarget(ent.Owner, args.Target);
    }

    private void OnToggleMessage(Entity<CMUThermostatPlateComponent> ent, ref CMUThermostatToggleMessage args)
    {
        SetEnabled(ent.Owner, !ent.Comp.Enabled);
    }

    private void OnGetVerbs(Entity<CMUThermostatPlateComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var uid = ent.Owner;
        var enabled = ent.Comp.Enabled;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(enabled ? "cmu-thermostat-verb-off" : "cmu-thermostat-verb-on"),
            Act = () => SetEnabled(uid, !enabled),
        });
    }

    private void OnExamined(Entity<CMUThermostatPlateComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using var block = args.PushGroup(nameof(CMUThermostatPlateComponent));
        args.PushMarkup(Loc.GetString(ent.Comp.Enabled ? "cmu-thermostat-examine-on" : "cmu-thermostat-examine-off"));
        args.PushMarkup(Loc.GetString("cmu-thermostat-examine-target",
            ("target", (int) MathF.Round(ent.Comp.TargetTemperature))));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CMUActiveThermostatPlateComponent, CMUThermostatPlateComponent, ItemPlacerComponent>();
        while (query.MoveNext(out _, out _, out var plate, out var placer))
        {
            var energy = plate.EnergyPerSecond * frameTime;
            if (energy <= 0f)
                continue;

            foreach (var placed in placer.PlacedEntities)
            {
                foreach (var (_, soln) in _solutionContainer.EnumerateSolutions(placed))
                {
                    StepToward(soln, plate.TargetTemperature, energy, plate.Mode);
                }
            }
        }
    }
}
