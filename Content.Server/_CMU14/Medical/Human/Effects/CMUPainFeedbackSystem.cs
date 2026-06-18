using Content.Server._RMC14.Damage;
using Content.Server.Body.Systems;
using Content.Server.Speech.Components;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared._CMU14.Medical.Human.Effects.Events;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Synth;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Drunk;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Human.Effects;

public sealed partial class CMUPainFeedbackSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private CMUTemporaryBlurryVisionSystem _blur = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StatusEffectQuerySystem _status = default!;
    [Dependency] private SharedDrunkSystem _drunk = default!;

    private static readonly ProtoId<StatusEffectPrototype> Stutter = "Stutter";
    private const float SevereBlurMax = 0.49f;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(RMCDamageableSystem));
        UpdatesAfter.Add(typeof(RespiratorSystem));

        SubscribeLocalEvent<PainTierChangedEvent>(OnPainTierChanged);
        SubscribeLocalEvent<PainShockStartupEvent>(OnPainStartup);
        SubscribeLocalEvent<HumanMedicalStartupEvent>(OnHumanMedicalStartup);
        SubscribeLocalEvent<HumanMedicalShutdownEvent>(OnHumanMedicalShutdown);
        SubscribeLocalEvent<CMUPainFeedbackComponent, ComponentStartup>(OnPainFeedbackStartup);
        SubscribeLocalEvent<CMUPainFeedbackComponent, ComponentShutdown>(OnPainFeedbackShutdown);
        SubscribeLocalEvent<PainShockShutdownEvent>(OnPainShockShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_pain.IsLayerEnabled())
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPainFeedbackComponent, PainShockComponent, ActivePainFeedbackComponent>();
        while (query.MoveNext(out var uid, out var feedback, out var pain, out _))
        {
            if (!HasComp<HumanMedicalComponent>(uid))
            {
                DeactivatePainFeedback(uid, feedback);
                continue;
            }

            if ((TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead)
                || HasComp<SynthComponent>(uid))
                continue;

            if (pain.Tier < PainTier.Distressing)
            {
                DeactivatePainFeedback(uid, feedback);
                continue;
            }

            if (feedback.NextEffect > now)
                continue;

            feedback.NextEffect = now + feedback.EffectInterval;
            ApplyFeedback(uid, feedback, pain);
        }
    }

    private void OnPainTierChanged(ref PainTierChangedEvent args)
    {
        RefreshPainFeedbackActivity(args.Body);

        if (args.NewTier == PainTier.Severe && args.OldTier < PainTier.Severe)
            _stun.TryParalyze(args.Body, TimeSpan.FromSeconds(3), refresh: false);
    }

    private void OnPainStartup(ref PainShockStartupEvent args)
    {
        RefreshPainFeedbackActivity(args.Body);
    }

    private void OnHumanMedicalStartup(ref HumanMedicalStartupEvent args)
    {
        RefreshPainFeedbackActivity(args.Body);
    }

    private void OnHumanMedicalShutdown(ref HumanMedicalShutdownEvent args)
    {
        RemCompDeferred<ActivePainFeedbackComponent>(args.Body);
    }

    private void OnPainFeedbackStartup(Entity<CMUPainFeedbackComponent> ent, ref ComponentStartup args)
    {
        RefreshPainFeedbackActivity(ent.Owner);
    }

    private void OnPainFeedbackShutdown(Entity<CMUPainFeedbackComponent> ent, ref ComponentShutdown args)
    {
        RemCompDeferred<ActivePainFeedbackComponent>(ent.Owner);
    }

    private void OnPainShockShutdown(ref PainShockShutdownEvent args)
    {
        RemCompDeferred<ActivePainFeedbackComponent>(args.Body);
    }

    private void RefreshPainFeedbackActivity(EntityUid uid)
    {
        if (!TryComp<CMUPainFeedbackComponent>(uid, out var feedback))
        {
            RemCompDeferred<ActivePainFeedbackComponent>(uid);
            return;
        }

        if (!HasComp<HumanMedicalComponent>(uid)
            || !TryComp<PainShockComponent>(uid, out var pain)
            || pain.Tier < PainTier.Distressing)
        {
            DeactivatePainFeedback(uid, feedback);
            return;
        }

        EnsureComp<ActivePainFeedbackComponent>(uid);
    }

    private void DeactivatePainFeedback(EntityUid uid, CMUPainFeedbackComponent feedback)
    {
        feedback.NextEffect = TimeSpan.Zero;
        RemCompDeferred<ActivePainFeedbackComponent>(uid);
    }

    private void ApplyFeedback(EntityUid uid, CMUPainFeedbackComponent feedback, PainShockComponent pain)
    {
        var tier = pain.Tier;
        if (tier < PainTier.Distressing)
            return;

        ApplyTemporaryBlur(
            uid,
            GetBlurDuration(feedback, tier),
            GetBlurAmount(feedback, pain));

        ApplyTimedStatus<StutteringAccentComponent>(
            uid,
            Stutter,
            GetStutterDuration(feedback, tier));

        if (tier < PainTier.Severe)
            return;

        ApplyDrunkenness(
            uid,
            GetDrunkPower(feedback, tier),
            slur: true);

        ApplyAsphyxiation(
            uid,
            GetAsphyxiation(feedback, tier));

        TryPainEmote(
            uid,
            GetEmoteChance(feedback, tier),
            GetEmotes(feedback, tier));

        if (tier >= PainTier.Horrible)
            _stun.TryParalyze(uid, TimeSpan.FromSeconds(2), refresh: true);
    }

    private TimeSpan GetBlurDuration(CMUPainFeedbackComponent feedback, PainTier tier)
    {
        return tier switch
        {
            PainTier.Distressing => feedback.SevereBlurDuration,
            PainTier.Severe => feedback.SevereBlurDuration,
            PainTier.Shock => feedback.ShockBlurDuration,
            _ => TimeSpan.Zero,
        };
    }

    private TimeSpan GetStutterDuration(CMUPainFeedbackComponent feedback, PainTier tier)
    {
        return tier >= PainTier.Horrible
            ? feedback.ShockStutterDuration
            : feedback.SevereStutterDuration;
    }

    private float GetDrunkPower(CMUPainFeedbackComponent feedback, PainTier tier)
    {
        return tier >= PainTier.Horrible
            ? feedback.ShockDrunkPower
            : feedback.SevereDrunkPower;
    }

    private FixedPoint2 GetAsphyxiation(CMUPainFeedbackComponent feedback, PainTier tier)
    {
        return tier >= PainTier.Horrible
            ? feedback.ShockAsphyxiation
            : feedback.SevereAsphyxiation;
    }

    private float GetEmoteChance(CMUPainFeedbackComponent feedback, PainTier tier)
    {
        return tier >= PainTier.Horrible
            ? feedback.ShockEmoteChance
            : feedback.SevereEmoteChance;
    }

    private IReadOnlyList<ProtoId<EmotePrototype>> GetEmotes(CMUPainFeedbackComponent feedback, PainTier tier)
    {
        return tier >= PainTier.Horrible
            ? feedback.ShockEmotes
            : feedback.SevereEmotes;
    }

    private float GetBlurAmount(CMUPainFeedbackComponent feedback, PainShockComponent pain)
    {
        var value = pain.Pain.Float();
        var severe = PainTierThresholds.UpwardThresholds[(int) PainTier.Distressing - 1].Float();
        var shock = _pain.ShockThreshold.Float();

        if (value < severe)
            return 0f;

        if (value < shock)
            return GetSevereBlurAmount(feedback, severe, shock, feedback.SevereBlurEquivalentPain);

        return GetSevereBlurAmount(feedback, severe, shock, feedback.ShockBlurEquivalentPain);
    }

    private static float GetSevereBlurAmount(
        CMUPainFeedbackComponent feedback,
        float severe,
        float shock,
        float value)
    {
        var severeAmount = Math.Min(feedback.SevereBlurAmount, SevereBlurMax);
        return Lerp(feedback.SevereBlurStartAmount, severeAmount, InverseLerp(severe, shock, value));
    }

    private static float InverseLerp(float from, float to, float value)
    {
        if (to <= from)
            return 1f;

        return Math.Clamp((value - from) / (to - from), 0f, 1f);
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private void ApplyTemporaryBlur(EntityUid uid, TimeSpan duration, float amount)
    {
        _blur.AddTemporaryBlurModifier(uid, duration, amount);
    }

    private void ApplyDrunkenness(EntityUid uid, float power, bool slur)
    {
        if (power <= 0f)
            return;

        var targetDuration = TimeSpan.FromSeconds(power);
        if (_status.TryGetTime(uid, SharedDrunkSystem.DrunkKey, out var time) &&
            time.Value.Item2 - _timing.CurTime >= targetDuration)
        {
            return;
        }

        _drunk.TryApplyDrunkenness(uid, power, slur);
    }

    private void ApplyTimedStatus<T>(
        EntityUid uid,
        ProtoId<StatusEffectPrototype> status,
        TimeSpan duration)
        where T : IComponent, new()
    {
        if (duration <= TimeSpan.Zero)
            return;

        _status.TryAddStatusEffect<T>(uid, status, duration, refresh: true);
    }

    private void ApplyAsphyxiation(EntityUid uid, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Asphyxiation"] = amount;
        _damage.TryChangeDamage(uid, damage, ignoreResistances: true, interruptsDoAfters: false);
    }

    private void TryPainEmote(
        EntityUid uid,
        float chance,
        IReadOnlyList<ProtoId<EmotePrototype>> emotes)
    {
        if (chance <= 0f || emotes.Count == 0 || !_random.Prob(chance))
            return;

        var emote = _random.Pick(emotes);
        if (!_prototypes.HasIndex<EmotePrototype>(emote))
            return;

        _emote.TryEmoteWithChat(uid, emote, forceEmote: true, cooldown: TimeSpan.Zero);
    }
}
