using System;
using System.Collections.Generic;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.Antag;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags;

public sealed partial class ColonyAntagsRuleSystem : GameRuleSystem<ColonyAntagsRuleComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private AntagPitySystem _pity = default!;

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
        { "Replicant", 0.20f },
        { "Rider", 0.20f }
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

        var demand = _cfg.GetCVar(CCVars.AntagDemandEnabled)
            ? ComputeDemand()
            : null;
        var boost = MathF.Max(0f, _cfg.GetCVar(CCVars.AntagDemandBoost));
        var cap = Math.Clamp(_cfg.GetCVar(CCVars.AntagDemandCap), 0f, 1f);

        foreach (var (antag, chance) in AntagRulePrototypes)
        {
            var effective = chance;
            if (demand != null)
                effective = MathF.Min(chance + boost * demand.GetValueOrDefault(antag), MathF.Max(chance, cap));

            if (_random.Prob(effective))
                GameTicker.AddGameRule(antag);
        }

        if (_random.Prob(CorporateAntagChance))
            GameTicker.AddGameRule(_random.Pick(CorporateRuleWeights));
    }

    // One demand point is one max weight player whose enabled antag preferences all
    // point at a single rule. Broad preferences spread thin and barely move a coin.
    private Dictionary<string, float> ComputeDemand()
    {
        // Rule -> antag preferences its slots can be filled from.
        var filters = new Dictionary<string, List<ProtoId<AntagPrototype>>>();
        foreach (var rule in AntagRulePrototypes.Keys)
        {
            if (!_prototypeManager.TryIndex<EntityPrototype>(rule, out var proto)
                || !proto.TryGetComponent<AntagSelectionComponent>(out var selection))
                continue;

            List<ProtoId<AntagPrototype>>? roles = null;
            foreach (var def in selection.Antags)
            {
                if (!_prototypeManager.Resolve(def.Proto, out var spec))
                    continue;

                roles ??= new List<ProtoId<AntagPrototype>>();
                foreach (var pref in spec.PrefRoles)
                {
                    if (!roles.Contains(pref))
                        roles.Add(pref);
                }
            }

            if (roles != null)
                filters[rule] = roles;
        }

        var demand = new Dictionary<string, float>();
        foreach (var session in _playerManager.Sessions)
        {
            // Same ready filter the round's spawn pool is built from.
            if (GameTicker.LobbyEnabled
                && GameTicker.PlayerGameStatuses.GetValueOrDefault(session.UserId) != PlayerGameStatus.ReadyToPlay)
                continue;

            if (!_antag.TryGetValidAntagPreferences(session, out var prefs))
                continue;

            var matched = new List<string>();
            foreach (var (rule, roles) in filters)
            {
                foreach (var role in roles)
                {
                    if (!prefs.Contains(role))
                        continue;

                    matched.Add(rule);
                    break;
                }
            }

            if (matched.Count == 0)
                continue;

            var share = _pity.GetWeight(session) / matched.Count;
            foreach (var rule in matched)
                demand[rule] = demand.GetValueOrDefault(rule) + share;
        }

        return demand;
    }
}
