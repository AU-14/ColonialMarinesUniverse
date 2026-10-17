using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Lobby;

[Serializable, NetSerializable]
public enum LobbyLineupEmote : byte
{
    Salute,
    Wave,
    CheckGear,
    Stretch,
    SquadRally,
    Dance,
    PushUps,
    VictorySpin,
    SquadDisco,
    Moonwalk,
    Robot,
    Shuffle,
    Breakdance,
    Headbang,
    AirGuitar,
    Backflip,
    Shadowbox,
    FakeFaint,
    SquadConga,
    SquadWave,
    SquadWorkout,
    SquadDanceOff,
    BurstFire,
    SprayAndPray,
    XenoHug,
    Facehugger,
    Chestburst,
    XenoMorph,
    DodgeRoll,
    GrenadeOops,
    SquadVolley,
    SquadXeno,
    PieToss,
    BananaPeel,
    ConfettiCannon,
}

/// <summary>Only requests an action. The server determines the sender and squad membership.</summary>
[Serializable, NetSerializable]
public sealed class LobbyLineupEmoteRequest(LobbyLineupEmote emote) : EntityEventArgs
{
    public LobbyLineupEmote Emote { get; } = emote;
}

[Serializable, NetSerializable]
public sealed class LobbyLineupEmoteEvent(NetUserId sender, LobbyLineupEmote emote, List<NetUserId> participants,
    List<NetUserId>? targets = null, int seed = 0) : EntityEventArgs
{
    public const float ActionCooldown = 3f;
    public const float SquadCooldown = 8f;

    public static bool IsTeamEmote(LobbyLineupEmote emote) =>
        emote is LobbyLineupEmote.SquadRally or LobbyLineupEmote.SquadDisco or LobbyLineupEmote.SquadConga or
            LobbyLineupEmote.SquadWave or LobbyLineupEmote.SquadWorkout or LobbyLineupEmote.SquadDanceOff or
            LobbyLineupEmote.SquadVolley or LobbyLineupEmote.SquadXeno;

    public NetUserId Sender { get; } = sender;
    public LobbyLineupEmote Emote { get; } = emote;
    public List<NetUserId> Participants { get; } = participants;
    /// <summary>Server-selected recipients, aligned with participants. Empty when nobody else is ready.</summary>
    public List<NetUserId> Targets { get; } = targets ?? new();
    public int Seed { get; } = seed;
}
