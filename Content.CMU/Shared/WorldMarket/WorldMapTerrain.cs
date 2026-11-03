// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using System.Numerics;

namespace Content.Shared.CMU14.WorldMarket;

// The land of the world map, made from a number (the seed of the round) and nothing else, so that the server and every client make the same one. The pieces of land are set at random, the
// height, the moisture and the warmth of every place come of a noise (so that the coasts are ragged, there are islands and bays, and the mountains lie in ranges), and the biome is what they
// give. The server uses it to put the cities where they are on the land, the client to draw it.
public sealed class WorldMapTerrain
{
    public const string Ocean = "RUMCBiomeOcean";
    public const string Shallows = "RUMCBiomeShallows";
    public const string Ice = "RUMCBiomeIce";
    public const string Tundra = "RUMCBiomeTundra";
    public const string Taiga = "RUMCBiomeTaiga";
    public const string Forest = "RUMCBiomeForest";
    public const string Plains = "RUMCBiomePlains";
    public const string Desert = "RUMCBiomeDesert";
    public const string Savanna = "RUMCBiomeSavanna";
    public const string Jungle = "RUMCBiomeJungle";
    public const string Mountains = "RUMCBiomeMountains";
    public const string Swamp = "RUMCBiomeSwamp";

    private const float Degrees = MathF.PI / 180f;

    private readonly int _noise;
    private readonly (Vector3 Center, float Radius)[] _pieces;

    // Places that are land whatever the noise says (the client puts them where the server put the cities, so that a city can never be in the sea if the two disagree by a hair).
    private readonly Vector3[] _landmarks;

    public WorldMapTerrain(int seed, IReadOnlyList<Vector3>? landmarks = null)
    {
        _noise = seed & 0xFFFF;
        _landmarks = landmarks?.ToArray() ?? Array.Empty<Vector3>();

        var rng = new Rng(seed);
        var pieces = new List<(Vector3, float)>();

        // Eighteen to twenty-four pieces of land (the world has about twice the land that it had), not on one another.
        var count = 18 + (int) (rng.Next() * 7f);
        for (var tries = 0; pieces.Count < count && tries < 1500; tries++)
        {
            var latitude = MathF.Asin(-0.8f + 1.6f * rng.Next()) / Degrees;
            var longitude = -180f + 360f * rng.Next();
            var radius = 11f + 19f * rng.Next();
            var center = ToVector(latitude, longitude);

            var overlaps = false;
            foreach (var (other, otherRadius) in pieces)
            {
                if (AngleBetween(center, other) < (radius + otherRadius) * 0.45f)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
                pieces.Add((center, radius));
        }

        // The ice at the poles, now and then.
        if (rng.Next() < 0.7f)
            pieces.Add((ToVector(-80f, -180f + 360f * rng.Next()), 12f));

        if (rng.Next() < 0.5f)
            pieces.Add((ToVector(80f, -180f + 360f * rng.Next()), 10f));

        _pieces = pieces.ToArray();
    }

    public static Vector3 ToVector(float latitude, float longitude)
    {
        var phi = latitude * Degrees;
        var lambda = longitude * Degrees;
        return new Vector3(MathF.Cos(phi) * MathF.Sin(lambda), MathF.Sin(phi), MathF.Cos(phi) * MathF.Cos(lambda));
    }

    public static float AngleBetween(Vector3 a, Vector3 b)
    {
        return MathF.Acos(Math.Clamp(Vector3.Dot(a, b), -1f, 1f)) / Degrees;
    }

    public static bool IsSea(string biome) => biome is Ocean or Shallows;

    // A small generator of numbers that is the same everywhere (the seeded one of the system is not promised to be).
    private sealed class Rng(int seed)
    {
        private uint _state = (uint) ((seed * 2654435761L + 12345L) & 0xFFFFFFFFL);

        public float Next()
        {
            unchecked
            {
                _state = _state * 1664525u + 1013904223u;
            }

            return (_state >> 8) / 16777216f;
        }
    }

    #region The noise

    private static float Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            var h = (uint) (x * 374761393 + y * 668265263 + z * 2147483647 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 8388607f - 1f;
        }
    }

