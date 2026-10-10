using System.Linq;
using Content.Shared.DoAfter;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private void CaptureLivingDiagnostics(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (!_npcs.Enabled || !_mobs.IsAlive(uid) || HasComp<ActorComponent>(uid))
            return;
        var hasGun = _guns.TryGetGun(uid, out var gun);
        TryComp<CMUExpeditionMedicComponent>(uid, out var medic);
        agent.LastLivingDiagnostics = new CMUExpeditionDiagnosticSnapshot(now, agent.State, agent.Duty,
            agent.Doctrine, agent.SquadPhase, agent.DecisionOwner, agent.DecisionReason, agent.LastFireCheck,
            agent.WeaponDecision, hasGun ? MetaData(gun).EntityName : null, hasGun ? WeaponAmmo(gun) : 0,
            agent.LastDamage, agent.Stress, agent.KnownFireLanes.Count, agent.CornerDecision,
            agent.HeardKind, agent.HeardAt, hasGun ? Math.Max(0, (gun.Comp.NextFire - now).TotalSeconds) : 0,
            Math.Max(0, (agent.FireAt - now).TotalSeconds),
            agent.SquadDecision, agent.TrafficDecision, agent.DoorDecision, agent.VaultDecision,
            agent.ExposureMovementDecision, agent.SustainedFire, agent.FireControlVolley,
            agent.Treatment is { } treatment ? _doAfter.GetStatus(treatment) : null, agent.TreatmentMedicine != null,
            Math.Max(0, ((agent.TreatmentMedicine != null ? agent.TreatmentPreparingUntil : agent.NextHeal) - now).TotalSeconds),
            medic?.Phase, medic?.Decision ?? "idle", medic?.Doses ?? 0, medic?.Shocks ?? 0);
        // Revival or returning control to AI starts a new living record.
        agent.DiagnosticsStoppedAt = null;
        agent.FrozenDecisionHistory = Array.Empty<string>();
    }

    private void FreezeLivingDiagnostics(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.DiagnosticsStoppedAt != null)
            return;
        // Capture history before Stop writes its administrative interruption. Ammo
        // comes from the last living sample: death may already have dropped the gun.
        agent.DiagnosticsStoppedAt = _timing.CurTime;
        agent.FrozenDecisionHistory = agent.DecisionHistory.ToArray();
    }

    private string SelfTreatmentDiagnostic(CMUExpeditionAgentComponent agent, TimeSpan now) =>
        SelfTreatmentDiagnostic(agent.Treatment is { } treatment ? _doAfter.GetStatus(treatment) : null,
            agent.TreatmentMedicine != null,
            Math.Max(0, ((agent.TreatmentMedicine != null ? agent.TreatmentPreparingUntil : agent.NextHeal) - now).TotalSeconds));

    private string SelfTreatmentDiagnostic(DoAfterStatus? status, bool preparing, double wait)
    {
        if (status is { } native)
            return Loc.GetString("cmu-squads-treatment-native", ("status", native));
        if (preparing)
            return Loc.GetString("cmu-squads-treatment-preparing",
                ("remaining", wait.ToString("F2")));
        return Loc.GetString("cmu-squads-treatment-idle", ("retry", wait.ToString("F2")));
    }

    private string MedicalTaskDiagnostic(EntityUid uid) =>
        TryComp<CMUExpeditionMedicComponent>(uid, out var medic)
            ? MedicalTaskDiagnostic(medic.Phase, medic.Decision, medic.Doses, medic.Shocks)
            : MedicalTaskDiagnostic(null, "idle", 0, 0);

    private string MedicalTaskDiagnostic(CMUExpeditionMedicalPhase? phase, string decision, int doses, int shocks) =>
        phase is { } medical
            ? Loc.GetString("cmu-squads-medic-task", ("phase", medical), ("decision", decision), ("doses", doses), ("shocks", shocks))
            : Loc.GetString("cmu-squads-medic-none");

    private string HearingDiagnostic(string kind, TimeSpan heardAt, TimeSpan at) => kind == "none"
        ? Loc.GetString("cmu-squads-diagnostic-hearing-none")
        : Loc.GetString("cmu-squads-diagnostic-hearing",
            ("kind", kind == "gunfire" ? Loc.GetString("cmu-squads-noise-gunfire") : Loc.GetString("cmu-squads-noise-door")),
            ("age", Math.Max(0, (at - heardAt).TotalSeconds).ToString("F1")));

    private string FrozenDiagnosticDetail(CMUExpeditionAgentComponent agent, CMUExpeditionDiagnosticSnapshot recorded,
        string condition)
    {
        var header = Loc.GetString("cmu-squads-diagnostic-recorded", ("condition", condition),
            ("at", recorded.At.TotalSeconds.ToString("F1")),
            ("age", Math.Max(0, ((agent.DiagnosticsStoppedAt ?? _timing.CurTime) - recorded.At).TotalSeconds).ToString("F2")));
        return header + "\n" + Loc.GetString("cmu-squads-diagnostic-recorded-activity",
            ("state", recorded.State), ("duty", recorded.Duty), ("doctrine", recorded.Doctrine), ("phase", recorded.Phase),
            ("owner", recorded.Owner), ("reason", recorded.Reason), ("fire", recorded.FireCheck),
            ("weaponDecision", recorded.WeaponDecision), ("weapon", recorded.WeaponName ?? Loc.GetString("cmu-squads-unarmed")),
            ("ammo", recorded.Ammo), ("damage", recorded.Damage.ToString("F0")), ("stress", recorded.Stress.ToString("F2")),
            ("nativeWait", recorded.NativeFireWait.ToString("F2")), ("aimWait", recorded.AimWait.ToString("F2")),
            ("corner", recorded.CornerDecision), ("lanes", recorded.FireLanes)) + "\n" +
            Loc.GetString("cmu-squads-diagnostic-recorded-movement", ("squad", recorded.SquadDecision),
                ("traffic", recorded.TrafficDecision), ("door", recorded.DoorDecision), ("vault", recorded.VaultDecision),
                ("movement", recorded.FiringMovement), ("sustained", recorded.SustainedFire), ("volley", recorded.Volley)) + "\n" +
            HearingDiagnostic(recorded.HeardKind, recorded.HeardAt, recorded.At) + "\n" +
            SelfTreatmentDiagnostic(recorded.TreatmentStatus, recorded.PreparingTreatment, recorded.TreatmentWait) + "\n" +
            MedicalTaskDiagnostic(recorded.MedicalPhase, recorded.MedicalDecision, recorded.MedicalDoses, recorded.MedicalShocks) + "\n" +
            Loc.GetString("cmu-squads-diagnostic-recorded-history") + "\n" +
            string.Join("\n", agent.FrozenDecisionHistory) + "\n\n" +
            Loc.GetString("cmu-squads-diagnostic-lifetime") + "\n" +
            Loc.GetString("cmu-squads-diagnostic-timing", ("last", agent.LastThinkMilliseconds.ToString("F2")),
                ("average", agent.AverageThinkMilliseconds.ToString("F2")), ("peak", agent.MaxThinkMilliseconds.ToString("F2")),
                ("samples", agent.ThinkSamples)) + "\n" +
            Loc.GetString("cmu-squads-diagnostic-lifetime-movement", ("changes", agent.StateTransitions),
                ("searches", agent.Searches), ("milliseconds", agent.TotalSearchMilliseconds.ToString("F2")),
                ("detours", agent.LocalDetours), ("nudges", agent.TrafficNudges), ("doors", agent.DoorsOpened),
                ("doorFailures", agent.DoorFailures), ("covered", agent.CoveredMoves), ("interrupted", agent.InterruptedMoves)) + "\n" +
            Loc.GetString("cmu-squads-diagnostic-lifetime-combat", ("movingShots", agent.TotalMovingShots),
                ("reloads", agent.Reloads), ("grenades", agent.GrenadesThrown), ("failedPlans", agent.FailedPlans),
                ("flanks", agent.CornerFlanks), ("staging", agent.CornerStagingMoves));
    }
}
