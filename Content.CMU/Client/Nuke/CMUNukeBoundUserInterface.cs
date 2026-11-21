using JetBrains.Annotations;
using Content.Shared.CMU14.Nuke;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Nuke;

[UsedImplicitly]
public sealed class CMUNukeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CMUNukeMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<CMUNukeMenu>();
        _menu.AnchorButton.OnPressed += _ => SendMessage(new CMUNukeAnchorMessage());
        _menu.StartDecryption.OnPressed += _ => SendMessage(new CMUNukeStartDecryptionMessage());
        _menu.ArmButton.OnPressed += _ => SendMessage(new CMUNukeActivateMessage());
        _menu.ToggleSafety.OnPressed += _ => SendMessage(new CMUNukeToggleSafetyMessage());
        _menu.CommandLockoutButton.OnPressed += _ => SendMessage(new CMUNukeToggleCommandLockoutMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not CMUNukeUiState nukeState)
            return;

        _menu.UpdateState(nukeState);
    }
}