    // A smooth noise over the space, from minus one to one.
    private static float Noise(float x, float y, float z, int seed)
    {
        var x0 = (int) MathF.Floor(x);
        var y0 = (int) MathF.Floor(y);
        var z0 = (int) MathF.Floor(z);
        var fx = x - x0;
        var fy = y - y0;
        var fz = z - z0;
        var ux = fx * fx * (3f - 2f * fx);
        var uy = fy * fy * (3f - 2f * fy);
        var uz = fz * fz * (3f - 2f * fz);

        float C(int a, int b, int c) => Hash(x0 + a, y0 + b, z0 + c, seed);

        var x00 = C(0, 0, 0) + (C(1, 0, 0) - C(0, 0, 0)) * ux;
        var x10 = C(0, 1, 0) + (C(1, 1, 0) - C(0, 1, 0)) * ux;
        var x01 = C(0, 0, 1) + (C(1, 0, 1) - C(0, 0, 1)) * ux;
        var x11 = C(0, 1, 1) + (C(1, 1, 1) - C(0, 1, 1)) * ux;
        var y0v = x00 + (x10 - x00) * uy;
        var y1v = x01 + (x11 - x01) * uy;
        return y0v + (y1v - y0v) * uz;
    }

    // Noises of many sizes laid on one another: the large ones are the shapes of the continents, the small ones the ragged edges.
    private static float Fbm(Vector3 p, int seed)
    {
        var sum = 0f;
        var amplitude = 1f;
        var norm = 0f;
        var frequency = 1f;

        for (var i = 0; i < 5; i++)
        {
            sum += amplitude * Noise(p.X * frequency, p.Y * frequency, p.Z * frequency, seed + i * 31);
            norm += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.03f;
        }

        return sum / norm;
    }

    #endregion

    // The height of a place of the ball (a point of one): above nothing it is land. The place that it was warped to is given as well, the other noises are taken from it.
    public float Height(Vector3 p, out Vector3 warped)
    {
        // How deep into a piece of land the place is: nothing at the edge, one in the middle.
        var mask = 0f;
        foreach (var (center, radius) in _pieces)
            mask = MathF.Max(mask, 1f - AngleBetween(p, center) / (radius * 1.25f));

        foreach (var landmark in _landmarks)
            mask = MathF.Max(mask, 1f - AngleBetween(p, landmark) / 5.5f);

        // The place is warped a little, so that the edges wind.
        const float warp = 0.35f;
        var shift = _noise * 7;
        warped = new Vector3(
            p.X + warp * Noise(p.X * 1.7f + 3f, p.Y * 1.7f, p.Z * 1.7f, 101 + shift),
            p.Y + warp * Noise(p.X * 1.7f, p.Y * 1.7f + 5f, p.Z * 1.7f, 102 + shift),
            p.Z + warp * Noise(p.X * 1.7f, p.Y * 1.7f, p.Z * 1.7f + 7f, 103 + shift));

        var shape = Math.Clamp(Fbm(warped * 2.4f, 7 + shift) * 1.7f, -1f, 1f);
        return 1.35f * MathF.Max(mask, 0f) + 0.6f * shape - 0.46f;
    }

    public float Height(Vector3 p) => Height(p, out _);

    public string Classify(float latitude, float longitude)
    {
        return Classify(latitude, ToVector(latitude, longitude), out _);
    }

    // The biome of a place: the sea (deep, or shallow near the land) or, on the land, what the warmth, the moisture and the height give. The height is given too.
    public string Classify(float latitude, Vector3 p, out float height)
    {
        height = Height(p, out var warped);
        if (height <= 0f)
            return height > -0.22f ? Shallows : Ocean;

        var shift = _noise * 7;
        var absolute = MathF.Abs(latitude);

        var ridge = 1f - MathF.Abs(Math.Clamp(Fbm(warped * 3.1f, 19 + shift) * 1.7f, -1f, 1f));
        if (height > 0.12f && ridge > 0.86f)
            return Mountains;

        // The moisture: more near the coast, less in the dry belt of the subtropics.
        var moisture = 0.5f + 0.5f * Math.Clamp(Fbm(p * 1.8f, 31 + shift) * 1.7f, -1f, 1f);
        if (height < 0.35f)
            moisture += 0.1f;

        var belt = (absolute - 26f) / 9f;
        moisture -= 0.25f * MathF.Exp(-belt * belt);

        var warmth = 1f - absolute / 90f + 0.11f * Noise(p.X * 3.2f, p.Y * 3.2f, p.Z * 3.2f, 41 + shift);

        if (warmth < 0.22f)
            return Ice;

        if (warmth < 0.36f)
            return moisture > 0.52f ? Taiga : Tundra;

        if (warmth < 0.56f)
            return moisture > 0.6f ? Forest : Plains;

        if (warmth < 0.76f)
        {
            if (moisture < 0.3f)
                return Desert;

            if (moisture > 0.78f)
                return Swamp;

            return moisture > 0.52f ? Forest : Plains;
        }

        if (moisture > 0.55f)
            return Jungle;

        return moisture < 0.3f ? Desert : Savanna;
    }

