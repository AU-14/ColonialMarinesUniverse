using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Lobby;

[Serializable, NetSerializable]
public enum LobbyPartyShow : byte
{
    Flyby,
    Parade,
    SupplyScramble,
}

/// <summary>The server chooses the cast and random seed; clients only request the routine.</summary>
[Serializable, NetSerializable]
public sealed class LobbyPartyShowRequest(LobbyPartyShow show) : EntityEventArgs
{
    public LobbyPartyShow Show { get; } = show;
}

[Serializable, NetSerializable]
public sealed class LobbyPartyShowEvent(LobbyPartyShow show, int seed, List<NetUserId> participants, bool automatic) : EntityEventArgs
{
    public const float Cooldown = 40;
    public const float AutomaticInterval = 75;
    public LobbyPartyShow Show { get; } = show;
    public int Seed { get; } = seed;
    public List<NetUserId> Participants { get; } = participants;
    public bool Automatic { get; } = automatic;
}
