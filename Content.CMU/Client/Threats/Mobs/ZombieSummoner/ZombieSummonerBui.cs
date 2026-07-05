using Content.Shared._CMU14.Threats.Mobs.ZombieSummoner;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Threats.Mobs.ZombieSummoner;

[UsedImplicitly]
public sealed class ZombieSummonerBui : BoundUserInterface
{
    [ViewVariables]
    private ZombieSummonerWindow? _window;

    public ZombieSummonerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ZombieSummonerWindow>();
        _window.OnSummon += (count, type) => SendPredictedMessage(new ZombieSummonerSpawnMessage(count, type));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ZombieSummonerBuiState summonerState)
            _window?.UpdateState(summonerState);
    }
}
