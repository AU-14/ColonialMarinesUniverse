using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Humanoid;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// The one place that says which bodies the Early Warning Joe's objectives and meldings treat as
/// people. Kill credit, kill targets, meld eligibility and THE LIGHT all read these so the
/// lists cannot drift apart. The kill-person objective's YAML blacklist mirrors
/// <see cref="IsNonHumanBody"/>; keep them in step.
/// </summary>
public static class EWJoeTargetRules
{
    /// <summary>
    /// The one cone test the Joe's cone abilities share (Judgement, Saturate and its wave): inside
    /// <paramref name="halfAngle"/> of <paramref name="facing"/>, or close enough to share the Joe's tile.
    /// </summary>
    public static bool InCone(System.Numerics.Vector2 offset, Angle facing, Angle halfAngle)
    {
        return offset.Length() < 0.1f
            || Math.Abs(Angle.ShortestDistance(facing, offset.ToWorldAngle()).Theta) <= halfAngle.Theta;
    }

    /// <summary>Threats, xenos, yautja and drone androids are never people to the Joe.</summary>
    public static bool IsNonHumanBody(IEntityManager entities, EntityUid uid)
    {
        return entities.HasComponent<ThreatComponent>(uid)
            || entities.HasComponent<XenoComponent>(uid)
            || entities.HasComponent<YautjaComponent>(uid)
            || entities.HasComponent<CMUDroneAndroidComponent>(uid);
    }

    /// <summary>
    /// Xenofauna: non-humanoid threat life. A xeno, or a threat-faction body without a humanoid profile
    /// (apes, biomorphs, neomorphs, wendigo). Humanoid threats (cultists, tribals), Yautja and drone
    /// androids are not xenofauna. The xenofauna objectives and the anti-xeno kit share this rule.
    /// </summary>
    public static bool IsXenofauna(IEntityManager entities, EntityUid uid)
    {
        if (!entities.HasComponent<XenoComponent>(uid) && !entities.HasComponent<ThreatComponent>(uid))
            return false;

        return !entities.HasComponent<HumanoidProfileComponent>(uid)
            && !entities.HasComponent<YautjaComponent>(uid)
            && !entities.HasComponent<CMUDroneAndroidComponent>(uid);
    }

    /// <summary>
    /// ECS-11: an EW Joe stun duration for <paramref name="target"/>. Xenofauna that RMC's status-effect system
    /// shortens (<see cref="XenoComponent"/>, ×0.667, applied on top of this) get <paramref name="xenofaunaMultiplier"/>
    /// so the stun still lands. Other xenofauna (apes, threat mobs without a xeno body) take EW Joe stuns at full
    /// length already and are left as they are. XenoComponent is networked, so the client predicts the same value.
    /// </summary>
    public static TimeSpan ScaleStun(IEntityManager entities, EntityUid target, TimeSpan baseTime, float xenofaunaMultiplier)
    {
        return entities.HasComponent<XenoComponent>(target) && IsXenofauna(entities, target)
            ? baseTime * xenofaunaMultiplier
            : baseTime;
    }

    /// <summary>FLOW-12: who THE LIGHT judges: organics, and Yautja, who are not people for any other purpose.</summary>
    public static bool IsLightTarget(IEntityManager entities, EntityUid uid)
    {
        return IsOrganic(entities, uid)
            || entities.HasComponent<YautjaComponent>(uid) && entities.HasComponent<HumanoidProfileComponent>(uid);
    }

    /// <summary>An organic for kill credit: a humanoid that is not a synthetic.</summary>
    public static bool IsOrganic(IEntityManager entities, EntityUid uid)
    {
        if (!entities.HasComponent<HumanoidProfileComponent>(uid) || IsNonHumanBody(entities, uid))
            return false;

        return !entities.HasComponent<SynthComponent>(uid);
    }
}
