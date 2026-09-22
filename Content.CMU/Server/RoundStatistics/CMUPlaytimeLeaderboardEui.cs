using System;
using Content.Server.EUI;
using Content.Shared.CMU14.RoundStatistics;
using Content.Shared.Eui;
using Robust.Shared.Asynchronous;
using Robust.Shared.Log;

namespace Content.Server.CMU14.RoundStatistics;

public sealed partial class CMUPlaytimeLeaderboardEui : BaseEui
{
    [Dependency] private readonly ITaskManager _task = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("cmu.playtime_leaderboard");

    private CMUPlaytimeLeaderboard _board = new(0, [], [], [], []);

    public CMUPlaytimeLeaderboardEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        base.Opened();

        Load();
    }

    public override EuiStateBase GetNewState()
        => new CMUPlaytimeLeaderboardEuiState(_board);

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is CMUPlaytimeLeaderboardRefreshMsg)
            Load();
    }

    private async void Load()
    {
        try
        {
            var leaderboard = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<CMUPlaytimeLeaderboardSystem>();
            var snapshot = await leaderboard.GetSnapshot();
            _task.RunOnMainThread(() =>
            {
                try
                {
                    if (IsShutDown)
                        return;

                    _board = leaderboard.Bake(snapshot, Player);
                    StateDirty();
                }
                catch (Exception e)
                {
                    _sawmill.Error($"Failed to bake CMU playtime leaderboard:\n{e}");
                }
            });
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to load CMU playtime leaderboard:\n{e}");
        }
    }
}
