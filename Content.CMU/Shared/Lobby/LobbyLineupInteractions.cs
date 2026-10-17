using Robust.Shared.Network;

namespace Content.Shared.CMU14.Lobby;

/// <summary>Cosmetic interactions use a snapshot of the ready roster, never gameplay entities.</summary>
public static class LobbyLineupInteractions
{
    public static bool CanTarget(LobbyLineupEmote emote) => emote is
        LobbyLineupEmote.BurstFire or LobbyLineupEmote.SprayAndPray or LobbyLineupEmote.SquadVolley or
        LobbyLineupEmote.PieToss or LobbyLineupEmote.BananaPeel or LobbyLineupEmote.ConfettiCannon;

    public static List<NetUserId> SelectTargets(LobbyLineupEmote emote, IReadOnlyList<NetUserId> participants,
        IReadOnlyList<NetUserId> roster, int seed)
    {
        var targets = new List<NetUserId>();
        if (!CanTarget(emote) || roster.Count < 2)
            return targets;
        var random = new System.Random(seed);
        foreach (var participant in participants)
        {
            var ownIndex = -1;
            for (var i = 0; i < roster.Count; i++)
            {
                if (roster[i] == participant)
                {
                    ownIndex = i;
                    break;
                }
            }
            var target = random.Next(roster.Count - (ownIndex >= 0 ? 1 : 0));
            if (ownIndex >= 0 && target >= ownIndex)
                target++;
            targets.Add(roster[target]);
        }
        return targets;
    }
}
