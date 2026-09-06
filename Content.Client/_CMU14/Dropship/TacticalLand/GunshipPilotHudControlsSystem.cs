using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Dropship.TacticalLand;

/// <summary>
/// Interactive portion of the pilot HUD. The remaining indicators stay in an
/// overlay, while this control provides a real button that consumes UI clicks.
/// </summary>
public sealed partial class GunshipPilotHudControlsSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private GunshipPilotTopBar? _bar;
    private LayoutContainer? _parent;
    private TimeSpan _nextStatusUpdate;

    public override void Shutdown()
    {
        HideBar();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } pilot ||
            !TryComp(pilot, out GunshipPilotHudComponent? hud) ||
            hud.Dropship is not { } dropship)
        {
            HideBar();
            return;
        }

        if (!EnsureBar())
            return;

        if (_timing.CurTime < _nextStatusUpdate)
            return;

        _nextStatusUpdate = _timing.CurTime + TimeSpan.FromMilliseconds(100);

        _bar!.SetViewMode(hud.RearView ? Loc.GetString("cmu-gunship-view-rear-camera") : hud.ViewOffset switch
        {
            > 0 => Loc.GetString("cmu-gunship-view-upper-camera"),
            < 0 => Loc.GetString("cmu-gunship-view-lower-camera"),
            _ => Loc.GetString("cmu-gunship-view-pilot"),
        });

        if (hud.FlightControlsAvailable)
        {
            _bar.SetFlightStatus("--", Loc.GetString("cmu-gunship-stage-tactical-hover"));
            return;
        }

        if (!TryComp(dropship, out FTLComponent? ftl))
        {
            _bar.SetFlightStatus("--", Loc.GetString("cmu-gunship-stage-ready"));
            return;
        }

        var stage = ftl.State switch
        {
            FTLState.Starting => Loc.GetString("cmu-gunship-stage-launching"),
            FTLState.Travelling => Loc.GetString("cmu-gunship-stage-transit"),
            FTLState.Arriving => Loc.GetString("cmu-gunship-stage-final-approach"),
            FTLState.Cooldown => Loc.GetString("cmu-gunship-stage-refueling"),
            _ => Loc.GetString("cmu-gunship-stage-ready"),
        };
        var phaseRemaining = Math.Max(0, (ftl.StateTime.End - _timing.CurTime).TotalSeconds);
        var destinationRemaining = ftl.State switch
        {
            // TravelTime contains both transit and final approach.
            FTLState.Starting => phaseRemaining + ftl.TravelTime,
            FTLState.Travelling => phaseRemaining +
                                   Math.Max(0, ftl.TravelTime - ftl.StateTime.Length.TotalSeconds),
            FTLState.Arriving => phaseRemaining,
            _ => -1,
        };
        var eta = destinationRemaining < 0
            ? "--"
            : Loc.GetString("cmu-gunship-eta-countdown", ("seconds", Math.Ceiling(destinationRemaining)));
        _bar.SetFlightStatus(eta, stage);
    }

    private bool EnsureBar()
    {
        var screen = _ui.ActiveScreen;
        if (screen == null)
            return false;

        var parent = screen.FindControl<LayoutContainer>("ViewportContainer");
        if (_parent != parent)
        {
            HideBar();
            _parent = parent;
        }

        if (_bar != null)
            return true;

        _bar = new GunshipPilotTopBar(() => RaiseNetworkEvent(new GunshipOpenNavigationInputEvent()));
        parent.AddChild(_bar);
        LayoutContainer.SetAnchorPreset(_bar, LayoutContainer.LayoutPreset.CenterTop);
        LayoutContainer.SetMarginLeft(_bar, -340f);
        LayoutContainer.SetMarginRight(_bar, 340f);
        LayoutContainer.SetMarginTop(_bar, 8f);
        LayoutContainer.SetMarginBottom(_bar, 70f);
        return true;
    }

    private void HideBar()
    {
        _bar?.Orphan();
        _bar = null;
        _parent = null;
        _nextStatusUpdate = TimeSpan.Zero;
    }
}
