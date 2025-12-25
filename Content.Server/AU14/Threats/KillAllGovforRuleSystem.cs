using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.Mobs.Components;
using System.Linq;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;

namespace Content.Server.AU14.Threats;

public sealed class KillAllGovforRuleSystem : GameRuleSystem<KillAllGovforRuleComponent>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly Content.Server.AU14.Round.AuRoundSystem _auRoundSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        // Only run this logic when the KillAllGovfor rule is active
        if (!_gameTicker.IsGameRuleActive<KillAllGovforRuleComponent>())
            return;

        // Only care about dead mobs
        if (ev.NewMobState != MobState.Dead)
            return;

        // Check if any Govfor mobs remain
        var anyGovfor = false;
        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var faction))
        {
            if (mobState.CurrentState != MobState.Dead && faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "govfor"))
            {
                anyGovfor = true;
                break;
            }
        }

        if (!anyGovfor)
        {
            // End round, threat wins
            var winMessage = _auRoundSystem._selectedthreat?.WinMessage;
            if (!string.IsNullOrEmpty(winMessage))
                _gameTicker.EndRound(winMessage);
            else
                _gameTicker.EndRound("Threat victory: All Govfor eliminated.");
        }
    }
}
