using System.Collections.Immutable;
using System.Numerics;
using Content.Shared.CMU14.Lobby;

namespace Content.Client.CMU14.Lobby;

/// <summary>Cosmetic poses and shared cue times, in seconds. Offsets are fractions of each preview.</summary>
public static class LobbyLineupChoreography
{
    // Collection expressions for ImmutableArray emit marshal calls rejected by the content sandbox.
    public static readonly ImmutableArray<LobbyLineupEmote> SoloMoves = ImmutableArray.CreateRange(new[]
    {
        LobbyLineupEmote.Moonwalk, LobbyLineupEmote.Robot, LobbyLineupEmote.Shuffle,
        LobbyLineupEmote.Breakdance, LobbyLineupEmote.Headbang, LobbyLineupEmote.AirGuitar,
        LobbyLineupEmote.Backflip, LobbyLineupEmote.Shadowbox, LobbyLineupEmote.FakeFaint,
        LobbyLineupEmote.Dance, LobbyLineupEmote.PushUps, LobbyLineupEmote.VictorySpin,
        LobbyLineupEmote.BurstFire, LobbyLineupEmote.SprayAndPray, LobbyLineupEmote.XenoHug,
        LobbyLineupEmote.Facehugger, LobbyLineupEmote.Chestburst, LobbyLineupEmote.XenoMorph,
        LobbyLineupEmote.DodgeRoll, LobbyLineupEmote.GrenadeOops,
        LobbyLineupEmote.PieToss, LobbyLineupEmote.BananaPeel, LobbyLineupEmote.ConfettiCannon,
    });

    public static readonly ImmutableArray<LobbyLineupEmote> TeamMoves = ImmutableArray.CreateRange(new[]
    {
        LobbyLineupEmote.SquadDisco, LobbyLineupEmote.SquadConga, LobbyLineupEmote.SquadWave,
        LobbyLineupEmote.SquadWorkout, LobbyLineupEmote.SquadDanceOff, LobbyLineupEmote.SquadRally,
        LobbyLineupEmote.SquadVolley, LobbyLineupEmote.SquadXeno,
    });

    public static (LobbyLineupEmote Move, float Delay) TeamMember(LobbyLineupEmote emote, int index) => emote switch
    {
        LobbyLineupEmote.SquadWorkout => (LobbyLineupEmote.PushUps, 0),
        LobbyLineupEmote.SquadVolley => (LobbyLineupEmote.BurstFire, 0),
        LobbyLineupEmote.SquadXeno => (index % 2 == 0 ? LobbyLineupEmote.Facehugger : LobbyLineupEmote.Chestburst,
            Math.Min(index, 15) * 0.12f),
        LobbyLineupEmote.SquadWave => (LobbyLineupEmote.Wave, Math.Min(index, 15) * 0.16f),
        LobbyLineupEmote.SquadConga => (LobbyLineupEmote.SquadConga, Math.Min(index, 15) * 0.14f),
        LobbyLineupEmote.SquadDanceOff => ((index % 3) switch
        {
            0 => LobbyLineupEmote.Moonwalk, 1 => LobbyLineupEmote.Robot, _ => LobbyLineupEmote.Shuffle,
        }, 0),
        _ => (emote, 0),
    };

    public static bool IsDance(LobbyLineupEmote? emote) => emote is
        LobbyLineupEmote.Dance or LobbyLineupEmote.SquadDisco or LobbyLineupEmote.Moonwalk or
        LobbyLineupEmote.Robot or LobbyLineupEmote.Shuffle or LobbyLineupEmote.Breakdance or
        LobbyLineupEmote.Headbang or LobbyLineupEmote.AirGuitar or LobbyLineupEmote.SquadConga;

    public static float Duration(LobbyLineupEmote emote) => emote switch
    {
        LobbyLineupEmote.PushUps => 6.2f,
        LobbyLineupEmote.BurstFire => 4.3f,
        LobbyLineupEmote.SprayAndPray => 4.1f,
        LobbyLineupEmote.Facehugger => 5.2f,
        LobbyLineupEmote.Chestburst or LobbyLineupEmote.XenoMorph => 5.4f,
        LobbyLineupEmote.XenoHug => 5.6f,
        LobbyLineupEmote.Backflip => 3.1f,
        LobbyLineupEmote.DodgeRoll => 2.7f,
        LobbyLineupEmote.FakeFaint => 3.8f,
        LobbyLineupEmote.GrenadeOops => 5.0f,
        LobbyLineupEmote.PieToss or LobbyLineupEmote.BananaPeel or LobbyLineupEmote.ConfettiCannon => 3.6f,
        LobbyLineupEmote.VictorySpin or LobbyLineupEmote.Shadowbox => 4.2f,
        _ => IsDance(emote) ? 5.6f : 2.8f,
    };

