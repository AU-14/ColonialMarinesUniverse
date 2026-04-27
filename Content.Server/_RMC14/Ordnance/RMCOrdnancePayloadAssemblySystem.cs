using Content.Server.Popups;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Ordnance;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Ordnance;

/// <summary>
///     Combines armed chemical warheads with fueled rocket and mortar bodies.
/// </summary>
public sealed class RMCOrdnancePayloadAssemblySystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private static readonly SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/Guns/Reload/grenade_insert.ogg");

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCOrdnancePayloadAssemblyComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<RMCOrdnancePayloadAssemblyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<RMCChembombCasingComponent>(args.Used, out var casing))
            return;

        var resultProto = GetResult(ent.Comp, args.Used);
        if (resultProto == null)
            return;

        args.Handled = true;

        if (casing.Stage != RMCCasingAssemblyStage.Armed || !casing.HasActiveDetonator)
        {
            _popup.PopupEntity(Loc.GetString("rmc-ordnance-payload-not-ready"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!TryValidateFuel(ent, args.User))
        {
            return;
        }

        if (!_solution.TryGetSolution(args.Used, casing.ChemicalSolution, out _, out var chemicalSolution) ||
            chemicalSolution.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("rmc-ordnance-payload-no-chemicals"), args.Used, args.User, PopupType.SmallCaution);
            return;
        }

        var result = Spawn(resultProto.Value, Transform(ent).Coordinates);
        TransferPayloadChemicals(args.Used, casing.ChemicalSolution, result, ent.Comp.ChemicalSolution, chemicalSolution.Volume);

        _audio.PlayPredicted(InsertSound, result, args.User);
        _popup.PopupEntity(Loc.GetString("rmc-ordnance-payload-assembled"), result, args.User);

        QueueDel(args.Used);
        QueueDel(ent);
    }

    private EntProtoId? GetResult(RMCOrdnancePayloadAssemblyComponent comp, EntityUid payload)
    {
        var prototypeId = MetaData(payload).EntityPrototype?.ID;
        if (prototypeId == null)
            return null;

        foreach (var result in comp.Results)
        {
            if (result.Payload == prototypeId)
                return result.Result;
        }

        return null;
    }

    private bool TryValidateFuel(Entity<RMCOrdnancePayloadAssemblyComponent> ent, EntityUid user)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.FuelSolution, out _, out Solution? fuelSolution) ||
            fuelSolution == null)
        {
            _popup.PopupEntity(Loc.GetString("rmc-ordnance-payload-no-fuel"), ent, user, PopupType.SmallCaution);
            return false;
        }

        if (fuelSolution.Volume < ent.Comp.RequiredFuel)
        {
            _popup.PopupEntity(Loc.GetString("rmc-ordnance-payload-no-fuel"), ent, user, PopupType.SmallCaution);
            return false;
        }

        if (ent.Comp.RequiredFuelReagent is not { } fuelReagent ||
            fuelSolution.GetTotalPrototypeQuantity(fuelReagent) >= ent.Comp.RequiredFuel)
        {
            return true;
        }

        var fuelName = _prototype.TryIndexReagent(fuelReagent, out ReagentPrototype? fuelProto)
            ? fuelProto.LocalizedName
            : fuelReagent.Id;

        _popup.PopupEntity(
            Loc.GetString("rmc-ordnance-payload-wrong-fuel", ("fuel", fuelName)),
            ent,
            user,
            PopupType.SmallCaution);
        return false;
    }

    private void TransferPayloadChemicals(
        EntityUid source,
        string sourceSolutionName,
        EntityUid result,
        string resultSolutionName,
        FixedPoint2 quantity)
    {
        if (!_solution.TryGetSolution(source, sourceSolutionName, out var sourceChemicals, out _)
            || !_solution.TryGetSolution(result, resultSolutionName, out var resultChemicals, out _))
        {
            return;
        }

        var transferred = _solution.SplitSolution(sourceChemicals.Value, quantity);
        _solution.TryAddSolution(resultChemicals.Value, transferred);
    }
}
