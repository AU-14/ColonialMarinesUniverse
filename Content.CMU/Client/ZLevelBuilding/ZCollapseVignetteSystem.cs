// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Content.Shared._AU14.ZLevelBuilding;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._AU14.ZLevelBuilding;

/// <summary>
/// Receives <see cref="ZCollapseVignetteEvent"/> from the server and drives
/// <see cref="ZCollapseVignetteOverlay"/>: grey events start a one-second blink (ignored while one is
/// already playing), engulfed events extend the rapid black flutter - the server re-sends them on its
/// rumble cadence while the cave is still collapsing on the player, so the flutter lasts exactly as
/// long as the collapse does.
/// </summary>
public sealed class ZCollapseVignetteSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // 🔧 TUNABLE: must comfortably outlast the server's rumble re-send interval (0.45s) so the black
    // flutter never gaps mid-collapse.
    private static readonly TimeSpan BlackExtension = TimeSpan.FromSeconds(0.8);

    private ZCollapseVignetteOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ZCollapseVignetteEvent>(OnVignette);

        _overlay = new ZCollapseVignetteOverlay();
        _overlayMan.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnVignette(ZCollapseVignetteEvent ev)
    {
        if (_overlay == null)
            return;

        var now = _timing.CurTime;
        if (ev.Engulfed)
        {
            var extended = now + BlackExtension;
            if (extended > _overlay.BlackUntil)
                _overlay.BlackUntil = extended;
            return;
        }

        // Only restart the grey blink once the previous one finished - repeated events during a sustained
        // collapse then read as distinct pulses instead of one frozen vignette.
        if ((now - _overlay.GreyStart).TotalSeconds >= 1.0)
            _overlay.GreyStart = now;
    }
}
