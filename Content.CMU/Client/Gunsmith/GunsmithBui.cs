// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Gunsmith;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Gunsmith;

// Client side of the gunsmith bench: opens its window and gives it the state the server sends.
public sealed class GunsmithBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private GunsmithWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GunsmithWindow>();
        _window.OnBuildFirearm += design => SendMessage(new GunsmithBuildFirearmMessage(design));
        _window.OnBuildMagazine += design => SendMessage(new GunsmithBuildMagazineMessage(design));
        _window.OnEjectStock += type => SendMessage(new GunsmithEjectStockMessage(type));
        _window.OnTakeWeapon += () => SendMessage(new GunsmithTakeWeaponMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is GunsmithBuiState bench)
            _window?.UpdateState(bench);
    }
}
