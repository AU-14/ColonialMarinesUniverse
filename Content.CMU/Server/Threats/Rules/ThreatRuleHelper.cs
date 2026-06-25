using System.Linq;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.SSDIndicator;
using AbominationComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationComponent;
using AbominationMimicComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationMimicComponent;
using ApeComponent = Content.Shared._CMU14.Threats.Mobs.Ape.ApeComponent;
using TribalComponent = Content.Shared._CMU14.Threats.Mobs.Tribal.TribalComponent;

namespace Content.Server._CMU14.Threats.Rules;

internal enum EvacuatedMobPolicy
{
    CountAsEliminated,
    CountAsAlive,
    Exclude
}

internal sealed class ThreatRuleHelper : EntitySystem
{
    private EntityQuery<EvacuatedGridComponent> _evacuatedQuery;

    public override void Initialize()
    {
        base.Initialize();
        _evacuatedQuery = GetEntityQuery<EvacuatedGridComponent>();
    }

    internal static bool MeetsRequiredPercent(int eliminated, int total, int requiredPercent)
        => eliminated * 100 >= total * requiredPercent;

    internal static bool HasFaction(NpcFactionMemberComponent factionComp, string factionId)
        => factionComp.Factions.Any(f => f.ToString().Equals(factionId, StringComparison.OrdinalIgnoreCase));

    internal bool IsEvacuated(EntityUid uid)
        => Transform(uid).GridUid is { } grid && _evacuatedQuery.HasComp(grid);

    internal bool HasCrashedDropship()
    {
        EntityQueryEnumerator<DropshipComponent> query = EntityQueryEnumerator<DropshipComponent>();
        while (query.MoveNext(out _, out DropshipComponent? dropship))
        {
            if (dropship.Crashed)
                return true;
        }

        return false;
    }

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
            || HasComp<AbominationComponent>(uid) || HasComp<AbominationMimicComponent>(uid))
            return true;

        if (HasComp<SynthComponent>(uid))
            return true;

        if (mobState.CurrentState == MobState.Dead)
            return false;

        // Alive and nested/SSD
        return HasComp<XenoNestedComponent>(uid)
            || (TryComp(uid, out SSDIndicatorComponent? ssd) && ssd.IsSSD);
    }
}
