using System.Linq;
using Content.Server.Chat;
using Content.Server.Chat.Systems;
using Content.Server.Polymorph.Systems;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.Popups;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using AbominationAssimilationProfile = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationAssimilationProfile;
using AbominationComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationComponent;
using AbominationInfectableComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationInfectableComponent;
using AbominationInfectionComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationInfectionComponent;
using AbominationMimicTransformedComponent
    = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationMimicTransformedComponent;
using CauseAbominationInfection = Content.Shared._CMU14.Threats.Mobs.Abomination.Reagents.CauseAbominationInfection;
using CureAbominationInfection = Content.Shared._CMU14.Threats.Mobs.Abomination.Reagents.CureAbominationInfection;

namespace Content.Server._CMU14.Threats.Mobs.Abomination;

/// <summary>
///     Abomination melee hits roll AbominationComponent.InfectionChance against
///     each humanoid hit. The infection is silent: no cough, no jitter, no
///     vomit, no drunkenness, no scream. It still kills quietly — a flat poison
///     tick drains the host — but gives no visible warning. Any death while
///     infected, regardless of cause, polymorphs the body into an abomination
///     and seeds flesh kudzu at the corpse. The fear comes from the not
///     knowing: the colony falls to its own paranoia, not to a visible disease.
/// </summary>
public sealed partial class AbominationInfectionSystem : EntitySystem
{
    [Dependency] private AbominationAssimilateSystem _assimilate = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EmoteOnDamageSystem _emoteOnDamage = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    public static readonly EntProtoId FleshKudzuSource = "AU14AbominationFleshKudzuSource";
    public static readonly ProtoId<PolymorphPrototype> TurnIntoMimic = "AbominationAssimilationToMimic";
    public static readonly ProtoId<PolymorphPrototype> TurnIntoSkitter = "AbominationAssimilationToSkitter";
    public static readonly ProtoId<PolymorphPrototype> TurnIntoSpider = "AbominationAssimilationToSpider";
    public const string HumanScreamEmote = "Scream";

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationComponent, MeleeHitEvent>(OnAbominationMeleeHit);
        SubscribeLocalEvent<AbominationInfectionComponent, MobStateChangedEvent>(OnInfectedMobStateChanged);
        SubscribeLocalEvent<ExecuteEntityEffectEvent<CauseAbominationInfection>>(OnExecuteCauseInfection);
        SubscribeLocalEvent<ExecuteEntityEffectEvent<CureAbominationInfection>>(OnExecuteCureInfection);
        SubscribeLocalEvent<BodyPartSeveredEvent>(OnBodyPartSevered);
    }

    public override void Update(float frameTime)
    {
        TimeSpan now = _timing.CurTime;
        EntityQueryEnumerator<AbominationInfectionComponent> query
            = EntityQueryEnumerator<AbominationInfectionComponent>();
        while (query.MoveNext(out EntityUid uid, out AbominationInfectionComponent? infection))
        {
            // Silent poison tick — no emote, no popup, just a quiet health
            // drain that eventually kills the host. Nobody watching can tell
            // who is infected; the first visible sign is the corpse turning.
            if (now >= infection.NextTickAt)
            {
                infection.NextTickAt = now + infection.TickInterval;

                // Suppress the automatic damage-scream for this tick only.
                // The poison must stay silent, but a real hit (shot/stab)
                // should still make the host scream normally. Only restore the
                // emote if we actually removed it, so we never invent a scream
                // for a mob that never had one.
                bool removedScream = false;
                if (TryComp<EmoteOnDamageComponent>(uid, out var emoteOnDamage))
                    removedScream = _emoteOnDamage.RemoveEmote(uid, HumanScreamEmote, emoteOnDamage, false);

                _damageable.TryChangeDamage(uid, infection.TickDamage, true);

                if (!HasComp<AbominationInfectionComponent>(uid))
                    continue;

                if (removedScream)
                    _emoteOnDamage.AddEmote(uid, HumanScreamEmote, emoteOnDamage);

                if (now - infection.InfectedAt >= infection.AmputationWindow)
                {
                    infection.TickDamage.DamageDict["Poison"] += infection.PostWindowTickDamageGain;
                    Dirty(uid, infection);
                }
            }
        }
    }

    private void OnExecuteCauseInfection(ref ExecuteEntityEffectEvent<CauseAbominationInfection> args)
    {
        EntityUid target = args.Args.TargetEntity;

        if (!IsValidInfectionTarget(target))
            return;

        ApplyInfection(target);
    }

    private void OnExecuteCureInfection(ref ExecuteEntityEffectEvent<CureAbominationInfection> args)
    {
        EntityUid target = args.Args.TargetEntity;

        if (!RemComp<AbominationInfectionComponent>(target))
            return;

        _popup.PopupEntity(Loc.GetString("abomination-infection-cured-counteragent"), target, target);
    }

    private void OnBodyPartSevered(ref BodyPartSeveredEvent args)
    {
        if (!TryComp<AbominationInfectionComponent>(args.Body, out var infection)
            || _timing.CurTime - infection.InfectedAt >= infection.AmputationWindow)
            return;

        // GetBodyPartChildren includes the severed part itself, so this cures
        // both a direct hit and the chain case — a hand anchor dies with the
        // arm it hangs from, whichever way that arm comes off
        if (infection.AnchoredPart is not { } anchored
            || !_body.GetBodyPartChildren(args.Part).Any(p => p.Id == anchored))
            return;

        RemComp<AbominationInfectionComponent>(args.Body);
        _popup.PopupEntity(Loc.GetString("abomination-infection-cured-amputation"), args.Body, args.Body);
    }

    private void OnAbominationMeleeHit(Entity<AbominationComponent> abomination, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (EntityUid hit in args.HitEntities)
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
        TimeSpan now = _timing.CurTime;
        var infection = EnsureComp<AbominationInfectionComponent>(target);
        infection.InfectedAt = now;
        infection.NextTickAt = now; // apply the first silent poison tick immediately

        // Flat poison until the amputation window closes (then it ramps, see
        // Update) — kills an unassisted host in ~3 minutes. RMC humans die at
        // 275 total damage (crit at 200), so 9 Poison every 6 s crosses the
        // death threshold on the 31st tick at t = 180 s. Crit hits earlier,
        // at ~2:10.
        // Must be the damage *type* "Poison" — "Toxin" is a damage *group*
        // (Poison + Radiation) and DamageableSystem only applies per-type keys.
        infection.TickDamage = new();
        infection.TickDamage.DamageDict["Poison"] = 9;
        infection.AnchoredPart = PickAnchorPart(target);
        Dirty(target, infection);
    }

    /// <summary>
    ///     Head and torso can't be anchored, and they can't be severed. Hands
    ///     and feet are included — surgical amputation only takes whole limbs,
    ///     but knives and brute damage can take the extremity alone, and a
    ///     severed arm carries its hand (and the anchor) with it. Animal
    ///     hosts may have none of these parts at all; they get no anchor and
    ///     rely on the counteragent (or just die).
    /// </summary>
    private EntityUid? PickAnchorPart(EntityUid target)
    {
        List<EntityUid> limbs = new();
        foreach (var (partUid, part) in _body.GetBodyChildren(target))
        {
            if (part.PartType is BodyPartType.Arm or BodyPartType.Hand
                or BodyPartType.Leg or BodyPartType.Foot)
                limbs.Add(partUid);
        }

        return limbs.Count == 0 ? null : _random.Pick(limbs);
    }

    /// <summary>
    ///     Once the victim dies, the threat reclaims the body regardless of
    ///     cause and regardless of how long they were infected — the infection
    ///     gives no warning beforehand, so the first sign anyone gets is the
    ///     corpse turning. Flesh kudzu is seeded at the corpse coords before
    ///     polymorph swaps the entity, and the victim's identity profile is
    ///     pushed into the shared mimic pool so other mimics can wear their
    ///     face. Humanoids 50/50 roll between mimic and skitter; animals always
    ///     turn into a spider.
    /// </summary>
    private void OnInfectedMobStateChanged(Entity<AbominationInfectionComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        // Capture coords before the polymorph deletes the body.
        MapCoordinates coords = _transform.GetMapCoordinates(ent.Owner);
        if (coords.MapId != default(MapId))
            Spawn(FleshKudzuSource, coords);

        // Snapshot the victim's identity FIRST while the original entity still
        // exists — polymorph would otherwise delete/banish it before we can
        // read its appearance + factions. Even animal victims add their
        // (prototype-keyed) profile to the pool so mimics can wear their form.
        AbominationAssimilationProfile profile = _assimilate.BuildProfile(ent.Owner);
        _assimilate.AddProfileToAllMimics(profile);

        ProtoId<PolymorphPrototype> polymorphId;
        if (HasComp<HumanoidAppearanceComponent>(ent.Owner))
        {
            // 50/50 — sometimes the host body collapses into a builder caste
            // (skitter) instead of yet another mimic. Keeps the threat from
            // being a pure mimic-snowball.
            polymorphId = _random.Prob(0.5f)
                ? TurnIntoMimic
                : TurnIntoSkitter;
        }
        else
            polymorphId = TurnIntoSpider;

        _polymorph.PolymorphEntity(ent.Owner, polymorphId);

        RemComp<AbominationInfectionComponent>(ent.Owner);
    }
}
