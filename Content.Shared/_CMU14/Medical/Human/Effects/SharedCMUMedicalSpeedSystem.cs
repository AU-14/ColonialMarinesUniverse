using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared._CMU14.Medical.Human.Effects.Events;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Wheelchair;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Human.Effects;

public abstract partial class SharedCMUMedicalSpeedSystem : EntitySystem
{
    private const float MissingRegionSlowdownPoints = 2.5f;
    private const float SplintedOrEscharSlowdownPoints = 0.25f;
    private const float BrokenRegionSlowdownPoints = 0.65f;

    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected MovementSpeedModifierSystem Movement = default!;
    [Dependency] protected SharedPainShockSystem Pain = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected TemporarySpeedModifiersSystem TemporarySpeed = default!;

    private static readonly BodyRegion[] WalkingSlowdownRegions =
    {
        BodyRegion.LeftFoot,
        BodyRegion.RightFoot,
        BodyRegion.LeftLeg,
        BodyRegion.RightLeg,
        BodyRegion.Chest,
        BodyRegion.Groin,
        BodyRegion.Head,
    };

    private static readonly BodyRegion[] WheelchairSlowdownRegions =
    {
        BodyRegion.LeftHand,
        BodyRegion.RightHand,
        BodyRegion.LeftArm,
        BodyRegion.RightArm,
        BodyRegion.Chest,
        BodyRegion.Groin,
        BodyRegion.Head,
    };

