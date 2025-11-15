using Content.Shared.AU14.Threats;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using System.Linq;
using Robust.Client.Player;
using Robust.Shared.Log;
using Robust.Shared.Random;

namespace Content.Shared.AU14.Util;

/// <summary>
/// This handles...
/// </summary>
public abstract class AuThirdPartySystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] protected readonly IPlayerManager PlayerManager = default!;
    [Dependency] protected readonly IRobustRandom _random = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {

    }


    public void SpawnThirdPartyForContext(string currentGamemode, string? currentThreat, string? govforPlatoon, string? opforPlatoon)
    {
        var allThirdParties = PrototypeManager.EnumeratePrototypes<AuThirdPartyPrototype>().ToList();
        var playerCount = PlayerManager.PlayerCount;
        var filtered = allThirdParties.ToList();

        filtered.RemoveAll(proto =>
            proto.BlacklistedGamemodes.Contains(currentGamemode) ||
            (proto.whitelistedgamemodes.Count > 0 && !proto.whitelistedgamemodes.Contains(currentGamemode)) ||
            proto.MaxPlayers < playerCount ||
            proto.MinPlayers > playerCount ||
            (currentThreat != null && proto.BlacklistedThreats.Contains(currentThreat)) ||
            (proto.WhitelistedThreats.Count > 0 && (currentThreat == null || !proto.WhitelistedThreats.Contains(currentThreat))) ||
            (govforPlatoon != null && proto.BlacklistedPlatoons.Contains(govforPlatoon)) ||
            (opforPlatoon != null && proto.BlacklistedPlatoons.Contains(opforPlatoon)) ||
            (proto.WhitelistedPlatoons.Any() && ((govforPlatoon != null && !proto.WhitelistedPlatoons.Contains(govforPlatoon)) || (opforPlatoon != null && !proto.WhitelistedPlatoons.Contains(opforPlatoon))))
        );

        if (filtered.Count == 0)
        {
            Logger.Debug("[AuThirdPartySystem] No valid third parties found for current context.");
            return;
        }

        // Build weighted list
        var weighted = new List<AuThirdPartyPrototype>();
        foreach (var proto in filtered)
        {
            int weight = Math.Max(1, proto.weight);
            for (int i = 0; i < weight; i++)
                weighted.Add(proto);
        }
        if (weighted.Count == 0)
        {
            Logger.Debug("[AuThirdPartySystem] No weighted third parties available after filtering.");
            return;
        }
        var selected = _random.Pick(weighted);
        Logger.Debug($"[AuThirdPartySystem] Selected third party: {selected.ID}");
        SpawnThirdParty(selected);
    }

    public void SpawnThirdParty(AuThirdPartyPrototype party)
    {

        Logger.Debug($"[AuThirdPartySystem] Spawning third party: {party.ID}");
    }
}

