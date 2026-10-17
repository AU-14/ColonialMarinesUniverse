using System.Numerics;
using System.Linq;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.CMU14.Fighter;

/// <summary>Static terrain chart with editable entry/exit points and the shared aircraft position.</summary>
public sealed class FighterRouteControl : Control
{
    private readonly Font _font;
    private FighterAircraftComponent? _aircraft;
    private FighterChartComponent? _chart;
    private Vector2 _receivedEntry;
    private Vector2 _receivedExit;
    private bool _editable;
    private FighterWeaponsComponent? _weapons;
    private FighterSeatComponent? _seat;
    private FighterAirCombatComponent? _combat;
    private FighterEffectsComponent? _effects;
    private TimeSpan _now;
    public Action<NetEntity>? SelectTarget;
    public Action<int>? SelectSector;
    public bool CoverageMode;
    public bool ReadOnly;
    public bool EditEntry = true;
    public Vector2 Entry;
    public Vector2 Exit;
    public bool HasDraft => Entry != _receivedEntry || Exit != _receivedExit;
    private static readonly Color[] TerrainColors = [Color.FromHex("#182726"), Color.FromHex("#778579"), Color.FromHex("#386774"), Color.FromHex("#47633C"), Color.FromHex("#ABA780")];

    public FighterRouteControl()
    {
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Stop;
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    public void SetFlight(FighterAircraftComponent a, FighterChartComponent? chart, bool pilot)
    {
        if (_aircraft == null || a.Entry != _receivedEntry || a.Exit != _receivedExit || a.Flying)
        {
            Entry = _receivedEntry = a.Entry;
            Exit = _receivedExit = a.Exit;
        }
        _aircraft = a;
        _chart = chart;
        _editable = pilot && !a.Flying;
    }

    private float Scale => CoverageMode
        ? Math.Max(.01f, Math.Min((PixelSize.X - 24) / _aircraft!.Battlefield.Width, (PixelSize.Y - 24) / _aircraft.Battlefield.Height))
        : Math.Max(1, Math.Min(PixelSize.X, PixelSize.Y) - 42) / (_aircraft!.AirspaceRadius * 2);
    private Vector2 Center => CoverageMode ? _aircraft!.Battlefield.Center : _aircraft!.Home;
    private Vector2 Project(Vector2 p) => (Vector2) PixelSize / 2 + (p - Center) * new Vector2(Scale, -Scale);

    public void SetWeapons(FighterWeaponsComponent? weapons, FighterSeatComponent seat, FighterAirCombatComponent? combat, TimeSpan now)
    {
        _weapons = weapons;
        _seat = seat;
        _combat = combat;
        _now = now;
    }

    public void SetEffects(FighterEffectsComponent? effects) => _effects = effects;

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (ReadOnly || args.Function != EngineKeyFunctions.UIClick || _aircraft == null)
            return;
        if (CoverageMode)
        {
            args.Handle();
            var position = Center + (args.RelativePosition * UIScale - (Vector2) PixelSize / 2) / new Vector2(Scale, -Scale);
            var sector = FighterAirCombat.SectorAt(_aircraft.Battlefield, position);
            if (sector >= 0 && !_aircraft.ForcedRetreat) SelectSector?.Invoke(sector);
            return;
        }
        if (_weapons != null)
            foreach (var flare in _weapons.Targets)
                if (Vector2.Distance(Project(flare.Position), args.RelativePosition * UIScale) <= 12)
                {
                    args.Handle();
                    SelectTarget?.Invoke(flare.Id);
                    return;
                }
        if (!_editable) return;
        args.Handle();
        var point = (args.RelativePosition * UIScale - (Vector2) PixelSize / 2) / new Vector2(Scale, -Scale);
        if (point.Length() > _aircraft.AirspaceRadius)
            point = Vector2.Normalize(point) * _aircraft.AirspaceRadius;
        point += _aircraft.Home;
        if (EditEntry) Entry = point;
        else Exit = point;
        EditEntry = !EditEntry;
    }

