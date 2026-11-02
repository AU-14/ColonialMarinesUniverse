using Content.Shared.CMU14.CaveSurvey;
using System.Numerics;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUReconstructionWindow
{
    private bool _caveSurvey;
    public Func<(Vector2? Position, int Depth)>? CavePosition;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_caveSurvey || CavePosition == null || View.Scene is not { } scene) return;
        var location = CavePosition();
        scene.OperatorPosition = location.Position;
        scene.OperatorDepth = location.Depth;
        CenterPlayer.Disabled = location.Position == null;
    }

    public void BeginCaveViewRequest(int request, bool center)
    {
        BeginViewRequest(request);
        if (center) _views.Clear();
        _centerPending = center;
    }

    public void ConfigureCaveSurvey(Action<Vector2i, int> page, Action center, Action refresh, Action nextWorld, Action surface)
    {
        _caveSurvey = true;
        View.ShowOperatorPosition = true;
        Title = Heading.Text = Loc.GetString("cmu-survey-title");
        CaveNavigation.Visible = CaveInfo.Visible = true;
        MapHeading.Visible = MapSelection.Visible = Contacts.Visible = MarineNames.Visible = false;
        CaveWest.OnPressed += _ => page(new Vector2i(-64, 0), 0);
        CaveEast.OnPressed += _ => page(new Vector2i(64, 0), 0);
        CaveNorth.OnPressed += _ => page(new Vector2i(0, 64), 0);
        CaveSouth.OnPressed += _ => page(new Vector2i(0, -64), 0);
        CaveDeeper.OnPressed += _ => page(default, -8);
        CaveShallower.OnPressed += _ => page(default, 8);
        CaveCenter.OnPressed += _ => center();
        CaveRefresh.OnPressed += _ => refresh();
        CaveWorld.OnPressed += _ => nextWorld();
        CaveSurface.OnPressed += _ => surface();
    }

    public void SetCaveSection(CMUCaveSurveySectionMessage message, bool hasSurface)
    {
        CaveInfo.Text = Loc.GetString("cmu-survey-world-info", ("world", message.Name), ("blocks", message.Blocks));
        CaveSurface.Visible = hasSurface;
    }

    private void UpdateCaveStatus()
    {
        if (!_caveSurvey || View.Scene is not { } scene) return;
        Feed.Text = Loc.GetString("cmu-survey-recorded");
        Status.Text = Loc.GetString("cmu-survey-section", ("x", scene.Origin.X), ("y", scene.Origin.Y),
            ("bottom", scene.MinDepth), ("top", scene.MinDepth + scene.Levels - 1));
        CaveShallower.Disabled = scene.MinDepth >= -8;
        CaveDeeper.Disabled = scene.MinDepth <= -4096;
    }
}
