using System.Numerics;
using Content.Shared.CMU14.Lobby;

namespace Content.Client.CMU14.Lobby;

/// <summary>Screen-space choreography. Positions scale with the window; timing never depends on frame rate.</summary>
public static partial class LobbyPartyChoreography
{
    public const float FlybyDuration = 28;
    public const float ParadeDuration = 31;
    public const float PassDuration = 6;
    public const int PassCount = 3;
    public const int ShotsPerPass = 24;
    public const float MissileFlight = 1.1f;

    public readonly record struct FlightPose(Vector2 Position, float Rotation, float Pitch, float Bank, float Altitude)
    {
        public Vector2 Heading => new(MathF.Sin(Rotation), -MathF.Cos(Rotation));
    }

    public readonly record struct MarchPose(Vector2 Position, float Rotation, float Height = 0, bool Backwards = false);

    public static float Duration(LobbyPartyShow show, bool reduced) => reduced ? 5 :
        show switch
        {
            LobbyPartyShow.Flyby => FlybyDuration,
            LobbyPartyShow.SupplyScramble => SupplyDuration,
            _ => ParadeDuration,
        };

    public static float PassStart(int pass) => 2.5f + pass * 7.2f;
    public static float ShotTime(int pass, int shot) => PassStart(pass) + 1.9f + shot * 0.042f;
    public static float MissileTime(int missile) => PassStart(2) + 2.1f + missile * 0.7f;
    public static float ThreatTime => PassStart(1) + 3;
    public static float FlareTime => ThreatTime + 1.1f;

    /// <summary>The attack leg points at the target; a continuous tangent carries the nose into the pull-out.</summary>
    public static FlightPose Jet(float elapsed, int pass, Vector2 size, Vector2? target = null)
    {
        var t = Math.Clamp((elapsed - PassStart(pass)) / PassDuration, 0, 1);
        var sign = pass % 2 == 0 ? 1 : -1;
        var focus = target ?? new Vector2(sign > 0 ? 0.395f : 0.61f, 0.7f);
        var entry = new Vector2(sign > 0 ? -0.3f : 1.3f, focus.Y - 0.28f);
        var exit = new Vector2(sign > 0 ? 1.3f : -0.3f, focus.Y - 0.32f);
        var diveStart = focus - new Vector2(sign * 0.32f, 0.20f);
        var diveEnd = focus - new Vector2(sign * 0.12f, 0.075f);
        var course = diveEnd - diveStart;
        Vector2 position;
        Vector2 tangent;
        if (t < 0.24f)
        {
            var a = new Vector2(sign > 0 ? -0.08f : 1.08f, focus.Y - 0.26f);
            var b = diveStart - course * 0.35f;
            position = Curve(entry, a, b, diveStart, t / 0.24f);
            tangent = Tangent(entry, a, b, diveStart, t / 0.24f);
        }
        else if (t < 0.56f)
        {
            position = Vector2.Lerp(diveStart, diveEnd, (t - 0.24f) / 0.32f);
            tangent = course;
        }
        else
        {
            var a = diveEnd + course * 0.65f;
            var b = new Vector2(sign > 0 ? 1.1f : -0.1f, focus.Y - 0.04f);
            position = Curve(diveEnd, a, b, exit, (t - 0.56f) / 0.44f);
            tangent = Tangent(diveEnd, a, b, exit, (t - 0.56f) / 0.44f);
        }
        tangent *= size;
        var dive = Window(t, 0.08f, 0.62f, 0.16f) * 0.5f;
        var bank = sign * Window(t, 0.54f, 0.97f, 0.15f) * 0.75f;
        return new FlightPose(position * size, MathF.Atan2(tangent.Y, tangent.X) + MathF.PI / 2,
            dive, bank, 1 - dive * 0.75f);
    }

