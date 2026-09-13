using Content.Shared._RMC14.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;

namespace Content.Server._RMC14.Medical;

public sealed class RMCHypospraySystem : RMCSharedHypospraySystem
{
    [Dependency] private InjectorSystem _injector = default!;

    protected override void OnInteractUsing(Entity<RMCHyposprayComponent> ent, ref InteractUsingEvent args)
    {
        base.OnInteractUsing(ent, ref args);

        if (args.Handled)
            return;

        if (!_container.TryGetContainer(ent, ent.Comp.SlotId, out var container))
            return;


        if (!TryComp<ItemSlotsComponent>(ent, out var slots))
            return;
        // Dont transfer when vial is used
        if (_slots.CanInsert(ent, slots.Slots[ent.Comp.SlotId], args.Used, args.User, true))
            return;

        if (container.ContainedEntities.Count == 0)
            return;

        var vial = container.ContainedEntities[0];

        // Syringe and Spikable handling mostly copied from various places
        // Might be better to convert some stuff to events later

        if (HasComp<InjectorComponent>(args.Used))
        {
            InjectorVialHandling(ent, vial, args.Used, args.User);
            args.Handled = true;
            return;
        }

        if (HasComp<SolutionSpikerComponent>(args.Used))
        {
            SpikableHandling(ent, vial, args.Used, args.User);
            args.Handled = true;
            return;
        }
    }

    // Pretty much a direct copy of the spikablesystem with slight tweaks
    private void SpikableHandling(Entity<RMCHyposprayComponent> ent, EntityUid vial, EntityUid spikable, EntityUid user)
    {
        if (!TryComp<SolutionSpikerComponent>(spikable, out var spike))
            return;

        if (!_solution.TryGetRefillableSolution(vial, out var targetSoln, out var targetSolution)
    || !_solution.TryGetSolution(spikable, spike.SourceSolution, out _, out var sourceSolution))
        {
            return;
        }

        if (targetSolution.Volume == 0 && !spike.IgnoreEmpty)
        {
            _popup.PopupEntity(Loc.GetString(spike.PopupEmpty, ("spiked-entity", vial), ("spike-entity", spikable)), user, user);
            return;
        }

        if (!_solution.ForceAddSolution(targetSoln.Value, sourceSolution))
            return;

        _popup.PopupEntity(Loc.GetString(spike.Popup, ("spiked-entity", vial), ("spike-entity", spikable)), user, user);
        sourceSolution.RemoveAllSolution();
        if (spike.Delete)
            QueueDel(spikable);

        UpdateAppearance(ent);
    }

    private void InjectorVialHandling(Entity<RMCHyposprayComponent> ent, EntityUid vial, EntityUid injector, EntityUid user)
    {
        if (!TryComp<InjectorComponent>(injector, out var syringe))
            return;

        if (!_solution.TryGetSolution(injector, syringe.SolutionName, out var syringeSolutionComp, out var syringeSolution))
            return;

        if (!ProtoMan.Resolve(syringe.ActiveModeProtoId, out var activeMode))
            return;

        var injecting = activeMode.Behavior switch
        {
            InjectorBehavior.Inject => true,
            InjectorBehavior.Draw or InjectorBehavior.Dynamic => false,
            _ => throw new ArgumentOutOfRangeException(),
        };

        Entity<SolutionComponent>? vialSolutionComp;
        Solution? vialSolution;

        if (injecting)
        {
            if (!_solution.TryGetInjectableSolution(vial, out vialSolutionComp, out vialSolution))
                return;
        }
        else
        {
            if (!_solution.TryGetDrawableSolution(vial, out vialSolutionComp, out vialSolution))
                return;
        }

        var requestedAmount = syringe.CurrentTransferAmount ??
            (injecting ? syringeSolution.Volume : FixedPoint2.New(5));
        var transferAmount = injecting
            ? FixedPoint2.Min(requestedAmount, vialSolution.AvailableVolume)
            : FixedPoint2.Min(requestedAmount, syringeSolution.AvailableVolume);

        if (transferAmount <= 0)
        {
            if (injecting)
                _popup.PopupEntity(Loc.GetString("rmc-hypospray-full", ("vial", vial)), ent, user);
            else
                _popup.PopupEntity(Loc.GetString("rmc-hypospray-full", ("vial", injector)), ent, user);
            return;
        }

        if (!injecting)
        {
            var removed = _solution.Draw(vial, vialSolutionComp.Value, transferAmount);
            if (!_solution.TryAddSolution(syringeSolutionComp.Value, removed))
                return;
            _popup.PopupEntity(Loc.GetString("injector-component-draw-success-message",
                                ("amount", removed.Volume),
                                ("target", Identity.Entity(vial, EntityManager))), injector, user);

            if (syringeSolution.Volume == syringeSolution.MaxVolume)
                TrySetMode((injector, syringe), user, InjectorBehavior.Inject);
        }
        else
        {
            var adding = _solution.SplitSolution(syringeSolutionComp.Value, transferAmount);
            _solution.Inject(vial, vialSolutionComp.Value, adding);
            _popup.PopupEntity(Loc.GetString("injector-component-transfer-success-message",
                                ("amount", adding.Volume),
                                ("target", Identity.Entity(vial, EntityManager))), injector, user);

            if (syringeSolution.Volume == 0)
                TrySetMode((injector, syringe), user, InjectorBehavior.Draw);
        }

        Dirty(syringeSolutionComp.Value);
        Dirty(vialSolutionComp.Value);

        UpdateAppearance(ent);
    }

    private void TrySetMode(Entity<InjectorComponent> injector, EntityUid user, InjectorBehavior behavior)
    {
        if (!ProtoMan.Resolve(injector.Comp.ActiveModeProtoId, out var activeMode) ||
            activeMode.Behavior == InjectorBehavior.Dynamic)
        {
            return;
        }

        foreach (var mode in injector.Comp.AllowedModes)
        {
            if (!ProtoMan.Resolve(mode, out InjectorModePrototype? modePrototype) ||
                !modePrototype.Behavior.HasFlag(behavior))
            {
                continue;
            }

            _injector.ToggleMode(injector, user, modePrototype, false);
            return;
        }
    }
}
