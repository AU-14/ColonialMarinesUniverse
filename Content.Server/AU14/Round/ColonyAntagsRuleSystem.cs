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

    private static readonly string[] AntagRulePrototypes =
    {
        "RunawaySynth",
        "Fugitive",
        "DrugDealer",
        "CorporateSpy"
    };

    protected override void Added(EntityUid uid, ColonyAntagsRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);
        foreach (var antag in AntagRulePrototypes)
        {
            if (_random.Prob(0.5f))
            {
                GameTicker.AddGameRule(antag);
            }
        }
    }
}

