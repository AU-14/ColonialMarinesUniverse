using System.Numerics;
using Content.Shared._RMC14.Tracker;
using Content.Shared.Alert;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Repentance (phase E4, balance A-8): the Joe remembers whoever last hurt it for a while, sees a
/// tracker arrow toward them, and the Sense action names direction and distance.
/// </summary>
public sealed partial class EWJoeRepentanceSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TrackerSystem _tracker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<EWJoeComponent, EWJoeSenseActionEvent>(OnSense);
        SubscribeLocalEvent<EWJoeRepentanceComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<EWJoeRepentanceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + TimeSpan.FromSeconds(1);
            RefreshAlert((uid, comp));
        }
    }

    private void OnDamaged(Entity<EWJoeComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || !TryComp(ent, out EWJoeRepentanceComponent? repentance))
            return;

        if (ResolveAttacker(ent, args.Origin) is not { } attacker)
            return;

        repentance.LastAttacker = attacker;
        repentance.Until = _timing.CurTime + ent.Comp.RepentanceMemory;
        repentance.NextUpdate = TimeSpan.Zero;
    }

    /// <summary>The mob behind a damage origin: the shooter of a projectile, otherwise the origin itself.</summary>
    public EntityUid? ResolveAttacker(EntityUid joe, EntityUid? origin)
    {
        if (origin is not { } uid || uid == joe)
            return null;

        if (TryComp(uid, out ProjectileComponent? projectile) && projectile.Shooter is { } shooter)
            uid = shooter;
        else if (TryComp(uid, out ThrownItemComponent? thrown) && thrown.Thrower is { } thrower)
            uid = thrower;

        return uid == joe || !Exists(uid) ? null : uid;
    }

    /// <summary>The attacker the Joe still remembers, if any.</summary>
    public bool TryGetRemembered(Entity<EWJoeRepentanceComponent> ent, out EntityUid attacker)
    {
        attacker = default;
        if (ent.Comp.LastAttacker is not { } last || !Exists(last) || _timing.CurTime >= ent.Comp.Until || _mobState.IsDead(last))
            return false;

        attacker = last;
        return true;
    }

    private void RefreshAlert(Entity<EWJoeRepentanceComponent> ent)
    {
        if (!TryGetRemembered(ent, out var attacker))
        {
            ClearAlert(ent);
            return;
        }

        var coords = _transform.GetMapCoordinates(attacker);
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert, _tracker.GetAlertSeverity(ent, coords));
        ent.Comp.AlertShown = true;
    }

    private void ClearAlert(Entity<EWJoeRepentanceComponent> ent)
    {
        if (!ent.Comp.AlertShown)
            return;

        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
        ent.Comp.AlertShown = false;
    }

    private void OnSense(Entity<EWJoeComponent> ent, ref EWJoeSenseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _popup.PopupEntity(Describe(ent), ent, ent, PopupType.Medium);
    }

    /// <summary>Direction and distance to the remembered attacker, as the K-9 track-owner popup phrases it.</summary>
    public string Describe(EntityUid joe)
    {
        if (!TryComp(joe, out EWJoeRepentanceComponent? repentance) || !TryGetRemembered((joe, repentance), out var attacker))
            return Loc.GetString("cmu-ew-joe-repentance-none");

        var here = _transform.GetMapCoordinates(joe);
        var there = _transform.GetMapCoordinates(attacker);
        if (here.MapId != there.MapId)
            return Loc.GetString("cmu-ew-joe-repentance-lost");

        var offset = there.Position - here.Position;
        var direction = offset.LengthSquared() < 0.01f ? Direction.South : offset.GetDir();
        var directionText = Loc.GetString("rmc-k9-direction-" + direction.ToString().ToLowerInvariant());
        return Loc.GetString("cmu-ew-joe-repentance-result", ("direction", directionText), ("distance", (int) Math.Round(offset.Length())));
    }

    private void OnShutdown(Entity<EWJoeRepentanceComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        ClearAlert(ent);
    }
}
