using System;
using Content.Shared._CMU14.Medical.Chemistry.Data;
using Content.Shared._CMU14.Medical.Chemistry.Rules;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Chemistry.Systems;

public sealed partial class SharedHumanChemicalLedgerSystem : EntitySystem
{
    [Dependency] private SharedHumanMedicalSystem _medical = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<HumanChemicalOrganStasisComponent, HumanMedicalComponent>();
        while (query.MoveNext(out var uid, out var stasis, out var medical))
        {
            if (stasis.ExpiresAt > _timing.CurTime)
                continue;

            var transaction = HumanChemicalLedgerRules.CreateClearOrganStasisTransaction(medical);
            if (transaction.Count > 0)
                _medical.ApplyTransaction((uid, medical), transaction);

            RemComp<HumanChemicalOrganStasisComponent>(uid);
        }
    }

    public bool TryApplyChemicalTick(
        EntityUid body,
        HumanChemicalTick tick,
        HumanMedicalComponent? medical = null)
    {
        if (!CanMutateHumanLedger(body, ref medical) || medical is not { } resolvedMedical)
            return false;

        var plan = HumanChemicalLedgerRules.CreatePlan(resolvedMedical, tick);
        var applied = false;
        if (plan.Transaction.Count > 0)
        {
            var result = _medical.ApplyTransaction((body, resolvedMedical), plan.Transaction);
            applied = result.Applied;
        }

        if (plan.OrganStasisDuration > TimeSpan.Zero)
        {
            var stasis = EnsureComp<HumanChemicalOrganStasisComponent>(body);
            var expiresAt = _timing.CurTime + plan.OrganStasisDuration;
            if (expiresAt > stasis.ExpiresAt)
                stasis.ExpiresAt = expiresAt;

            applied = true;
        }

        return applied;
    }

    public bool CanMutateHumanLedger(
        EntityUid body,
        ref HumanMedicalComponent? medical)
    {
        return Resolve(body, ref medical, logMissing: false) &&
            !HasComp<SynthComponent>(body) &&
            !HasComp<XenoComponent>(body);
    }
}
