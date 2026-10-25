using Content.Client.Movement.Systems;
using Content.Shared.CMU14.Yautja;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;

namespace Content.Client.CMU14.Yautja;

public sealed partial class YautjaWallVisionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    /// <summary>
    /// CMU14: whether this system is currently holding the local player's field of view switched off for the clan
    /// mask's biological overlay. Tracked so the cone is only touched on the transition, not every frame.
    /// </summary>
    private bool _fovSuppressed;

    /// <summary>
    /// The local player's own FOV setting, captured when the cone is suppressed so it can be handed straight back.
    /// </summary>
    private bool _drawFovBeforeSuppression = true;

    public override void Initialize()
    {
        _overlay.AddOverlay(new YautjaWallVisionOverlay(EntityManager, _players));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_players.LocalEntity is not { } viewer || !TryComp<EyeComponent>(viewer, out var eye))
            return;

        // While the mask's bio overlay (thermals) is up, the player sees through walls, so a field of view cone drawn
        // over that would only hide the thing the overlay exists to show. Only the transition is applied, and the
        // player's own FOV preference is handed back as soon as the overlay goes away.
        var thermal = HasActiveThermalOverlay(viewer);
        if (thermal == _fovSuppressed)
            return;

        _fovSuppressed = thermal;
        if (thermal)
        {
            _drawFovBeforeSuppression = eye.DrawFov;
            _eye.SetDrawFov(viewer, false, eye);
        }
        else
        {
            _eye.SetDrawFov(viewer, _drawFovBeforeSuppression, eye);
        }
    }

    private bool HasActiveThermalOverlay(EntityUid viewer)
    {
        var visors = EntityQueryEnumerator<YautjaMaskVisorGlassesComponent>();
        while (visors.MoveNext(out _, out var visor))
        {
            if (visor.ThermalVisionEnabled && visor.User == viewer)
                return true;
        }

        return false;
    }
}