    public static bool IsCombat(LobbyLineupEmote? emote) => emote is
        LobbyLineupEmote.BurstFire or LobbyLineupEmote.SprayAndPray or LobbyLineupEmote.XenoHug or
        LobbyLineupEmote.Facehugger or LobbyLineupEmote.Chestburst or LobbyLineupEmote.XenoMorph or
        LobbyLineupEmote.DodgeRoll or LobbyLineupEmote.GrenadeOops;

    public static float Smooth(float value)
    {
        var t = Math.Clamp(value, 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static float Envelope(float phase, float duration, float enter = 0.35f, float exit = 0.45f) =>
        Smooth(phase / enter) * Smooth((duration - phase) / exit);

    public static int ShotCount(LobbyLineupEmote emote) => emote == LobbyLineupEmote.BurstFire ? 9 : 18;

    public static float ShotTime(LobbyLineupEmote emote, int shot) => emote == LobbyLineupEmote.BurstFire
        ? 0.65f + shot / 3 * 1.10f + shot % 3 * 0.13f
        : 0.65f + shot * 0.13f;

    public static float Recoil(LobbyLineupEmote emote, float phase)
    {
        var recoil = 0f;
        for (var shot = 0; shot < ShotCount(emote); shot++)
        {
            var age = phase - ShotTime(emote, shot);
            if (age is >= 0 and < 0.18f)
                recoil = Math.Max(recoil, MathF.Pow(1 - age / 0.18f, 3));
        }
        return recoil;
    }

    public static float XenoBlend(float phase) => Smooth((phase - 1.75f) / 0.25f) * (1 - Smooth((phase - 4.55f) / 0.65f));

    public static (Direction Facing, float Rotation, Vector2 Offset) Sample(LobbyLineupEmote? emote, float phase, bool reducedMotion)
    {
        if (reducedMotion || emote == null)
            return (Direction.South, 0, Vector2.Zero);

        var duration = Duration(emote.Value);
        var blend = Envelope(phase, duration);
        var beat = MathF.Sin(phase * MathF.Tau * 1.35f);
        var facing = Direction.South;
        var rotation = 0f;
        var offset = Vector2.Zero;
        var fullTurn = false;
        switch (emote)
        {
            case LobbyLineupEmote.PieToss:
            case LobbyLineupEmote.BananaPeel:
            case LobbyLineupEmote.ConfettiCannon:
                facing = Direction.East;
                var throwBlend = Envelope(phase, 1.4f, 0.4f, 0.6f);
                rotation = -throwBlend * 0.17f;
                offset.X = throwBlend * 0.09f;
                break;
            case LobbyLineupEmote.Salute:
            case LobbyLineupEmote.SquadRally:
                offset.Y = -0.025f;
                break;
            case LobbyLineupEmote.Wave:
                rotation = beat * 0.05f;
                break;
            case LobbyLineupEmote.CheckGear:
                facing = Direction.East;
                rotation = MathF.Sin(phase * 3) * 0.07f;
                break;
            case LobbyLineupEmote.Stretch:
                facing = Direction.West;
                rotation = MathF.Sin(phase * 2.5f) * 0.16f;
                break;
            case LobbyLineupEmote.Dance:
            case LobbyLineupEmote.SquadDisco:
                facing = Turn(phase * 1.6f);
                rotation = beat * 0.12f;
                offset = new Vector2(beat * 0.06f, -MathF.Abs(beat) * 0.07f);
                break;
            case LobbyLineupEmote.PushUps:
                // East faces right: negative sprite rotation lays the face toward the floor.
                facing = Direction.East;
                rotation = -MathF.PI / 2;
                var rep = (1 - MathF.Cos(Math.Max(0, phase - 0.5f) * MathF.Tau)) / 2;
                offset.Y = 0.17f + rep * 0.05f;
                blend = Envelope(phase, duration, 0.5f, 0.65f);
                break;
            case LobbyLineupEmote.BurstFire:
            case LobbyLineupEmote.SprayAndPray:
                facing = Direction.East;
                var recoil = Recoil(emote.Value, phase);
                offset.X = -recoil * 0.035f;
                offset.Y = recoil * 0.008f;
                break;
            case LobbyLineupEmote.XenoHug:
                rotation = phase is > 1.1f and < 4.3f ? MathF.Sin(phase * 3) * 0.06f : 0;
                offset.X = -0.06f * Smooth(phase / 1.1f) * Smooth((duration - phase) / 1.1f);
                break;
            case LobbyLineupEmote.Facehugger:
                // Keep the attached mask aligned while the character shakes sideways.
                offset.X = phase is > 1.45f and < 3.7f ? MathF.Sin(phase * 19) * 0.035f : 0;
                offset.Y = phase is > 1.45f and < 3.7f ? -MathF.Abs(beat) * 0.035f : 0;
                break;
            case LobbyLineupEmote.Chestburst:
            case LobbyLineupEmote.XenoMorph:
                var tremor = Smooth((phase - 0.4f) / 0.8f) * (1 - Smooth((phase - 1.7f) / 0.3f));
                rotation = MathF.Sin(phase * 24) * 0.09f * tremor;
                offset.Y = MathF.Abs(beat) * 0.045f * tremor;
                break;
            case LobbyLineupEmote.DodgeRoll:
                facing = Direction.East;
                var roll = Smooth((phase - 0.55f) / 1.0f);
                rotation = -MathF.Tau * roll;
                fullTurn = true;
                offset = new Vector2(MathF.Sin(roll * MathF.PI) * 0.16f, MathF.Sin(roll * MathF.PI) * 0.13f);
                break;
            case LobbyLineupEmote.GrenadeOops:
                facing = Direction.East;
                var dive = Smooth((phase - 1.65f) / 0.45f) * (1 - Smooth((phase - 3.8f) / 0.8f));
                rotation = -MathF.PI / 2 * dive;
                offset = new Vector2(-0.12f, 0.18f) * dive;
                break;
            case LobbyLineupEmote.VictorySpin:
                facing = Turn(phase * 3);
                rotation = beat * 0.10f;
                offset.Y = -MathF.Abs(beat) * 0.09f;
                break;
            case LobbyLineupEmote.Moonwalk:
                facing = Direction.East;
                offset = new Vector2(-MathF.Sin(phase * 2) * 0.14f, -MathF.Abs(beat) * 0.02f);
                rotation = -0.06f;
                break;
            case LobbyLineupEmote.Robot:
                facing = Turn(MathF.Floor(phase * 1.5f));
                rotation = ((int) (phase * 3) % 3 - 1) * 0.11f;
                offset.X = ((int) (phase * 3) % 2 == 0 ? -1 : 1) * 0.04f;
                break;
            case LobbyLineupEmote.Shuffle:
                facing = (int) (phase * 1.35f) % 2 == 0 ? Direction.West : Direction.East;
                offset = new Vector2(beat * 0.10f, -MathF.Abs(MathF.Cos(phase * MathF.Tau * 1.35f)) * 0.06f);
                rotation = beat * 0.06f;
                break;
            case LobbyLineupEmote.Breakdance:
                rotation = MathF.Tau * 4 * Smooth((phase - 0.5f) / (duration - 1));
                fullTurn = true;
                offset = new Vector2(MathF.Sin(phase * 3) * 0.05f, 0.10f);
                break;
            case LobbyLineupEmote.Headbang:
                rotation = beat * 0.22f;
                offset.Y = MathF.Abs(beat) * 0.055f;
                break;
            case LobbyLineupEmote.AirGuitar:
                facing = Direction.East;
                rotation = -0.12f + beat * 0.09f;
                offset = new Vector2(MathF.Sin(phase * 3) * 0.05f, -MathF.Abs(beat) * 0.03f);
                break;
            case LobbyLineupEmote.Backflip:
                var flip = Math.Clamp((phase - 0.65f) / 1.25f, 0, 1);
                rotation = MathF.Tau * Smooth(flip);
                fullTurn = true;
                offset.Y = -MathF.Sin(flip * MathF.PI) * 0.20f;
                offset.Y += 0.045f * Smooth(phase / 0.3f) * (1 - Smooth((phase - 0.45f) / 0.2f));
                offset.Y += 0.045f * Smooth((phase - 1.9f) / 0.12f) * (1 - Smooth((phase - 2.1f) / 0.3f));
                break;
            case LobbyLineupEmote.Shadowbox:
                facing = (int) phase % 2 == 0 ? Direction.East : Direction.West;
                var punch = MathF.Pow(Math.Max(0, MathF.Sin(phase * MathF.Tau * 2)), 4);
                offset.X = punch * (facing == Direction.East ? 0.10f : -0.10f);
                rotation = offset.X;
                break;
            case LobbyLineupEmote.FakeFaint:
                var collapse = Smooth((phase - 0.4f) / 0.55f) * (1 - Smooth((phase - 2.6f) / 0.8f));
                rotation = MathF.PI / 2 * collapse;
                offset.Y = 0.18f * collapse;
                break;
            case LobbyLineupEmote.SquadConga:
                facing = Direction.East;
                offset = new Vector2(MathF.Sin(phase * 2.7f) * 0.10f, -MathF.Abs(MathF.Sin(phase * 5.4f)) * 0.07f);
                rotation = MathF.Sin(phase * 2.7f) * 0.10f;
                break;
        }
        if (phase < 0.15f || phase > duration - 0.15f)
            facing = Direction.South;
        return (facing, fullTurn ? rotation : rotation * blend, offset * blend);
    }

    private static Direction Turn(float phase) => ((int) phase % 4) switch
    {
        0 => Direction.South, 1 => Direction.West, 2 => Direction.North, _ => Direction.East,
    };
}
