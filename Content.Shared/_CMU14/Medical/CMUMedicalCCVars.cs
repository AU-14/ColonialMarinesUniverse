using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._CMU14.Medical;

[CVarDefs]
public sealed partial class CMUMedicalCCVars : CVars
{
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("cmu.medical.enabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> HitLocationEnabled =
        CVarDef.Create("cmu.medical.hit_location.enabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> BodyPartEnabled =
        CVarDef.Create("cmu.medical.body_part.enabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> BoneEnabled =
        CVarDef.Create("cmu.medical.bone.enabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> OrganEnabled =
        CVarDef.Create("cmu.medical.organ.enabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> WoundsEnabled =
        CVarDef.Create("cmu.medical.wounds.enabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> SurgeryEnabled =
        CVarDef.Create("cmu.medical.surgery.enabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DiagnosticsEnabled =
        CVarDef.Create("cmu.medical.diagnostics.enabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> StatusEffectsEnabled =
        CVarDef.Create("cmu.medical.status_effects.enabled", true, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Defaults <c>false</c> — the penalty aggregator stays on
    ///     <c>StatusEffectsEnabled</c> alone.
    /// </summary>
    public static readonly CVarDef<bool> PainEnabled =
        CVarDef.Create("cmu.medical.pain.enabled", false, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> HitLocationHeadWeight =
        CVarDef.Create("cmu.medical.hit_location.head_weight", 0.15f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> HitLocationChestWeight =
        CVarDef.Create("cmu.medical.hit_location.chest_weight", 0.50f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> HitLocationArmWeight =
        CVarDef.Create("cmu.medical.hit_location.arm_weight", 0.10f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> HitLocationLegWeight =
        CVarDef.Create("cmu.medical.hit_location.leg_weight", 0.07f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> BodyPartDamagePropagation =
        CVarDef.Create("cmu.medical.body_part.damage_propagation", 1.0f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> SeveranceHeadDisabled =
        CVarDef.Create("cmu.medical.severance.head_disabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> SeveranceTorsoDisabled =
        CVarDef.Create("cmu.medical.severance.torso_disabled", true, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Seconds the shooter's last aim-mode click stays authoritative for. After
    ///     this window the routing layer falls back to weighted-random.
    /// </summary>
    public static readonly CVarDef<float> AimModeFreshnessSeconds =
        CVarDef.Create("cmu.medical.aim_mode.freshness_seconds", 3f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> BoneBruteAbsorb =
        CVarDef.Create("cmu.medical.bone.brute_absorb", 0.6f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> BoneHealRate =
        CVarDef.Create("cmu.medical.bone.heal_rate", 1.0f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> FractureCompoundInternalBleed =
        CVarDef.Create("cmu.medical.fracture.compound_internal_bleed", 0.5f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> FractureComminutedInternalBleed =
        CVarDef.Create("cmu.medical.fracture.comminuted_internal_bleed", 1.0f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> OrganPassiveHealMultiplier =
        CVarDef.Create("cmu.medical.organ.passive_heal_multiplier", 0.5f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> OrganNativeRegenCap =
        CVarDef.Create("cmu.medical.organ.native_regen_cap", 0.9f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> OrganTransplantRejectionMinutes =
        CVarDef.Create("cmu.medical.organ.transplant_rejection_minutes", 10f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> OrganStasisMinutes =
        CVarDef.Create("cmu.medical.organ.stasis_minutes", 30f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> PainDecayPerSecond =
        CVarDef.Create("cmu.medical.pain.decay_per_second", 1.0f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> PainShockThreshold =
        CVarDef.Create("cmu.medical.pain.shock_threshold", 70f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Pain tier downward-cross threshold offset (in raw pain units).
    ///     Prevents 1-pain-cross flicker.
    /// </summary>
    public static readonly CVarDef<float> PainTierHysteresis =
        CVarDef.Create("cmu.medical.pain.tier_hysteresis", 3f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<int> PainSuppressionLevelsPerStep =
        CVarDef.Create("cmu.medical.pain.suppression_levels_per_step", 1, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> SurgeryWrongToolDamageMultiplier =
        CVarDef.Create("cmu.medical.surgery.wrong_tool_damage_multiplier", 1.0f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Fraction of <c>BodyPartHealthComponent.MaxHealth</c> a
    ///     freshly-reattached limb starts at.
    /// </summary>
    public static readonly CVarDef<float> SurgeryLimbReattachStartingHpFraction =
        CVarDef.Create("cmu.medical.surgery.limb_reattach_starting_hp_fraction", 0.5f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> WoundsInternalBleedTickSeconds =
        CVarDef.Create("cmu.medical.wounds.internal_bleed_tick_seconds", 4.0f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Single-hit Burn damage above which the part spawns a
    ///     <c>CMUEscharComponent</c>.
    /// </summary>
    public static readonly CVarDef<float> EscharBurnThreshold =
        CVarDef.Create("cmu.medical.eschar.burn_threshold", 30f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> TourniquetNecrosisMinutes =
        CVarDef.Create("cmu.medical.tourniquet.necrosis_minutes", 5f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> TourniquetSeveranceMinutes =
        CVarDef.Create("cmu.medical.tourniquet.severance_minutes", 10f, CVar.REPLICATED | CVar.SERVER);
}
