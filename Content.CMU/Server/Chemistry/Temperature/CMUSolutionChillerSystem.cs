using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Placeable;
using Content.Shared.Power;

namespace Content.Server.CMU14.Chemistry.Temperature;

/// <summary>
/// Mirrors the upstream <c>SolutionHeaterSystem</c> the other way round: a powered chiller with something on its
/// <see cref="ItemPlacerComponent"/> removes heat from every solution on it. Uses the clamped thermal energy call so a
/// solution stops at <see cref="CMUSolutionChillerComponent.MinTemperature"/> instead of falling towards 0 K.
/// </summary>
public sealed partial class CMUSolutionChillerSystem : EntitySystem
{
    [Dependency] private PowerReceiverSystem _powerReceiver = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUSolutionChillerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<CMUSolutionChillerComponent, ItemPlacedEvent>(OnItemPlaced);
        SubscribeLocalEvent<CMUSolutionChillerComponent, ItemRemovedEvent>(OnItemRemoved);
    }

    private void TurnOn(EntityUid uid)
    {
        // the hotplate's "on" layer, recoloured on the chiller prototype
        _appearance.SetData(uid, SolutionHeaterVisuals.IsOn, true);
        EnsureComp<CMUActiveSolutionChillerComponent>(uid);
    }

    public bool TryTurnOn(EntityUid uid, ItemPlacerComponent? placer = null)
    {
        if (!Resolve(uid, ref placer))
            return false;

        if (placer.PlacedEntities.Count <= 0 || !_powerReceiver.IsPowered(uid))
            return false;

        TurnOn(uid);
        return true;
    }

    public void TurnOff(EntityUid uid)
    {
        _appearance.SetData(uid, SolutionHeaterVisuals.IsOn, false);
        RemComp<CMUActiveSolutionChillerComponent>(uid);
    }

    private void OnPowerChanged(Entity<CMUSolutionChillerComponent> ent, ref PowerChangedEvent args)
    {
        if (args.Powered && TryComp(ent, out ItemPlacerComponent? placer) && placer.PlacedEntities.Count > 0)
            TurnOn(ent);
        else
            TurnOff(ent);
    }

    private void OnItemPlaced(Entity<CMUSolutionChillerComponent> ent, ref ItemPlacedEvent args)
    {
        TryTurnOn(ent);
    }

    private void OnItemRemoved(Entity<CMUSolutionChillerComponent> ent, ref ItemRemovedEvent args)
    {
        if (!TryComp(ent, out ItemPlacerComponent? placer) || placer.PlacedEntities.Count == 0)
            TurnOff(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CMUActiveSolutionChillerComponent, CMUSolutionChillerComponent, ItemPlacerComponent>();
        while (query.MoveNext(out _, out _, out var chiller, out var placer))
        {
            var energy = -chiller.CoolPerSecond * frameTime;
            if (energy >= 0)
                continue;

            foreach (var placed in placer.PlacedEntities)
            {
                foreach (var (_, soln) in _solutionContainer.EnumerateSolutions(placed))
                {
                    var solution = soln.Comp.Solution;

                    // empty solutions have no heat capacity, and anything already colder is left alone
                    // (the clamp would otherwise warm it back up to the minimum)
                    if (solution.Volume <= 0 || solution.Temperature <= chiller.MinTemperature)
                        continue;

                    _solutionContainer.AddThermalEnergyClamped(soln, energy, chiller.MinTemperature, float.PositiveInfinity);
                }
            }
        }
    }
}
