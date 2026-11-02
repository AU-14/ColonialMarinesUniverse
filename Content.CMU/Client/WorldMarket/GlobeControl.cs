// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Content.Client.CMU14.Crafting;
using Content.Shared.CMU14.WorldMarket;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.WorldMarket;

internal enum SelectionKind : byte
{
    None,
    Cell,
    City,
    Road,
}

// What the cursor is on, or what was picked: a cell of the ball (its biome and place), a city, or a road.
internal readonly record struct MapSelection(SelectionKind Kind, string? Id, float Latitude, float Longitude)
{
    public static readonly MapSelection None = new(SelectionKind.None, null, 0f, 0f);
}

// One cell of the world map: a patch of the ball between two parallels and two meridians, with a biome. The cells are not the same in every row (nearer to the poles a row has fewer of them,
// so that they are about as wide as they are tall everywhere on the ball), the way the tiles of a world map are. What the cell is made of is kept as points of the ball, so that turning the
// ball is only a turning of those points.
internal sealed class MapCell
{
    public float Lat0;
    public float Lat1;
    public float Lon0;
    public float Lon1;
    public float LatC;
    public float LonC;
    public string Biome = string.Empty;

    // The corners of the cell (the lower edge, then the upper one, each in steps that bend with the ball) and the middle of it, as points of a ball of one.
    public Vector3[] Ring = Array.Empty<Vector3>();
    public Vector3 Center;

    // How high the land is, or how deep the sea (the sea is below nothing).
    public float Height;

    // A little lighter or darker than the others of its biome, so that a field of one biome is not a flat colour.
    public sbyte Shade;
}

// The world map as a ball in the manner of a world map of a colony game: a dark sky with stars, a planet with a rim of air, the surface cut into small separate cells, each of a biome with its
// colour, roads that go from city to city, the cities as marks with their names, and the names of the seas. The ball is turned by dragging it with the mouse (the point under the cursor follows
// it), is brought nearer and farther with the wheel, and a click picks a city, a road or a cell. The land is not drawn by hand: the height, the moisture and the warmth of every place of the
// ball come of a noise (so that the coasts are ragged, there are islands and bays, and the mountains lie in ranges), with the pieces of land set where the cities are.
internal sealed class GlobeControl : Control
{
    private const float Degrees = MathF.PI / 180f;
    // The cells of the ball are a third of the size they were in area (three times as many of them): 125 rows instead of 72 (a row is 2.5 divided by the root of three).
    private const int Rows = 125;
    private const float RowDegrees = 180f / Rows;
    private const float MinZoom = 0.7f;
    private const float MaxZoom = 6f;

    // The names of the seas, in the order that they are given to the deepest places of the sea.
    private static readonly string[] SeaNames =
    {
        "rumc-map-sea-tadarinum",
        "rumc-map-sea-grey",
        "rumc-map-sea-silver",
        "rumc-map-sea-calm",
    };

    private const string Ocean = WorldMapTerrain.Ocean;