    // What a road between two places really is: if the whole way is land it is what it was meant to be (a sea route that goes over land is a road), if any of it is sea it is a sea route.
    public WorldRoadKind KindOfRoad(WorldRoadKind intended, Vector3 a, Vector3 b)
    {
        var angle = MathF.Acos(Math.Clamp(Vector3.Dot(a, b), -1f, 1f));
        var steps = Math.Max(2, (int) MathF.Ceiling(angle / (2f * Degrees)));
        var sin = MathF.Sin(angle);

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float) steps;
            var point = sin < 0.0001f ? a : (MathF.Sin((1f - t) * angle) * a + MathF.Sin(t * angle) * b) / sin;
            if (Height(point) <= 0f)
                return WorldRoadKind.SeaRoute;
        }

        return intended == WorldRoadKind.SeaRoute ? WorldRoadKind.Road : intended;
    }

    // Puts the cities on the land: a port on a coast, any other inland, none on a mountain or on the ice, and as far from one another as the land allows.
    public Dictionary<string, (float Latitude, float Longitude)> PlaceCities(IReadOnlyList<(string Id, bool Coastal)> cities)
    {
        var rng = new Rng(_noise * 31 + 17);
        var coast = new List<(float Lat, float Lon)>();
        var inland = new List<(float Lat, float Lon)>();

        for (var latitude = -64f; latitude <= 64f; latitude += 4f)
        {
            var step = 4f / MathF.Max(MathF.Cos(latitude * Degrees), 0.2f);
            for (var longitude = -180f; longitude < 180f; longitude += step)
            {
                var lat = latitude + (rng.Next() - 0.5f) * 3f;
                var lon = longitude + (rng.Next() - 0.5f) * 3f;
                var p = ToVector(lat, lon);

                var biome = Classify(lat, p, out var height);
                if (IsSea(biome) || biome is Mountains or Ice or Swamp || height < 0.12f)
                    continue;

                // The sea within six degrees of it, to the four sides: some of it is a coast, none of it is inland.
                var sea = 0;
                foreach (var (dLat, dLon) in new[] { (6f, 0f), (-6f, 0f), (0f, 6f), (0f, -6f) })
                {
                    if (Height(ToVector(lat + dLat, lon + dLon / MathF.Max(MathF.Cos(lat * Degrees), 0.2f))) <= 0f)
                        sea++;
                }

                if (sea > 0)
                    coast.Add((lat, lon));
                else
                    inland.Add((lat, lon));
            }
        }

        Shuffle(coast, rng);
        Shuffle(inland, rng);

        var placed = new Dictionary<string, (float, float)>();
        var all = new List<(float Lat, float Lon)>(coast);
        all.AddRange(inland);

        foreach (var (id, isCoastal) in cities)
        {
            var pools = isCoastal ? new[] { coast, all } : new[] { inland, all };
            (float, float)? chosen = null;

            // As far from the others as is asked, and less and less if the land is too small for it.
            foreach (var separation in new[] { 13f, 8f, 4f, 0f })
            {
                foreach (var pool in pools)
                {
                    foreach (var candidate in pool)
                    {
                        var p = ToVector(candidate.Lat, candidate.Lon);
                        var free = true;
                        foreach (var (otherLat, otherLon) in placed.Values)
                        {
                            if (AngleBetween(p, ToVector(otherLat, otherLon)) < separation)
                            {
                                free = false;
                                break;
                            }
                        }

                        if (!free)
                            continue;

                        chosen = candidate;
                        break;
                    }

                    if (chosen != null)
                        break;
                }

                if (chosen != null)
                    break;
            }

            placed[id] = chosen ?? (0f, 0f);
        }

        return placed;
    }

    private static void Shuffle<T>(List<T> list, Rng rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = (int) (rng.Next() * (i + 1));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
