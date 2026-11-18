using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Slow;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-703: the Berserker's Blood Rage. Usable under half health: for RageDuration the Berserker hits harder
/// with Bad Blood melee (BadBloodMeleeSystem reads <see cref="BadBloodBloodRageComponent"/>), shrugs off slows and
/// stuns, cannot cloak, and counts double toward seizing a Hunt Ground. D16: a light alone is not enough, so the rage
/// starts with a roar everyone in range hears, a popup everyone in view sees, a red light and a networked red tint.
/// </summary>
public sealed class BadBloodBloodRageSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private YautjaCloakSystem _cloak = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<EntityUid> _ended = new();

    /// <summary>
    /// How many Blood Rages started with a roar. Instrumentation for the tests.
    /// </summary>
    public int RoarsPlayed { get; private set; }

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodBerserkerComponent, BadBloodBloodRageActionEvent>(OnRageAction);
        SubscribeLocalEvent<BadBloodBerserkerComponent, BadBloodContestWeightEvent>(OnContestWeight);
        SubscribeLocalEvent<BadBloodBloodRageComponent, GetChemicalStunTimeMultiplierEvent>(OnStunTime);
        SubscribeLocalEvent<BadBloodBloodRageComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BadBloodBloodRageComponent, ComponentShutdown>(OnRageShutdown);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        _ended.Clear();
        var query = EntityQueryEnumerator<BadBloodBloodRageComponent>();
        while (query.MoveNext(out var uid, out var rage))
        {
            if (now >= rage.EndsAt)
                _ended.Add(uid);
        }

        foreach (var uid in _ended)
        {
            EndRage(uid, popup: true);
        }
    }

    /// <summary>
    /// Why <paramref name="berserker"/> cannot rage now, as a loc key, or null when they can. The action's own
    /// cooldown is not looked at here.
    /// </summary>
    public string? GetRageDenial(Entity<BadBloodBerserkerComponent> berserker)
    {
        if (!_mobState.IsAlive(berserker))
            return "cmu-bad-blood-rage-not-alive";

        if (HasComp<BadBloodBloodRageComponent>(berserker))
            return "cmu-bad-blood-rage-already";

        if (!_thresholds.TryGetThresholdForState(berserker, MobState.Critical, out var crit) || crit.Value <= FixedPoint2.Zero)
            return "cmu-bad-blood-rage-healthy";

        var damage = _damageable.GetTotalDamage(berserker.Owner);
        return damage.Float() < crit.Value.Float() * berserker.Comp.RageHealthThreshold
            ? "cmu-bad-blood-rage-healthy"
            : null;
    }

    /// <summary>
    /// Starts a Blood Rage on <paramref name="berserker"/>, telling them why when it is refused.
    /// </summary>
    public bool TryStartRage(Entity<BadBloodBerserkerComponent> berserker)
    {
        if (GetRageDenial(berserker) is { } denial)
        {
            _popup.PopupEntity(Loc.GetString(denial), berserker, berserker, PopupType.SmallCaution);
            return false;
        }

        var uid = berserker.Owner;
        var rage = EnsureComp<BadBloodBloodRageComponent>(uid);
        rage.EndsAt = _timing.CurTime + berserker.Comp.RageDuration;

        // a slow already on the body falls away; new ones do not take while the rage lasts
        RemComp<RMCSlowdownComponent>(uid);
        RemComp<RMCSuperSlowdownComponent>(uid);

        // no cloak while raging: drop it now, and hold the combat lockout to the rage's end
        _cloak.DecloakFromAttack(uid);
        if (_power.TryGetWornBracer(uid, out var bracer) && bracer.Comp.CloakCombatLockoutUntil < rage.EndsAt)
        {
            bracer.Comp.CloakCombatLockoutUntil = rage.EndsAt;
            Dirty(bracer);
        }

        rage.AddedLight = !_pointLight.TryGetLight(uid, out _);
        var light = _pointLight.EnsureLight(uid);
        _pointLight.SetColor(uid, berserker.Comp.RageLightColor, light);
        _pointLight.SetRadius(uid, berserker.Comp.RageLightRadius, light);
        _pointLight.SetEnabled(uid, true, light);
        Dirty(uid, rage);

        RoarsPlayed++;
        _audio.PlayPvs(berserker.Comp.RageSound, uid);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-rage-start", ("name", Identity(uid))), uid, PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(uid):player} started a Bad Blood Blood Rage");
        return true;
    }

    /// <summary>
    /// Ends a Blood Rage at once: the light it added and the tint go with the component.
    /// </summary>
    public void EndRage(EntityUid uid, bool popup)
    {
        if (!HasComp<BadBloodBloodRageComponent>(uid))
            return;

        // the light goes with the component (OnRageShutdown), however it is removed
        RemComp<BadBloodBloodRageComponent>(uid);

        if (popup)
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-rage-end"), uid, uid);
    }

    private string Identity(EntityUid uid)
    {
        return Content.Shared.IdentityManagement.Identity.Name(uid, EntityManager);
    }

    private void OnRageAction(Entity<BadBloodBerserkerComponent> ent, ref BadBloodBloodRageActionEvent args)
    {
        if (args.Handled)
            return;

        // a refused rage does not start the action's cooldown
        args.Handled = TryStartRage(ent);
    }

    private void OnContestWeight(Entity<BadBloodBerserkerComponent> ent, ref BadBloodContestWeightEvent args)
    {
        if (HasComp<BadBloodBloodRageComponent>(ent))
            args.Weight = Math.Max(args.Weight, ent.Comp.RageContestWeight);
    }

    private void OnStunTime(Entity<BadBloodBloodRageComponent> ent, ref GetChemicalStunTimeMultiplierEvent args)
    {
        args.Multiplier = 0f;
    }

    // every way a rage ends lands here, the Berserker caste being dropped mid-rage too (ARCH-704)
    private void OnRageShutdown(Entity<BadBloodBloodRageComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (ent.Comp.AddedLight)
            _pointLight.RemoveLightDeferred(ent);
        else if (_pointLight.TryGetLight(ent, out var light))
            _pointLight.SetEnabled(ent, false, light);
    }

    private void OnMobStateChanged(Entity<BadBloodBloodRageComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            EndRage(ent, popup: false);
    }
}
