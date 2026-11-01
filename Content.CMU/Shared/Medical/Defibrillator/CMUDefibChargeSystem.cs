using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Defibrillator;

/// <summary>
/// Lets medics select a manual defibrillator's energy in joules, following the biphasic steps real units use.
/// More energy heals more on a revival shock, making it more likely to bring someone back, but burns the chest;
/// less energy is gentler and less effective. AEDs pick their energy automatically and can't be tuned.
/// </summary>
public sealed partial class CMUDefibChargeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHitLocationSystem _hitLocation = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private IGameTiming _timing = default!;

    public const int DefaultJoules = 150;

    /// <summary>
    /// At or below this energy the shock is too gentle to do any of the device's own revival damage.
    /// </summary>
    private const int NoBurnJoules = 100;

    /// <summary>
    /// How far under the death threshold an analysis wants the patient to end up before calling an energy enough.
    /// </summary>
    private static readonly FixedPoint2 AdviceMargin = 5;

    /// <summary>
    /// Selectable energies, lowest first: how much of the revival heal each delivers, the shock damage it deals
    /// (null for the device's full shock damage, and never more than that), the paddle burn it leaves on the chest,
    /// and the chance that burn is full thickness (eschar, which needs surgical debridement).
    /// </summary>
    private static readonly (int Joules, float HealMultiplier, int? ShockDamage, int Burn, float EscharChance)[] Settings =
    {
        (50, 0.4f, 0, 0, 0f),
        (70, 37f / 75f, 5, 0, 0f), // 37 of the Lifepak's 75
        (100, 0.6f, 10, 0, 0f),
        (120, 0.8f, 18, 0, 0f),
        (150, 1f, null, 0, 0f),
        (170, 1.2f, null, 5, 0.1f),
        (200, 115f / 75f, null, 10, 0.25f), // 115 of the Lifepak's 75
    };

    /// <summary>
    /// Selecting an energy takes at least basic medical training.
    /// </summary>
    private static readonly Dictionary<EntProtoId<SkillDefinitionComponent>, int> AdjustSkill = new()
    {
        ["RMCSkillMedical"] = 1,
    };

    private static readonly SoundSpecifier AdjustSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    public override void Initialize()
    {
        SubscribeLocalEvent<DefibrillatorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DefibrillatorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUDefibChargeComponent, RMCDefibrillatorDamageModifyEvent>(OnDamageModify,
            after: new[] { typeof(RMCDefibrillatorSystem) });
    }

    public int GetJoules(EntityUid defib)
    {
        return CompOrNull<CMUDefibChargeComponent>(defib)?.Joules ?? DefaultJoules;
    }

    /// <summary>
    /// The shock damage a shock from this defibrillator does at its selected energy, given its full zap damage.
    /// A device that does no shock damage (like the AED) never does any.
    /// </summary>
    public int GetZapDamage(EntityUid defib, int stockDamage)
    {
        return Settings[IndexOf(GetJoules(defib))].ShockDamage is { } damage
            ? Math.Min(damage, stockDamage)
            : stockDamage;
    }

    /// <summary>
    /// Whether the patient is in a rhythm a shock can fix: dead, but not rotten, unrevivable or blocked.
    /// </summary>
    public bool IsShockable(EntityUid target)
    {
        return _mobState.IsDead(target) &&
               !_rotting.IsRotten(target) &&
               !HasComp<UnrevivableComponent>(target) &&
               !HasComp<RMCDefibrillatorBlockedComponent>(target);
    }

    /// <summary>
    /// Analyzes the patient and picks the lowest energy expected to bring them back with this defibrillator, so
    /// they take as little burn as possible. Falls back to the highest energy when nothing is expected to work.
    /// </summary>
    public CMUDefibAdvice Analyze(EntityUid defib, EntityUid target)
    {
        if (!IsShockable(target) || !TryComp<DefibrillatorComponent>(defib, out var defibrillator))
            return new CMUDefibAdvice(false, DefaultJoules, false);

        if (!_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold))
            return new CMUDefibAdvice(true, DefaultJoules, true);

        // Rebuild the revival heal the same way the shock does, then see which setting gets under the threshold.
        var heal = new DamageSpecifier(defibrillator.ZapHeal);
        if (defibrillator.RMCZapDamage is { } groups)
        {
            foreach (var (group, amount) in groups)
            {
                heal = _rmcDamageable.DistributeDamageCached(target, group, amount, heal);
            }
        }

        var damage = _damageable.GetAllDamage(target);
        var total = damage.GetTotal();

        foreach (var setting in Settings)
        {
            var after = total + setting.Burn;
            foreach (var (type, amount) in heal.DamageDict)
            {
                if (amount < FixedPoint2.Zero)
                    after -= FixedPoint2.Min(damage.DamageDict.GetValueOrDefault(type), -amount * setting.HealMultiplier);
                else if (setting.Joules > NoBurnJoules)
                    after += amount;
            }

            if (after < threshold.Value - AdviceMargin)
                return new CMUDefibAdvice(true, setting.Joules, true);
        }

        return new CMUDefibAdvice(true, Settings[^1].Joules, false);
    }

    /// <summary>
    /// Sets the energy an analysis chose. This works on automated defibrillators too, which can't be tuned by hand.
    /// </summary>
    public void SetAdvisedJoules(EntityUid defib, int joules)
    {
        if (!HasComp<DefibrillatorComponent>(defib))
            return;

        var charge = EnsureComp<CMUDefibChargeComponent>(defib);
        if (charge.Joules == joules)
            return;

        charge.Joules = joules;
        Dirty(defib, charge);
    }

    private bool IsFixed(EntityUid defib)
    {
        return CompOrNull<CMUDefibChargeComponent>(defib)?.Fixed ?? false;
    }

    private static int IndexOf(int joules)
    {
        var index = Array.FindIndex(Settings, s => s.Joules == joules);
        return index >= 0 ? index : Array.FindIndex(Settings, s => s.Joules == DefaultJoules);
    }

    private void OnGetVerbs(Entity<DefibrillatorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || IsFixed(ent) || !_skills.HasAllSkills(args.User, AdjustSkill))
            return;

        var user = args.User;
        var index = IndexOf(GetJoules(ent));

        if (index < Settings.Length - 1)
        {
            var next = Settings[index + 1].Joules;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-defib-charge-raise", ("joules", next)),
                Priority = 2,
                Act = () => SetJoules(ent, next, user),
            });
        }

        if (index > 0)
        {
            var previous = Settings[index - 1].Joules;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-defib-charge-lower", ("joules", previous)),
                Priority = 1,
                Act = () => SetJoules(ent, previous, user),
            });
        }
    }

    /// <summary>
    /// Steps a manual defibrillator's energy up or down one setting.
    /// </summary>
    public void StepJoules(EntityUid defib, bool raise, EntityUid user)
    {
        if (IsFixed(defib) || !HasComp<DefibrillatorComponent>(defib))
            return;

        if (!_skills.HasAllSkills(user, AdjustSkill))
        {
            _popup.PopupEntity(Loc.GetString("cmu-defib-charge-no-skill"), defib, user);
            return;
        }

        var index = IndexOf(GetJoules(defib)) + (raise ? 1 : -1);
        if (index < 0 || index >= Settings.Length)
            return;

        SetJoules(defib, Settings[index].Joules, user);
    }

    private void SetJoules(EntityUid defib, int joules, EntityUid user)
    {
        var charge = EnsureComp<CMUDefibChargeComponent>(defib);
        if (charge.Fixed || charge.Joules == joules)
            return;

        charge.Joules = joules;
        Dirty(defib, charge);

        _audio.PlayPredicted(AdjustSound, defib, user);
        _popup.PopupEntity(Loc.GetString("cmu-defib-charge-set", ("joules", joules)), defib, user);
    }

    private void OnExamined(Entity<DefibrillatorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var key = IsFixed(ent) ? "cmu-defib-charge-examine-fixed" : "cmu-defib-charge-examine";
        args.PushMarkup(Loc.GetString(key, ("joules", GetJoules(ent))));
    }

    private void OnDamageModify(Entity<CMUDefibChargeComponent> ent, ref RMCDefibrillatorDamageModifyEvent args)
    {
        var joules = ent.Comp.Joules;
        if (args.Cancelled || joules == DefaultJoules)
            return;

        var setting = Settings[IndexOf(joules)];

        // Only the healing part of the shock is scaled. A low energy shock does no damage at all.
        var heal = new DamageSpecifier();
        foreach (var (type, amount) in args.Heal.DamageDict)
        {
            if (amount < FixedPoint2.Zero)
                heal.DamageDict[type] = amount * setting.HealMultiplier;
            else if (joules > NoBurnJoules)
                heal.DamageDict[type] = amount;
        }

        args.Heal = heal;

        if (setting.Burn > 0)
            BurnChest(args.Target, setting.Burn, setting.EscharChance);
    }

    /// <summary>
    /// Leaves a paddle burn on the patient's chest, dealt separately from the revival heal so it lands as a real
    /// burn wound instead of cancelling out part of the heal.
    /// </summary>
    private void BurnChest(EntityUid target, int burn, float escharChance)
    {
        if (_net.IsClient)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Heat"] = burn;

        _hitLocation.SetForcedHit(target, BodyPartType.Torso);
        _damageable.TryChangeDamage(target, damage, true);

        if (escharChance <= 0f || !_random.Prob(escharChance))
            return;

        foreach (var part in _body.GetBodyChildrenOfType(target, BodyPartType.Torso))
        {
            if (HasComp<CMUEscharComponent>(part.Id))
                break;

            var eschar = AddComp<CMUEscharComponent>(part.Id);
            eschar.AppliedAt = _timing.CurTime;
            Dirty(part.Id, eschar);
            break;
        }
    }
}
