// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Armorsmith;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Armorsmith;

// Client side of the armorsmith bench: opens its window and gives it the state the server sends.
public sealed class ArmorsmithBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ArmorsmithWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ArmorsmithWindow>();
        _window.OnBuild += design => SendMessage(new ArmorsmithBuildMessage(design));
        _window.OnEjectStock += type => SendMessage(new ArmorsmithEjectStockMessage(type));
        _window.OnTakeItem += () => SendMessage(new ArmorsmithTakeItemMessage());
        _window.OnRecolor += color => SendMessage(new ArmorsmithRecolorMessage(color));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ArmorsmithBuiState bench)
            _window?.UpdateState(bench);
    }
}