    // The lit side is the upper left; toward the edge of the ball it grows darker.
    private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(-0.45f, 0.5f, 0.75f));

    private static (float X, float Y)[]? _stars;

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly Font _small;
    private readonly Font _large;

    private float _yaw = 0.5f;
    private float _pitch = 0.35f;
    private float _zoom = 1f;
    private float _time;

    // The turning of the ball, as sines and cosines that are found once for a frame.
    private float _cy = 1f;
    private float _sy;
    private float _cp = 1f;
    private float _sp;

    private bool _dragging;
    private Vector2 _lastMouse;
    private float _dragTravel;
    private bool _mouseIn;
    private Vector2 _mousePixel;
    private float? _focusYaw;
    private float? _focusPitch;

    private List<CityEntry> _cities = new();
    private List<ZoneEntry> _zones = new();
    private List<HazardEntry> _hazards = new();
    private List<CaravanEntry> _caravans = new();
    private readonly List<RoadLine> _roads = new();
    private string _roadsFor = string.Empty;
    private readonly Dictionary<string, Color> _biomeColor = new();

    private List<MapCell>? _cells;
    private int[] _rowStart = Array.Empty<int>();
    private int[] _rowCount = Array.Empty<int>();
    private string _cellsFor = string.Empty;
    private Vector3[] _cityPoints = Array.Empty<Vector3>();

    // The land of the round (made of the number that the server gives), and where the seas are deepest, for their names.
    private WorldMapTerrain? _terrain;
    private readonly List<(float Lat, float Lon, string Name)> _seas = new();

    // What the batches of the cells were made for: when it is the same as now they are drawn again as they are and not made again.
    private (float Yaw, float Pitch, float Zoom, Vector2i Size, bool Biomes, bool Grid) _batchedFor;
    private bool _batched;
    private readonly Dictionary<Color, List<Vector2>> _batches = new();
    private readonly List<Vector2> _lines = new();
    private readonly List<Vector2> _ring = new();

    private MapSelection _hover = MapSelection.None;
    private MapSelection _picked = MapSelection.None;

    // The layers that are shown.
    public bool ShowRoads { get; set; } = true;
    public bool ShowBiomes { get; set; } = true;
    public bool ShowGrid { get; set; } = true;

    public float Zoom => _zoom;

    // The cursor came to something else (or to nothing).
    public event Action<MapSelection>? OnHovered;

    // A click that was not a drag, or a pick that was made from outside.
    public event Action<MapSelection>? OnPicked;

    public GlobeControl()
    {
        IoCManager.InjectDependencies(this);

        var regular = _cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf");
        _small = new VectorFont(regular, 11);
        _large = new VectorFont(regular, 18);

        MinSize = new Vector2(640, 520);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;

        EnsureStars();
    }

    #region The data

    // A road that is drawn: the points of it on the ball, from city to city.
    private sealed class RoadLine
    {
        public string Id = string.Empty;
        public string FromCity = string.Empty;
        public string ToCity = string.Empty;
        public WorldRoadKind Kind;
        public bool Blocked;
        public readonly List<(float Lat, float Lon)> Points = new();
    }

    public void SetCities(List<CityEntry> cities, int seed, List<RoadEntry> roads)
    {
        _cities = cities;
        EnsureCells(seed);
        BuildRoads(roads);
    }

    public void SetZones(List<ZoneEntry> zones)
    {
        _zones = zones;
    }

    public void SetHazards(List<HazardEntry> hazards)
    {
        _hazards = hazards;
    }

    public void SetCaravans(List<CaravanEntry> caravans)
    {
        _caravans = caravans;
    }

    // Turns the ball to a place.
    public void FocusPlace(float latitude, float longitude)
    {
        FocusOn(latitude, longitude);
    }

    private CityEntry? CityById(string? id)
    {
        foreach (var city in _cities)
        {
            if (city.Id == id)
                return city;
        }

        return null;
    }

    // The roads, as lines of points along the great circle from one city to the other (made again when the roads are others).
    private void BuildRoads(List<RoadEntry> roads)
    {
        var signature = _cellsFor + "|" + string.Join(",", roads.Select(r => r.Id + r.Kind + (r.Blocked.Length > 0)));
        if (signature == _roadsFor)
            return;

        _roadsFor = signature;
        _roads.Clear();

        foreach (var road in roads)
        {
            if (CityById(road.From) is not { } fromCity || CityById(road.To) is not { } toCity)
                continue;

            var a = ToVector(fromCity.Latitude, fromCity.Longitude);
            var b = ToVector(toCity.Latitude, toCity.Longitude);
            var line = new RoadLine { Id = road.Id, FromCity = road.From, ToCity = road.To, Kind = road.Kind, Blocked = road.Blocked.Length > 0 };
            var angle = MathF.Acos(Math.Clamp(Vector3.Dot(a, b), -1f, 1f));
            var steps = Math.Max(2, (int) MathF.Ceiling(angle / (2f * Degrees)));

            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float) steps;
                var sin = MathF.Sin(angle);
                var point = sin < 0.0001f ? a : (MathF.Sin((1f - t) * angle) * a + MathF.Sin(t * angle) * b) / sin;
                line.Points.Add((MathF.Asin(Math.Clamp(point.Y, -1f, 1f)) / Degrees, MathF.Atan2(point.X, point.Z) / Degrees));
            }

            _roads.Add(line);
        }
    }

    private static Vector3 ToVector(float latitude, float longitude)
    {
        var phi = latitude * Degrees;
        var lambda = longitude * Degrees;
        return new Vector3(MathF.Cos(phi) * MathF.Sin(lambda), MathF.Sin(phi), MathF.Cos(phi) * MathF.Cos(lambda));
    }

    private Color ColorOf(string biome)
    {
        if (_biomeColor.TryGetValue(biome, out var known))
            return known;

        var color = _proto.TryIndex<WorldBiomePrototype>(biome, out var proto) ? proto.Color : Color.Gray;
        _biomeColor[biome] = color;
        return color;
    }

    #endregion

    #region The view

    public void ZoomBy(float factor)
    {
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
    }

    public void ResetView()
    {
        _zoom = 1f;
        _pitch = 0.35f;
        _focusYaw = null;
        _focusPitch = null;
    }

    // Turns the ball to a city and picks it.
    public void FocusCity(string id, bool pick)
    {
        if (CityById(id) is not { } city)
            return;

        FocusOn(city.Latitude, city.Longitude);
        if (!pick)
            return;

        _picked = new MapSelection(SelectionKind.City, id, city.Latitude, city.Longitude);
        OnPicked?.Invoke(_picked);
    }

    public void FocusRoad(string id)
    {
        foreach (var road in _roads)
        {
            if (road.Id != id || road.Points.Count == 0)
                continue;

            var middle = road.Points[road.Points.Count / 2];
            FocusOn(middle.Lat, middle.Lon);
            _picked = new MapSelection(SelectionKind.Road, id, middle.Lat, middle.Lon);
            OnPicked?.Invoke(_picked);
            return;
        }
    }

    private void FocusOn(float latitude, float longitude)
    {
        _focusYaw = -longitude * Degrees;
        _focusPitch = Math.Clamp(latitude * Degrees, -1.1f, 1.1f);
    }

    private static float WrapPi(float angle)
    {
        angle = (angle + MathF.PI) % (MathF.PI * 2f);
        if (angle < 0f)
            angle += MathF.PI * 2f;

        return angle - MathF.PI;
    }

    private (Vector2 Center, float Radius) Frame()
    {
        var size = PixelSize;
        var radius = MathF.Min(size.X, size.Y) / 2f - 24f;
        return (new Vector2(size.X, size.Y) / 2f, radius * _zoom);
    }

    private void UpdateTurning()
    {
        _cy = MathF.Cos(_yaw);
        _sy = MathF.Sin(_yaw);
        _cp = MathF.Cos(_pitch);
        _sp = MathF.Sin(_pitch);
    }

    // A point of the ball (of one) as the eye sees it: x to the right, y up, z toward the viewer.
    private Vector3 Rotate(Vector3 v)
    {
        var x = v.X * _cy + v.Z * _sy;
        var z = v.Z * _cy - v.X * _sy;
        return new Vector3(x, v.Y * _cp - z * _sp, v.Y * _sp + z * _cp);
    }

    // Where a point of the ball is on the screen, and how far toward the viewer it is. A point on the far side is put on the edge of the disc (so that a cell that goes over the edge is cut
    // by the edge and not thrown away).
    private Vector2 Transform(Vector3 v, Vector2 center, float radius, out float depth)
    {
        var eye = Rotate(v);
        depth = eye.Z;

        var x = eye.X;
        var up = eye.Y;
        if (eye.Z < 0f)
        {
            var length = MathF.Sqrt(x * x + up * up);
            if (length > 0.0001f)
            {
                x /= length;
                up /= length;
            }
        }

        return new Vector2(center.X + x * radius, center.Y - up * radius);
    }

    private Vector2 Project(float latitude, float longitude, Vector2 center, float radius, out float depth)
    {
        return Transform(ToVector(latitude, longitude), center, radius, out depth);
    }

    // What place of the ball is under a point of the screen (the opposite of Project), if the point is on the ball.
    private bool Unproject(Vector2 pixel, out float latitude, out float longitude)
    {
        var (center, radius) = Frame();
        var x = (pixel.X - center.X) / radius;
        var up = -(pixel.Y - center.Y) / radius;
        latitude = 0f;
        longitude = 0f;

        var rest = 1f - x * x - up * up;
        if (rest < 0f)
            return false;

        var toward = MathF.Sqrt(rest);
        var y = up * _cp + toward * _sp;
        var z = -up * _sp + toward * _cp;

        latitude = MathF.Asin(Math.Clamp(y, -1f, 1f)) / Degrees;
        longitude = (MathF.Atan2(x, z) - _yaw) / Degrees;
        longitude = (longitude + 540f) % 360f - 180f;
        return true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _time += args.DeltaSeconds;

        if (_dragging)
        {
            // The mouse is followed by where it is on the screen and not by the events, so that letting go outside of the globe cannot leave it stuck to the cursor.
            if (!_input.IsKeyDown(Keyboard.Key.MouseLeft))
            {
                _dragging = false;
                return;
            }

            var position = _ui.MousePositionScaled.Position;
            var delta = position - _lastMouse;
            _lastMouse = position;
            _dragTravel += delta.Length();

            // A click that shakes a little does not turn the ball.
            if (_dragTravel > 4f)
            {
                // The point of the ball under the cursor follows it.
                var k = 1f / MathF.Max(Frame().Radius / UIScale, 80f);
                _yaw += delta.X * k;
                _pitch = Math.Clamp(_pitch + delta.Y * k, -1.2f, 1.2f);
            }

            return;
        }

        // Turning to what was picked.
        if (_focusYaw is { } focusYaw && _focusPitch is { } focusPitch)
        {
            var k = MathF.Min(1f, args.DeltaSeconds * 5f);
            _yaw += WrapPi(focusYaw - _yaw) * k;
            _pitch += (focusPitch - _pitch) * k;

            if (MathF.Abs(WrapPi(focusYaw - _yaw)) < 0.003f && MathF.Abs(focusPitch - _pitch) < 0.003f)
            {
                _focusYaw = null;
                _focusPitch = null;
            }
        }

        // What is under the cursor, found again every frame (the ball may have moved under it, or been zoomed).
        var hover = MapSelection.None;
        if (_mouseIn)
        {
            UpdateTurning();
            hover = SelectionAt(_mousePixel);
        }

        if (hover == _hover)
            return;

        _hover = hover;
        OnHovered?.Invoke(hover);
    }

    #endregion

    #region The terrain

    private static void EnsureStars()
    {
        if (_stars != null)
            return;

        // The stars of the sky, where they are is the same every time.
        var stars = new (float X, float Y)[160];
        var seed = 12345u;
        for (var i = 0; i < stars.Length; i++)
        {
            seed = seed * 1664525u + 1013904223u;
            var x = (seed >> 8 & 0xFFFF) / 65535f;
            seed = seed * 1664525u + 1013904223u;
            var y = (seed >> 8 & 0xFFFF) / 65535f;
            stars[i] = (x, y);
        }

        _stars = stars;
    }

    // Which of five shades a cell is (the same every time).
    private static int ShadeOf(int row, int index)
    {
        unchecked
        {
            var h = (uint) (row * 374761393 + index * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (int) (h % 5u);
        }
    }

    // The cells of the ball. They are made again only when the cities are others (the pieces of land are set round them).
    private void EnsureCells(int seed)
    {
        var signature = seed + "|" + string.Join("|", _cities.Select(c => $"{c.Id}:{c.Latitude}:{c.Longitude}"));
        if (_cells != null && signature == _cellsFor)
            return;

        _cellsFor = signature;
        _cityPoints = _cities.Select(c => ToVector(c.Latitude, c.Longitude)).ToArray();
        _terrain = new WorldMapTerrain(seed, _cityPoints);
        _batched = false;

        var cells = new List<MapCell>();
        const int rows = Rows;
        var starts = new int[rows];
        var counts = new int[rows];

        for (var row = 0; row < rows; row++)
        {
            var lat0 = -90f + row * RowDegrees;
            var lat1 = lat0 + RowDegrees;
            var latC = (lat0 + lat1) / 2f;
            var count = Math.Max(3, (int) MathF.Round(360f * MathF.Cos(latC * Degrees) / RowDegrees));
            var width = 360f / count;

            starts[row] = cells.Count;
            counts[row] = count;

            for (var index = 0; index < count; index++)
            {
                var lon0 = -180f + index * width;
                var cell = new MapCell
                {
                    Lat0 = lat0,
                    Lat1 = lat1,
                    Lon0 = lon0,
                    Lon1 = lon0 + width,
                    LatC = latC,
                    LonC = lon0 + width / 2f,
                };

                // The corners: the lower edge, then the upper one, each in steps of at most six degrees so that it bends with the ball.
                var steps = Math.Max(1, (int) MathF.Ceiling(width / 6f));
                var ring = new Vector3[(steps + 1) * 2];
                for (var s = 0; s <= steps; s++)
                {
                    var lon = cell.Lon0 + width * s / steps;
                    ring[s] = ToVector(lat0, lon);
                    ring[ring.Length - 1 - s] = ToVector(lat1, lon);
                }

                cell.Ring = ring;
                cell.Center = ToVector(cell.LatC, cell.LonC);
                cell.Biome = _terrain.Classify(cell.LatC, cell.Center, out cell.Height);
                cell.Shade = (sbyte) (ShadeOf(row, index) - 2);
                cells.Add(cell);
            }
        }

        _rowStart = starts;
        _rowCount = counts;
        _cells = cells;

        // The names of the seas go to the deepest places, far from one another.
        _seas.Clear();
        foreach (var cell in cells.Where(c => c.Biome == Ocean).OrderBy(c => c.Height))
        {
            if (_seas.Count >= SeaNames.Length)
                break;

            if (_seas.Any(s => WorldMapTerrain.AngleBetween(ToVector(s.Lat, s.Lon), cell.Center) < 55f))
                continue;

            _seas.Add((cell.LatC, cell.LonC, SeaNames[_seas.Count]));
        }
    }

    // The cell that the place is in.
    private MapCell? CellAt(float latitude, float longitude)
    {
        if (_cells == null || _rowStart.Length == 0)
            return null;

        var row = Math.Clamp((int) MathF.Floor((latitude + 90f) / RowDegrees), 0, _rowStart.Length - 1);
        var count = _rowCount[row];
        var index = Math.Clamp((int) MathF.Floor((longitude + 180f) / 360f * count), 0, count - 1);
        return _cells[_rowStart[row] + index];
    }

    #endregion

    #region Drawing

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var (center, radius) = Frame();
        UpdateTurning();

        // The sky.
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), new Color(0.015f, 0.02f, 0.035f));
        DrawStars(handle, size);

        // The air round the planet, in layers that grow fainter.
        for (var layer = 5; layer >= 1; layer--)
            handle.DrawCircle(center, radius + layer * 3f, new Color(0.35f, 0.6f, 1f, 0.035f * (6 - layer)));

        var sea = ColorOf(Ocean);
        handle.DrawCircle(center, radius, new Color(sea.R * 0.8f, sea.G * 0.8f, sea.B * 0.8f));

        DrawCells(handle, size, center, radius);
        DrawSeas(handle, center, radius);

        DrawZones(handle, center, radius);
        DrawHazards(handle, center, radius);

        if (ShowRoads)
            DrawRoads(handle, center, radius);

        DrawCaravans(handle, center, radius);
        DrawCities(handle, center, radius);

        // The edge of the planet.
        handle.DrawCircle(center, radius, new Color(0.6f, 0.8f, 1f, 0.5f), filled: false);
    }

    private void DrawStars(DrawingHandleScreen handle, Vector2i size)
    {
        if (_stars == null)
            return;

        foreach (var (x, y) in _stars)
        {
            var twinkle = 0.45f + 0.35f * MathF.Sin(_time * 1.3f + x * 40f);
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x * size.X, y * size.Y), new Vector2(1.6f, 1.6f)), new Color(0.85f, 0.9f, 1f, twinkle));
        }
    }

    private static Color Lit(Color color, Vector3 normal, int shade)
    {
        var light = 0.55f + 0.45f * Math.Clamp(Vector3.Dot(normal, LightDirection), 0f, 1f);
        var rim = 0.6f + 0.4f * MathF.Sqrt(Math.Clamp(normal.Z, 0f, 1f));

        // The light is cut into steps so that the cells of one biome fall into a few batches and not into one batch each.
        var factor = MathF.Round(light * rim * (1f + 0.05f * shade) * 20f) / 20f;
        return new Color(color.R * factor, color.G * factor, color.B * factor, color.A);
    }

    // The cells: filled (two triangles for a small cell, a fan from the middle for a wide one at the pole; one batch for each colour), and edged by thin lines. The batches are kept and drawn
    // again while the ball stands as it is.
    private void DrawCells(DrawingHandleScreen handle, Vector2i size, Vector2 center, float radius)
    {
        if (_cells == null)
            return;

        var key = (_yaw, _pitch, _zoom, size, ShowBiomes, ShowGrid);
        if (!_batched || key != _batchedFor)
        {
            _batchedFor = key;
            _batched = true;
            BuildBatches(size, center, radius);
        }

        foreach (var (color, batch) in _batches)
            Flush(handle, DrawPrimitiveTopology.TriangleList, batch, color);

        if (ShowGrid)
            Flush(handle, DrawPrimitiveTopology.LineList, _lines, new Color(0f, 0f, 0f, Math.Clamp(0.08f + 0.06f * _zoom, 0.1f, 0.4f)));

        // The cell under the cursor and the cell that was picked: edged.
        DrawCellEdge(handle, center, radius, _hover, new Color(1f, 1f, 1f, 0.8f));
        DrawCellEdge(handle, center, radius, _picked, CraftPalette.Gold);
    }

    private void BuildBatches(Vector2i size, Vector2 center, float radius)
    {
        foreach (var batch in _batches.Values)
            batch.Clear();

        _lines.Clear();

        foreach (var cell in _cells!)
        {
            // Not those on the far side.
            var normal = Rotate(cell.Center);
            if (normal.Z < -0.1f)
                continue;

            var middle = new Vector2(center.X + normal.X * radius, center.Y - normal.Y * radius);

            // Not those that are off the control (the wide ones at the poles are kept: their middle is not where they are).
            if (cell.Lon1 - cell.Lon0 < 10f && (middle.X < -30f || middle.Y < -30f || middle.X > size.X + 30f || middle.Y > size.Y + 30f))
                continue;

            _ring.Clear();
            foreach (var corner in cell.Ring)
                _ring.Add(Transform(corner, center, radius, out _));

            var color = ShowBiomes
                ? ColorOf(cell.Biome)
                : (WorldMapTerrain.IsSea(cell.Biome) ? ColorOf(Ocean) : new Color(0.42f, 0.5f, 0.38f));
            var lit = Lit(color, normal, ShowBiomes ? cell.Shade : 0);

            if (!_batches.TryGetValue(lit, out var batch))
                _batches[lit] = batch = new List<Vector2>();

            if (_ring.Count == 4)
            {
                batch.Add(_ring[0]);
                batch.Add(_ring[1]);
                batch.Add(_ring[2]);
                batch.Add(_ring[0]);
                batch.Add(_ring[2]);
                batch.Add(_ring[3]);
            }
            else
            {
                for (var i = 0; i < _ring.Count; i++)
                {
                    batch.Add(middle);
                    batch.Add(_ring[i]);
                    batch.Add(_ring[(i + 1) % _ring.Count]);
                }
            }

            if (ShowGrid)
            {
                for (var i = 0; i < _ring.Count; i++)
                {
                    _lines.Add(_ring[i]);
                    _lines.Add(_ring[(i + 1) % _ring.Count]);
                }
            }
        }
    }

    // A batch of primitives in pieces, because one that is too big is too big for the stack.
    private static void Flush(DrawingHandleScreen handle, DrawPrimitiveTopology topology, List<Vector2> vertices, Color color)
    {
        const int piece = 1500;

        for (var start = 0; start < vertices.Count; start += piece)
        {
            var count = Math.Min(piece, vertices.Count - start);
            handle.DrawPrimitives(topology, CollectionsMarshal.AsSpan(vertices).Slice(start, count), color);
        }
    }

    private void DrawCellEdge(DrawingHandleScreen handle, Vector2 center, float radius, MapSelection selection, Color color)
    {
        if (selection.Kind is not (SelectionKind.Cell or SelectionKind.City) || CellAt(selection.Latitude, selection.Longitude) is not { } cell)
            return;

        if (Rotate(cell.Center).Z < 0f)
            return;

        for (var i = 0; i < cell.Ring.Length; i++)
        {
            var a = Transform(cell.Ring[i], center, radius, out _);
            var b = Transform(cell.Ring[(i + 1) % cell.Ring.Length], center, radius, out _);
            handle.DrawLine(a, b, color);
            handle.DrawLine(a + new Vector2(0.7f, 0.7f), b + new Vector2(0.7f, 0.7f), color);
        }
    }

    // The roads: lines of the colour of the kind, a dark line under them, and the one that is under the cursor or picked drawn thicker and brighter. A road goes only where it is on the near
    // side of the ball.
    private void DrawRoads(DrawingHandleScreen handle, Vector2 center, float radius)
    {
        foreach (var road in _roads)
        {
            var active = (_hover.Kind == SelectionKind.Road && _hover.Id == road.Id) || (_picked.Kind == SelectionKind.Road && _picked.Id == road.Id);
            var color = road.Kind switch
            {
                WorldRoadKind.Road => new Color(0.82f, 0.62f, 0.32f),
                WorldRoadKind.Trail => new Color(0.7f, 0.55f, 0.4f),
                _ => new Color(0.55f, 0.8f, 1f),
            };

            if (road.Blocked)
                color = new Color(0.62f, 0.2f, 0.18f);

            if (active)
                color = new Color(1f, 0.92f, 0.55f);

            // The roads of a city that somebody has mended are drawn whole: a second, light line along them.
            var mended = road.Kind != WorldRoadKind.SeaRoute && (CityById(road.FromCity)?.RoadsRepaired == true || CityById(road.ToCity)?.RoadsRepaired == true);

            Vector2? before = null;
            var index = 0;
            foreach (var (lat, lon) in road.Points)
            {
                var point = Project(lat, lon, center, radius, out var depth);
                index++;

                if (depth < 0.02f)
                {
                    before = null;
                    continue;
                }

                // A sea route is dashed.
                var draw = (road.Kind != WorldRoadKind.SeaRoute || index % 4 < 3) && (!road.Blocked || index % 2 == 0);
                if (before is { } previous && draw)
                {
                    handle.DrawLine(previous + new Vector2(0f, 1f), point + new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.5f));
                    handle.DrawLine(previous, point, color);

                    if (mended)
                        handle.DrawLine(previous + new Vector2(0f, -1f), point + new Vector2(0f, -1f), new Color(1f, 0.95f, 0.8f, 0.8f));

                    if (active)
                    {
                        handle.DrawLine(previous + new Vector2(0f, -1f), point + new Vector2(0f, -1f), color);
                        handle.DrawLine(previous + new Vector2(1f, 0f), point + new Vector2(1f, 0f), color);
                    }
                }

                before = point;
            }
        }
    }

    // The colour of a faction: the same for the same name.
    private static Color ZoneColor(string owner)
    {
        var hash = 17u;
        foreach (var c in owner)
            hash = hash * 31u + c;

        return Color.FromHsv(new Vector4((hash % 360u) / 360f, 0.65f, 0.95f, 1f));
    }

    // The regions of interest: a circle on the ball round the place (the line of the faction's colour; thicker if it is the one of the console), with the name of the faction.
    private void DrawZones(DrawingHandleScreen handle, Vector2 center, float radius)
    {
        const int steps = 56;

        foreach (var zone in _zones)
        {
            var color = ZoneColor(zone.Owner);
            var phi = zone.Latitude * Degrees;
            var lambda = zone.Longitude * Degrees;
            var delta = zone.Radius * Degrees;

            Vector2? before = null;
            for (var i = 0; i <= steps; i++)
            {
                var theta = i * MathF.PI * 2f / steps;
                var lat = MathF.Asin(MathF.Sin(phi) * MathF.Cos(delta) + MathF.Cos(phi) * MathF.Sin(delta) * MathF.Cos(theta));
                var lon = lambda + MathF.Atan2(MathF.Sin(theta) * MathF.Sin(delta) * MathF.Cos(phi), MathF.Cos(delta) - MathF.Sin(phi) * MathF.Sin(lat));

                var point = Project(lat / Degrees, lon / Degrees, center, radius, out var depth);
                if (depth < 0.02f)
                {
                    before = null;
                    continue;
                }

                if (before is { } previous)
                {
                    handle.DrawLine(previous, point, color);

                    if (zone.Mine)
                    {
                        handle.DrawLine(previous + new Vector2(1f, 0f), point + new Vector2(1f, 0f), color);
                        handle.DrawLine(previous + new Vector2(0f, 1f), point + new Vector2(0f, 1f), color);
                    }
                }

                before = point;
            }

            if (_zoom < 0.9f)
                continue;

            var middle = Project(zone.Latitude, zone.Longitude, center, radius, out var middleDepth);
            if (middleDepth < 0.3f)
                continue;

            var width = handle.GetDimensions(_small, zone.Owner, 1f).X;
            handle.DrawString(_small, middle - new Vector2(width / 2f, 6f), zone.Owner, color.WithAlpha(0.9f));
        }
    }

    // The hazards: a storm is a bluish circle, a threat a red one that beats; the name is at the middle.
    private void DrawHazards(DrawingHandleScreen handle, Vector2 center, float radius)
    {
        const int steps = 48;
        var pulse = 0.5f + 0.5f * MathF.Sin(_time * 4f);

        foreach (var hazard in _hazards)
        {
            var color = hazard.Weather ? new Color(0.65f, 0.8f, 1f) : new Color(1f, 0.3f + 0.2f * pulse, 0.25f);
            var phi = hazard.Latitude * Degrees;
            var lambda = hazard.Longitude * Degrees;
            var delta = hazard.Radius * Degrees;

            Vector2? before = null;
            for (var i = 0; i <= steps; i++)
            {
                var theta = i * MathF.PI * 2f / steps;
                var lat = MathF.Asin(MathF.Sin(phi) * MathF.Cos(delta) + MathF.Cos(phi) * MathF.Sin(delta) * MathF.Cos(theta));
                var lon = lambda + MathF.Atan2(MathF.Sin(theta) * MathF.Sin(delta) * MathF.Cos(phi), MathF.Cos(delta) - MathF.Sin(phi) * MathF.Sin(lat));

                var point = Project(lat / Degrees, lon / Degrees, center, radius, out var depth);
                if (depth < 0.02f)
                {
                    before = null;
                    continue;
                }

                if (before is { } previous)
                {
                    handle.DrawLine(previous, point, color);
                    handle.DrawLine(previous + new Vector2(0f, 1f), point + new Vector2(0f, 1f), color);
                    handle.DrawLine(previous + new Vector2(1f, 0f), point + new Vector2(1f, 0f), color);
                }

                before = point;
            }

            var middle = Project(hazard.Latitude, hazard.Longitude, center, radius, out var middleDepth);
            if (middleDepth < 0.15f)
                continue;

            // A red mark at the middle, and the name.
            handle.DrawCircle(middle, 5f + pulse * 3f, color.WithAlpha(0.35f));
            handle.DrawCircle(middle, 3f, color);

            var width = handle.GetDimensions(_small, hazard.Name, 1f).X;
            handle.DrawString(_small, middle + new Vector2(-width / 2f, 8f), hazard.Name, color.WithAlpha(0.95f));
        }
    }

    // The caravans on the roads: a diamond of the colour of the faction (a ring round it if it has an escort), and the name of the faction when the ball is near.
    private void DrawCaravans(DrawingHandleScreen handle, Vector2 center, float radius)
    {
        foreach (var caravan in _caravans)
        {
            var point = Project(caravan.Latitude, caravan.Longitude, center, radius, out var depth);
            if (depth < 0.05f)
                continue;

            var color = ZoneColor(caravan.Faction);
            var size = 4f + (caravan.Mine ? 1.5f : 0f);

            handle.DrawRect(UIBox2.FromDimensions(point - new Vector2(size + 1f, size + 1f), new Vector2(size * 2f + 2f, size * 2f + 2f)), new Color(0f, 0f, 0f, 0.85f));
            handle.DrawRect(UIBox2.FromDimensions(point - new Vector2(size, size), new Vector2(size * 2f, size * 2f)), color);

            if (caravan.Escorted)
                handle.DrawCircle(point, size + 4f, Color.White.WithAlpha(0.85f), filled: false);

            if (_zoom < 1.1f)
                continue;

            var label = caravan.Faction + " → " + caravan.Target;
            handle.DrawString(_small, point + new Vector2(size + 5f, 2f) + new Vector2(1f, 1f), label, new Color(0f, 0f, 0f, 0.9f));
            handle.DrawString(_small, point + new Vector2(size + 5f, 2f), label, color);
        }
    }

    private void DrawSeas(DrawingHandleScreen handle, Vector2 center, float radius)
    {
        // The names of the seas, faint, when the ball is near enough to read them.
        if (_zoom < 0.95f)
            return;

        foreach (var (lat, lon, key) in _seas)
        {
            var point = Project(lat, lon, center, radius, out var depth);
            if (depth < 0.35f)
                continue;

            var text = Loc.GetString(key);
            var width = handle.GetDimensions(_large, text, 1f).X;
            var alpha = Math.Clamp(depth, 0f, 1f) * 0.55f;
            handle.DrawString(_large, point - new Vector2(width / 2f, 9f), text, new Color(0.8f, 0.9f, 1f, alpha));
        }
    }

    private void DrawCities(DrawingHandleScreen handle, Vector2 center, float radius)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin(_time * 3f);

        foreach (var city in _cities)
        {
            var point = Project(city.Latitude, city.Longitude, center, radius, out var depth);
            if (depth < 0.08f)
                continue;

            var selected = _picked.Kind == SelectionKind.City && _picked.Id == city.Id;
            var hover = _hover.Kind == SelectionKind.City && _hover.Id == city.Id;
            var size = 3.5f + Math.Min(_zoom, 2.5f) * 1.5f;

            // A city in trouble has a red ring that beats.
            if (city.CrisisSeconds > 0)
            {
                handle.DrawCircle(point, size + 6f + pulse * 3f, new Color(1f, 0.25f, 0.2f, 0.28f));
                handle.DrawCircle(point, size + 5f, new Color(1f, 0.35f, 0.3f), filled: false);
            }

            if (selected)
            {
                handle.DrawCircle(point, size + 5f + pulse * 2f, CraftPalette.Gold.WithAlpha(0.30f));
                handle.DrawCircle(point, size + 3f, CraftPalette.Gold, filled: false);
            }

            // A mark in the manner of a settlement: a dark square with a square of the colour of the city in it. The other objects of the map (the sites) are round and smaller.
            if (city.Site)
            {
                var small = size * 0.72f;
                handle.DrawCircle(point, small + 1.5f, new Color(0f, 0f, 0f, 0.8f));
                handle.DrawCircle(point, small, hover ? Color.White : city.Color);
            }
            else
            {
                handle.DrawRect(UIBox2.FromDimensions(point - new Vector2(size + 1f, size + 1f), new Vector2(size * 2f + 2f, size * 2f + 2f)), new Color(0f, 0f, 0f, 0.8f));
                handle.DrawRect(UIBox2.FromDimensions(point - new Vector2(size, size), new Vector2(size * 2f, size * 2f)), hover ? Color.White : city.Color);
            }

            // The name, when it can be read (the sites are named only when the map is nearer).
            if (_zoom >= (city.Site ? 1.7f : 1.15f) || hover || selected)
            {
                var at = point + new Vector2(size + 4f, -6f);
                handle.DrawString(_small, at + new Vector2(1f, 1f), city.Name, new Color(0f, 0f, 0f, 0.9f));
                handle.DrawString(_small, at, city.Name, Color.White);
            }
        }
    }

    #endregion

    #region The mouse

    // What is under a point of the screen: a city first, then a road, then a cell.
    private MapSelection SelectionAt(Vector2 pixel)
    {
        var (center, radius) = Frame();

        foreach (var city in _cities)
        {
            var point = Project(city.Latitude, city.Longitude, center, radius, out var depth);
            if (depth >= 0.08f && (point - pixel).Length() < 12f)
                return new MapSelection(SelectionKind.City, city.Id, city.Latitude, city.Longitude);
        }

        if (ShowRoads)
        {
            string? best = null;
            var bestDistance = 6f;
            var bestPlace = (Lat: 0f, Lon: 0f);

            foreach (var road in _roads)
            {
                foreach (var (lat, lon) in road.Points)
                {
                    var point = Project(lat, lon, center, radius, out var depth);
                    if (depth < 0.05f)
                        continue;

                    var distance = (point - pixel).Length();
                    if (distance >= bestDistance)
                        continue;

                    best = road.Id;
                    bestDistance = distance;
                    bestPlace = (lat, lon);
                }
            }

            if (best != null)
                return new MapSelection(SelectionKind.Road, best, bestPlace.Lat, bestPlace.Lon);
        }

        if (Unproject(pixel, out var latitude, out var longitude) && CellAt(latitude, longitude) is { } cell)
            return new MapSelection(SelectionKind.Cell, cell.Biome, cell.LatC, cell.LonC);

        return MapSelection.None;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = true;
        _dragTravel = 0f;
        _lastMouse = _ui.MousePositionScaled.Position;
        _focusYaw = null;
        _focusPitch = null;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || !_dragging)
            return;

        _dragging = false;

        // A click that did not drag the ball picks what is under it.
        if (_dragTravel <= 4f)
        {
            UpdateTurning();
            _picked = SelectionAt(args.RelativePixelPosition);
            OnPicked?.Invoke(_picked);
        }

        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        _mouseIn = true;
        _mousePixel = args.RelativePixelPosition;
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        _mouseIn = false;
        if (_hover == MapSelection.None)
            return;

        _hover = MapSelection.None;
        OnHovered?.Invoke(MapSelection.None);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        ZoomBy(args.Delta.Y > 0f ? 1.15f : 1f / 1.15f);
        args.Handle();
    }

    #endregion
}
