using Robust.Shared.Player;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Xenonids.JoinXeno;

public sealed partial class JoinXenoSystem
{
    private static readonly TimeSpan OpenWindowRefresh = TimeSpan.FromSeconds(1);
    private TimeSpan _nextOpenWindowRefresh;

    // the window only got its state when it opened or you picked a hive, so the queue position
    // sat frozen until you reopened it. resend to whoever has it open
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        if (now < _nextOpenWindowRefresh)
            return;

        _nextOpenWindowRefresh = now + OpenWindowRefresh;

        var query = EntityQueryEnumerator<JoinXenoComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out var actor))
        {
            if (_ui.IsUiOpen(uid, JoinXenoUIKey.Key))
                UpdateJoinXenoUi(uid, actor.PlayerSession.UserId);
        }
    }
}
