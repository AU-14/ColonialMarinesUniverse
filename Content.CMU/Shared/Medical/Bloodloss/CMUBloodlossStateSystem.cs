using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.CrawlState;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Bloodloss;

/// <summary>
/// Severe blood loss: below <see cref="UnconsciousBloodLevel"/> a human passes out into crit (and can still crawl),
/// and below <see cref="FatalBloodLevel"/> they die.
/// </summary>
public sealed partial class CMUBloodlossStateSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedCrawlWhileCritSystem _crawl = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    public const float UnconsciousBloodLevel = 0.40f;
    public const float FatalBloodLevel = 0.10f;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private static readonly DamageSpecifier FatalDamage = new() { DamageDict = { ["Asphyxiation"] = FixedPoint2.New(1) } };

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBloodlossUnconsciousComponent, UpdateMobStateEvent>(OnUpdateMobState,
            after: new[] { typeof(MobThresholdSystem) });
    }

    private void OnUpdateMobState(Entity<CMUBloodlossUnconsciousComponent> ent, ref UpdateMobStateEvent args)
    {
        if (args.State == MobState.Alive)
            args.State = MobState.Critical;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<CMUHumanMedicalComponent, BloodstreamComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var blood, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead || HasComp<SynthComponent>(uid))
            {
                SetUnconscious(uid, false);
                continue;
            }

            var level = _bloodstream.GetBloodLevel((uid, blood));
            if (level < FatalBloodLevel)
            {
                SetUnconscious(uid, false);
                KillFromBloodloss(uid);
                continue;
            }

            SetUnconscious(uid, level < UnconsciousBloodLevel);
        }
    }

    private void SetUnconscious(EntityUid uid, bool unconscious)
    {
        if (unconscious == HasComp<CMUBloodlossUnconsciousComponent>(uid))
            return;

        if (unconscious)
            AddComp<CMUBloodlossUnconsciousComponent>(uid);
        else
            RemComp<CMUBloodlossUnconsciousComponent>(uid);

        _mobState.UpdateMobState(uid);
        if (unconscious)
            _crawl.TryStartBloodlossCrawl(uid);
    }

    /// <summary>Deals just enough damage to cross the death threshold, so death and revival work as usual.</summary>
    private void KillFromBloodloss(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable) ||
            !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out var deadThreshold))
        {
            return;
        }

        var missing = deadThreshold.Value - _damageable.GetTotalDamage((uid, damageable));
        if (missing < FixedPoint2.Zero)
            missing = FixedPoint2.Zero;

        _damageable.TryChangeDamage(uid, FatalDamage * (missing + 1), ignoreResistances: true,
            interruptsDoAfters: false, damageable: damageable, ignoreGlobalModifiers: true);
    }
}
