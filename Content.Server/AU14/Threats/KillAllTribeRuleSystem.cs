using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.AU14;
using Content.Shared._RMC14.Evacuation;

namespace Content.Server.AU14.Threats;

public sealed class KillAllTribeRuleSystem : GameRuleSystem<KillAllTribeRuleComponent>
{
	[Dependency] private readonly IEntityManager _entityManager = default!;
	[Dependency] private readonly GameTicker _gameTicker = default!;
	[Dependency] private readonly Round.AuRoundSystem _auRoundSystem = default!;

	private EntityQuery<EvacuatedGridComponent> _evacuatedQuery;

	public override void Initialize()
	{
		base.Initialize();
		_evacuatedQuery = GetEntityQuery<EvacuatedGridComponent>();
		SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
		SubscribeLocalEvent<EvacuationLaunchedEvent>(OnEvacuationLaunched);
	}

	private bool IsEvacuated(EntityUid uid)
	{
		var xform = Transform(uid);
		return xform.GridUid is { } grid && _evacuatedQuery.HasComp(grid);
	}

	private void OnEvacuationLaunched(ref EvacuationLaunchedEvent ev)
	{
		if (_gameTicker.IsGameRuleActive<KillAllTribeRuleComponent>())
			CheckVictoryCondition();
	}

	private void OnMobStateChanged(MobStateChangedEvent ev)
	{
		// Only run this logic when the KillAllTribe rule is active
		if (!_gameTicker.IsGameRuleActive<KillAllTribeRuleComponent>())
			return;

		// Only care about dead mobs
		if (ev.NewMobState != MobState.Dead)
			return;

		CheckVictoryCondition();
	}

	private void CheckVictoryCondition()
	{
		// Get the active rule entity and its component to read Percent
		var queryRule = EntityQueryEnumerator<KillAllTribeRuleComponent, GameRuleComponent>();
		if (!queryRule.MoveNext(out var ruleEnt, out var ruleComp, out var gameRuleComp) || !GameTicker.IsGameRuleActive(ruleEnt, gameRuleComp))
			return;

		var requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);

		// Count total and dead Tribal mobs (excluding evacuated)
		var total = 0;
		var dead = 0;

		var query = _entityManager.EntityQueryEnumerator<MobStateComponent>();
		while (query.MoveNext(out var uid, out var mobState))
		{
			if (!_entityManager.HasComponent<TribalComponent>(uid))
				continue;

			// If the entity's grid was evacuated, count them as dead (do not skip)
			if (IsEvacuated(uid))
			{
				total++;
				dead++;
				continue;
			}

			total++;
			if (mobState.CurrentState == MobState.Dead)
				dead++;
		}

		if (total == 0)
			return; // nothing to count

		var percentDead = (int) ((double)dead / total * 100.0);

		if (percentDead >= requiredPercent)
		{
			if (_gameTicker.RunLevel != GameRunLevel.InRound)
				return;

			var winMessage = _auRoundSystem._selectedthreat?.WinMessage;
			if (!string.IsNullOrEmpty(winMessage))
			{
				_gameTicker.EndRound(winMessage);
			}
			else
			{
				_gameTicker.EndRound("The Threat has been Eliminated");
			}
		}
	}
}

