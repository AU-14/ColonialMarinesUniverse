using System.Numerics;
using Content.Server.Humanoid;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Medical.Injuries;
using Content.Shared._RMC14.Commendations;
using Content.Shared._RMC14.IdentityManagement;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Server.Humanoid.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Whitelist;
using Content.Shared.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaStatsSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private HumanoidOrganAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private GunIFFSystem _iff = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private NamingSystem _naming = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCStatusEffectSystem _rmcStatusEffects = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private YautjaHonorboundAbilitiesSystem _honorboundAbilities = default!;

    private const string YautjaSpecies = "Yautja";
    private const string DreadlocksMarking = "CMUYautjaDreadlocksStandard";
    private static readonly Color DreadlocksColor = Color.FromHex("#1a1512");
    private readonly HashSet<EntityUid> _pendingSkinRandomization = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, ComponentInit>(OnYautjaInit);
        SubscribeLocalEvent<YautjaComponent, ComponentStartup>(OnYautjaStartup);
        SubscribeLocalEvent<YautjaComponent, MapInitEvent>(OnYautjaMapInit);
        SubscribeLocalEvent<YautjaComponent, IdentityChangedEvent>(OnIdentityChanged);
        SubscribeLocalEvent<YautjaComponent, RandomHumanoidSpawnedEvent>(OnRandomHumanoidSpawned);
    }

    private void OnYautjaInit(Entity<YautjaComponent> ent, ref ComponentInit args)
    {
        ApplyIntrinsicStats(ent);
    }

    private void OnYautjaStartup(Entity<YautjaComponent> ent, ref ComponentStartup args)
    {
        ApplyIntrinsicStats(ent);
        ent.Comp.SkinColorRandomized = false;
        _pendingSkinRandomization.Add(ent);
    }

    private void OnYautjaMapInit(Entity<YautjaComponent> ent, ref MapInitEvent args)
    {
        SetYautjaName(ent);
        _honorboundAbilities.GrantActions(ent);
    }

    private void OnRandomHumanoidSpawned(Entity<YautjaComponent> ent, ref RandomHumanoidSpawnedEvent args)
    {
        ApplyIntrinsicStats(ent);
        _honorboundAbilities.GrantActions(ent);
    }

    private void OnIdentityChanged(Entity<YautjaComponent> ent, ref IdentityChangedEvent args)
    {
        SetUnknownIdentity(ent, args.IdentityEntity);
    }

    private void ApplyIntrinsicStats(Entity<YautjaComponent> ent)
    {
        // The human base supplies colonist allegiance and marine commendation eligibility.
        _faction.ClearFactions(ent.Owner, false);
        _faction.AddFaction(ent.Owner, ent.Comp.NpcFaction);
        _iff.SetUserFaction(ent.Owner, ent.Comp.IffFaction);
        RemComp<CommendationReceiverComponent>(ent);

        var movement = EnsureComp<MovementSpeedModifierComponent>(ent);
        _movement.ChangeBaseSpeed(ent, ent.Comp.BaseWalkSpeed, ent.Comp.BaseSprintSpeed, movement.BaseAcceleration, movement);
        EnsureComp<IgnoreXenoWeedsSlowdownComponent>(ent);
        EnsureComp<ParalyzeOnPullAttemptImmuneComponent>(ent);
        EnsureComp<InfectOnPullAttemptImmuneComponent>(ent);
        EnsureComp<YautjaTrophyRecordComponent>(ent);
        RemComp<StatusIconComponent>(ent);

        if (ent.Comp.StunResistance > 0f)
            _rmcStatusEffects.GiveStunResistance(ent, ent.Comp.StunResistance);

        var badBlood = HasComp<YautjaBadBloodComponent>(ent);
        var slowOnDamage = EnsureComp<SlowOnDamageComponent>(ent);
        slowOnDamage.SpeedModifierThresholds = new(badBlood
            ? ent.Comp.SlowOnDamageThresholds
            : ent.Comp.FrontlineSlowOnDamageThresholds);
        Dirty(ent, slowOnDamage);

        if (badBlood)
        {
            RemComp<CMUMedicalResilienceComponent>(ent);
        }
        else
        {
            var resilience = EnsureComp<CMUMedicalResilienceComponent>(ent);
            resilience.PainAccumulationMultiplier = ent.Comp.PainAccumulationMultiplier;
            resilience.MinimumPenalizingFractureSeverity = ent.Comp.MinimumPenalizingFractureSeverity;
            resilience.MovementPenaltyFloor = ent.Comp.MedicalMovementPenaltyFloor;
            resilience.AimPenaltyCeiling = ent.Comp.MedicalAimPenaltyCeiling;
            resilience.ActionSpeedPenaltyCeiling = ent.Comp.MedicalActionSpeedPenaltyCeiling;
            Dirty(ent, resilience);
        }

        var damageable = EnsureComp<DamageableComponent>(ent);
        _damageable.SetDamageModifierSetId((ent.Owner, damageable), ent.Comp.DamageModifierSet?.Id);

        var melee = EnsureComp<MeleeWeaponComponent>(ent);
        melee.AttackRate = ent.Comp.UnarmedAttackRate;
        melee.Damage = new()
        {
            DamageDict =
            {
                ["Blunt"] = ent.Comp.UnarmedBluntDamage,
                ["Structural"] = ent.Comp.UnarmedStructuralDamage,
            },
        };
        Dirty(ent, melee);

        var speech = EnsureComp<SpeechComponent>(ent);
        speech.SpeechSounds = ent.Comp.SpeechSounds;
        speech.AllowedEmotes.Clear();
        speech.AllowedEmotes.AddRange(ent.Comp.AllowedEmotes);
        Dirty(ent, speech);

        EnsureComp<VocalComponent>(ent);
        if (TryComp<HumanoidProfileComponent>(ent, out var humanoid) &&
            ent.Comp.VocalSounds.TryGetValue(humanoid.Sex, out var voice))
        {
            _humanoidProfile.SetVoice((ent.Owner, humanoid), voice);
        }

        GrantAllSkills(ent);
        ClearMarineHudIcon(ent);
        NormalizeAppearance(ent);
        SetFixedUnknownIdentity(ent);
        SetUnknownIdentity(ent);
        SetUnknownVoice(ent);
    }

    public override void Update(float frameTime)
    {
        if (_pendingSkinRandomization.Count == 0)
            return;

        foreach (var uid in _pendingSkinRandomization)
        {
            if (TryComp<YautjaComponent>(uid, out var yautja) &&
                TryComp<HumanoidProfileComponent>(uid, out var humanoid))
            {
                RandomizeSkinColor((uid, yautja), humanoid);
            }
        }

        _pendingSkinRandomization.Clear();
    }

    private void SetYautjaName(Entity<YautjaComponent> ent)
    {
        if (!TryComp<HumanoidProfileComponent>(ent, out var humanoid) ||
            humanoid.Species.Id != YautjaSpecies)
        {
            return;
        }

        var meta = MetaData(ent);
        if (meta.EntityPrototype != null && meta.EntityName != meta.EntityPrototype.Name)
            return;

        _metaData.SetEntityName(ent, _naming.GetName(YautjaSpecies, humanoid.Gender), meta);
    }

    private void ClearMarineHudIcon(Entity<YautjaComponent> ent)
    {
        if (!TryComp<MarineComponent>(ent, out var marine) || marine.Icon == null)
            return;

        marine.Icon = null;
        Dirty(ent.Owner, marine);
    }

    private void NormalizeAppearance(Entity<YautjaComponent> ent)
    {
        if (!_humanoidAppearance.TryGetMarkings(
                ent,
                HumanoidVisualLayers.Hair,
                out var organ,
                out _,
                out _) ||
            !ProtoMan.TryIndex<MarkingPrototype>(DreadlocksMarking, out var dreadlocksPrototype))
        {
            return;
        }

        var dreadlocks = dreadlocksPrototype.AsMarking().WithColor(DreadlocksColor) with { Forced = true };
        _humanoidAppearance.SetMarkings(ent, organ, HumanoidVisualLayers.Hair, [dreadlocks]);
    }

    private void RandomizeSkinColor(Entity<YautjaComponent> ent, HumanoidProfileComponent humanoid)
    {
        if (ent.Comp.SkinColorRandomized ||
            humanoid.Species.Id != YautjaSpecies)
        {
            return;
        }

        ent.Comp.SkinColorRandomized = true;
        if (!ent.Comp.RandomizeSkinColor)
            return;

        var hue = _random.NextFloat(ent.Comp.SkinHueMin, ent.Comp.SkinHueMax);
        var sat = _random.NextFloat(ent.Comp.SkinSaturationMin, ent.Comp.SkinSaturationMax);
        var val = _random.NextFloat(ent.Comp.SkinValueMin, ent.Comp.SkinValueMax);
        var skinColor = Color.FromHsv(new Vector4(hue, sat, val, 1f));
        _humanoidAppearance.TrySetSkinColor(ent, skinColor);
    }

    private void GrantAllSkills(Entity<YautjaComponent> ent)
    {
        if (ent.Comp.SkillLevel <= 0)
            return;

        TryComp(ent, out SkillsComponent? skills);
        var toGrant = new Dictionary<EntProtoId<SkillDefinitionComponent>, int>();
        foreach (var skill in _skills.Skills)
        {
            if (skills?.Skills.GetValueOrDefault(skill) >= ent.Comp.SkillLevel)
                continue;

            toGrant[skill] = ent.Comp.SkillLevel;
        }

        if (toGrant.Count == 0)
            return;

        _skills.SetSkills(ent.Owner, toGrant);
    }

    private void SetUnknownIdentity(Entity<YautjaComponent> ent)
    {
        if (!TryComp<IdentityComponent>(ent, out var identity) ||
            identity.IdentityEntitySlot is not { } identitySlot ||
            identitySlot.ContainedEntity is not { } identityEntity)
        {
            return;
        }

        SetUnknownIdentity(ent, identityEntity);
    }

    private void SetFixedUnknownIdentity(Entity<YautjaComponent> ent)
    {
        var fixedIdentity = EnsureComp<FixedIdentityComponent>(ent);
        fixedIdentity.Name = ent.Comp.IdentityName;
        fixedIdentity.Whitelist = new EntityWhitelist { RequireAll = true };
        Dirty(ent, fixedIdentity);
    }

    private void SetUnknownIdentity(Entity<YautjaComponent> ent, EntityUid identityEntity)
    {
        if (Deleted(identityEntity))
            return;

        var name = Loc.GetString(ent.Comp.IdentityName);
        if (MetaData(identityEntity).EntityName == name)
            return;

        _metaData.SetEntityName(identityEntity, name);
    }

    private void SetUnknownVoice(Entity<YautjaComponent> ent)
    {
        var voice = EnsureComp<VoiceOverrideComponent>(ent);
        voice.NameOverride = Loc.GetString(ent.Comp.IdentityName);
        voice.Enabled = true;
    }
}
