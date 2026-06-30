// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Client.Graphics;

namespace Content.Client._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): registers the holder-only structural scanner heat-map overlay. The overlay itself
/// decides each frame whether to render (only while the local player holds an enabled scanner underground).
/// </summary>
public sealed class StructuralScannerSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlay.HasOverlay<StructuralScannerOverlay>())
            _overlay.AddOverlay(new StructuralScannerOverlay());

        // The structural-integrity warning vignette is always active (it decides per-frame whether to show),
        // not gated behind holding the scanner - you should feel unstable ground under you regardless.
        if (!_overlay.HasOverlay<StructuralWarningOverlay>())
            _overlay.AddOverlay(new StructuralWarningOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<StructuralScannerOverlay>();
        _overlay.RemoveOverlay<StructuralWarningOverlay>();
    }
}
