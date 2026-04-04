using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;
using System.Collections.Generic;
using Content.Server.GameTicking.Rules;

namespace Content.Server.AU14.round;

public sealed class ColonyAntagsRuleSystem : GameRuleSystem<ColonyAntagsRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private static readonly Dictionary<string, float> AntagRulePrototypes = new()
    {
        { "RunawaySynth", 0.5f },
        { "Fugitive", 0.5f },
        { "DrugDealer", 0.5f },
        { "CorporateSpy", 0.35f },
        { "CLFVeteran", 0.5f },
        { "StrikeOrganizer", 0.5f },
        { "Cannibal", 0.25f },
        { "SerialKiller", 0.25f }
    };

    protected override void Added(EntityUid uid, ColonyAntagsRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);
        foreach (var (antag, chance) in AntagRulePrototypes)
        {
            if (_random.Prob(chance))
            {
                GameTicker.AddGameRule(antag);
            }
        }
    }
}

