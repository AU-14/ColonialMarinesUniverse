using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;
using System.Collections.Generic;
using Content.Server.GameTicking.Rules;

namespace Content.Server._CMU14.Round.Antags;

public sealed partial class ColonyAntagsRuleSystem : GameRuleSystem<ColonyAntagsRuleComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public static readonly Dictionary<string, float> AntagRulePrototypes = new()
    {
        { "RunawaySynth", 0.5f },
        { "Fugitive", 0.5f },
        { "DrugDealer", 0.5f },
        { "CorporateSpy", 0.25f },
        { "CLFVeteran", 0.35f },
        { "StrikeOrganizer", 0.45f },
        { "Cannibal", 0.40f },
        { "SerialKiller", 0.35f },
        { "CLFSleeperAgent", 0.40f },
        { "WeylandYutaniAgent", 0.25f },
        { "Arsonist", 0.40f },
        { "BountyHunter", 0.20f },
        { "CLFSaboteur", 0.30f },
        { "Vigilante", 0.25f }
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

