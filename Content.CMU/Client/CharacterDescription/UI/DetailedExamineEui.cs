using Content.Client.Eui;
using Content.Shared._CMU14.CharacterDescription;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._CMU14.CharacterDescription.UI;

[UsedImplicitly]
public sealed class DetailedExamineEui : BaseEui
{
    private readonly DetailedExamineWindow _window;

    public DetailedExamineEui()
    {
        _window = new DetailedExamineWindow();
        _window.OnClose += OnClosed;
    }

    private void OnClosed()
    {
        SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is DetailedExamineEuiState detailedState)
            _window.UpdateState(detailedState);
    }
}