    protected override void Draw(DrawingHandleScreen h)
    {
        h.DrawRect(UIBox2.FromDimensions(Vector2.Zero, PixelSize), TerrainColors[0]);
        if (_aircraft is not { } a)
            return;
        var scale = Scale;
        var bounds = a.Battlefield;
        var n = FighterChartComponent.Resolution;
        if (_chart?.Terrain.Length == n * n)
        {
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n;)
            {
                var color = _chart.Terrain[y * n + x];
                var end = x + 1;
                while (end < n && _chart.Terrain[y * n + end] == color) end++;
                var p = Project(bounds.BottomLeft + bounds.Size * new Vector2((float) x / n, (float) (y + 1) / n));
                h.DrawRect(UIBox2.FromDimensions(p, bounds.Size * scale * new Vector2((float) (end - x) / n, 1f / n) + new Vector2(.5f)), TerrainColors[Math.Min(color, (byte) 4)]);
                x = end;
            }
        }
        if (CoverageMode) DrawCoverage(h, a);
        else h.DrawCircle((Vector2) PixelSize / 2, a.AirspaceRadius * scale, new Color(.32f, .42f, .36f), false);
        var green = new Color(.72f, .92f, .62f);
        h.DrawLine(Project(Entry), Project(Exit), green);
        h.DrawCircle(Project(Entry), 6, green, false);
        h.DrawRect(UIBox2.FromDimensions(Project(Exit) - new Vector2(5), new Vector2(10)), green, false);
        h.DrawString(_font, Project(Entry) + new Vector2(9, -7), Loc.GetString("cmu-fighter-entry-short"), green);
        h.DrawString(_font, Project(Exit) + new Vector2(9, 14), Loc.GetString("cmu-fighter-exit-short"), green);
        if (a.Flying && a.Phase != FighterPhase.Pass)
        {
            var previous = Project(FighterFlight.Curve(a, 0));
            for (var i = 1; i <= 32; i++)
            {
                var next = Project(FighterFlight.Curve(a, i / 32f));
                h.DrawLine(previous, next, new Color(.47f, .6f, .53f));
                previous = next;
            }
        }
        var position = Project(a.Position);
        var forward = FighterFlight.Forward(a.Heading) * new Vector2(1, -1);
        var right = new Vector2(-forward.Y, forward.X);
        h.DrawLine(position + forward * 11, position - forward * 7 + right * 6, green);
        h.DrawLine(position + forward * 11, position - forward * 7 - right * 6, green);
        if (a.Mark is { } mark)
            h.DrawRect(UIBox2.FromDimensions(Project(mark) - new Vector2(6), new Vector2(12)), Color.Yellow, false);
        if (!CoverageMode && _weapons != null && _seat != null)
        {
            var weapon = _weapons.Loadout.FirstOrDefault(slot => slot.Slot == _seat.WeaponSlot);
            foreach (var flare in _weapons.Targets)
            {
                var color = FighterWeapons.Status(a, _weapons, _seat, weapon, flare, _now) == FighterFireStatus.Ready ? Color.LimeGreen : Color.Orange;
                var marker = Project(flare.Position);
                if (flare.Laser)
                {
                    h.DrawRect(UIBox2.FromDimensions(marker - new Vector2(5), new Vector2(10)), color, false);
                    h.DrawLine(marker - new Vector2(8, 0), marker + new Vector2(8, 0), color);
                    h.DrawLine(marker - new Vector2(0, 8), marker + new Vector2(0, 8), color);
                }
                else h.DrawCircle(marker, 5, color);
                if (_seat.Target == flare.Id) h.DrawCircle(marker, 9, Color.White, false);
                h.DrawString(_font, marker + new Vector2(8, -8), flare.Name, color);
            }
        }
        h.DrawString(_font, new Vector2(8, 18), Loc.GetString("cmu-fighter-north"), green);
        h.DrawCircle(Project(FighterFlight.HoldingPoint(a)), 7, Color.Gray, false);
    }

    private void DrawCoverage(DrawingHandleScreen h, FighterAircraftComponent aircraft)
    {
        var grid = FighterAirCombat.Grid(aircraft.Battlefield);
        for (var sector = 0; sector < grid.X * grid.Y; sector++)
        {
            var bounds = FighterAirCombat.SectorBounds(aircraft.Battlefield, sector);
            var rect = new UIBox2(Project(bounds.TopLeft), Project(bounds.BottomRight));
            var selected = _combat?.CoveredSectors.Contains(sector) == true;
            var color = selected ? _now < _combat!.CoverageReadyAt ? Color.Orange : Color.LimeGreen : Color.FromHex("#A8BABA");
            if (selected) h.DrawRect(rect, color.WithAlpha(.24f));
            h.DrawRect(rect, color.WithAlpha(selected ? 1 : .6f), false);
            h.DrawString(_font, rect.TopLeft + new Vector2(5, 16), FighterAirCombat.Label(aircraft.Battlefield, sector), color);
            if (_combat is { } combat && combat.LastLaunchSector == sector && _now < combat.InterceptReadyAt)
                h.DrawString(_font, Project(bounds.Center) + new Vector2(-17, 4), Loc.GetString("cmu-fighter-intercept-launch"), Color.Orange);
        }
        if (_combat?.CoveredSectors.Count > 0)
        {
            var sweep = (float) (_now.TotalSeconds * .25 % 1);
            var y = aircraft.Battlefield.Bottom + aircraft.Battlefield.Height * sweep;
            h.DrawLine(Project(new Vector2(aircraft.Battlefield.Left, y)), Project(new Vector2(aircraft.Battlefield.Right, y)),
                Color.LimeGreen.WithAlpha(.3f));
        }
        if (_effects == null) return;
        foreach (var cue in _effects.Cues)
        {
            if (cue.Kind != FighterEffectKind.Interceptor || cue.Index < 0 || cue.Index >= grid.X * grid.Y ||
                !FighterEffects.Active(_effects, cue, _now)) continue;
            var progress = FighterEffects.Age(_effects, cue, _now) / cue.Duration;
            var start = Project(FighterFlight.HoldingPoint(aircraft));
            var end = Project(FighterAirCombat.SectorBounds(aircraft.Battlefield, cue.Index).Center);
            var position = Vector2.Lerp(start, end, progress);
            h.DrawLine(Vector2.Lerp(start, end, Math.Max(0, progress - .15f)), position, Color.Orange);
            h.DrawCircle(position, 3, Color.White);
            h.DrawCircle(end, 8 + (1 - progress) * 12, Color.Orange.WithAlpha(progress), false);
        }
    }
}
