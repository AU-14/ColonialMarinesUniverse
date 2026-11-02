using Content.Shared.CMU14.CaveSurvey;
using System.Numerics;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUReconstructionWindow
{
    private bool _caveSurvey;
    private Action? _cavePan;
    private Vector2i _caveMin;
    private Vector2i _caveMax;
    public Func<(Vector2? Position, int Depth)>? CavePosition;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_caveSurvey || CavePosition == null || View.Scene is not { } scene) return;
        var location = CavePosition();
        scene.OperatorPosition = location.Position;
        scene.OperatorDepth = location.Depth;
        CenterPlayer.Disabled = location.Position == null;
        if (IsRefreshing || _cavePan == null) return;
        var center = View.CaptureCamera().Center;
        const int margin = 128;
        if (scene.Origin.X > _caveMin.X && center.X < scene.Origin.X + margin ||
            scene.Origin.Y > _caveMin.Y && center.Y < scene.Origin.Y + margin ||
            scene.Origin.X + scene.Width < _caveMax.X && center.X > scene.Origin.X + scene.Width - margin ||
            scene.Origin.Y + scene.Height < _caveMax.Y && center.Y > scene.Origin.Y + scene.Height - margin)
            _cavePan();
    }

    public void BeginCaveViewRequest(int request, bool center)
    {
        BeginViewRequest(request, keepScene: !center);
        if (center) _views.Clear();
        _centerPending = center;
    }

    public void ConfigureCaveSurvey(Action pan, Action<int> depth, Action center, Action refresh, Action nextWorld, Action surface)
    {
        _caveSurvey = true;
        _cavePan = pan;
        View.ShowOperatorPosition = true;
        Title = Heading.Text = Loc.GetString("cmu-survey-title");
        CaveNavigation.Visible = CaveInfo.Visible = true;
        MapHeading.Visible = MapSelection.Visible = Contacts.Visible = MarineNames.Visible = false;
        CaveDeeper.OnPressed += _ => depth(-8);
        CaveShallower.OnPressed += _ => depth(8);
        CaveCenter.OnPressed += _ => center();
        CaveRefresh.OnPressed += _ => refresh();
        CaveWorld.OnPressed += _ => nextWorld();
        CaveSurface.OnPressed += _ => surface();
    }

    public void SetCaveSection(CMUCaveSurveySectionMessage message, bool hasSurface)
    {
        CaveInfo.Text = Loc.GetString("cmu-survey-world-info", ("world", message.Name), ("blocks", message.Blocks));
        CaveSurface.Visible = hasSurface;
        _caveMin = message.Min;
        _caveMax = message.Max;
    }

    private void UpdateCaveStatus()
    {
        if (!_caveSurvey || View.Scene is not { } scene) return;
        Feed.Text = Loc.GetString("cmu-survey-recorded");
        Status.Text = Loc.GetString("cmu-survey-navigation",
            ("bottom", scene.MinDepth), ("top", scene.MinDepth + scene.Levels - 1));
        CaveShallower.Disabled = scene.MinDepth >= -8;
        CaveDeeper.Disabled = scene.MinDepth <= -4096;
    }
}
