using System.Collections.Generic;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
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
        // its own rule: Distress Signal clears the planet's jobs, so it brings its own colony Working Joe
        { EWJoeRuleSystem.DistressAntagId, 0.25f },
    };

    protected override void Added(EntityUid uid, DistressAntagsRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        // a preset with its own table in YAML (e.g. FoFAntags) rolls that instead of the Distress Signal one
        if (component.Rules is { } rules)
        {
            foreach (var (rule, chance) in rules)
            {
                if (_random.Prob(chance))
                    GameTicker.AddGameRule(rule.Id);
            }

            return;
        }

        foreach (var (antag, chance) in AntagRulePrototypes)
        {
            if (_random.Prob(chance))
                GameTicker.AddGameRule(antag);
        }
    }
}
