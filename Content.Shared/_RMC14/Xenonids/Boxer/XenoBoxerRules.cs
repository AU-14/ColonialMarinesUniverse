using System;
using Robust.Shared.GameObjects;

namespace Content.Shared._RMC14.Xenonids.Boxer;

public enum XenoBoxerUppercutStage
{
    None,
    Damage,
    KnockBack,
    KnockDown,
    KnockOut,
}

public static class XenoBoxerRules
{
    public const float MaxKo = 15f;
    public const int ClearHeadMaxCharges = 3;
    public static readonly TimeSpan KoResetDelay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ClearHeadRegenDelay = TimeSpan.FromSeconds(15);

    public static float AddKo(float current, float amount)
    {
        return Math.Clamp(current + amount, 0, MaxKo);
    }

    public static bool IsDifferentTarget(EntityUid? currentTarget, EntityUid target)
    {
        return currentTarget is { } current && current != target;
    }

    public static bool IsKoExpired(TimeSpan now, TimeSpan lastHitAt)
    {
        return now - lastHitAt >= KoResetDelay;
    }

    public static XenoBoxerUppercutStage GetUppercutStage(float ko)
    {
        if (ko >= 9)
            return XenoBoxerUppercutStage.KnockOut;
        if (ko >= 6)
            return XenoBoxerUppercutStage.KnockDown;
        if (ko >= 3)
            return XenoBoxerUppercutStage.KnockBack;
        if (ko >= 1)
            return XenoBoxerUppercutStage.Damage;

        return XenoBoxerUppercutStage.None;
    }

    public static float GetUppercutDamage(float ko, float damagePerKo)
    {
        return GetUppercutStage(ko) >= XenoBoxerUppercutStage.Damage ? ko * damagePerKo : 0;
    }

    public static float GetUppercutKnockBackPower(float ko, float powerPerKo)
    {
        return GetUppercutStage(ko) >= XenoBoxerUppercutStage.KnockBack ? ko * powerPerKo : 0;
    }

    public static TimeSpan GetUppercutKnockDownDuration(float ko)
    {
        return GetUppercutStage(ko) >= XenoBoxerUppercutStage.KnockDown
            ? TimeSpan.FromSeconds(ko * 0.25)
            : TimeSpan.Zero;
    }

    public static float GetUppercutHealAmount(float ko, float maxHealth, float healPercentPerKo, float xenoMultiplier, bool targetIsXeno)
    {
        if (ko <= 0 || maxHealth <= 0 || healPercentPerKo <= 0)
            return 0;

        var multiplier = targetIsXeno ? xenoMultiplier : 1f;
        return ko * healPercentPerKo * maxHealth * multiplier;
    }

    public static bool TryConsumeClearHead(ref int charges, bool forced)
    {
        if (forced || charges <= 0)
            return false;

        charges--;
        return true;
    }

    public static bool IsClearHeadRegenDue(TimeSpan now, TimeSpan nextRegenAt, int charges)
    {
        return charges < ClearHeadMaxCharges && now >= nextRegenAt;
    }

    public static TimeSpan GetNextClearHeadRegenAt(TimeSpan now)
    {
        return now + ClearHeadRegenDelay;
    }

    public static int GetInitialClearHeadCharges(bool isXenoVersusXeno)
    {
        return isXenoVersusXeno ? 0 : ClearHeadMaxCharges;
    }
}
