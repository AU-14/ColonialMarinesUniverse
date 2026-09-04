using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Medical.IV;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Medical.IV;

public sealed partial class IVDripSystem : SharedIVDripSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedRMCBloodstreamSystem _rmcBloodstream = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private BatterySystem _battery = default!;

    private readonly List<string> _reagentRemovalBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PortableDialysisComponent, PowerCellChangedEvent>(OnDialysisBatteryChargeChanged);
    }

    protected override void OnServerDialysisDetached(Entity<PortableDialysisComponent> dialysis)
    {
        base.OnServerDialysisDetached(dialysis);
        // Raise networked event to update client sprites with current state
        var ev = new DialysisDetachedEvent(GetNetEntity(dialysis), dialysis.Comp.IsDetaching);
        RaiseNetworkEvent(ev);
    }

    private void OnDialysisBatteryChargeChanged(Entity<PortableDialysisComponent> dialysis, ref PowerCellChangedEvent args)
    {
        UpdateDialysisBatteryLevel(dialysis);
    }

    private bool TryGetBloodstream(
        EntityUid attachedTo,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solEnt,
        [NotNullWhen(true)] out Solution? solution,
        [NotNullWhen(true)] out BloodstreamComponent? bloodstream)
    {
        solEnt = default;
        solution = default;
        bloodstream = default;
        if (!TryComp(attachedTo, out bloodstream) ||
            !_solutionContainer.ResolveSolution(attachedTo, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out solution))
        {
            return false;
        }

        solEnt = bloodstream.BloodSolution;
        return true;
    }

    protected override void DoRip(DamageSpecifier? damage, EntityUid attached, EntityUid? user, ProtoId<EmotePrototype> ripEmote, bool predict)
    {
        base.DoRip(damage, attached, user, ripEmote, predict);
        _chat.TryEmoteWithoutChat(attached, ripEmote);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var ivs = EntityQueryEnumerator<IVDripComponent>();
        while (ivs.MoveNext(out var ivId, out var ivComp))
        {
            if (ivComp.AttachedTo is not { } attachedTo)
                continue;

            if (!InRange(ivId, attachedTo, ivComp.Range))
                DetachIV((ivId, ivComp), null, true, false);

            if (time < ivComp.TransferAt)
                continue;

            if (_itemSlots.GetItemOrNull(ivId, ivComp.Slot) is not { } pack)
                continue;

            if (!TryComp(pack, out BloodPackComponent? packComponent))
                continue;

            ivComp.TransferAt = time + ivComp.TransferDelay;

            if (!_solutionContainer.TryGetSolution(pack, packComponent.Solution, out var packSolEnt, out var packSol))
                continue;

            if (!TryGetBloodstream(attachedTo, out var streamSolEnt, out var streamSol, out var bloodstream))
                continue;

            if (ivComp.Injecting)
            {
                if (streamSolEnt.Value.Comp.Solution.Volume < streamSolEnt.Value.Comp.Solution.MaxVolume)
                    TransferBloodToRecipient(
                        streamSolEnt.Value,
                        packSolEnt.Value,
                        packSol,
                        bloodstream,
                        packComponent.TransferableReagents,
                        ivComp.TransferAmount);
            }
            else
            {
                if (packSol.Volume < packSol.MaxVolume)
                    TransferReferenceBlood(packSolEnt.Value, streamSolEnt.Value, streamSol, bloodstream, ivComp.TransferAmount);
            }

            Dirty(ivId, ivComp);
            UpdateIVVisuals((ivId, ivComp));
            UpdatePackVisuals((pack, packComponent));
        }

        var packs = EntityQueryEnumerator<BloodPackComponent>();
        while (packs.MoveNext(out var packId, out var packComp))
        {
            if (packComp.AttachedTo is not { } attachedTo)
                continue;

            if (!InRange(packId, attachedTo, packComp.Range))
                DetachPack((packId, packComp), null, true, false);

            if (time < packComp.TransferAt)
                continue;

            packComp.TransferAt = time + packComp.TransferDelay;

            if (!_solutionContainer.TryGetSolution(packId, packComp.Solution, out var packSolEnt, out var packSol))
                continue;

            if (!TryGetBloodstream(attachedTo, out var streamSolEnt, out var streamSol, out var bloodstream))
                continue;

            if (packComp.Injecting)
            {
                if (streamSolEnt.Value.Comp.Solution.Volume < streamSolEnt.Value.Comp.Solution.MaxVolume)
                    TransferBloodToRecipient(
                        streamSolEnt.Value,
                        packSolEnt.Value,
                        packSol,
                        bloodstream,
                        packComp.TransferableReagents,
                        packComp.TransferAmount);
            }
            else
            {
                if (packSol.Volume < packSol.MaxVolume)
                    TransferReferenceBlood(packSolEnt.Value, streamSolEnt.Value, streamSol, bloodstream, packComp.TransferAmount);
            }

            Dirty(packId, packComp);
            UpdatePackVisuals((packId, packComp));
        }

        var dialysis = EntityQueryEnumerator<PortableDialysisComponent>();
        while (dialysis.MoveNext(out var dialysisId, out var dialysisComp))
        {
            if (dialysisComp.AttachedTo is not { } attachedTo)
                continue;

            if (!InRange(dialysisId, attachedTo, dialysisComp.Range))
                DetachDialysis((dialysisId, dialysisComp), null, true, false);

            if (time < dialysisComp.TransferAt)
                continue;

            dialysisComp.TransferAt = time + dialysisComp.TransferDelay;

            if (!_powerCell.HasActivatableCharge(dialysisId) || !HasComp<BloodstreamComponent>(attachedTo))
                DetachDialysis((dialysisId, dialysisComp), null, false, false);

            if (_rmcBloodstream.TryGetChemicalSolution(attachedTo, out var chemicalSolEnt, out var chemicalSol))
            {
                _reagentRemovalBuffer.Clear();

                foreach (var reagentQuantity in chemicalSol.Contents)
                {
                    if (!dialysisComp.NonTransferableReagents.Contains(reagentQuantity.Reagent.Prototype))
                    {
                        _reagentRemovalBuffer.Add(reagentQuantity.Reagent.Prototype);
                    }
                }

                foreach (var reagent in _reagentRemovalBuffer)
                {
                    _solutionContainer.RemoveReagent(chemicalSolEnt, reagent, dialysisComp.ReagentRemovalAmount);
                }
            }

            if (TryComp(attachedTo, out BloodstreamComponent? bloodstreamComp))
                _bloodstream.TryRegulateBloodLevel((attachedTo, bloodstreamComp), dialysisComp.BloodRemovalCost, referenceFactor: 0f);

            _powerCell.TryUseActivatableCharge(dialysisId);

            Dirty(dialysisId, dialysisComp);
            UpdateDialysisVisuals((dialysisId, dialysisComp));
        }
    }

    private void TransferReferenceBlood(
        Entity<SolutionComponent> destination,
        Entity<SolutionComponent> source,
        Solution sourceSolution,
        BloodstreamComponent bloodstream,
        FixedPoint2 amount)
    {
        var references = _bloodstream.GetReferenceReagentPrototypes((source.Owner, bloodstream));
        var excludedSolution = sourceSolution.SplitSolutionWithout(sourceSolution.MaxVolume, references);

        _solutionContainer.TryTransferSolution(destination, sourceSolution, amount);
        _solutionContainer.TryAddSolution(source, excludedSolution);
        Dirty(source);
    }

    private void TransferBloodToRecipient(
        Entity<SolutionComponent> destination,
        Entity<SolutionComponent> source,
        Solution sourceSolution,
        BloodstreamComponent bloodstream,
        string[] transferableReagents,
        FixedPoint2 amount)
    {
        amount = FixedPoint2.Min(amount, destination.Comp.Solution.AvailableVolume);
        if (amount <= FixedPoint2.Zero)
            return;

        var transferablePrototypes = transferableReagents
            .Select(reagent => (ProtoId<ReagentPrototype>) reagent)
            .ToArray();
        var transferred = sourceSolution.SplitSolutionWithOnly(amount, transferablePrototypes);
        if (transferred.Volume <= FixedPoint2.Zero)
            return;

        // Blood packs do not model compatibility yet. Convert matching donor blood IDs (including DNA data)
        // to the recipient's reference IDs so they restore blood volume instead of metabolizing as foreign blood.
        foreach (var (referenceReagent, _) in bloodstream.BloodReferenceSolution)
        {
            var quantity = transferred.GetTotalPrototypeQuantity(referenceReagent.Prototype);
            if (quantity <= FixedPoint2.Zero)
                continue;

            transferred.RemoveReagent(referenceReagent, quantity, ignoreReagentData: true);
            transferred.AddReagent(referenceReagent, quantity);
        }

        if (!_solutionContainer.TryAddSolution(destination, transferred))
            _solutionContainer.TryAddSolution(source, transferred);

        Dirty(source);
    }

    private void UpdateDialysisBatteryLevel(Entity<PortableDialysisComponent> dialysis)
    {
        var batteryLevel = GetDialysisBatteryLevel(dialysis);
        UpdateDialysisBatteryAppearance(dialysis.Owner, batteryLevel);
    }

    private DialysisBatteryLevel GetDialysisBatteryLevel(Entity<PortableDialysisComponent> dialysis)
    {
        if (!_powerCell.TryGetBatteryFromSlot(dialysis.Owner, out var battery) || battery.Value.Comp.MaxCharge <= 0)
            return DialysisBatteryLevel.Empty;

        var percentCharged = _battery.GetCharge(battery.Value.AsNullable()) / battery.Value.Comp.MaxCharge;
        return percentCharged switch
        {
            >= 0.86f => DialysisBatteryLevel.Full,
            >= 0.61f => DialysisBatteryLevel.VeryHigh,
            >= 0.46f => DialysisBatteryLevel.High,
            >= 0.31f => DialysisBatteryLevel.Medium,
            >= 0.16f => DialysisBatteryLevel.Low,
            >= 0.01f => DialysisBatteryLevel.VeryLow,
            _ => DialysisBatteryLevel.Empty,
        };
    }
}
