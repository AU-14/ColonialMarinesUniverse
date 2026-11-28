using Content.Shared.CMU14.Botany;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Botany;

// CMU14: client side of the batch seed extractor: opens its window and sends the pressed slot and the button messages.
public sealed class CMUBatchSeedExtractorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CMUBatchSeedExtractorWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CMUBatchSeedExtractorWindow>();
        _window.SetOwner(Owner);
        _window.OnSlotPressed += slotId => SendPredictedMessage(new ItemSlotButtonPressedEvent(slotId));
        _window.OnStartPressed += () => SendMessage(new CMUBatchSeedExtractorStartMessage());
        _window.OnEjectAllPressed += () => SendMessage(new CMUBatchSeedExtractorEjectAllMessage());
        _window.OnToggleAutoPressed += () => SendMessage(new CMUBatchSeedExtractorToggleAutoMessage());
    }
}
