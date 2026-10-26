using System.Collections.Generic;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.IoC;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags;

public sealed partial class DistressAntagsRuleSystem : GameRuleSystem<DistressAntagsRuleComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    // Every entry is a per round weighted coin flip. DS antags are garnish on a
    // war operation, so the chances sit below their ColonyFall equivalents.
    public static readonly Dictionary<string, float> AntagRulePrototypes = new()
    {
        { "RunawaySynth", 0.25f },
        { "Replicant", 0.15f },
    };

    protected override void Added(EntityUid uid, DistressAntagsRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);
        foreach (var (antag, chance) in AntagRulePrototypes)
        {
            if (_random.Prob(chance))
                GameTicker.AddGameRule(antag);
        }
    }
}
