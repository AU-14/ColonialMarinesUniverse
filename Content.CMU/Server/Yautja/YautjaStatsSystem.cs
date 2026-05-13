using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Pulling;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Server.Humanoid;
using Content.Server.Humanoid.Systems;
using Content.Server.Speech.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaStatsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly NamingSystem _naming = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;

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
    }

    private void OnRandomHumanoidSpawned(Entity<YautjaComponent> ent, ref RandomHumanoidSpawnedEvent args)
    {
        ApplyIntrinsicStats(ent);
    }

    private void OnIdentityChanged(Entity<YautjaComponent> ent, ref IdentityChangedEvent args)
    {
        SetUnknownIdentity(ent, args.IdentityEntity);
    }

    private void ApplyIntrinsicStats(Entity<YautjaComponent> ent)
    {
        var movement = EnsureComp<MovementSpeedModifierComponent>(ent);
        _movement.ChangeBaseSpeed(ent, ent.Comp.BaseWalkSpeed, ent.Comp.BaseSprintSpeed, movement.BaseAcceleration, movement);
        EnsureComp<IgnoreXenoWeedsSlowdownComponent>(ent);
        EnsureComp<ParalyzeOnPullAttemptImmuneComponent>(ent);
        EnsureComp<InfectOnPullAttemptImmuneComponent>(ent);
        EnsureComp<YautjaTrophyRecordComponent>(ent);
        RemComp<StatusIconComponent>(ent);

        var damageable = EnsureComp<DamageableComponent>(ent);
        _damageable.SetDamageModifierSetId(ent, ent.Comp.DamageModifierSet?.Id, damageable);

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

        var vocal = EnsureComp<VocalComponent>(ent);
        vocal.Sounds = new(ent.Comp.VocalSounds);
        Dirty(ent, vocal);

        GrantAllSkills(ent);
        ClearMarineHudIcon(ent);
        NormalizeAppearance(ent);
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
                TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            {
                RandomizeSkinColor((uid, yautja), humanoid);
            }
        }

        _pendingSkinRandomization.Clear();
    }

    private void SetYautjaName(Entity<YautjaComponent> ent)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid) ||
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
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        humanoid.MarkingSet.RemoveCategory(MarkingCategories.Hair);
        _humanoid.AddMarking(ent, DreadlocksMarking, DreadlocksColor, false, forced: true, humanoid);
        Dirty(ent, humanoid);
    }

    private void RandomizeSkinColor(Entity<YautjaComponent> ent, HumanoidAppearanceComponent humanoid)
    {
        if (ent.Comp.SkinColorRandomized ||
            humanoid.Species.Id != YautjaSpecies)
        {
            return;
        }

        ent.Comp.SkinColorRandomized = true;
        if (!ent.Comp.RandomizeSkinColor)
            return;

        var skinColor = new Color(_random.NextFloat(), _random.NextFloat(), _random.NextFloat(), 1f);
        _humanoid.SetSkinColor(ent, skinColor, humanoid: humanoid);
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
