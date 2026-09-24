using System.Linq;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.CMU14.Threats.Mobs.Biomorph;
using Content.Shared.CMU14.Threats.Mobs.ZombieSummoner;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Zombies;
using BiomorphComponent = Content.Shared.CMU14.Threats.Mobs.Biomorph.BiomorphComponent;
using ApeComponent = Content.Shared.CMU14.Threats.Mobs.Ape.ApeComponent;
using TribalComponent = Content.Shared.CMU14.Threats.Mobs.Tribal.TribalComponent;

namespace Content.Server.CMU14.Threats.Rules;

internal enum EvacuatedMobPolicy
{
    CountAsEliminated,
    CountAsAlive,
    Exclude
}

internal sealed class ThreatRuleHelper : EntitySystem
{
    private EntityQuery<EvacuatedGridComponent> _evacuatedQuery;
    private bool _dropshipHijackLanded;

    public override void Initialize()
    {
        base.Initialize();
        _evacuatedQuery = GetEntityQuery<EvacuatedGridComponent>();
        SubscribeLocalEvent<DropshipHijackLandedEvent>(OnDropshipHijackLanded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    internal static bool MeetsRequiredPercent(int eliminated, int total, int requiredPercent)
        => eliminated * 100 >= total * requiredPercent;

    internal static bool HasFaction(NpcFactionMemberComponent factionComp, string factionId)
        => factionComp.Factions.Any(f => f.ToString().Equals(factionId, StringComparison.OrdinalIgnoreCase));

    internal bool IsEvacuated(EntityUid uid)
        => Transform(uid).GridUid is { } grid && _evacuatedQuery.HasComp(grid);

    private void OnDropshipHijackLanded(ref DropshipHijackLandedEvent args)
    {
        if (!args.IsHumanHijack)
            _dropshipHijackLanded = true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
        => _dropshipHijackLanded = false;

    // Crashed also marks ordinary hull-integrity wrecks and flights that have only
    // started hijacking. Neither abandons the living factions on the planet.
    internal bool HasLandedDropshipHijack() => _dropshipHijackLanded;

    internal static bool TryGetActiveRule<TRule>(
        ref EntityQueryEnumerator<ActiveGameRuleComponent, TRule, GameRuleComponent> query,
        out TRule rule, out GameRuleComponent gameRule)
        where TRule : IComponent
    {
        if (query.MoveNext(out _, out _, out rule!, out gameRule!))
            return true;

        rule = default(TRule)!;
        gameRule = default(GameRuleComponent)!;
        return false;
    }

    internal bool IsExcludedFromVictory(EntityUid uid, MobStateComponent mobState)
    {
        if (HasComp<XenoComponent>(uid) || HasComp<YautjaComponent>(uid)
            || HasComp<ApeComponent>(uid) || HasComp<TribalComponent>(uid)
            || HasComp<BiomorphComponent>(uid) || HasComp<BiomorphMimicComponent>(uid)
            || HasComp<ZombieSummonerComponent>(uid) || HasComp<ZombieSummonerMinionComponent>(uid))
            return true;

        if (HasComp<SynthComponent>(uid))
            return true;

        if (IsEliminated(uid, mobState))
            return false;

        // Alive and nested/SSD
        return HasComp<XenoNestedComponent>(uid)
            || (TryComp(uid, out SSDIndicatorComponent? ssd) && ssd.IsSSD);
    }

    // Reanimating a casualty as a hostile zombie does not restore a human survivor.
    internal bool IsEliminated(EntityUid uid, MobStateComponent mobState)
        => mobState.CurrentState == MobState.Dead || HasComp<ZombieComponent>(uid);
}
