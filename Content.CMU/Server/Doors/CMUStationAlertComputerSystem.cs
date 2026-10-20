using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Areas;
using Content.Shared.Chat;
using Content.Shared.CMU14.Doors;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Doors;

/// <summary>
///     Has every <see cref="CMUStationAlertComputerComponent"/> on a ship call out each of its firelocks that seals
///     itself against escaping air, naming the area it's in.
/// </summary>
public sealed partial class CMUStationAlertComputerSystem : EntitySystem
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFirelockSealedEvent>(OnFirelockSealed);
        InitializeScreen();
    }

    private void OnFirelockSealed(ref CMUFirelockSealedEvent ev)
    {
        if (Transform(ev.Firelock).MapUid is not { } firelockMap)
            return;

        var area = _area.TryGetArea(ev.Firelock, out _, out var areaProto)
            ? areaProto.Name
            : Loc.GetString("cmu-station-alert-unknown-area");

        var now = _timing.CurTime;
        var message = Loc.GetString("cmu-station-alert-firelock", ("area", area));
        var query = EntityQueryEnumerator<CMUStationAlertComputerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var computer, out var xform))
        {
            if (!_zLevels.IsSameZNetwork(xform.MapUid, firelockMap))
                continue;

            if (computer.LastAlert.TryGetValue(area, out var last) && now < last + computer.AreaCooldown)
                continue;

            computer.LastAlert[area] = now;
            _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, false, ignoreActionBlocker: true);
        }
    }
}