    private bool _medicalEnabled;
    private bool _statusEffectsEnabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovement);
        SubscribeLocalEvent<HumanMedicalSummaryComponent, ComponentStartup>(OnSummaryStartup);
        SubscribeLocalEvent<HumanMedicalSummaryComponent, AfterAutoHandleStateEvent>(OnSummaryAfterState);
        SubscribeLocalEvent<HumanMedicalSummaryComponent, RefreshMovementSpeedModifiersEvent>(OnSummaryRefreshMovement);
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
        SubscribeLocalEvent<HumanTreatmentAppliedEvent>(OnTreatmentApplied);
        SubscribeLocalEvent<PainShockComponent, AfterAutoHandleStateEvent>(OnPainAfterState);
        SubscribeLocalEvent<PainShockStartupEvent>(OnPainStartup);
        SubscribeLocalEvent<PainTierChangedEvent>(OnPainTierChanged);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.StatusEffectsEnabled, v => _statusEffectsEnabled = v, true);
    }

    public bool IsLayerEnabled()
    {
        return _medicalEnabled && _statusEffectsEnabled;
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (Timing.ApplyingState)
            return;

        RefreshAggregatedPenalties(args.Body);
    }

    private void OnTreatmentApplied(ref HumanTreatmentAppliedEvent args)
    {
        if (Timing.ApplyingState)
            return;

        RefreshAggregatedPenalties(args.Patient);
    }

    private void OnPainStartup(ref PainShockStartupEvent args)
    {
        if (Timing.ApplyingState)
            return;

        RefreshAggregatedPenalties(args.Body);
    }

    private void OnPainTierChanged(ref PainTierChangedEvent args)
    {
        RefreshAggregatedPenalties(args.Body);
    }

    private void OnRefreshMovement(Entity<HumanMedicalComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!IsLayerEnabled())
            return;

        var mult = ComputeMovementMultiplier(ent.Owner);
        args.ModifySpeed(mult, mult);
    }

    private void OnSummaryStartup(Entity<HumanMedicalSummaryComponent> ent, ref ComponentStartup args)
    {
        RefreshSummaryMovement(ent.Owner);
    }

    private void OnSummaryAfterState(Entity<HumanMedicalSummaryComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshSummaryMovement(ent.Owner);
    }

    private void OnSummaryRefreshMovement(Entity<HumanMedicalSummaryComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!IsLayerEnabled() || HasComp<HumanMedicalComponent>(ent.Owner))
            return;

        var mult = ComputeSummaryMovementMultiplier(ent.Owner, ent.Comp.Summary);
        args.ModifySpeed(mult, mult);
    }

    private void OnPainAfterState(Entity<PainShockComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void RefreshSummaryMovement(EntityUid body)
    {
        if (HasComp<HumanMedicalComponent>(body))
            return;

        Movement.RefreshMovementSpeedModifiers(body);
    }

    public virtual void RefreshAggregatedPenalties(EntityUid body)
    {
        if (!HasComp<HumanMedicalComponent>(body))
            return;

        Movement.RefreshMovementSpeedModifiers(body);

        if (Net.IsClient)
            return;

        var aim = EnsureComp<CMUAimAccuracyComponent>(body);
        aim.SwayMultiplier = ComputeAimSwayMultiplier(body);
        aim.SpreadMultiplier = aim.SwayMultiplier;
        Dirty(body, aim);

        RefreshAimDependentWeapons(body);
    }

    protected virtual void RefreshAimDependentWeapons(EntityUid body)
    {
    }

    public float ComputeMovementMultiplier(EntityUid body)
    {
        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return 1f;

        var painTier = GetPredictedPainTier(body);

        var slowdown = CalculateLedgerMovementSlowdownPoints(
            medical,
            painTier,
            HasComp<ActiveWheelchairPilotComponent>(body));

        return ComputeMovementMultiplierFromSlowdown(body, slowdown);
    }

    private float ComputeSummaryMovementMultiplier(EntityUid body, MedicalSummary summary)
    {
        var slowdown = HasComp<ActiveWheelchairPilotComponent>(body)
            ? summary.WheelchairSlowdownPoints
            : summary.WalkingSlowdownPoints;
        slowdown += PainTierSlowdownPoints(GetPredictedPainTier(body));

        return ComputeMovementMultiplierFromSlowdown(body, slowdown);
    }

    private float ComputeMovementMultiplierFromSlowdown(EntityUid body, float slowdown)
    {
        var mult = slowdown <= 0f
            ? 1f
            : TemporarySpeed.CalculateSpeedModifier(body, slowdown) ?? FallbackSlowdownMultiplier(slowdown);

        if (HasComp<RecoveringFromSurgeryComponent>(body))
            mult = MathF.Min(mult, 0.70f);

        return Math.Clamp(mult, 0.20f, 1f);
    }

    private PainTier GetPredictedPainTier(EntityUid body)
    {
        if (!TryComp<PainShockComponent>(body, out var pain))
            return PainTier.None;

        return Net.IsClient
            ? pain.Tier
            : Pain.GetEffectiveTier(body, pain);
    }

    public float ComputeAimSwayMultiplier(EntityUid body)
    {
        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return 1f;

        var painTier = TryComp<PainShockComponent>(body, out var pain)
            ? Pain.GetEffectiveTier(body, pain)
            : PainTier.None;

        return CalculateLedgerAimSwayMultiplier(medical, painTier);
    }

    public float ComputeActionSpeedMultiplier(EntityUid body)
    {
        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return 1f;

        var painTier = TryComp<PainShockComponent>(body, out var pain)
            ? Pain.GetEffectiveTier(body, pain)
            : PainTier.None;

        return CalculateLedgerActionSpeedMultiplier(medical, painTier);
    }

    public static float CalculateLedgerMovementMultiplier(
        HumanMedicalComponent medical,
        PainTier painTier,
        bool recoveringFromSurgery = false)
    {
        var slowdown = CalculateLedgerMovementSlowdownPoints(medical, painTier, wheelchair: false);
        var mult = FallbackSlowdownMultiplier(slowdown);

        if (recoveringFromSurgery)
            mult = MathF.Min(mult, 0.70f);

        return Math.Clamp(mult, 0.20f, 1f);
    }

    public static float CalculateLedgerMovementSlowdownPoints(
        HumanMedicalComponent medical,
        PainTier painTier,
        bool wheelchair = false)
    {
        var slowdown = PainTierSlowdownPoints(painTier);
        slowdown += CalculateLedgerMedicalMovementSlowdownPoints(medical, wheelchair);

        return MathF.Max(0f, slowdown);
    }

    public static float CalculateLedgerMedicalMovementSlowdownPoints(
        HumanMedicalComponent medical,
        bool wheelchair = false)
    {
        var slowdown = 0f;
        var regions = wheelchair
            ? WheelchairSlowdownRegions
            : WalkingSlowdownRegions;

        for (var i = 0; i < regions.Length; i++)
        {
            var region = regions[i];
            var index = (int) region;
            if (index <= 0 || index >= medical.Regions.Length)
                continue;

            slowdown += RegionMovementSlowdownPoints(medical, region);
        }

        return MathF.Max(0f, slowdown);
    }

    private static float PainTierSlowdownPoints(PainTier painTier)
    {
        return painTier switch
        {
            PainTier.Mild => 0.25f,
            PainTier.Discomforting => 0.50f,
            PainTier.Moderate => 0.90f,
            PainTier.Distressing => 1.25f,
            PainTier.Severe => 1.60f,
            PainTier.Shock => 2.00f,
            _ => 0f,
        };
    }

    private static float FallbackSlowdownMultiplier(float slowdown)
    {
        if (slowdown <= 0f)
            return 1f;

        return 1f / MathF.Max(1f + slowdown / 4f, 1f);
    }

    public static float CalculateLedgerAimSwayMultiplier(
        HumanMedicalComponent medical,
        PainTier painTier)
    {
        var mult = 1f;

        mult *= RegionAimSwayMultiplier(medical.Regions[(int) BodyRegion.LeftArm]);
        mult *= RegionAimSwayMultiplier(medical.Regions[(int) BodyRegion.RightArm]);
        mult *= RegionAimSwayMultiplier(medical.Regions[(int) BodyRegion.LeftHand]);
        mult *= RegionAimSwayMultiplier(medical.Regions[(int) BodyRegion.RightHand]);
        mult *= EyeAimSwayMultiplier(medical.Organs[(int) OrganSlot.Eyes]);

        mult *= painTier switch
        {
            PainTier.None => 1.00f,
            PainTier.Mild => 1.01f,
            PainTier.Discomforting => 1.02f,
            PainTier.Moderate => 1.03f,
            PainTier.Distressing => 1.06f,
            PainTier.Severe => 1.10f,
            PainTier.Shock => 1.15f,
            _ => 1f,
        };

        return MathF.Min(mult, 2.50f);
    }

    public static float CalculateLedgerActionSpeedMultiplier(
        HumanMedicalComponent medical,
        PainTier painTier)
    {
        var mult = BrainActionSpeedMultiplier(medical.Organs[(int) OrganSlot.Brain]);

        mult *= painTier switch
        {
            PainTier.None => 1.00f,
            PainTier.Mild => 1.05f,
            PainTier.Discomforting => 1.10f,
            PainTier.Moderate => 1.15f,
            PainTier.Distressing => 1.25f,
            PainTier.Severe => 1.35f,
            PainTier.Shock => 1.50f,
            _ => 1f,
        };

        return MathF.Min(mult, 3.00f);
    }

    private static float RegionMovementSlowdownPoints(HumanMedicalComponent medical, BodyRegion region)
    {
        var state = medical.Regions[(int) region];
        if (IsMissingOrDetached(state))
            return MissingRegionSlowdownPoints;

        if (state.Skeletal.Splinted ||
            (!state.Skeletal.Broken && HasEschar(medical, region)))
        {
            return SplintedOrEscharSlowdownPoints;
        }

        return state.Skeletal.Broken ? BrokenRegionSlowdownPoints : 0f;
    }

    private static bool HasEschar(HumanMedicalComponent medical, BodyRegion region)
    {
        for (var i = 0; i < medical.Injuries.Count; i++)
        {
            var injury = medical.Injuries[i];
            if (injury.Region == region &&
                injury.Kind == InjuryKind.Burn &&
                injury.Flags.HasFlag(InjuryFlags.Necrotic) &&
                !injury.Flags.HasFlag(InjuryFlags.Debrided))
            {
                return true;
            }
        }

        return false;
    }

    private static float RegionAimSwayMultiplier(RegionState region)
    {
        if (IsMissingOrDetached(region))
            return 1.25f;

        if (!region.Skeletal.Broken)
            return 1f;

        return region.Skeletal.Splinted ? 1.05f : 1.10f;
    }

    private static float EyeAimSwayMultiplier(OrganState eyes)
    {
        if (eyes.Missing)
            return 2.00f;

        return eyes.Status switch
        {
            OrganDamageStatus.LittleBruised => 1.05f,
            OrganDamageStatus.Bruised => 1.10f,
            OrganDamageStatus.Broken => 1.30f,
            _ => 1f,
        };
    }

    private static float BrainActionSpeedMultiplier(OrganState brain)
    {
        if (brain.Missing)
            return 3.00f;

        return brain.Status switch
        {
            OrganDamageStatus.LittleBruised => 1.05f,
            OrganDamageStatus.Bruised => 1.25f,
            OrganDamageStatus.Broken => 1.60f,
            _ => 1f,
        };
    }

    private static bool IsMissingOrDetached(RegionState region)
    {
        return region.Presence is LimbPresence.Missing or LimbPresence.Detached;
    }
}
