using Content.Server.Chat.Systems;
using Content.Server.Medical;
using Content.Server.Polymorph.Systems;
using Content.Shared._AU14.Abominations;
using Content.Shared._AU14.Abominations.Reagents;
using Content.Shared._RMC14.Synth;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Drunk;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Abominations;

/// <summary>
/// Abomination melee hits roll AbominationComponent.InfectionChance against
/// each humanoid hit. Once infected the victim ramps from light coughs and
/// drunkenness up to constant seizures and vomiting over CrescendoAfter
/// minutes. Any infected death polymorphs the body into a mimic and seeds
/// flesh kudzu at the corpse.
/// </summary>
public sealed partial class AbominationInfectionSystem : EntitySystem
{
    public static readonly EntProtoId FleshKudzuSource = "AU14AbominationFleshKudzuSource";
    public static readonly ProtoId<PolymorphPrototype> TurnIntoMimic = "AbominationAssimilationToMimic";
    public static readonly ProtoId<PolymorphPrototype> TurnIntoSkitter = "AbominationAssimilationToSkitter";
    public static readonly ProtoId<PolymorphPrototype> TurnIntoSpider = "AbominationAssimilationToSpider";
    public static readonly ProtoId<EmotePrototype> CoughEmote = "Cough";
    public static readonly ProtoId<EmotePrototype> ScreamEmote = "Scream";

