using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;
using System.Collections.Generic;
using Content.Server.GameTicking.Rules;
using Content.Shared.Random.Helpers;

namespace Content.Server.CMU14.Round.Antags;

public sealed partial class ColonyAntagsRuleSystem : GameRuleSystem<ColonyAntagsRuleComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    // Every entry is a per round weighted coin flip
    public static readonly Dictionary<string, float> AntagRulePrototypes = new()
    {
        { "RunawaySynth", 0.5f },
        { "Fugitive", 0.5f },
        { "DrugDealer", 0.5f },
        { "StrikeOrganizer", 0.45f },
        { "Cannibal", 0.40f },
        { "CLFSleeperAgent", 0.40f },
        { "Arsonist", 0.40f },
        { "CLFVeteran", 0.35f },
        { "SerialKiller", 0.35f },
        { "CLFSaboteur", 0.30f },
        { "Vigilante", 0.25f },
        { "BountyHunter", 0.20f },
        { "Replicant", 0.20f }
    };

    public const float CorporateAntagChance = 0.35f;
    public static readonly Dictionary<string, float> CorporateRuleWeights = new()
    {
        { "CorporateRivals", 2f },
        { "CorporateSpy", 1f },
        { "WeylandYutaniAgent", 1f }
    };

    public static bool IsColonyAntagRule(string protoId)
        => AntagRulePrototypes.ContainsKey(protoId)
        || CorporateRuleWeights.ContainsKey(protoId);

    protected override void Added(EntityUid uid, ColonyAntagsRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);
        foreach (var (antag, chance) in AntagRulePrototypes)
        {
            if (_random.Prob(chance))
                GameTicker.AddGameRule(antag);
        }

        if (_random.Prob(CorporateAntagChance))
            GameTicker.AddGameRule(_random.Pick(CorporateRuleWeights));
    }
}

