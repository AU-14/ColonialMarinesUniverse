using System;
using System.Numerics;

namespace Content.Shared.CMU14.Expeditions;

/// <summary>Infantry tracking error, separate from the weapon's native scatter and firing delays.</summary>
public static class CMUInfantryAimPolicy
{
    public static float ClampSkill(float skill) => float.IsFinite(skill) ? Math.Clamp(skill, 0, 1) : 1;

    public static float CorrectionInterval(float skill) => 0.35f + (1 - ClampSkill(skill)) * 0.65f;

    public static Vector2 AimPoint(Vector2 origin, Vector2 target, Vector2 velocity, float projectileSpeed,
        float skill, Vector2 error)
    {
        skill = ClampSkill(skill);
        var distance = Vector2.Distance(origin, target);
        var flight = Math.Min(distance / Math.Max(1, projectileSpeed), 0.4f);
        var point = target + velocity * flight;
        if (skill >= 1 || distance < 0.01f)
            return point;
        // Inexperienced shooters under-lead and hold a bounded bias between corrections.
        // The caller owns that sample's lifetime so safety checks and the native trigger agree.
        var inaccuracy = 1 - skill;
        point -= velocity * (flight * inaccuracy * 0.8f);
        var forward = (target - origin) / distance;
        var side = new Vector2(-forward.Y, forward.X);
        var radius = Math.Min(1.2f, distance * 0.09f) * inaccuracy;
        error = Vector2.Clamp(error, new Vector2(-1), Vector2.One);
        return point + (side * error.X + forward * (error.Y * 0.25f)) * radius;
    }
}
