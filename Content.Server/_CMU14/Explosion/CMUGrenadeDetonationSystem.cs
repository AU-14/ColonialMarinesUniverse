using Content.Server._RMC14.Trigger;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Explosion;

/// <summary>
/// CMU-owned Timed/Impact detonation selector.
///
/// The system intentionally does not duplicate HEDP, HEFA, or smoke payload behavior.
/// Both modes ultimately use the existing TriggerSystem so upstream TriggerEvent consumers
/// remain authoritative for explosions, fragmentation, smoke, audio, deletion, etc.
/// </summary>
public sealed class CMUGrenadeDetonationSystem : EntitySystem
{
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> M40GrenadeTag = "RMCGrenadeM40";

    // Impact callbacks can be raised while ThrownItemSystem is actively enumerating
    // ThrownItemComponent + PhysicsComponent. Never trigger/delete the grenade from
    // inside those callbacks; process it after the callback stack unwinds.
    private readonly Dictionary<EntityUid, EntityUid?> _pendingImpactDetonations = new();

    public override void Initialize()
    {
        base.Initialize();

        // Right-click mode selector.
        SubscribeLocalEvent<CMUGrenadeDetonationModeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);

        // BeforeUseTimerTriggerEvent is raised as a broadcast event by TriggerSystem.OnUse.
        SubscribeLocalEvent<BeforeUseTimerTriggerEvent>(OnBeforeUseTimerTrigger);

        // Covers both hand priming and the existing RMC grenade-launcher timer path.
        SubscribeLocalEvent<CMUGrenadeDetonationModeComponent, ActiveTimerTriggerEvent>(OnActiveTimerTrigger);

        // Keep the best user attribution available once the grenade is actually thrown.
        SubscribeLocalEvent<CMUGrenadeDetonationModeComponent, ThrownEvent>(OnThrown);

        // True hard collision plus throw-end/ground fallback.
        SubscribeLocalEvent<CMUGrenadeDetonationModeComponent, ThrowDoHitEvent>(OnThrowDoHit);
        SubscribeLocalEvent<CMUGrenadeDetonationModeComponent, StopThrowEvent>(OnStopThrow,
            after: [typeof(RMCTriggerSystem)]);

        // Apply reduced payload properties only to the current Impact-mode trigger.
        SubscribeLocalEvent<CMUGrenadeDetonationModeComponent, GetExplosionTriggerPropertiesEvent>(OnGetExplosionProperties);
        SubscribeLocalEvent<CMUGrenadeDetonationModeComponent, GetProjectileGrenadePayloadEvent>(OnGetProjectilePayload);
        SubscribeLocalEvent<CMUGrenadeDetonationModeComponent, GetSpawnOnTriggerPrototypeEvent>(OnGetSpawnPayload);

