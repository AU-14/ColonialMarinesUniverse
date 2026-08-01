using System;
using Content.Shared._AU14.Insurgency.CellKit;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._AU14.Insurgency.CellKit;

/// <summary>
///     Client side of the Heavy Cell Kit UI. Shows the deployables still in the kit with their sprites
///     and turns a Deploy press into a message; the server runs the do-after and spawns the entity.
/// </summary>
[UsedImplicitly]
public sealed class InsurgencyCellKitBui : BoundUserInterface
{
    private InsurgencyCellKitWindow? _window;

    public InsurgencyCellKitBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<InsurgencyCellKitWindow>();
        _window.OnDeploy += index => SendMessage(new InsurgencyCellKitDeployMessage(index));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is InsurgencyCellKitBuiState s)
            _window?.SetEntries(s.Entries, s.Names);
    }
}