    public static Vector2 Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        var u = 1 - t;
        return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
    }

    private static Vector2 Tangent(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t) =>
        3 * (1 - t) * (1 - t) * (b - a) + 6 * (1 - t) * t * (c - b) + 3 * t * t * (d - c);

    public static float Window(float t, float start, float end, float fade) =>
        LobbyLineupChoreography.Smooth(Math.Clamp((t - start) / fade, 0, 1)) *
        LobbyLineupChoreography.Smooth(Math.Clamp((end - t) / fade, 0, 1));

    private static int Rows(int count) => Math.Clamp((int) MathF.Ceiling(MathF.Sqrt(Math.Max(1, count) / 2.5f)), 1, 8);
    public static float ActorScale(int count) => Rows(count) > 6 ? 1.4f : 1.75f;

    private static int BattleColumns(int count, Vector2 size) => Math.Clamp(
        (int) MathF.Ceiling(MathF.Sqrt(Math.Max(1, count) * size.X / Math.Max(1, size.Y) * 0.7f)), 1, Math.Max(1, count));

    public static float FlybyActorScale(int count, Vector2 size)
    {
        var columns = BattleColumns(count, size);
        var rows = (Math.Max(1, count) + columns - 1) / columns;
        return Math.Min(1.65f * Math.Clamp(size.Y / 900, 0.55f, 1.4f),
            Math.Min(size.X * 0.88f / (columns * 42), size.Y * 0.61f / (rows * 50)));
    }

    /// <summary>Seeded combat positions fill the battlefield, including wide and crowded lobbies.</summary>
    public static Vector2 BattlePosition(int index, int count, Vector2 size, int seed)
    {
        var columns = BattleColumns(count, size);
        var rows = (Math.Max(1, count) + columns - 1) / columns;
        var jitter = new Vector2(MathF.Sin(index * 13.7f + (uint) seed % 97),
            MathF.Sin(index * 7.1f + (uint) seed % 61)) * 0.20f;
        return new Vector2(0.06f + (index % columns + 0.5f + jitter.X) / columns * 0.88f,
            0.22f + (index / columns + 0.5f + jitter.Y) / rows * 0.61f) * size;
    }

    public static MarchPose FlybyCrew(float elapsed, int index, int count, Vector2 size,
        float hitAt, bool reduced, int seed)
    {
        var position = BattlePosition(index, count, size, seed);
        if (reduced)
            return new MarchPose(position, 0);
        var unit = Math.Clamp(size.Y / 900, 0.55f, 1.4f);
        var scale = FlybyActorScale(count, size);
        var side = index % 2 == 0 ? 1 : -1;
        var entrance = LobbyLineupChoreography.Smooth(Math.Clamp((elapsed - index % 5 * 0.04f) / 1.2f, 0, 1));
        var entryX = side > 0 ? -60 * unit : size.X + 60 * unit;
        position.X = entryX + (position.X - entryX) * entrance;

        var age = elapsed - hitAt - index % 7 * 0.035f;
        var rotation = 0f;
        var height = 0f;
        if (age is >= 0 and <= 1.6f)
        {
            var t = age / 1.6f;
            var arc = MathF.Sin(t * MathF.PI);
            position.X -= side * arc * 22 * scale;
            height = arc * 32 * scale;
            rotation = ((uint) seed + (uint) index) % 3 == 0
                ? side * MathF.Tau * LobbyLineupChoreography.Smooth(t)
                : side * arc * 1.3f;
        }
        else
        {
            // Staggered advances and retreats keep every part of the scene moving between air passes.
            var cycle = (elapsed + index * 0.41f) % 7.5f;
            var advance = Window(cycle, 1.7f, 5.6f, 0.9f);
            position.X += side * advance * Math.Min(42 * unit, size.X / BattleColumns(count, size) * 0.20f);
            position.Y += MathF.Sin(index * 5) * advance * 12 * scale;
            height = MathF.Abs(MathF.Sin(elapsed * 13 + index)) * advance * 3 * scale;
            rotation = MathF.Sin(elapsed * 13 + index) * advance * 0.08f;
            // Duck when the low pass reaches this part of the field.
            for (var pass = 0; pass < PassCount; pass++)
            {
                var duck = Window(elapsed - index % 11 * 0.04f, PassStart(pass) + 2, PassStart(pass) + 3.5f, 0.4f);
                rotation += side * duck * 0.35f;
            }
        }
        position.Y -= height;
        return new MarchPose(position, rotation, height, side < 0);
    }

    public static MarchPose March(float elapsed, int index, int count, Vector2 size, bool reduced, int seed)
    {
        var rows = Rows(count);
        var columns = (Math.Max(1, count) + rows - 1) / rows;
        var column = index / rows;
        var row = index % rows;
        var delay = columns <= 1 ? 0 : column / (float) (columns - 1) * 12;
        var t = (elapsed - delay - 1) / 16;
        var unit = Math.Clamp(size.Y / 900, 0.55f, 1.4f);
        var spacing = Math.Min(57 * unit, size.Y * 0.30f / rows);
        var position = new Vector2(-120 * unit + t * (size.X + 240 * unit), size.Y * 0.45f + row * spacing);
        if (reduced)
            return new MarchPose(new Vector2(size.X * (0.12f + 0.76f * (column + 0.5f) / columns), position.Y), 0);

        var beat = elapsed * 9.5f + column * 0.35f;
        var height = MathF.Abs(MathF.Sin(beat)) * 5 * unit;
        var rotation = MathF.Sin(beat) * 0.08f;
        var backwards = false;
        // Everyone keeps advancing, including the show-offs, latecomers and temporary truck passengers.
        var stunt = Math.Clamp((t - 0.32f) / 0.25f, 0, 1);
        var arc = MathF.Sin(stunt * MathF.PI);
        switch ((int) (((uint) seed + (uint) index) % 9))
        {
            case 0:
                rotation -= MathF.Tau * LobbyLineupChoreography.Smooth(stunt);
                height += arc * 78 * unit;
                break;
            case 1:
                rotation += MathF.Tau * 2 * LobbyLineupChoreography.Smooth(stunt);
                height += arc * 25 * unit;
                break;
            case 2:
                backwards = stunt is > 0.05f and < 0.8f;
                position.X -= arc * 100 * unit;
                rotation -= arc * 0.22f;
                break;
            case 3:
                rotation += Window(stunt, 0, 1, 0.22f) * MathF.PI / 2;
                position.X += MathF.Sin(stunt * MathF.Tau) * 22 * unit;
                height *= 1 - arc;
                break;
            case 4:
                height += MathF.Abs(MathF.Sin(stunt * MathF.PI * 3)) * 43 * unit;
                break;
            case 5:
                var ride = Window(elapsed, 12, 19, 1.2f) * Window(t, 0.15f, 0.9f, 0.12f);
                position = Vector2.Lerp(position, Vehicle(elapsed, 2, size, false).Position + new Vector2(0, -46 * unit), ride);
                height += MathF.Sin(ride * MathF.PI) * 90 * unit;
                break;
            case 6:
                rotation -= arc * 0.9f;
                position.X -= arc * 18 * unit;
                break;
            case 7:
                rotation += MathF.Sin(stunt * MathF.Tau * 3) * arc * 0.65f;
                height += arc * 16 * unit;
                break;
            case 8:
                height += arc * 95 * unit;
                rotation += MathF.Sin(stunt * MathF.Tau) * 0.65f;
                break;
        }
        position.Y -= height;
        return new MarchPose(position, rotation, height, backwards);
    }

    public static (Vector2 Position, float Rotation) Vehicle(float elapsed, int index, Vector2 size, bool reduced)
    {
        var t = (elapsed - index * 3.5f - 0.4f) / 18;
        var unit = Math.Clamp(size.Y / 900, 0.55f, 1.4f);
        var x = reduced ? size.X * (0.22f + index * 0.28f) : -220 * unit + t * (size.X + 440 * unit);
        var bump = reduced ? 0 : MathF.Sin(Math.Clamp((t - 0.46f) / 0.1f, 0, 1) * MathF.PI);
        return (new Vector2(x, size.Y * (index == 1 ? 0.32f : 0.85f) - bump * 13 * unit), -bump * 0.07f);
    }
}
