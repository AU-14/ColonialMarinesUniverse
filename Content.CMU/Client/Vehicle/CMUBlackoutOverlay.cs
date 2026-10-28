using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Blacks the local player's view out while they have a <see cref="CMUBlackoutComponent"/>: dark almost
/// at once, fading back over the last moments. Drawn over the whole view, whatever eye renders it, so
/// it holds while the Z levels draw through their own eye.
/// </summary>
public sealed class CMUBlackoutOverlay : Overlay
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float FadeIn = 0.15f;
    private const float FadeOut = 0.8f;

    private float _alpha;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CMUBlackoutOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        _alpha = 0f;
        if (_player.LocalEntity is not { } local || !_entities.TryGetComponent(local, out CMUBlackoutComponent? blackout))
            return false;

        var now = _timing.CurTime;
        var since = (float) (now - blackout.Start).TotalSeconds;
        var left = (float) (blackout.End - now).TotalSeconds;
        _alpha = Math.Clamp(Math.Min(since / FadeIn, left / FadeOut), 0f, 1f);
        return _alpha > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        args.ScreenHandle.DrawRect(args.ViewportBounds, Color.Black.WithAlpha(_alpha));
    }
}
