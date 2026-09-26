using System.Linq;
using Content.Shared.CMU14.Hijack;
using Content.Shared.GameTicking;
using Content.Client.RoundEnd;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Hijack;

public sealed partial class ShipHijackSystem : CMUShipHijackSystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    private ShipDestructionOverlay? _cinematic;
    private RoundEndMessageEvent? _pendingSummary;

    /// <summary>The server records the result before the final explosion animation finishes.</summary>
    public bool TryDeferRoundEndSummary(RoundEndMessageEvent message)
    {
        if (_cinematic == null || _cinematic.Finished)
            return false;
        _pendingSummary = message;
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_pendingSummary == null || _cinematic is { Finished: false })
            return;
        var summary = _pendingSummary;
        _pendingSummary = null;
        _ui.GetUIController<RoundEndSummaryUIController>().OpenRoundEndSummaryWindow(summary);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CMUShipCinematicEvent>(OnCinematic);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ =>
        {
            _pendingSummary = null;
            Clear();
        });
    }

    private void OnCinematic(CMUShipCinematicEvent args)
    {
        if (args.Stage == CMUShipCinematicStage.Clear)
        {
            Clear();
            return;
        }
        if (_cinematic == null)
        {
            var texture = _resources.GetResource<TextureResource>("/Textures/CMU14/Hijack/station_explosion.png").Texture;
            _cinematic = new ShipDestructionOverlay(texture, _timing);
            _overlays.AddOverlay(_cinematic);
        }
        _cinematic.SetStage(args.Stage);
    }

    private void Clear()
    {
        if (_cinematic == null)
            return;
        _overlays.RemoveOverlay(_cinematic);
        _cinematic = null;
    }

    public override void Shutdown()
    {
        _pendingSummary = null;
        Clear();
        base.Shutdown();
    }
}

/// <summary>CM-SS13's 512px cinematic, including the original per-frame timings.</summary>
public sealed class ShipDestructionOverlay(Texture atlas, IGameTiming timing) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;
    private CMUShipCinematicStage _stage;
    private TimeSpan _started;
    private static readonly double[] DestroyDelays =
        [0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.2, 0.2, 0.4, 0.4, 0.4, 0.4, 0.4,
            0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.2, 0.2];
    public bool Finished => _stage == CMUShipCinematicStage.Destroyed &&
        timing.CurTime >= _started + TimeSpan.FromSeconds(DestroyDelays.Sum());

    public void SetStage(CMUShipCinematicStage stage)
    {
        _stage = stage;
        _started = timing.CurTime;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var elapsed = (timing.CurTime - _started).TotalSeconds;
        var frame = 0;
        if (_stage == CMUShipCinematicStage.Detonation)
            frame = 18 + Math.Min(2, (int) elapsed);
        else if (_stage == CMUShipCinematicStage.Destroyed)
        {
            var i = 0;
            while (i < DestroyDelays.Length && elapsed >= DestroyDelays[i])
                elapsed -= DestroyDelays[i++];
            frame = i == DestroyDelays.Length ? 58 : 21 + i;
        }
        var bounds = args.ViewportBounds;
        var size = Math.Min(bounds.Width, bounds.Height);
        var x = bounds.Left + (bounds.Width - size) / 2f;
        var y = bounds.Top + (bounds.Height - size) / 2f;
        args.ScreenHandle.DrawRect(bounds, Color.Black);
        args.ScreenHandle.DrawTextureRectRegion(atlas, new UIBox2(x, y, x + size, y + size),
            new UIBox2(frame % 8 * 512, frame / 8 * 512, (frame % 8 + 1) * 512, (frame / 8 + 1) * 512));
    }
}
