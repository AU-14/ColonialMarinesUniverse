using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;

namespace Content.Server.AU14.Threats;

public sealed class KillAllColonistRuleSystem : GameRuleSystem<KillAllColonistRuleComponent>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly Round.AuRoundSystem _auRoundSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        // Only run this logic when the KillAllColonist rule is active
        if (!_gameTicker.IsGameRuleActive<KillAllColonistRuleComponent>())
            return;

        // Only care about dead mobs
        if (ev.NewMobState != MobState.Dead)
            return;

        // Check if any AUColonist mobs remain
        var anyColonist = false;
        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var faction))
        {
            if (mobState.CurrentState != MobState.Dead && faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "aucolonist"))
            {
                anyColonist = true;
                break;
            }
        }

        if (!anyColonist)
        {
            // End round, threat wins
            var winMessage = _auRoundSystem._selectedthreat?.WinMessage;
            if (!string.IsNullOrEmpty(winMessage))
                _gameTicker.EndRound(winMessage);
            else
                _gameTicker.EndRound("Threat victory: All AUColonists eliminated.");
        }
    }
}
