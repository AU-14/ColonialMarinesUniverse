namespace Content.Shared._RMC14.Vehicle;

public static class VehicleFailureRules
{
    public static float GetChance(HardpointIntegrityComponent integrity, float damage)
    {
        if (integrity.MaxIntegrity <= 0f || integrity.Integrity <= 0f || damage <= 0f ||
            integrity.Integrity / integrity.MaxIntegrity > integrity.FailureIntegrityThreshold ||
            damage / integrity.MaxIntegrity < integrity.FailureMinimumDamageFraction)
            return 0f;

        return Math.Clamp(integrity.FailureChance, 0f, 1f);
    }
}
