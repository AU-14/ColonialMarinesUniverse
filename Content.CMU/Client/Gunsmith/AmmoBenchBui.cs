// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Gunsmith;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Gunsmith;

// Client side of the ammunition bench: opens its window and gives it the state the server sends.
public sealed class AmmoBenchBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private AmmoBenchWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AmmoBenchWindow>();
        _window.OnBuild += (design, count) => SendMessage(new AmmoBuildMessage(design, count));
        _window.OnEjectStock += type => SendMessage(new AmmoEjectStockMessage(type));
        _window.OnDrain += () => SendMessage(new AmmoDrainTankMessage());
        _window.OnTakeItem += () => SendMessage(new AmmoTakeItemMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is AmmoBenchBuiState bench)
            _window?.UpdateState(bench);
    }
}