    [Dependency] private AbominationAssimilateSystem _assimilate = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDrunkSystem _drunk = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectQuerySystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VomitSystem _vomit = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationComponent, MeleeHitEvent>(OnAbominationMeleeHit);
        SubscribeLocalEvent<AbominationInfectionComponent, MobStateChangedEvent>(OnInfectedMobStateChanged);
        SubscribeLocalEvent<ExecuteEntityEffectEvent<CauseAbominationInfection>>(OnExecuteCauseInfection);
    }

    private void OnExecuteCauseInfection(ref ExecuteEntityEffectEvent<CauseAbominationInfection> args)
    {
        var target = args.Args.TargetEntity;
        if (!IsValidInfectionTarget(target))
            return;

        ApplyInfection(target);
    }

    private void OnAbominationMeleeHit(Entity<AbominationComponent> abomination, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!IsValidInfectionTarget(hit))
                continue;
            if (!_random.Prob(abomination.Comp.InfectionChance))
                continue;

            ApplyInfection(hit);
        }
    }

    private bool IsValidInfectionTarget(EntityUid target)
    {
        if (HasComp<AbominationComponent>(target) || HasComp<AbominationInfectionComponent>(target))
            return false;
        // Disguised mimics ARE flesh underneath, but they shouldn't trigger
        // the infection ramp on themselves and get re-polymorphed into a
        // mimic that's already a mimic. Block them at the disguise marker.
        if (HasComp<AbominationMimicTransformedComponent>(target))
            return false;
        if (HasComp<SynthComponent>(target))
            return false;
        // Dead targets can't be infected — the corpse has nothing left to host.
        if (_mobState.IsDead(target))
            return false;
        // Humanoids OR tagged-infectable animals are valid.
        return HasComp<HumanoidAppearanceComponent>(target) || HasComp<AbominationInfectableComponent>(target);
    }

    public bool TryInfect(EntityUid target)
    {
        if (!IsValidInfectionTarget(target))
            return false;
        ApplyInfection(target);
        return true;
    }

    private void ApplyInfection(EntityUid target)
    {
        var now = _timing.CurTime;
        var infection = EnsureComp<AbominationInfectionComponent>(target);
        infection.InfectedAt = now;
        infection.NextTickAt = now; // apply first poison tick immediately
        infection.NextCoughAt = now + infection.CoughIntervalEarly;
        infection.NextJitterAt = now + infection.JitterIntervalEarly;
        // Heavy flat poison — enough to kill an unassisted host in ~3 minutes
        // (30 ticks × 8 Toxin = 240 damage over 3 min at 6 s/tick).
        infection.TickDamage = new DamageSpecifier();
        infection.TickDamage.DamageDict["Toxin"] = 8;
        Dirty(target, infection);
    }

    /// <summary>
    /// Once the victim has shown any symptoms, dying turns them into an
    /// abomination regardless of cause — the threat reclaims the body.
    /// Flesh kudzu is seeded at the corpse coords before polymorph swaps the
    /// entity, and the victim's identity profile is pushed into the shared
    /// mimic pool so other mimics can wear their face. Humanoids 50/50 roll
    /// between mimic and skitter; animals always turn into a spider.
    /// </summary>
    private void OnInfectedMobStateChanged(Entity<AbominationInfectionComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        // Capture coords before the polymorph deletes the body.
        var coords = _transform.GetMapCoordinates(ent.Owner);
        if (coords.MapId != default)
            Spawn(FleshKudzuSource, coords);

        if (!ent.Comp.HasShownSymptoms)
            return;

        // Snapshot the victim's identity FIRST while the original entity still
        // exists — polymorph would otherwise delete/banish it before we can
        // read its appearance + factions. Even animal victims add their
        // (prototype-keyed) profile to the pool so mimics can wear their form.
        var profile = _assimilate.BuildProfile(ent.Owner);
        _assimilate.AddProfileToAllMimics(profile);

        ProtoId<PolymorphPrototype> polymorphId;
        if (HasComp<HumanoidAppearanceComponent>(ent.Owner))
        {
            // 50/50 — sometimes the host body collapses into a builder caste
            // (skitter) instead of yet another mimic. Keeps the threat from
            // being a pure mimic-snowball.
            polymorphId = _random.Prob(0.5f) ? TurnIntoMimic : TurnIntoSkitter;
        }
        else
        {
            polymorphId = TurnIntoSpider;
        }
        _polymorph.PolymorphEntity(ent.Owner, polymorphId);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AbominationInfectionComponent>();
        while (query.MoveNext(out var uid, out var infection))
        {
            // Auto-cure: if the host survives long enough the infection burns itself out.
            if (now - infection.InfectedAt >= infection.CureAfter)
            {
                if (!_mobState.IsDead(uid))
                    RemComp<AbominationInfectionComponent>(uid);
                continue;
            }

            var severity = GetSeverity(infection, now);
            if (severity > 0 && !infection.HasShownSymptoms)
            {
                infection.HasShownSymptoms = true;
                Dirty(uid, infection);
            }

            if (severity >= 1f && !infection.HasCrescendoed)
            {
                infection.HasCrescendoed = true;
                _chat.TryEmoteWithChat(uid, ScreamEmote);
                Dirty(uid, infection);
            }

            // Flat poison tick — damage is not scaled by severity so it hits
            // hard from the moment of infection. Drunk scales with severity.
            if (now >= infection.NextTickAt)
            {
                infection.NextTickAt = now + infection.TickInterval;
                _damageable.TryChangeDamage(uid, infection.TickDamage, true);
                _statusEffects.TryAddStatusEffect<DrunkComponent>(uid, SharedDrunkSystem.DrunkKey, infection.DrunkPerTick * severity, true);
            }

            // Coughing — interval shrinks as severity rises.
            if (now >= infection.NextCoughAt)
            {
                var coughInterval = Lerp(infection.CoughIntervalEarly, infection.CoughIntervalLate, severity);
                infection.NextCoughAt = now + coughInterval;
                _chat.TryEmoteWithChat(uid, CoughEmote);
            }

            // Jitter — interval shrinks aggressively as severity rises so it
            // becomes near-constant near crescendo.
            if (now >= infection.NextJitterAt)
            {
                var jitterInterval = Lerp(infection.JitterIntervalEarly, infection.JitterIntervalLate, severity);
                infection.NextJitterAt = now + jitterInterval;
                var burst = TimeSpan.FromSeconds(2 + 4 * severity);
                _jitter.DoJitter(uid, burst, refresh: true, amplitude: 6 + 16 * severity, frequency: 8 + 8 * severity);
            }

            // Vomiting only kicks in past the threshold and accelerates with severity.
            if (severity >= infection.VomitSeverityThreshold && now >= infection.NextVomitAt)
            {
                infection.NextVomitAt = now + infection.VomitInterval;
                _vomit.Vomit(uid);
            }
        }
    }

    private float GetSeverity(AbominationInfectionComponent infection, TimeSpan now)
    {
        var elapsed = (now - infection.InfectedAt).TotalSeconds;
        var total = Math.Max(1.0, infection.CrescendoAfter.TotalSeconds);
        return (float) Math.Clamp(elapsed / total, 0.0, 1.0);
    }

    private static TimeSpan Lerp(TimeSpan a, TimeSpan b, float t)
    {
        return TimeSpan.FromSeconds(a.TotalSeconds + (b.TotalSeconds - a.TotalSeconds) * t);
    }
}
