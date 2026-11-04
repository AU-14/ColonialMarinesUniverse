using System.Numerics;
using Content.Shared.CMU14.Lobby;

namespace Content.Client.CMU14.Lobby;

public enum LobbyLineupReaction : byte
{
    Duck,
    Tumble,
    Splat,
    Slip,
    Celebrate,
}

public static class LobbyLineupInteractionChoreography
{
    public const float ReactionDuration = 2.4f;
    public static bool IsGun(LobbyLineupEmote emote) => emote is LobbyLineupEmote.BurstFire or LobbyLineupEmote.SprayAndPray;
    public static float FlightTime(LobbyLineupEmote emote) => IsGun(emote) ? 0.18f : 0.75f;
    public static int CueCount(LobbyLineupEmote emote) => IsGun(emote) ? LobbyLineupChoreography.ShotCount(emote) : 1;
    public static float CueTime(LobbyLineupEmote emote, int cue) => IsGun(emote) ? LobbyLineupChoreography.ShotTime(emote, cue) : 0.65f;

    public static LobbyLineupReaction Reaction(LobbyLineupEmote incoming, LobbyLineupEmote? current, int seed)
    {
        if (incoming == LobbyLineupEmote.ConfettiCannon)
            return LobbyLineupReaction.Celebrate;
        // A dancer or a tumbling marine can evade an incoming gag without losing their own move.
        if (current is LobbyLineupEmote.DodgeRoll or LobbyLineupEmote.Backflip ||
            IsGun(incoming) && LobbyLineupChoreography.IsDance(current))
            return LobbyLineupReaction.Duck;
        return incoming switch
        {
            LobbyLineupEmote.PieToss => LobbyLineupReaction.Splat,
            LobbyLineupEmote.BananaPeel => LobbyLineupReaction.Slip,
            _ => (seed & 1) == 0 ? LobbyLineupReaction.Duck : LobbyLineupReaction.Tumble,
        };
    }

    public static (float Rotation, Vector2 Offset) Sample(LobbyLineupReaction reaction, float age, bool reduced)
    {
        if (reduced)
            return (0, Vector2.Zero);
        var blend = LobbyLineupChoreography.Envelope(age, ReactionDuration, 0.18f, 0.7f);
        return reaction switch
        {
            LobbyLineupReaction.Slip => (blend * MathF.PI / 2, new Vector2(0.09f, 0.18f) * blend),
            LobbyLineupReaction.Tumble => (-blend * 0.8f, new Vector2(-0.1f, 0.12f) * blend),
            LobbyLineupReaction.Duck => (blend * 0.25f, new Vector2(0, 0.13f) * blend),
            LobbyLineupReaction.Splat => (MathF.Sin(age * 17) * 0.07f * blend, Vector2.Zero),
            _ => (0, new Vector2(0, -MathF.Abs(MathF.Sin(age * 8)) * 0.09f * blend)),
        };
    }
}
