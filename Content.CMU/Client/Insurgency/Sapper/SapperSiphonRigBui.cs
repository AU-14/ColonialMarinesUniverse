using Content.Shared.CMU14.Insurgency.Sapper;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Insurgency.Sapper;

[UsedImplicitly]
public sealed class SapperSiphonRigBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SapperSiphonRigWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SapperSiphonRigWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not SapperSiphonRigBuiState s)
            return;

        _window.Populate(s.Accounts);
    }
}
