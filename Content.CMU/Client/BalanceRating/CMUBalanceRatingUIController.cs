using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Shared._CMU14.BalanceRating;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.BalanceRating;

public sealed partial class CMUBalanceRatingUIController : UIController,
    IOnStateEntered<LobbyState>,
    IOnStateExited<LobbyState>
{
    [Dependency] private IGameTiming _timing = default!;

    private PendingPrompt? _pending;
    private CMUBalanceRatingPopup? _popup;
    private bool _inLobby;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CMUBalanceRatingOpenEvent>(OnPromptOpened);
        SubscribeNetworkEvent<CMUBalanceRatingCloseEvent>(OnPromptClosed);
    }

    public void OnStateEntered(LobbyState state)
    {
        _inLobby = true;
        TryShowPending();
    }

    public void OnStateExited(LobbyState state)
    {
        _inLobby = false;
        _pending = null;
        RemovePopup();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_pending is { } pending && pending.EndTime <= _timing.RealTime)
        {
            _pending = null;
            RemovePopup();
            return;
        }

        TryShowPending();
    }

    private void OnPromptOpened(CMUBalanceRatingOpenEvent ev, EntitySessionEventArgs args)
    {
        var duration = ev.Duration < TimeSpan.Zero ? TimeSpan.Zero : ev.Duration;
        _pending = new PendingPrompt(
            ev.PollId,
            ev.Target,
            ev.Metric,
            ev.TargetId,
            ev.TargetName,
            _timing.RealTime + duration);

        RemovePopup();
        TryShowPending();
    }

    private void OnPromptClosed(CMUBalanceRatingCloseEvent ev, EntitySessionEventArgs args)
    {
        if (_pending?.PollId == ev.PollId)
            _pending = null;

        if (_popup?.PollId == ev.PollId)
            RemovePopup();
    }

    private void TryShowPending()
    {
        if (!_inLobby || _popup != null || _pending is not { } pending)
            return;

        if (pending.EndTime <= _timing.RealTime)
        {
            _pending = null;
            return;
        }

        if (UIManager.ActiveScreen is not LobbyGui lobby || lobby.BalanceRatingContainer.Disposed)
            return;

        var popup = new CMUBalanceRatingPopup(
            pending.PollId,
            pending.Target,
            pending.Metric,
            pending.TargetId,
            pending.TargetName,
            pending.EndTime);
        popup.RatingSelected += OnRatingSelected;
        popup.Expired += OnPopupExpired;

        _popup = popup;
        lobby.BalanceRatingContainer.AddChild(popup);
        lobby.PositionBalanceRatingContainer();
    }

    private void OnRatingSelected(byte rating)
    {
        if (_popup is not { } popup)
            return;

        var pollId = popup.PollId;
        _pending = null;
        RemovePopup();
        EntityManager.System<CMUBalanceRatingClientSystem>().SendRating(pollId, rating);
    }

    private void OnPopupExpired()
    {
        _pending = null;
        RemovePopup();
    }

    private void RemovePopup()
    {
        if (_popup == null)
            return;

        _popup.RatingSelected -= OnRatingSelected;
        _popup.Expired -= OnPopupExpired;
        _popup.Orphan();
        _popup = null;
    }

    private readonly record struct PendingPrompt(
        long PollId,
        CMUBalanceRatingTarget Target,
        CMUBalanceRatingMetric Metric,
        string TargetId,
        string TargetName,
        TimeSpan EndTime);
}
