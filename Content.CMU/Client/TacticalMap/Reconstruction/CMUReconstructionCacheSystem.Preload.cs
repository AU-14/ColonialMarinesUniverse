using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CCVar;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUReconstructionCacheSystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    private readonly record struct Context(EntityUid Actor, string? Faction, EntityUid? Location, bool PlanetOnShip);
    private Context? _preloadContext;
    private CMUReconstructionControl? _preloadView;
    private int _preloadRequest;
    private TimeSpan _nextPreload;
    private TimeSpan _preloadExpires;

    private Context? PreloadContext()
    {
        if (_client.RunLevel != ClientRunLevel.InGame || _cfg.GetCVar(CCVars.CMUTacMapClassic) ||
            _players.LocalEntity is not { } actor || Deleted(actor) || !HasComp<TacticalMapUserComponent>(actor) ||
            !Identity(actor, out var faction, out _)) return null;
        return new Context(actor, faction, Transform(actor).MapUid, _cfg.GetCVar(CCVars.CMUTacMapPlanetOnShip));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var context = PreloadContext();
        if (context != _preloadContext)
        {
            CancelPreload();
            _preloadContext = context;
            _nextPreload = _timing.CurTime + TimeSpan.FromSeconds(3);
        }
        if (context is not { } current) return;
        foreach (var (_, key) in _ui.GetActorUis(current.Actor))
            if (key is TacticalMapUserUi or TacticalMapComputerUi or CMUReconstructionUiKey)
            {
                CancelPreload();
                _nextPreload = _timing.CurTime + TimeSpan.FromSeconds(3);
                return;
            }
        if (_entry != null) return; // Preserve the last actual view, including command tablets.
        if (_preloadView is { } view)
        {
            view.PrepareResources();
            if (view.Scene is { } scene && (scene.LoadedChunks == scene.TotalChunks && view.ResourcesReady ||
                _timing.CurTime >= _preloadExpires))
                PromotePreload();
            if (_timing.CurTime >= _preloadExpires) CancelPreload();
            return;
        }
        if (_timing.CurTime < _nextPreload) return;
        CancelPreload();
        _nextPreload = _timing.CurTime + TimeSpan.FromSeconds(15);
        _preloadRequest = NextRequestId();
        _preloadExpires = _timing.CurTime + TimeSpan.FromMinutes(2);
        RaiseNetworkEvent(new CMUReconPreloadRequest(_preloadRequest, current.PlanetOnShip));
    }

    private void OnPreloadState(CMUReconPreloadState state)
    {
        if (_preloadRequest == 0 || state.RequestId != _preloadRequest || _entry != null ||
            _preloadContext == null || _preloadContext != PreloadContext()) return;
        if (state.Snapshot is { } snapshot && _preloadView == null)
        {
            _preloadView = new CMUReconstructionControl();
            _preloadView.SetScene(snapshot);
        }
        if (state.Patch is { } patch) _preloadView?.Apply(patch);
    }

    private void PromotePreload()
    {
        if (_preloadContext is not { } context || _preloadView is not { Scene: { LoadedChunks: > 0 } scene } view) return;
        Remember(context.Actor, context.Actor, scene, view.CaptureCamera(), view.TakeRenderData());
        CancelPreload();
    }

    private void CancelPreload(bool notify = true)
    {
        if (_preloadRequest != 0 && notify && _client.RunLevel == ClientRunLevel.InGame)
            RaiseNetworkEvent(new CMUReconPreloadRequest(_preloadRequest, false, true));
        _preloadRequest = 0;
        // Unattached controls do not receive ExitedTree, so release their GPU ownership explicitly.
        _preloadView?.TakeRenderData().Dispose();
        _preloadView?.Dispose();
        _preloadView = null;
    }
}
