using Content.Shared.CMU14.Chemistry.Synthesizer;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Chemistry.Synthesizer;

/// <summary>Client side of the chemical synthesizer: opens its window and sends slot presses, batches, start/stop, sample programming, the chain toggle and continuous mode (FLOW-02).</summary>
[UsedImplicitly]
public sealed class CMUChemSynthesizerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CMUChemSynthesizerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CMUChemSynthesizerWindow>();
        _window.SetOwner(Owner);
        _window.OnSlotPressed += slotId => SendPredictedMessage(new ItemSlotButtonPressedEvent(slotId));
        _window.OnBatchesChanged += batches => SendMessage(new CMUChemSynthesizerSetBatchesMessage(batches));
        _window.OnStart += () => SendMessage(new CMUChemSynthesizerStartMessage());
        _window.OnStop += () => SendMessage(new CMUChemSynthesizerStopMessage());
        _window.OnProgramFromSample += () => SendMessage(new CMUChemSynthesizerProgramFromSampleMessage());
        _window.OnChainToggled += enabled => SendMessage(new CMUChemSynthesizerSetChainMessage(enabled));
        _window.OnContinuousToggled += enabled => SendMessage(new CMUChemSynthesizerSetContinuousMessage(enabled));
    }
}
