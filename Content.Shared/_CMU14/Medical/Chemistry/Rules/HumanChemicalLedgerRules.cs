using System;
using Content.Shared._CMU14.Medical.Chemistry.Data;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Chemistry.Rules;

public static class HumanChemicalLedgerRules
{
    private static readonly BodyRegion[] RegionRecoveryPriority =
    {
        BodyRegion.Head,
        BodyRegion.Chest,
        BodyRegion.Groin,
        BodyRegion.LeftArm,
        BodyRegion.RightArm,
        BodyRegion.LeftHand,
        BodyRegion.RightHand,
        BodyRegion.LeftLeg,
        BodyRegion.RightLeg,
        BodyRegion.LeftFoot,
        BodyRegion.RightFoot,
    };

    private static readonly ReagentRule[] ReagentRules =
    {
        new("CMBicaridine", BruteRecovery: FixedPoint2.New(1)),
        new("CMMeralyne", BruteRecovery: FixedPoint2.New(1.5)),
        new("CMKelotane", BurnRecovery: FixedPoint2.New(1)),
        new("CMDermaline", BurnRecovery: FixedPoint2.New(1.5)),
        new("CMDylovene", ToxinRecovery: FixedPoint2.New(2)),
        new("CMDexalin", OxygenRecovery: FixedPoint2.New(4)),
        new("CMDexalinPlus", ClearOxygen: true),
        new(
            "CMTricordrazine",
            BruteRecovery: FixedPoint2.New(0.5),
            BurnRecovery: FixedPoint2.New(0.5),
            ToxinRecovery: FixedPoint2.New(0.5),
            OxygenRecovery: FixedPoint2.New(0.5)),
        new("CMAlkysine", OrganRepairSlot: OrganSlot.Brain, OrganRepair: FixedPoint2.New(3)),
        new("CMImidazoline", OrganRepairSlot: OrganSlot.Eyes, OrganRepair: FixedPoint2.New(1)),
        new(
            "CMCryoxadone",
            BruteRecovery: FixedPoint2.New(3),
            BurnRecovery: FixedPoint2.New(3),
            ToxinRecovery: FixedPoint2.New(3),
            OxygenRecovery: FixedPoint2.New(1)),
        new(
            "CMClonexadone",
            BruteRecovery: FixedPoint2.New(6),
            BurnRecovery: FixedPoint2.New(6),
            ToxinRecovery: FixedPoint2.New(6)),
        new("CMPeridaxon", OrganStasisDuration: TimeSpan.FromSeconds(2)),
        new("CMInaprovaline", PreventCriticalOxygen: true),
        new("CMUOxycodone"),
        new("CMUTramadol"),
        new("CMUParacetamol"),
    };

    public static HumanChemicalLedgerPlan CreatePlan(
        HumanMedicalComponent medical,
        HumanChemicalTick tick)
    {
        HumanMedicalLedger.EnsureInitialized(medical);

        var transaction = new MedicalTransaction(BodyRegion.Chest);
        if (!TryGetRule(tick.ReagentId, out var rule))
            return new HumanChemicalLedgerPlan(transaction, TimeSpan.Zero);

        var scale = tick.Scale <= FixedPoint2.Zero ? FixedPoint2.New(1) : tick.Scale;
        AddRegionRecoveryEffects(
            medical,
            transaction,
            InjuryKind.Bruise,
            rule.BruteRecovery * scale);
        AddRegionRecoveryEffects(
            medical,
            transaction,
            InjuryKind.Burn,
            rule.BurnRecovery * scale);
        AddOrganRepairEffects(transaction, rule.OrganRepairSlot, rule.OrganRepair * scale);

        var stasisDuration = TimeSpan.Zero;
        if (rule.OrganStasisDuration > TimeSpan.Zero &&
            AddOrganStasisEffects(medical, transaction, enabled: true))
        {
            stasisDuration = rule.OrganStasisDuration;
        }

        HumanOverdoseRules.AppendOverdoseEffects(
            transaction,
            tick.ReagentId,
            tick.TotalQuantity,
            scale);

        return new HumanChemicalLedgerPlan(transaction, stasisDuration);
    }

