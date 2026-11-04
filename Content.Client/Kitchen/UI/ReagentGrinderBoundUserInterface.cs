using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Robust.Client.UserInterface;

namespace Content.Client.Kitchen.UI;

public sealed class ReagentGrinderBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private GrinderMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<GrinderMenu>();
        _menu.SetEntity(Owner);
        _menu.OnToggleAuto += ToggleAutoMode;
        _menu.OnGrind += StartGrinding;
        _menu.OnJuice += StartJuicing;
        _menu.OnEjectAll += EjectAll;
        _menu.OnLink += Link;
        _menu.OnEjectBeaker += EjectBeaker;
        _menu.OnEjectChamber += EjectChamberContent;
        _menu.OnBottle += Bottle;
        _menu.OnDisposeReagent += DisposeReagent;
    }

    public override void Update()
    {
        base.Update();
        _menu?.UpdateUi();
    }

    public void ToggleAutoMode()
    {
        SendPredictedMessage(new ReagentGrinderToggleAutoModeMessage());
    }

    public void StartGrinding()
    {
        SendPredictedMessage(new ReagentGrinderStartMessage(GrinderProgram.Grind));
    }

    public void StartJuicing()
    {
        SendPredictedMessage(new ReagentGrinderStartMessage(GrinderProgram.Juice));
    }

    public void EjectAll()
    {
        SendPredictedMessage(new ReagentGrinderEjectChamberAllMessage());
    }

    public void EjectBeaker()
    {
        SendPredictedMessage(new ItemSlotButtonPressedEvent(ReagentGrinderComponent.BeakerSlotId));
    }

    public void EjectChamberContent(EntityUid uid)
    {
        SendPredictedMessage(new ReagentGrinderEjectChamberContentMessage(EntMan.GetNetEntity(uid)));
    }

    public void Bottle(ReagentQuantity reagent)
    {
        SendPredictedMessage(new ReagentGrinderBottleMessage(reagent));
    }

    public void DisposeReagent(ReagentQuantity reagent)
    {
        SendPredictedMessage(new ReagentGrinderDisposeMessage(reagent));
    }

    public void Link()
    {
        SendPredictedMessage(new ReagentGrinderLinkMessage());
    }
}
