using Content.Client.CMU14.ZLevels.Core;
using Content.Client.Overlays;
using Content.Shared.CMU14.Round.Antags.Rider;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Round.Antags.Rider;

public sealed partial class RiderHostHealthSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
        => _overlays.AddOverlay(new RiderHostHealthOverlay(EntityManager, _prototypes, _timing, _eye, _player));

    public override void Shutdown()
        => _overlays.RemoveOverlay<RiderHostHealthOverlay>();
}

internal sealed class RiderHostHealthOverlay(
    IEntityManager entities,
    IPrototypeManager prototypes,
    IGameTiming timing,
    IEyeManager eye,
    IPlayerManager player) : EntityHealthBarOverlay(entities, prototypes, timing)
{
    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!args.Viewport.Eye.IsPlayerView(eye.CurrentEye)
            || !entities.TryGetComponent(player.LocalEntity, out RiderHostHealthComponent? health)
            || health.Host is not { } host)
        {
            return;
        }

        DrawHealthBars(args, host);
    }
}
