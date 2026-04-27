using Content.Shared._RMC14.Ordnance;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RMC14.Ordnance;

[UsedImplicitly]
public sealed class RMCDemolitionsScannerBui : BoundUserInterface
{
    private RMCDemolitionsScannerWindow? _window;

    public RMCDemolitionsScannerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<RMCDemolitionsScannerWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not RMCDemolitionsScannerBuiState scan)
            return;

        _window.UpdateState(scan);
    }
}
