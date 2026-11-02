using Content.Shared.CMU14.Fob;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Fob;

public sealed class CMUFobControlBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUFobControlWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUFobControlWindow>();
        _window.Instructions.OnPressed += _ => EntMan.System<Content.Client.Guidebook.GuidebookSystem>().OpenHelp(["CMUFobGuide"]);
        _window.Activate.OnPressed += _ => SendMessage(new CMUFobControlMessage(true));
        _window.Deactivate.OnPressed += _ => SendMessage(new CMUFobControlMessage(false));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CMUFobControlState status)
            _window?.UpdateState(status);
    }
}
