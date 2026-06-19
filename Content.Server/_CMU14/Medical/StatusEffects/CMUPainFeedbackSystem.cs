using Content.Server._RMC14.Damage;
using Content.Server.Body.Systems;
using Content.Server.Speech.Components;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._CMU14.Medical.TemporaryBlurryVision;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Synth;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Drunk;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.StatusEffects;

public sealed partial class CMUPainFeedbackSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private CMUTemporaryBlurryVisionSystem _blur = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private StatusEffectQuerySystem _status = default!;
    [Dependency] private SharedDrunkSystem _drunk = default!;

    private static readonly ProtoId<StatusEffectPrototype> Stutter = "Stutter";
    private const float SevereBlurMax = 0.49f;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(RMCDamageableSystem));
        UpdatesAfter.Add(typeof(RespiratorSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_pain.IsLayerEnabled())
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPainFeedbackComponent, PainShockComponent, CMUHumanMedicalComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var feedback, out var pain, out _, out var mob))
        {
            if (mob.CurrentState == MobState.Dead || HasComp<SynthComponent>(uid))
                continue;

            if (pain.Tier < PainTier.Severe)
            {
                feedback.NextEffect = TimeSpan.Zero;
                continue;
            }

            if (feedback.NextEffect > now)
                continue;

            feedback.NextEffect = now + feedback.EffectInterval;
            ApplyFeedback(uid, feedback, pain);
        }
    }

    private void ApplyFeedback(EntityUid uid, CMUPainFeedbackComponent feedback, PainShockComponent pain)
    {
        var tier = pain.Tier;
        if (tier < PainTier.Severe)
            return;

        ApplyTemporaryBlur(
            uid,
            GetBlurDuration(feedback, tier),
            GetBlurAmount(feedback, pain));

        if (tier < PainTier.Shock)
            return;

        ApplyTimedStatus<StutteringAccentComponent>(
            uid,
            Stutter,
            feedback.ShockStutterDuration);

        ApplyDrunkenness(
            uid,
            feedback.ShockDrunkPower,
            slur: true);

        ApplyAsphyxiation(
            uid,
            feedback.ShockAsphyxiation);

        TryPainEmote(
            uid,
            feedback.ShockEmoteChance,
            feedback.ShockEmotes);
    }

    private TimeSpan GetBlurDuration(CMUPainFeedbackComponent feedback, PainTier tier)
    {
        return tier switch
        {
            PainTier.Severe => feedback.SevereBlurDuration,
            PainTier.Shock => feedback.ShockBlurDuration,
            _ => TimeSpan.Zero,
        };
    }

    private float GetBlurAmount(CMUPainFeedbackComponent feedback, PainShockComponent pain)
    {
        var value = pain.Pain.Float();
        var severe = PainTierThresholds.UpwardThresholds[(int) PainTier.Severe - 1].Float();
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