    public static MedicalTransaction CreateClearOrganStasisTransaction(HumanMedicalComponent medical)
    {
        var transaction = new MedicalTransaction(BodyRegion.Chest);
        AddOrganStasisEffects(medical, transaction, enabled: false);
        return transaction;
    }

    public static bool HasConditionPoolEffect(string reagentId)
    {
        if (!TryGetRule(reagentId, out var rule))
            return false;

        return rule.ToxinRecovery > FixedPoint2.Zero ||
            rule.OxygenRecovery > FixedPoint2.Zero ||
            rule.ClearOxygen ||
            rule.PreventCriticalOxygen;
    }

    private static void AddRegionRecoveryEffects(
        HumanMedicalComponent medical,
        MedicalTransaction transaction,
        InjuryKind repairKind,
        FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return;

        var remaining = amount;
        for (var i = 0; i < RegionRecoveryPriority.Length; i++)
        {
            var region = RegionRecoveryPriority[i];
            var floor = GetHealingFloor(medical, region, repairKind);
            var healable = FixedPoint2.Max(
                FixedPoint2.Zero,
                GetRegionDamage(medical, region, repairKind) - floor);

            if (healable <= FixedPoint2.Zero)
                continue;

            var repair = FixedPoint2.Min(remaining, healable);
            transaction.Add(MedicalEffect.RepairRegionDamage(region, repairKind, repair, floor));
            remaining -= repair;

            if (remaining <= FixedPoint2.Zero)
                return;
        }
    }

    private static void AddOrganRepairEffects(
        MedicalTransaction transaction,
        OrganSlot organSlot,
        FixedPoint2 amount)
    {
        if (organSlot == OrganSlot.None || amount <= FixedPoint2.Zero)
            return;

        transaction.Add(MedicalEffect.AddOrganDamage(organSlot, -amount));
    }

    private static bool AddOrganStasisEffects(
        HumanMedicalComponent medical,
        MedicalTransaction transaction,
        bool enabled)
    {
        var added = false;
        for (var i = 1; i < medical.Organs.Length; i++)
        {
            var organ = medical.Organs[i];
            if (organ.Slot == OrganSlot.None ||
                organ.Missing ||
                organ.Status == OrganDamageStatus.None)
            {
                continue;
            }

            transaction.Add(MedicalEffect.SetOrganStasis(organ.Slot, enabled));
            added = true;
        }

        return added;
    }

    private static FixedPoint2 GetRegionDamage(
        HumanMedicalComponent medical,
        BodyRegion region,
        InjuryKind repairKind)
    {
        var state = HumanMedicalLedger.GetRegion(medical, region);
        return repairKind == InjuryKind.Burn
            ? state.BurnDamage
            : state.BruteDamage;
    }

    private static FixedPoint2 GetHealingFloor(
        HumanMedicalComponent medical,
        BodyRegion region,
        InjuryKind repairKind)
    {
        if (repairKind != InjuryKind.Burn)
            return FixedPoint2.Zero;

        for (var i = 0; i < medical.Injuries.Count; i++)
        {
            var injury = medical.Injuries[i];
            if (injury.Region == region &&
                injury.Kind == InjuryKind.Burn &&
                injury.Flags.HasFlag(InjuryFlags.Necrotic))
            {
                // TODO CMU14: replace this hook with dedicated severe-burn/eschar
                // flags when the burn complication worker lands.
                return FixedPoint2.New(5);
            }
        }

        return FixedPoint2.Zero;
    }

    private static bool TryGetRule(
        string reagentId,
        out ReagentRule rule)
    {
        for (var i = 0; i < ReagentRules.Length; i++)
        {
            rule = ReagentRules[i];
            if (rule.ReagentId == reagentId)
                return true;
        }

        rule = default;
        return false;
    }

    private readonly record struct ReagentRule(
        string ReagentId,
        FixedPoint2 BruteRecovery = default,
        FixedPoint2 BurnRecovery = default,
        FixedPoint2 ToxinRecovery = default,
        FixedPoint2 OxygenRecovery = default,
        bool ClearOxygen = false,
        bool PreventCriticalOxygen = false,
        OrganSlot OrganRepairSlot = OrganSlot.None,
        FixedPoint2 OrganRepair = default,
        TimeSpan OrganStasisDuration = default);
}
