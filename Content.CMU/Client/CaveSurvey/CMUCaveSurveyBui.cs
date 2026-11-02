using System.Threading;
using Content.Client._RMC14.UserInterface;
using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.CaveSurvey;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.ZLevels.Core.Components;
using System.Numerics;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client.CMU14.CaveSurvey;

[UsedImplicitly]
public sealed class CMUCaveSurveyBui(EntityUid owner, Enum uiKey) : RMCPopOutBui<CMUReconstructionWindow>(owner, uiKey)
{
    protected override CMUReconstructionWindow? Window { get; set; }
    private int _requestId;
    private Vector2i _origin;
    private int _minDepth = -8;
    private CMUCaveSurveyViewMessage? _pending;
    private CancellationTokenSource? _retry;
    private NetEntity _world;

    protected override void Open()
    {
        base.Open();
        Window = this.CreatePopOutableWindow<CMUReconstructionWindow>();
        Window.CenterOnOpening = true;
        Window.CavePosition = LocalPosition;
        Window.ConfigureCaveSurvey((offset, depth) =>
        {
            _origin += offset;
            _minDepth = Math.Clamp(_minDepth + depth, -4096, -8);
            Request(false);
        }, () => Request(true), () => Request(false), () => Request(true, true),
            () => SendMessage(new CMUCaveSurveySurfaceMessage()));
        Window.OnFinalClose += StopRetry;
        Request(true);
    }

    private void Request(bool center, bool nextWorld = false)
    {
        StopRetry();
        _pending = new CMUCaveSurveyViewMessage
        {
            Origin = _origin, MinDepth = _minDepth, Center = center, NextWorld = nextWorld,
            RequestId = _requestId = EntMan.System<CMUReconstructionCacheSystem>().NextRequestId(),
        };
        Window?.BeginCaveViewRequest(_requestId, center);
        SendMessage(_pending);
        _retry = new CancellationTokenSource();
        Timer.SpawnRepeating(TimeSpan.FromSeconds(1), () => { if (_pending != null) SendMessage(_pending); }, _retry.Token);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is CMUReconSnapshotMessage scene)
        {
            if (scene.RequestId != _requestId) return;
            StopRetry();
            _origin = scene.Origin;
            _minDepth = scene.MinDepth;
        }
        if (message is CMUCaveSurveySectionMessage section)
        {
            if (section.RequestId == _requestId)
            {
                _world = section.World;
                Window?.SetCaveSection(section, EntMan.HasComponent<TacticalMapUserComponent>(Owner));
            }
            return;
        }
        Window?.Receive(message);
    }

    private void StopRetry()
    {
        _pending = null;
        _retry?.Cancel();
        _retry?.Dispose();
        _retry = null;
    }

    private (Vector2? Position, int Depth) LocalPosition()
    {
        if (PlayerManager.LocalEntity is not { } actor || !EntMan.TryGetComponent<TransformComponent>(actor, out var xform) ||
            xform.MapUid is not { } map || !EntMan.TryGetComponent<CMUZLevelMapComponent>(map, out var level) ||
            EntMan.GetNetEntity(level.NetworkUid) != _world) return (null, 0);
        return (EntMan.System<SharedTransformSystem>().GetWorldPosition(actor), level.Depth);
    }

    protected override void Dispose(bool disposing)
    {
        StopRetry();
        base.Dispose(disposing);
    }
}
