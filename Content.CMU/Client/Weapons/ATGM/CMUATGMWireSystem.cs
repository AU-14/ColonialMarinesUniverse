using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Weapons.ATGM;

/// <summary>
///     Registers the purely cosmetic guidance-wire overlay that trails behind every in-flight
///     <see cref="Content.Shared.CMU14.Weapons.ATGM.CMUGuidedMissileComponent"/>.
/// </summary>
public sealed partial class CMUATGMWireSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new CMUATGMWireOverlay(EntityManager, _timing, _transform));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CMUATGMWireOverlay>();
    }
}
