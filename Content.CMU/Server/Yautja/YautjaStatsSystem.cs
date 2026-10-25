using System.Numerics;
using Content.Server.Humanoid;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Medical.Injuries;
using Content.Shared._RMC14.Commendations;
using Content.Shared._RMC14.IdentityManagement;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Eye;
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
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaStatsSystem : EntitySystem
{
    private const float YautjaFemaleChance = 0.2f;

    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    [Dependency] private YautjaProfileApplySystem _profileApply = default!;
    [Dependency] private HumanoidOrganAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private GunIFFSystem _iff = default!;
    [Dependency] private YautjaAbilitySystem _abilities = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private NamingSystem _naming = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCStatusEffectSystem _rmcStatusEffects = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private YautjaHonorboundAbilitiesSystem _honorboundAbilities = default!;
    [Dependency] private SharedCMAutomatedVendorSystem _vendors = default!;
    [Dependency] private YautjaVoiceSystem _voice = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    private const string YautjaSpecies = "Yautja";
    private const string DreadlocksMarking = "CMUYautjaDreadlocksStandard";
    private const int Cmss13TotalBuyPoints = 50;
    private static readonly Color DreadlocksColor = Color.FromHex("#1a1512");
    private static readonly Dictionary<string, int> Cmss13YautjaBuyCategoryUses = new()
    {
        ["CMUYautjaEssentials"] = 0,
        ["CMUYautjaArmor"] = 0,
        ["CMUYautjaPrimary"] = 0,
        ["CMUYautjaBracer"] = 0,
        ["CMUYautjaRanged"] = 0,
        ["CMUYautjaSupport"] = 0,
        ["CMUYautjaAccessory"] = 0,
    };

    private readonly HashSet<EntityUid> _pendingSkinRandomization = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, ComponentInit>(OnYautjaInit);
        SubscribeLocalEvent<YautjaComponent, ComponentStartup>(OnYautjaStartup);
        SubscribeLocalEvent<YautjaYoungbloodComponent, MapInitEvent>(OnYoungbloodMapInit);
        // CMU14: a role spawn adds this component after map init, so MapInit alone never fires for youngbloods - their
        // profile was never applied and every one of them rendered with the species default. ComponentStartup fires
        // whenever the component is added, which covers both spawn paths.
        SubscribeLocalEvent<YautjaYoungbloodComponent, ComponentStartup>(OnYoungbloodStartup);
        SubscribeLocalEvent<YautjaComponent, ComponentShutdown>(OnYautjaShutdown);
        SubscribeLocalEvent<YautjaComponent, MapInitEvent>(OnYautjaMapInit);
        SubscribeLocalEvent<YautjaComponent, IdentityChangedEvent>(OnIdentityChanged);
        SubscribeLocalEvent<YautjaComponent, RandomHumanoidSpawnedEvent>(OnRandomHumanoidSpawned);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnYautjaInit(Entity<YautjaComponent> ent, ref ComponentInit args)
    {
        ApplyIntrinsicStats(ent);
        InitializeVendorPoints(ent);
    }

    private void OnYautjaStartup(Entity<YautjaComponent> ent, ref ComponentStartup args)
    {
        ApplyIntrinsicStats(ent);

        // CMU14: a youngblood's own component can arrive before the Yautja/humanoid components exist, in which case its
        // handler bails and nothing retries - leaving the species default tone for every youngblood. Re-run the shared
        // profile application here, where all three components are guaranteed present.
        if (HasComp<YautjaYoungbloodComponent>(ent.Owner))
            ApplyYoungbloodProfile(ent.Owner);
        ent.Comp.SkinColorRandomized = false;
        _pendingSkinRandomization.Add(ent);
        _abilities.GrantActions(ent);
        _voice.GrantAudioPanelAction(ent);
    }

    private void OnYautjaShutdown(Entity<YautjaComponent> ent, ref ComponentShutdown args)
    {
        _abilities.RemoveActions(ent);
        _voice.RemoveAudioPanelAction(ent);
    }

    private void OnYautjaMapInit(Entity<YautjaComponent> ent, ref MapInitEvent args)
    {
        SetYautjaName(ent);
        _honorboundAbilities.GrantActions(ent);
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnRandomHumanoidSpawned(Entity<YautjaComponent> ent, ref RandomHumanoidSpawnedEvent args)
    {
        ApplyIntrinsicStats(ent);
        _honorboundAbilities.GrantActions(ent);
    }

    // CMU14: the entry message needs a live session. RandomHumanoidSpawnedEvent fires before the player is attached
    // (and mid-round antag spawns never see PlayerSpawnCompleteEvent), so delivery hangs off PlayerAttachedEvent,
    // which hands us the session directly.
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!TryComp(args.Entity, out YautjaComponent? yautja) || yautja.EntryMessageSent)
            return;

        yautja.EntryMessageSent = true;
        SendEntryMessage(args.Entity, args.Player);
    }

    // CMU14: mirrors CM-SS13's /datum/job/antag/predator/announce_entry_message (pred.dm:51-55). SPAN_NOTICE is
    // the "notice" span class (code/span_macros.dm:70), which SS14 represents as ChatChannel.Server - the system
    // message channel - not as an in-character line. Sent only to the spawning hunter's session.
    private void SendEntryMessage(EntityUid hunter, ICommonSession session)
    {
        SendEntryLine(hunter, session, "cmu-yautja-entry-1");
        SendEntryLine(hunter, session, "cmu-yautja-entry-2");
        SendEntryLine(hunter, session, "cmu-yautja-entry-3");
        SendEntryLine(hunter, session, "cmu-yautja-entry-4");
    }

    private void SendEntryLine(EntityUid hunter, ICommonSession session, string key)
    {
        var text = Loc.GetString(key);
        _chatManager.ChatMessageToOne(ChatChannel.Server, text, text, hunter, false, session.Channel);
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
        RemComp<StaminaComponent>(ent.Owner);
        RemComp<RMCStaminaComponent>(ent.Owner);

        if (ent.Comp.StunResistance > 0f)
            _rmcStatusEffects.GiveStunResistance(ent, ent.Comp.StunResistance);

        var badBlood = HasComp<YautjaBadBloodComponent>(ent);
        if (badBlood)
        {
            var slowOnDamage = EnsureComp<SlowOnDamageComponent>(ent);
            slowOnDamage.SpeedModifierThresholds = new(ent.Comp.SlowOnDamageThresholds);
            Dirty(ent, slowOnDamage);
            RemComp<CMUMedicalResilienceComponent>(ent);
        }
        else
        {
            RemComp<SlowOnDamageComponent>(ent);
            var resilience = EnsureComp<CMUMedicalResilienceComponent>(ent);
            resilience.PainAccumulationMultiplier = ent.Comp.PainAccumulationMultiplier;
            resilience.MinimumPenalizingFractureSeverity = ent.Comp.MinimumPenalizingFractureSeverity;
            resilience.MovementPenaltyFloor = ent.Comp.MedicalMovementPenaltyFloor;
            resilience.AimPenaltyCeiling = ent.Comp.MedicalAimPenaltyCeiling;
            resilience.ActionSpeedPenaltyCeiling = ent.Comp.MedicalActionSpeedPenaltyCeiling;
            Dirty(ent, resilience);
        }

        // Yautja damage modifier is in DamageableComponent.DamageModifierSetId.

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

    private void InitializeVendorPoints(Entity<YautjaComponent> ent)
    {
        var vendorUser = EnsureComp<CMVendorUserComponent>(ent);
        _vendors.SetPoints((ent.Owner, vendorUser), Cmss13TotalBuyPoints);
        _vendors.InitializeChoices((ent.Owner, vendorUser), Cmss13YautjaBuyCategoryUses);
        _vendors.SetChoiceWhitelist((ent.Owner, vendorUser), new HashSet<string>(Cmss13YautjaBuyCategoryUses.Keys));
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
        if (HasComp<YautjaAppliedProfileComponent>(ent))
            return;

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


    /// <summary>
    /// CMU14: a spawned youngblood is randomised like any hunter - CM-SS13's load_name picks gender with an 80/20
    /// male/female weight ("female hunters are rare"). Colour, name and gear all go through the same ApplyProfile path
    /// hunters use; there is deliberately no youngblood-specific colour code.
    /// </summary>
    private void OnYoungbloodStartup(Entity<YautjaYoungbloodComponent> ent, ref ComponentStartup args)
    {
        ApplyYoungbloodProfile(ent.Owner);
    }

    private void OnYoungbloodMapInit(Entity<YautjaYoungbloodComponent> ent, ref MapInitEvent args)
    {
        ApplyYoungbloodProfile(ent.Owner);
    }

    /// <summary>
    /// Replaces an existing youngblood profile's tone with a rolled CM-SS13 palette colour and re-applies it, so the
    /// renderer picks the matching pre-coloured variant. Uses the same palette as the Hunter randomiser.
    /// </summary>
    private void RollYoungbloodTone(EntityUid uid)
    {
        if (!TryComp<YautjaAppliedProfileComponent>(uid, out var applied))
            return;

        var tone = _random.Pick(YautjaCharacterProfile.SkinColorOrder[..6]);
        applied.Profile = applied.Profile.WithSkinColor(tone);
        _profileApply.ApplyProfile(uid, applied.Profile, equipProfileGear: false);
    }

    /// <summary>
    /// CMU14: rolls a CM-SS13 palette tone onto a youngblood that already has a profile (console spawns build one from the
    /// species default). Same palette call the hunter randomiser uses.
    /// </summary>
    private void ApplyYoungbloodProfile(EntityUid uid)
    {
        // No YautjaComponent requirement: CMUMobYautjaYoungblood's prototype does not carry one (it parents
        // AppearanceYautja + CMMobHuman), which is why this never ran and every youngblood kept the tan species default.
        if (!HasComp<HumanoidProfileComponent>(uid))
            return;

        // A console-spawned youngblood already carries a profile built from the species default tone (tan), so reusing it
        // unchanged gave every youngblood the same colour. Roll the shared palette onto whatever profile is there.
        if (HasComp<YautjaAppliedProfileComponent>(uid))
        {
            RollYoungbloodTone(uid);
            return;
        }

        // Reuse the same palette roll normal Hunters use: CM-SS13's load_name picks gender 80/20 and load_race picks the
        // tone from PRED_SKIN_COLOR. Choosing it here rather than relying on ApplyProfile's "unchosen" fallback means a
        // spawn-menu youngblood always gets a rolled tone instead of the profile default.
        var profile = new YautjaCharacterProfile()
            .WithSex(_random.Prob(YautjaFemaleChance) ? Sex.Female : Sex.Male)
            .WithSkinColor(_random.Pick(YautjaCharacterProfile.SkinColorOrder[..6]));
        _profileApply.ApplyProfile(uid, profile, equipProfileGear: false);
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

        // CMU14: same rule for hunters and youngbloods - CM-SS13 picks the tone from PRED_SKIN_COLOR (load_race),
        // so the HSV band that produced colours the reference does not offer is gone. The roll is written into the
        // stored profile as well when there is one, so the rendered tone and the profile cannot disagree and a rolled
        // Green is recorded as a chosen colour instead of being re-rolled as the "unchosen" sentinel later.
        var tone = _random.Pick(YautjaCharacterProfile.SkinColorOrder[..6]);
        if (TryComp<YautjaAppliedProfileComponent>(ent.Owner, out var applied))
            applied.Profile = applied.Profile.WithSkinColor(tone);

        _humanoidAppearance.TrySetSkinColor(ent.Owner, YautjaCharacterProfile.GetSkinColorColor(tone));
    }

    private void GrantAllSkills(Entity<YautjaComponent> ent)
    {
        if (ent.Comp.SkillLevel <= 0)
            return;

        if (TryComp(ent, out SkillsComponent? skills) && skills.Preset != null)
            return;

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
