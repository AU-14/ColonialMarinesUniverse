using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Chemistry.Rules;

public static class HumanOverdoseRules
{
    public static void AppendOverdoseEffects(
        MedicalTransaction transaction,
        string reagentId,
        FixedPoint2 totalQuantity,
        FixedPoint2 scale)
    {
        if (totalQuantity <= FixedPoint2.Zero)
            return;

        switch (reagentId)
        {
            case "CMBicaridine":
                AppendThreshold(
                    transaction,
                    totalQuantity,
                    scale,
                    overdose: FixedPoint2.New(30),
                    critical: FixedPoint2.New(50),
                    overdoseBurn: FixedPoint2.New(1),
                    criticalBurn: FixedPoint2.New(6));
                break;
            case "CMMeralyne":
                AppendThreshold(
                    transaction,
                    totalQuantity,
                    scale,
                    overdose: FixedPoint2.New(15),
                    critical: FixedPoint2.New(25),
                    overdoseBurn: FixedPoint2.New(1.5),
                    criticalBurn: FixedPoint2.New(9));
                break;
            case "CMKelotane":
                AppendThreshold(
                    transaction,
                    totalQuantity,
                    scale,
                    overdose: FixedPoint2.New(30),
                    critical: FixedPoint2.New(50),
                    overdoseBrute: FixedPoint2.New(1),
                    criticalBrute: FixedPoint2.New(5.5));
                break;
            case "CMDermaline":
                AppendThreshold(
                    transaction,
                    totalQuantity,
                    scale,
                    overdose: FixedPoint2.New(15),
                    critical: FixedPoint2.New(25),
                    overdoseBrute: FixedPoint2.New(1.5),
                    criticalBrute: FixedPoint2.New(9));
                break;
            case "CMDexalin":
                if (totalQuantity >= FixedPoint2.New(50))
                    transaction.Add(MedicalEffect.AddRegionDamage(
                        BodyRegion.Chest,
                        FixedPoint2.New(4) * scale,
                        FixedPoint2.Zero));
                break;
            case "CMDexalinPlus":
                if (totalQuantity >= FixedPoint2.New(25))
                    transaction.Add(MedicalEffect.AddRegionDamage(
                        BodyRegion.Chest,
                        FixedPoint2.New(3) * scale,
                        FixedPoint2.Zero));
                break;
            case "CMAlkysine":
                if (totalQuantity >= FixedPoint2.New(50))
                    transaction.Add(MedicalEffect.AddOrganDamage(OrganSlot.Brain, FixedPoint2.New(3) * scale));
                break;
            case "CMDylovene":
                if (totalQuantity >= FixedPoint2.New(30))
                    transaction.Add(MedicalEffect.AddOrganDamage(OrganSlot.Eyes, FixedPoint2.New(1) * scale));
                if (totalQuantity >= FixedPoint2.New(50))
                    transaction.Add(MedicalEffect.AddRegionDamage(
                        BodyRegion.Chest,
                        FixedPoint2.New(1) * scale,
                        FixedPoint2.New(1) * scale));
                break;
            case "CMTricordrazine":
                if (totalQuantity >= FixedPoint2.New(30))
                {
                    transaction.Add(MedicalEffect.AddRegionDamage(
                        BodyRegion.Chest,
                        FixedPoint2.New(0.5) * scale,
                        FixedPoint2.New(0.5) * scale));
                    transaction.Add(MedicalEffect.AddOrganDamage(OrganSlot.Eyes, FixedPoint2.New(0.5) * scale));
                }

                if (totalQuantity >= FixedPoint2.New(50))
                {
                    transaction.Add(MedicalEffect.AddRegionDamage(
                        BodyRegion.Chest,
                        FixedPoint2.New(2.5) * scale,
                        FixedPoint2.New(2.5) * scale));
                }

                break;
            case "CMImidazoline":
                if (totalQuantity >= FixedPoint2.New(50))
                {
                    transaction.Add(MedicalEffect.AddRegionDamage(
                        BodyRegion.Chest,
                        FixedPoint2.New(1) * scale,
                        FixedPoint2.New(1) * scale));
                    transaction.Add(MedicalEffect.AddOrganDamage(OrganSlot.Brain, FixedPoint2.New(1) * scale));
                }
                break;
            case "CMPeridaxon":
                AppendThreshold(
                    transaction,
                    totalQuantity,
                    scale,
                    overdose: FixedPoint2.New(15),
                    critical: FixedPoint2.New(25),
                    overdoseBrute: FixedPoint2.New(2),
                    criticalBrute: FixedPoint2.New(8),
                    criticalBurn: FixedPoint2.New(6));
                break;
            case "CMInaprovaline":
                if (totalQuantity >= FixedPoint2.New(100))
                    transaction.Add(MedicalEffect.AddOrganDamage(OrganSlot.Heart, FixedPoint2.New(0.5) * scale));
                break;
            case "CMUOxycodone":
                if (totalQuantity >= FixedPoint2.New(30))
                    AppendPainkillerCritical(
                        transaction,
                        scale,
                        liverDamage: FixedPoint2.New(12),
                        brainDamage: FixedPoint2.New(4));
                break;
            case "CMUTramadol":
                if (totalQuantity >= FixedPoint2.New(50))
                    AppendPainkillerCritical(
                        transaction,
                        scale,
                        liverDamage: FixedPoint2.New(7.5),
                        brainDamage: FixedPoint2.New(2.5));
                break;
            case "CMUParacetamol":
                if (totalQuantity >= FixedPoint2.New(100))
                    AppendPainkillerCritical(
                        transaction,
                        scale,
                        liverDamage: FixedPoint2.New(3),
                        brainDamage: FixedPoint2.New(1));
                break;
        }
    }

    private static void AppendThreshold(
        MedicalTransaction transaction,
        FixedPoint2 totalQuantity,
        FixedPoint2 scale,
        FixedPoint2 overdose,
        FixedPoint2 critical,
        FixedPoint2 overdoseBrute = default,
        FixedPoint2 overdoseBurn = default,
        FixedPoint2 criticalBrute = default,
        FixedPoint2 criticalBurn = default)
    {
        if (totalQuantity >= overdose &&
            (overdoseBrute > FixedPoint2.Zero || overdoseBurn > FixedPoint2.Zero))
        {
            transaction.Add(MedicalEffect.AddRegionDamage(
                BodyRegion.Chest,
                overdoseBrute * scale,
                overdoseBurn * scale));
        }

        if (totalQuantity >= critical &&
            (criticalBrute > FixedPoint2.Zero || criticalBurn > FixedPoint2.Zero))
        {
            transaction.Add(MedicalEffect.AddRegionDamage(
                BodyRegion.Chest,
                criticalBrute * scale,
                criticalBurn * scale));
        }
    }

    private static void AppendPainkillerCritical(
        MedicalTransaction transaction,
        FixedPoint2 scale,
        FixedPoint2 liverDamage,
        FixedPoint2 brainDamage)
    {
        transaction.Add(MedicalEffect.AddOrganDamage(OrganSlot.Brain, brainDamage * scale));
        transaction.Add(MedicalEffect.AddOrganDamage(OrganSlot.Liver, liverDamage * scale));
    }
}
