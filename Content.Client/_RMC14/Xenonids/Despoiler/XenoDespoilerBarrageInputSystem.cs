using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared.ActionBlocker;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;

namespace Content.Client._RMC14.Xenonids.Despoiler;

public sealed partial class XenoDespoilerBarrageInputSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private TransformSystem _transform = default!;

    private EntityQuery<XenoDespoilerArmedBarrageComponent> _armedQuery;
    private EntityQuery<XenoDespoilerChargingBarrageComponent> _chargingQuery;

    public override void Initialize()
    {
        _armedQuery = GetEntityQuery<XenoDespoilerArmedBarrageComponent>();
        _chargingQuery = GetEntityQuery<XenoDespoilerChargingBarrageComponent>();

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse, ignoreUp: false, outsidePrediction: true))
            .Register<XenoDespoilerBarrageInputSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<XenoDespoilerBarrageInputSystem>();
    }

    private bool OnUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (_player.LocalEntity is not { } ent)
            return false;

        if (args.State == BoundKeyState.Down)
        {
            if (!_armedQuery.HasComp(ent) || _chargingQuery.HasComp(ent))
                return false;

            if (!_actionBlocker.CanConsciouslyPerformAction(ent))
                return false;

            if (!TryGetCursorCoords(out var coords))
                return false;

            RaiseNetworkEvent(new XenoDespoilerBarrageStartChargeRequest(GetNetCoordinates(coords)));
            return true;
        }

        if (args.State == BoundKeyState.Up)
        {
            if (!_chargingQuery.HasComp(ent))
                return false;

            if (!TryGetCursorCoords(out var coords))
                return false;

            RaiseNetworkEvent(new XenoDespoilerBarrageFireRequest(GetNetCoordinates(coords)));
            return true;
        }

        return false;
    }

    private bool TryGetCursorCoords(out EntityCoordinates coords)
    {
        coords = default;
        var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);

        EntityUid grid;
        if (_mapManager.TryFindGridAt(mousePos, out var gridUid, out _))
            grid = gridUid;
        else if (_map.TryGetMap(mousePos.MapId, out var map))
            grid = map.Value;
        else
            return false;

        coords = _transform.ToCoordinates(grid, mousePos);
        return true;
    }
}
