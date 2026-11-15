// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Power;

// Adds the overlay that draws the cables run by hand between devices, and removes it on shutdown.
public sealed class DirectCableVisualSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlay.HasOverlay<DirectCableOverlay>())
            _overlay.AddOverlay(new DirectCableOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<DirectCableOverlay>();
    }
}
