using Content.Shared._AU14.Formations;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._AU14.Formations;

[UsedImplicitly]
public sealed partial class FormationMenuBui : BoundUserInterface
{
    [Dependency] private IEntityManager _entityManager = default!;

    private FormationMenuWindow? _window;
    private FormationClientSystem? _clientSystem;

    public FormationMenuBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _clientSystem = _entityManager.System<FormationClientSystem>();

        _window = this.CreateWindow<FormationMenuWindow>();
        _window.Title = "Formation Control";

        // Leader dot placement is kept in server code but not exposed in UI.
        _window.PlaceFollowerDotButton.OnPressed += _ => StartPlacement(false);
        _window.UndoLastButton.OnPressed += _ => SendMessage(new FormationUndoLastDotMsg());
        _window.ConfirmButton.OnPressed += _ => SendMessage(new FormationConfirmMsg());
        _window.CancelPlanningButton.OnPressed += _ =>
        {
            SendMessage(new FormationCancelPlanningMsg());
            _clientSystem?.ExitPlacementMode();
        };
        _window.ClearAllButton.OnPressed += _ => SendMessage(new FormationClearMsg());
        _window.DisbandButton.OnPressed += _ => SendMessage(new FormationDisbandMsg());
        _window.HaltMarchButton.OnPressed += _ => SendMessage(new FormationFreezeToggleMsg());
        _window.DebugToggleButton.OnPressed  += _ => SendMessage(new FormationDebugToggleMsg());
        _window.ModeHoldButton.OnPressed        += _ => SendMessage(new FormationSetFollowModeMsg { Mode = FormationFollowMode.Hold });
        _window.ModeChaseButton.OnPressed       += _ => SendMessage(new FormationSetFollowModeMsg { Mode = FormationFollowMode.Chase });
        _window.CollisionToggleButton.OnPressed    += _ => SendMessage(new FormationCollisionToggleMsg());
        _window.DotLifetimeToggleButton.OnPressed  += _ => SendMessage(new FormationDotLifetimeToggleMsg());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _clientSystem?.ExitPlacementMode();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null) return;
        if (state is not FormationMenuBuiState s) return;

        _window.UpdateState(s);

        if (s.IsInPlanningMode && _clientSystem != null && !(_clientSystem.IsInPlacementMode))
            _clientSystem.EnterPlacementMode(this, s.IsPlacingLeaderDot);
    }

    private void StartPlacement(bool isLeaderDot)
    {
        SendMessage(new FormationEnterPlacementMsg { IsLeaderDot = isLeaderDot });
        _clientSystem?.EnterPlacementMode(this, isLeaderDot);
    }

    /// <summary>Called by <see cref="FormationClientSystem"/> when the player clicks a tile in placement mode.</summary>
    public void SendPlaceDotMessage(Vector2i tile, Direction facing, bool isLeaderDot)
    {
        SendMessage(new FormationPlaceDotMsg
        {
            TileX = tile.X,
            TileY = tile.Y,
            Facing = facing,
            IsLeaderDot = isLeaderDot,
        });
    }
}
