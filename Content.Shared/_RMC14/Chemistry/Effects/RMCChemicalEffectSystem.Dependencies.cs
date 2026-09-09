using System.Diagnostics.CodeAnalysis;
using Content.Shared.CMU14.ChemicalIrritants;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Traits.Asthmatic;
using Content.Shared.CMU14.Traits.DrugAllergy;
using Content.Shared.CMU14.Traits.NicotineAddiction;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Temperature;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Drunk;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Network;
using Robust.Shared.Random;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.Shared._RMC14.Chemistry.Effects;

/// <summary>
/// The explicit service boundary available to stateless chemical-property hooks.
/// This intentionally exposes typed systems only: effects cannot locate arbitrary systems or access an entity manager.
/// </summary>
public sealed partial class RMCChemicalEffectSystem
{
    [Dependency] private readonly AllergicReactionSystem _allergicReaction = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly ChemicalAddictionSystem _chemicalAddiction = default!;
    [Dependency] private readonly ChemicalPropertyStatusSystem _chemicalPropertyStatus = default!;
    [Dependency] private readonly CMUChemicalMedicalSystem _chemicalMedical = default!;
    [Dependency] private readonly CMUMedicalBodyIndexSystem _medicalBodyIndex = default!;
    [Dependency] private readonly SatiationSystem _satiation = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly NicotineAddictionSystem _nicotineAddiction = default!;
    [Dependency] private readonly RespiratoryStrainSystem _respiratoryStrain = default!;
    [Dependency] private readonly RMCDefibrillatorSystem _defibrillator = default!;
    [Dependency] private readonly RMCUnrevivableSystem _unrevivable = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedBoneSystem _bone = default!;
    [Dependency] private readonly SharedCMUShrapnelSystem _shrapnel = default!;
    [Dependency] private readonly SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedHeartSystem _heart = default!;
    [Dependency] private readonly SharedOrganHealthSystem _organHealth = default!;
    [Dependency] private readonly SharedPainShockSystem _painShock = default!;
    [Dependency] private readonly SharedRMCBloodstreamSystem _rmcBloodstream = default!;
    [Dependency] private readonly SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private readonly SharedRMCEmoteSystem _rmcEmote = default!;
    [Dependency] private readonly SharedRMCTemperatureSystem _temperature = default!;
    [Dependency] private readonly TemporarySpeedModifiersSystem _temporarySpeedModifiers = default!;
    [Dependency] private readonly NewStatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StutteringSystem _stuttering = default!;
    [Dependency] private readonly SharedXenoParasiteSystem _parasite = default!;
    [Dependency] private readonly StatusEffectQuerySystem _statusEffectQuery = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;

    internal AllergicReactionSystem AllergicReaction => _allergicReaction;
    internal BlindableSystem Blindable => _blindable;
    internal ChemicalAddictionSystem ChemicalAddiction => _chemicalAddiction;
    internal ChemicalPropertyStatusSystem ChemicalPropertyStatus => _chemicalPropertyStatus;
    internal CMUChemicalMedicalSystem ChemicalMedical => _chemicalMedical;
    internal CMUMedicalBodyIndexSystem MedicalBodyIndex => _medicalBodyIndex;
    internal SatiationSystem Satiation => _satiation;
    internal MobStateSystem MobState => _mobState;
    internal MobThresholdSystem MobThreshold => _mobThreshold;
    internal NicotineAddictionSystem NicotineAddiction => _nicotineAddiction;
    internal RespiratoryStrainSystem RespiratoryStrain => _respiratoryStrain;
    internal RMCDefibrillatorSystem Defibrillator => _defibrillator;
    internal RMCUnrevivableSystem Unrevivable => _unrevivable;
    internal BloodstreamSystem Bloodstream => _bloodstream;
    internal SharedBoneSystem Bone => _bone;
    internal SharedCMUShrapnelSystem Shrapnel => _shrapnel;
    internal SharedCMUWoundsSystem Wounds => _wounds;
    internal SharedDrunkSystem Drunk => _drunk;
    internal SharedHeartSystem Heart => _heart;
    internal SharedOrganHealthSystem OrganHealth => _organHealth;
    internal SharedPainShockSystem PainShock => _painShock;
    internal SharedRMCBloodstreamSystem RMCBloodstream => _rmcBloodstream;
    internal SharedRMCDamageableSystem RMCDamageable => _rmcDamageable;
    internal SharedRMCEmoteSystem RMCEmote => _rmcEmote;
    internal SharedRMCTemperatureSystem Temperature => _temperature;
    internal TemporarySpeedModifiersSystem TemporarySpeedModifiers => _temporarySpeedModifiers;
    internal NewStatusEffectsSystem StatusEffects => _statusEffects;
    internal SharedStunSystem Stun => _stun;
    internal StutteringSystem Stuttering => _stuttering;
    internal SharedXenoParasiteSystem Parasite => _parasite;
    internal StatusEffectQuerySystem StatusEffectQuery => _statusEffectQuery;
    internal IRobustRandom Random => _random;
    internal INetManager Net => _net;