        // Always clear CMU runtime state after any trigger path.
        SubscribeLocalEvent<CMUGrenadeDetonationModeComponent, TriggerEvent>(OnTriggered);
    }

    private void OnGetVerbs(
        Entity<CMUGrenadeDetonationModeComponent> ent,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsM40Eligible(ent) ||
            !args.CanInteract ||
            !args.CanAccess ||
            args.Hands == null)
        {
            return;
        }

        var next = ent.Comp.Mode == CMUGrenadeDetonationMode.Timed
            ? CMUGrenadeDetonationMode.Impact
            : CMUGrenadeDetonationMode.Timed;

        var nextName = GetModeName(next);
        var locked = IsModeLocked(ent);

        // Do not capture the ref event argument in the verb callback.
        var user = args.User;
        var uid = ent.Owner;
        var component = ent.Comp;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-grenade-detonation-mode-toggle", ("mode", nextName)),
            Disabled = locked,
            Priority = 1,
            Act = () => TrySetMode(uid, next, user, component),
        });
    }

    /// <summary>
    /// Public CMU API for changing grenade detonation mode.
    /// Mode changes are rejected after the grenade has been armed or a timer is active.
    /// </summary>
    public bool TrySetMode(
        EntityUid uid,
        CMUGrenadeDetonationMode mode,
        EntityUid? user = null,
        CMUGrenadeDetonationModeComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) || !IsM40Eligible(uid))
            return false;

        if (IsModeLocked((uid, component)))
        {
            if (user != null)
            {
                _popup.PopupEntity(
                    Loc.GetString("cmu-grenade-detonation-mode-locked"),
                    uid,
                    user.Value);
            }

            return false;
        }

        component.Mode = mode;

        if (user != null)
        {
            _popup.PopupEntity(
                Loc.GetString(
                    "cmu-grenade-detonation-mode-set",
                    ("mode", GetModeName(mode))),
                uid,
                user.Value);
        }

        return true;
    }

    /// <summary>
    /// Prevents re-arming an already armed Impact-mode grenade.
    ///
    /// IMPORTANT: This handler intentionally does NOT start the timer for a newly
    /// primed Impact grenade. The normal TriggerSystem.OnUse flow must remain in
    /// control so every existing arming veto (including the pre-emergency/peace
    /// restriction) gets a chance to cancel BeforeUseTimerTriggerEvent.
    ///
    /// If no upstream rule cancels the use, TriggerSystem starts the normal timer
    /// and raises ActiveTimerTriggerEvent. OnActiveTimerTrigger then converts that
    /// approved timer into CMU Impact mode by removing the countdown.
    /// </summary>
    private void OnBeforeUseTimerTrigger(ref BeforeUseTimerTriggerEvent args)
    {
        if (args.Cancelled ||
            !IsM40Eligible(args.Timer) ||
            !TryComp(args.Timer, out CMUGrenadeDetonationModeComponent? mode) ||
            mode.Mode != CMUGrenadeDetonationMode.Impact)
        {
            return;
        }

        // Do not permit repeated use after an impact grenade has already been
        // approved and armed. For the first use, do nothing here and allow the
        // upstream timer pipeline (and all of its rule checks) to continue.
        if (mode.Armed)
            args.Cancelled = true;
    }

    /// <summary>
    /// Converts an APPROVED timer activation on an Impact-mode grenade into
    /// impact arming.
    ///
    /// This event is deliberately the authorization boundary for Impact mode:
    /// if the normal grenade-use pipeline is vetoed (for example by the
    /// pre-emergency/peace restriction), ActiveTimerTriggerEvent never occurs and
    /// the grenade never becomes armed.
    ///
    /// This also keeps the existing RMC grenade-launcher path working without
    /// editing RMCTriggerSystem: the launcher starts its normal timer, this handler
    /// sees ActiveTimerTriggerEvent, and CMU removes the countdown.
    /// </summary>
    private void OnActiveTimerTrigger(
        Entity<CMUGrenadeDetonationModeComponent> ent,
        ref ActiveTimerTriggerEvent args)
    {
        if (!IsM40Eligible(ent) ||
            ent.Comp.Mode != CMUGrenadeDetonationMode.Impact)
        {
            return;
        }

        if (!ent.Comp.Armed)
        {
            ent.Comp.Armed = true;
            ent.Comp.ArmedBy = args.User;
        }
        else if (ent.Comp.ArmedBy == null && args.User != null)
        {
            ent.Comp.ArmedBy = args.User;
        }

        // Deferred removal lets all ActiveTimerTriggerEvent subscribers observe
        // the same normal priming event/component before the countdown disappears.
        RemCompDeferred<ActiveTimerTriggerComponent>(ent);
    }

    private void OnThrown(
        Entity<CMUGrenadeDetonationModeComponent> ent,
        ref ThrownEvent args)
    {
        if (!IsM40Eligible(ent) ||
            ent.Comp.Mode != CMUGrenadeDetonationMode.Impact ||
            !ent.Comp.Armed ||
            args.User == null)
        {
            return;
        }

        ent.Comp.ArmedBy = args.User;
    }

    private void OnThrowDoHit(
        Entity<CMUGrenadeDetonationModeComponent> ent,
        ref ThrowDoHitEvent args)
    {
        if (!IsM40Eligible(ent) ||
            ent.Comp.Mode != CMUGrenadeDetonationMode.Impact ||
            !ent.Comp.Armed)
        {
            return;
        }

        QueueImpactDetonation(ent, ent.Comp.ArmedBy ?? args.Component.Thrower);
    }

    private void OnStopThrow(
        Entity<CMUGrenadeDetonationModeComponent> ent,
        ref StopThrowEvent args)
    {
        if (!IsM40Eligible(ent) ||
            ent.Comp.Mode != CMUGrenadeDetonationMode.Impact ||
            !ent.Comp.Armed)
        {
            return;
        }

        // StopThrowEvent is raised while ThrownItemSystem may still be inside its
        // EntityQueryEnumerator. Queue the trigger instead of detonating inline.
        QueueImpactDetonation(ent, ent.Comp.ArmedBy);
    }

    private void QueueImpactDetonation(
        Entity<CMUGrenadeDetonationModeComponent> ent,
        EntityUid? user)
    {
        if (!IsM40Eligible(ent) ||
            ent.Comp.Mode != CMUGrenadeDetonationMode.Impact ||
            !ent.Comp.Armed ||
            TerminatingOrDeleted(ent))
        {
            return;
        }

        // Clear immediately so ThrowDoHit -> StopThrow cannot enqueue twice.
        ent.Comp.Armed = false;
        ent.Comp.ArmedBy = null;

        _pendingImpactDetonations[ent.Owner] = user;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingImpactDetonations.Count == 0)
            return;

        // Snapshot + clear first. Trigger() can raise more events or delete entities.
        var pending = new List<KeyValuePair<EntityUid, EntityUid?>>(_pendingImpactDetonations);
        _pendingImpactDetonations.Clear();

        foreach (var entry in pending)
        {
            var uid = entry.Key;
            var user = entry.Value;

            if (TerminatingOrDeleted(uid) ||
                !IsM40Eligible(uid) ||
                !TryComp(uid, out CMUGrenadeDetonationModeComponent? mode) ||
                mode.Mode != CMUGrenadeDetonationMode.Impact)
            {
                continue;
            }

            // The launcher/hand priming path should already have removed this
            // deferred, but make certain no countdown can fire after impact.
            RemComp<ActiveTimerTriggerComponent>(uid);

            _trigger.Trigger(uid, user);
        }
    }

    private void OnTriggered(
        Entity<CMUGrenadeDetonationModeComponent> ent,
        ref TriggerEvent args)
    {
        ent.Comp.Armed = false;
        ent.Comp.ArmedBy = null;
    }

    private void OnGetExplosionProperties(
        Entity<CMUGrenadeDetonationModeComponent> ent,
        ref GetExplosionTriggerPropertiesEvent args)
    {
        if (!IsImpactPayload(ent))
            return;

        var multiplier = GetImpactPayloadMultiplier(ent.Comp);
        args.TotalIntensity *= multiplier;
        args.MaxIntensity *= multiplier;
    }

    private void OnGetProjectilePayload(
        Entity<CMUGrenadeDetonationModeComponent> ent,
        ref GetProjectileGrenadePayloadEvent args)
    {
        if (!IsImpactPayload(ent))
            return;

        var multiplier = GetImpactPayloadMultiplier(ent.Comp);
        args.DamageMultiplier *= multiplier;

        if (args.Count > 0)
            args.Count = Math.Max(1, (int) MathF.Floor(args.Count * multiplier));
    }

    private void OnGetSpawnPayload(
        Entity<CMUGrenadeDetonationModeComponent> ent,
        ref GetSpawnOnTriggerPrototypeEvent args)
    {
        if (!IsImpactPayload(ent) || ent.Comp.ImpactSpawn is not { } impactSpawn)
            return;

        args.Prototype = impactSpawn;
    }

    private bool IsImpactPayload(Entity<CMUGrenadeDetonationModeComponent> ent)
    {
        return IsM40Eligible(ent) && ent.Comp.Mode == CMUGrenadeDetonationMode.Impact;
    }

    private static float GetImpactPayloadMultiplier(CMUGrenadeDetonationModeComponent component)
    {
        return Math.Clamp(component.ImpactPayloadMultiplier, 0f, 1f);
    }

    /// <summary>
    /// Selectable detonation is restricted to the RMC M40 / 30mm grenade family.
    /// This remains authoritative even if the CMU component is inherited by a
    /// legacy grenade prototype such as an M15 or M12.
    /// </summary>
    private bool IsM40Eligible(EntityUid uid)
    {
        return _tag.HasTag(uid, M40GrenadeTag);
    }

    private bool IsM40Eligible(Entity<CMUGrenadeDetonationModeComponent> ent)
    {
        return IsM40Eligible(ent.Owner);
    }

    private bool IsModeLocked(Entity<CMUGrenadeDetonationModeComponent> ent)
    {
        return ent.Comp.Armed || HasComp<ActiveTimerTriggerComponent>(ent);
    }

    private string GetModeName(CMUGrenadeDetonationMode mode)
    {
        return mode switch
        {
            CMUGrenadeDetonationMode.Timed =>
                Loc.GetString("cmu-grenade-detonation-mode-timed"),
            CMUGrenadeDetonationMode.Impact =>
                Loc.GetString("cmu-grenade-detonation-mode-impact"),
            _ => mode.ToString(),
        };
    }
}
