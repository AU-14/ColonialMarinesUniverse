using System;
using Content.Shared._AU14.Insurgency.Sapper;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._AU14.Insurgency.Sapper;

[UsedImplicitly]
public sealed class SapperWorkbenchBui : BoundUserInterface
{
    private SapperWorkbenchWindow? _window;

    public SapperWorkbenchBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SapperWorkbenchWindow>();
        _window.OnAddAttachment += slot => SendMessage(new SapperWorkbenchAddAttachmentMessage(slot));
        _window.OnRemoveAttachment += slot => SendMessage(new SapperWorkbenchRemoveAttachmentMessage(slot));
        _window.OnTakeWeapon += () => SendMessage(new SapperWorkbenchTakeWeaponMessage());
        _window.OnCraft += index => SendMessage(new SapperWorkbenchCraftMessage(index));
        _window.OnEjectMaterial += id => SendMessage(new SapperWorkbenchEjectMaterialMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SapperWorkbenchBuiState sapperState)
            _window?.SetState(sapperState);
    }
}