    internal void RaiseHydroTick<T>(EntityUid target, FixedPoint2 potency, ReagentQuantity quantity)
        where T : RMCChemicalEffect
    {
        var ev = new HydroTickEvent<T>(target, potency, quantity);
        RaiseLocalEvent(ref ev);
    }

    internal bool HasSynth(EntityUid uid) => HasComp<SynthComponent>(uid);
    internal bool HasYautja(EntityUid uid) => HasComp<YautjaComponent>(uid);
    internal bool HasVictimInfected(EntityUid uid) => HasComp<VictimInfectedComponent>(uid);
    internal bool HasUnrevivable(EntityUid uid) => HasComp<UnrevivableComponent>(uid);

    internal void ReduceChemicalIrritant(EntityUid target, float amount)
    {
        if (_net.IsClient)
            return;

        EntityManager.System<SharedChemicalIrritantSystem>().ReduceIrritant(target, amount);
    }

    internal bool TryGetAllergy(EntityUid uid, [NotNullWhen(true)] out DrugAllergyComponent? component)
        => TryComp(uid, out component);

    internal bool TryGetBloodstream(EntityUid uid, [NotNullWhen(true)] out BloodstreamComponent? component)
        => TryComp(uid, out component);

    internal bool TryGetChemicalAddictionTreatment(
        EntityUid uid,
        [NotNullWhen(true)] out ChemicalAddictionTreatmentComponent? component)
        => TryComp(uid, out component);

    internal bool TryGetDamageable(EntityUid uid, [NotNullWhen(true)] out DamageableComponent? component)
        => TryComp(uid, out component);

    internal bool TryGetHeart(EntityUid uid, [NotNullWhen(true)] out HeartComponent? component)
        => TryComp(uid, out component);

    internal bool TryGetSatiation(EntityUid uid, [NotNullWhen(true)] out SatiationComponent? component)
        => TryComp(uid, out component);

    internal bool TryGetMobThresholds(EntityUid uid, [NotNullWhen(true)] out MobThresholdsComponent? component)
        => TryComp(uid, out component);

    internal bool TryGetPainShock(EntityUid uid, [NotNullWhen(true)] out PainShockComponent? component)
        => TryComp(uid, out component);

    internal void DirtyFluxing(EntityUid uid, ChemicalFluxingComponent component)
        => Dirty(uid, component);

    internal void DirtyPainShock(EntityUid uid, PainShockComponent component)
        => Dirty(uid, component);

    internal void RemoveMuted(EntityUid uid)
    {
        foreach (var effect in _statusEffects.EnumerateStatusEffects<MutedStatusEffectComponent>((uid, null)))
        {
            PredictedQueueDel(effect.Owner);
        }
    }

    internal void RaiseOrganDamaged(EntityUid organ, ref OrganDamagedEvent args)
        => RaiseLocalEvent(organ, ref args);

    internal void RaiseCureChemicalAddiction(EntityUid target)
    {
        var ev = new CureChemicalAddictionEvent();
        RaiseLocalEvent(target, ref ev);
    }

    internal void RaiseVomit(EntityUid target)
    {
        var ev = new RMCVomitEvent(target);
        RaiseLocalEvent(ref ev);
    }

    internal void ApplyCiphering(EntityUid target, int potency)
    {
        if (_net.IsClient || !TryComp<VictimInfectedComponent>(target, out _))
            return;

        Dictionary<string, EntityUid> hives = [];
        var query = EntityQueryEnumerator<HiveComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var metadata))
        {
            var hiveKey = metadata.EntityPrototype?.ID switch
            {
                "CMXenoHive" => "prime",
                "CMUCorruptedHive" => "corrupted",
                "CMUAlphaHive" => "alpha",
                "CMUBravoHive" => "bravo",
                "CMUCharlieHive" => "charlie",
                "CMUDeltaHive" => "delta",
                _ => null,
            };

            if (hiveKey != null)
                hives.TryAdd(hiveKey, uid);
        }

        var key = potency switch
        {
            2 => "corrupted",
            3 => "alpha",
            4 => "bravo",
            5 => "charlie",
            6 => "delta",
            _ => "prime",
        };

        if (!hives.TryGetValue(key, out var hive))
        {
            var prototype = key switch
            {
                "corrupted" => "CMUCorruptedHive",
                "alpha" => "CMUAlphaHive",
                "bravo" => "CMUBravoHive",
                "charlie" => "CMUCharlieHive",
                "delta" => "CMUDeltaHive",
                _ => "CMXenoHive",
            };
            hive = Spawn(prototype);
        }

        _parasite.SetHive(target, hive);
    }
}
