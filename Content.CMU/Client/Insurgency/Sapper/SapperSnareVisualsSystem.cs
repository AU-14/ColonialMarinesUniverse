using Content.Client.Eye;
using Content.Shared._AU14.Insurgency.Sapper;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;

namespace Content.Client._AU14.Insurgency.Sapper;

/// <summary>
///     Handles the two visual halves of a snare, both client-side:
///
///     1. Observers: a snared victim's body sprite is rotated upside down so everyone around them sees
///        them dangling in the trap.
///
///     2. The victim themselves: their whole world view is flipped upside down. This cannot be done once
///        server-side, because <see cref="EyeLerpingSystem"/> re-writes the local eye rotation back to the
///        grid angle every single frame. So we re-apply the flip in FrameUpdate, ordered to run right
///        after the lerping system, whenever the local player is snared.
///
///     Note: this flips the rendered game world. The 2D UI chrome (hands bar, chat, health doll) is drawn
///     by a separate UI pass that has no rotation transform, so it stays upright; only the world view
///     turns over. That is the disorientation the snare is going for.
/// </summary>
public sealed class SapperSnareVisualsSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SapperSnaredComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SapperSnaredComponent, ComponentShutdown>(OnShutdown);

        // Always-present overlay; it only paints while the local player is snared (BeforeDraw gates it).
        if (!_overlay.HasOverlay<SapperSnareVignetteOverlay>())
            _overlay.AddOverlay(new SapperSnareVignetteOverlay());

        // Run after the eye lerping system so our flip lands on top of the rotation it just set, rather
        // than being immediately overwritten by it.
        UpdatesAfter.Add(typeof(EyeLerpingSystem));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<SapperSnareVignetteOverlay>();
    }

    private void OnStartup(Entity<SapperSnaredComponent> ent, ref ComponentStartup args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            sprite.Rotation = ent.Comp.FlipAngle;
    }

    private void OnShutdown(Entity<SapperSnaredComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            sprite.Rotation = Angle.Zero;

        // Snapping the eye back the instant the snare ends avoids a lingering upside-down frame.
        if (_player.LocalEntity == ent.Owner && TryComp(ent, out EyeComponent? eye))
            _eye.SetRotation(ent, Angle.Zero, eye);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } local)
            return;

        if (!TryComp(local, out SapperSnaredComponent? snared) || !TryComp(local, out EyeComponent? eye))
            return;

        // The lerp system already set the eye to the upright grid angle this frame; add our flip on top.
        _eye.SetRotation(local, eye.Rotation + snared.FlipAngle, eye);
    }
}
