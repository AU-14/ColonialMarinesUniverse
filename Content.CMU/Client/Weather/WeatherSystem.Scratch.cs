using Robust.Shared.Map;

namespace Content.Client.Weather;

public sealed partial class WeatherSystem
{
    private readonly Queue<TileRef> _cmuWeatherFrontier = new();
    private readonly HashSet<Vector2i> _cmuWeatherVisited = new();
}
