using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Alert;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Traits.PanicProne;

public abstract partial class PanicSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PanicComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<PanicComponent, WoundAddedEvent>(OnWoundAdded);
        SubscribeLocalEvent<PanicComponent, ExplosionReceivedEvent>(OnExplosionReceived,
            after: new[] { typeof(SharedRMCExplosionSystem) });
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnStartup(Entity<PanicComponent> ent, ref ComponentStartup args)
    {
        SetAlert(ent);
    }

    private void OnProjectileHit(ref ProjectileHitEvent args)
    {
        if (TryComp<PanicComponent>(args.Target, out var panic))
            AddPanic((args.Target, panic), panic.ProjectileHitGain);
    }

    private void OnWoundAdded(Entity<PanicComponent> ent, ref WoundAddedEvent args)
    {
        if (args.Total >= ent.Comp.SeriousWoundThreshold)
            AddPanic((ent.Owner, ent.Comp), ent.Comp.SeriousWoundGain);
    }

    private void OnExplosionReceived(Entity<PanicComponent> ent, ref ExplosionReceivedEvent args)
    {
        if (HasComp<KnockedDownComponent>(ent))
            AddPanic((ent.Owner, ent.Comp), ent.Comp.ExplosionKnockdownGain);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var query = EntityQueryEnumerator<PanicComponent>();
        while (query.MoveNext(out var uid, out var panic))
        {
            if (uid == args.Target)
                continue;

            if (!IsNearbyAndVisible(uid, args.Target, panic.NearbyDeathRadius))
                continue;

            AddPanic((uid, panic), panic.NearbyDeathGain);
        }
    }

    private bool IsNearbyAndVisible(EntityUid observer, EntityUid dead, float radius)
    {
        var nearby = _lookup.GetEntitiesInRange(dead, radius, LookupFlags.Dynamic);
        if (!nearby.Contains(observer))
            return false;

        return _interaction.InRangeUnobstructed(dead, observer, radius);
    }

    public void AddPanic(Entity<PanicComponent?> ent, double amount)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.Current = Math.Clamp(ent.Comp.Current + amount, 0, ent.Comp.Max);
        Dirty(ent);
        ProcessEffects((ent, ent.Comp));
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<PanicComponent>();
        while (query.MoveNext(out var uid, out var panic))
        {
            if (time < panic.NextCheck)
                continue;

            panic.NextCheck = time + panic.TimeBetweenChecks;

            if (panic.Current > 0)
            {
                panic.Current = Math.Clamp(panic.Current - panic.RegenPerTick, 0, panic.Max);
                Dirty(uid, panic);
            }

            ProcessEffects((uid, panic));
        }
    }

    private void ProcessEffects(Entity<PanicComponent> ent)
    {
        var peaked = ent.Comp.Current >= ent.Comp.PeakThreshold;
        if (peaked != ent.Comp.Peaked)
        {
            ent.Comp.Peaked = peaked;
            Dirty(ent);

            if (peaked)
            {
                var aim = EnsureComp<PanicAimComponent>(ent.Owner);
                aim.SpreadMultiplier = ent.Comp.SpreadMultiplier;
                Dirty(ent.Owner, aim);
            }
            else
            {
                RemComp<PanicAimComponent>(ent.Owner);
            }

            if (!_net.IsClient)
                RefreshAimDependentWeapons(ent.Owner);

            _popup.PopupEntity(
                Loc.GetString(peaked ? "au14-panicprone-peak" : "au14-panicprone-calming"),
                ent.Owner, ent.Owner, peaked ? PopupType.MediumCaution : PopupType.Medium);
        }
        else if (!ent.Comp.Peaked && ent.Comp.Current >= ent.Comp.WarnThreshold && _timing.CurTime >= ent.Comp.NextWarnMessage)
        {
            ent.Comp.NextWarnMessage = _timing.CurTime + ent.Comp.WarnMessageCooldown;
            _popup.PopupEntity(Loc.GetString("au14-panicprone-warn"), ent.Owner, ent.Owner, PopupType.Small);
        }

        if (ent.Comp.Current >= ent.Comp.EmoteThreshold && _timing.CurTime >= ent.Comp.NextEmote)
        {
            ent.Comp.NextEmote = _timing.CurTime + ent.Comp.EmoteCooldown;
            _emote.TryEmoteWithChat(ent.Owner, ent.Comp.Emote, hideLog: true, ignoreActionBlocker: true, forceEmote: true);
        }

        SetAlert(ent);
    }

    /// <summary>
    ///     Ensures whichever weapon the entity currently holds reflects its current panic state.
    ///     Overridden server-side to track hand-held guns; a no-op on the client.
    /// </summary>
    protected virtual void RefreshAimDependentWeapons(EntityUid body)
    {
    }

    private void SetAlert(Entity<PanicComponent> ent)
    {
        if (ent.Comp.Current > 0)
            _alerts.ShowAlert(ent.Owner, ent.Comp.PanicAlert, ent.Comp.Peaked ? (short)1 : (short)0);
        else
            _alerts.ClearAlert(ent.Owner, ent.Comp.PanicAlert);
    }
}
