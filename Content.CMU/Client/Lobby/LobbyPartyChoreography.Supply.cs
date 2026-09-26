using System.Numerics;

namespace Content.Client.CMU14.Lobby;

public static partial class LobbyPartyChoreography
{
    public const float SupplyDuration = 28;
    public const int SupplyCrates = 12;
    public const float SupplyFlight = 0.85f;
    public static float SupplyImpact(int crate) => 5 + crate * 0.9f;
    public static int SupplyTarget(int crate, int count, int seed) =>
        (int) (((uint) seed + (uint) crate) % (uint) Math.Max(1, count));

    public static Vector2 SupplyCart(float elapsed, int cart, Vector2 size, bool reduced)
    {
        var progress = (elapsed - 1 - cart * 2.8f) / 17;
        var x = reduced ? size.X * (0.2f + cart * 0.3f) : -150 + progress * (size.X + 300);
        if (cart == 1 && !reduced)
            x = size.X - x;
        return new Vector2(x, size.Y * (0.30f + cart * 0.25f));
    }

    public static MarchPose SupplyCrew(float elapsed, int index, int count, Vector2 size, bool reduced, int seed)
    {
        var station = BattlePosition(index, count, size, seed);
        if (reduced)
            return new MarchPose(station, 0);
        var scale = FlybyActorScale(count, size);
        var entrance = LobbyLineupChoreography.Smooth((elapsed - index % 5 * 0.08f) / 2.5f);
        var position = Vector2.Lerp(new Vector2(-90 - index % 5 * 25, station.Y), station, entrance);
        var rotation = 0f;
        var height = MathF.Abs(MathF.Sin(elapsed * 10 + index)) * 3 * scale;
        var backwards = index % 2 == 1;
        for (var crate = 0; crate < SupplyCrates; crate++)
        {
            if (SupplyTarget(crate, count, seed) != index)
                continue;
            var age = elapsed - SupplyImpact(crate);
            if (age is < 0 or > 2.2f)
                continue;
            var t = age / 2.2f;
            var arc = MathF.Sin(t * MathF.PI);
            height += arc * 45 * scale;
            rotation += (crate % 2 == 0 ? 1 : -1) * MathF.Tau * LobbyLineupChoreography.Smooth(t);
            position.X += arc * (crate % 2 == 0 ? 24 : -24) * scale;
        }
        // The delivery dissolves into an orderly-enough conga, then everybody leaves the stage.
        var conga = LobbyLineupChoreography.Smooth((elapsed - 18) / 2);
        var columns = Math.Max(1, (count + 3) / 4);
        var line = new Vector2(size.X * (0.08f + 0.84f * (index / 4 + 0.5f) / columns),
            size.Y * (0.48f + index % 4 * 0.09f));
        position = Vector2.Lerp(position, line, conga);
        var exit = LobbyLineupChoreography.Smooth((elapsed - 21 - index % 4 * 0.15f) / 5);
        position.X += exit * (size.X + 160);
        position.Y -= height + conga * MathF.Abs(MathF.Sin(elapsed * 7 - index * 0.4f)) * 8 * scale;
        rotation *= 1 - conga;
        if (conga > 0)
            backwards = false;
        return new MarchPose(position, rotation, height, backwards);
    }
}
