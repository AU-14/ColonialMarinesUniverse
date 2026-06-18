using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Log;
using BreathToolComponent = Content.Shared.Atmos.Components.BreathToolComponent;
using InternalsComponent = Content.Shared.Body.Components.InternalsComponent;

namespace Content.Server.Body.Systems;

public sealed partial class LungSystem : EntitySystem
{
    // CMU14 start
    private const string CMUAnesthesiaSawmillName = "cmu.medical.anesthesia";
    // CMU14 end

    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private InternalsSystem _internals = default!;
    // CMU14 start
    [Dependency] private ILogManager _log = default!;
    // CMU14 end
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;

    // CMU14 start
    private ISawmill _anesthesiaSawmill = default!;
    // CMU14 end

    public static string LungSolutionName = "Lung";

    public override void Initialize()
    {
        base.Initialize();

        // CMU14 start
        _anesthesiaSawmill = _log.GetSawmill(CMUAnesthesiaSawmillName);
        // CMU14 end

        SubscribeLocalEvent<LungComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BreathToolComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<BreathToolComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotUnequipped(Entity<BreathToolComponent> ent, ref GotUnequippedEvent args)
    {
        // CMU14 start
        DebugBreathTool(ent, "unequipped", args.Equipee, args.SlotFlags);
        // CMU14 end
        _atmos.DisconnectInternals(ent);
    }

    private void OnGotEquipped(Entity<BreathToolComponent> ent, ref GotEquippedEvent args)
    {
        // CMU14 start
        DebugBreathTool(ent, "equipped-start", args.Equipee, args.SlotFlags);
        // CMU14 end

        if ((args.SlotFlags & ent.Comp.AllowedSlots) == 0)
        {
            // CMU14 start
            DebugBreathTool(ent, "equipped-rejected-slot", args.Equipee, args.SlotFlags);
            // CMU14 end
            return;
        }

        if (TryComp(args.Equipee, out InternalsComponent? internals))
        {
            ent.Comp.ConnectedInternalsEntity = args.Equipee;
            _internals.ConnectBreathTool((args.Equipee, internals), ent);
            // CMU14 start
            DebugBreathTool(ent, "equipped-connected", args.Equipee, args.SlotFlags);
            // CMU14 end
            return;
        }

        // CMU14 start
        DebugBreathTool(ent, "equipped-no-internals", args.Equipee, args.SlotFlags);
        // CMU14 end
    }

    private void OnComponentInit(Entity<LungComponent> entity, ref ComponentInit args)
    {
        if (_solutionContainerSystem.EnsureSolution(entity.Owner, entity.Comp.SolutionName, out var solution))
        {
            solution.MaxVolume = 100.0f;
            solution.CanReact = false; // No dexalin lungs
        }
    }

    public void GasToReagent(EntityUid uid, LungComponent lung)
    {
        if (!_solutionContainerSystem.ResolveSolution(uid, lung.SolutionName, ref lung.Solution, out var solution))
            return;

        GasToReagent(lung.Air, solution);
        _solutionContainerSystem.UpdateChemicals(lung.Solution.Value);
    }

    /* This should really be moved to somewhere in the atmos system and modernized,
     so that other systems, like CondenserSystem, can use it.
     */
    private void GasToReagent(GasMixture gas, Solution solution)
    {
        foreach (var gasId in Enum.GetValues<Gas>())
        {
            var i = (int) gasId;
            var moles = gas[i];
            if (moles <= 0)
                continue;

            var reagent = _atmos.GasReagents[i];
            if (reagent is null)
                continue;

            var amount = moles * Atmospherics.BreathMolesToReagentMultiplier;
            solution.AddReagent(reagent, amount);
        }
    }

    public Solution GasToReagent(GasMixture gas)
    {
        var solution = new Solution();
        GasToReagent(gas, solution);
        return solution;
    }

    // CMU14 start
    private void DebugBreathTool(
        Entity<BreathToolComponent> ent,
        string stage,
        EntityUid wearer,
        SlotFlags slotFlags)
    {
        if (!IsDebugMask(ent.Owner))
            return;

        var hasMask = TryComp<MaskComponent>(ent.Owner, out _);
        var hasInternals = TryComp<InternalsComponent>(wearer, out var internals);
        _anesthesiaSawmill.Debug(
            $"[CMU anesthesia] breath-tool-{stage}: tool={DebugEntity(ent.Owner)}, wearer={DebugEntity(wearer)}, slotFlags={slotFlags}, allowed={ent.Comp.AllowedSlots}, connected={DebugEntity(ent.Comp.ConnectedInternalsEntity)}, hasMask={hasMask}, wearerHasInternals={hasInternals}, wearerBreathTools={internals?.BreathTools.Count ?? -1}");
    }

    private bool IsDebugMask(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return false;

        var id = MetaData(uid).EntityPrototype?.ID;
        return id?.Contains("Mask", StringComparison.OrdinalIgnoreCase) == true ||
            id?.Contains("Gas", StringComparison.OrdinalIgnoreCase) == true ||
            id?.Contains("Breath", StringComparison.OrdinalIgnoreCase) == true;
    }

    private string DebugEntity(EntityUid? uid)
    {
        if (uid == null)
            return "null";

        if (TerminatingOrDeleted(uid.Value))
            return $"{uid.Value} deleted";

        var proto = MetaData(uid.Value).EntityPrototype?.ID ?? "no-proto";

        return $"{ToPrettyString(uid.Value)} proto={proto}";
    }
    // CMU14 end
}
