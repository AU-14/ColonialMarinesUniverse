using System.Linq;
using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Humanoid;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Random;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.CMU14.Synth;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Synth;

public abstract partial class SharedSynthSystem
{
    private static readonly EntProtoId<SkillPresetComponent> GenerationSkills = "AU14SkillPresetPlatoonAuxiliarySupportSynth";
    private static readonly ProtoId<DamageModifierSetPrototype> AdvancedDamageModifier = "CMUSynthAdvanced";
    private static readonly ProtoId<DamageModifierSetPrototype> EarlyDamageModifier = "RMCSynth";

    [Dependency] private IComponentFactory _generationComponentFactory = default!;
    [Dependency] private SharedDirectionalAttackBlockSystem _generationDirectionalBlock = default!;
    [Dependency] private XenoLeapSystem _generationLeap = default!;
    [Dependency] private MovementSpeedModifierSystem _generationMovement = default!;

    private void InitializeGenerations()
    {
        SubscribeLocalEvent<CMUSynthGenerationComponent, RefreshMovementSpeedModifiersEvent>(OnGenerationRefreshSpeed);
        SubscribeLocalEvent<CMUSynthGenerationComponent, XenoLeapHitAttempt>(OnGenerationLeapHitAttempt,
            after: [typeof(XenoLeapSystem)]);
    }

    /// <summary>
    /// Applies character-selected synthetic mechanics after job components and before loadout skill upgrades.
    /// Fixed job bodies such as Working Joes are not configured through this path.
    /// </summary>
    public void ConfigureGeneration(Entity<SynthComponent> ent, int generation)
    {
        if (generation < HumanoidCharacterProfile.MinSyntheticGeneration || generation > HumanoidCharacterProfile.MaxSyntheticGeneration)
            throw new ArgumentOutOfRangeException(nameof(generation), generation, "Unsupported synthetic generation.");

        var preferences = EnsureComp<CMUSynthGenerationComponent>(ent);
        preferences.Generation = generation;
        Dirty(ent, preferences);

        var early = generation <= 2;
        _generationLeap.RemoveInherentLeapProtection(ent.Owner);
        ent.Comp.Generation = generation switch
        {
            1 => "rmc-species-synth-generation-first",
            2 => "rmc-species-synth-generation-second",
            3 => "rmc-species-synth-generation-third",
            _ => "cmu-species-synth-generation-fourth",
        };
        ent.Comp.CritThreshold = early ? 250 : 200;
        ent.Comp.DeadThreshold = early ? 300 : 250;
        ent.Comp.NewDamageModifier = early ? EarlyDamageModifier : AdvancedDamageModifier;
        Dirty(ent);

        if (TryComp<MobThresholdsComponent>(ent, out var thresholds))
        {
            _mobThreshold.SetMobStateThreshold(ent, ent.Comp.CritThreshold, MobState.Critical, thresholds);
            _mobThreshold.SetMobStateThreshold(ent, ent.Comp.DeadThreshold, MobState.Dead, thresholds);
        }

        if (TryComp<DamageableComponent>(ent, out var damageable))
            _damageable.SetDamageModifierSetId((ent.Owner, damageable), ent.Comp.NewDamageModifier);

        if (!ent.Comp.HideGeneration)
        {
            var representation = EnsureComp<RMCHumanoidRepresentationOverrideComponent>(ent);
            representation.Age = ent.Comp.Generation;
            Dirty(ent, representation);
        }

        if (!GenerationSkills.TryGet(out var preset, _prototypes, _generationComponentFactory))
            throw new InvalidOperationException($"Missing synthetic skill preset: {GenerationSkills}");

        _skills.RemoveAllSkills(ent.Owner);
        _skills.SetSkills(ent.Owner, preset.Skills.ToDictionary(
            skill => skill.Key, skill => early ? Math.Max(0, skill.Value - 1) : skill.Value));
        _generationMovement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnGenerationRefreshSpeed(Entity<CMUSynthGenerationComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Generation <= 2)
            args.ModifySpeed(0.85f, 0.85f);
    }

    private void OnGenerationLeapHitAttempt(Entity<CMUSynthGenerationComponent> ent, ref XenoLeapHitAttempt args)
    {
        if (args.Cancelled || ent.Comp.Generation > 2 ||
            !TryComp<XenoComponent>(args.Leaper, out var xeno) ||
            xeno.Role.Id is not ("CMXenoLurker" or "CMXenoRunner") ||
            !TryComp<XenoLeapingComponent>(args.Leaper, out var leap) ||
            !_generationDirectionalBlock.IsFacingTarget(ent, args.Leaper, leap.Origin))
        {
            return;
        }

        // The replicated leap end time and network IDs give prediction the same roll as the server.
        var seed = leap.LeapEndTime.Ticks ^
            ((long) GetNetEntity(args.Leaper).Id << 32) ^ (uint) GetNetEntity(ent).Id;
        if (new Xoroshiro64S(seed).NextFloat() >= 0.15f)
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("cmu-synth-pounce-blocked"), ent, args.Leaper);
    }
}
