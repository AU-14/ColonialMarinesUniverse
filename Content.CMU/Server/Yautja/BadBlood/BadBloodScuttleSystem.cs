using Content.Shared._RMC14.Explosion;
using Content.Shared.Administration.Logs;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

// the fallen Leader's bracer: a countdown everyone nearby can see and hear, then a small blast that gibs no one.
// the honourable self-destruct is a different path and never comes through here
public sealed partial class BadBloodScuttleSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodScuttleComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<BadBloodScuttleComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<BadBloodScuttleComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    ///     Starts the scuttle countdown on a bracer. False if it is not a bracer, is already counting down,
    ///     or its honourable self-destruct is armed (that blast destroys it anyway).
    /// </summary>
    public bool TryStartScuttle(EntityUid bracer, EntityUid? cause = null)
    {
        if (TerminatingOrDeleted(bracer) ||
            !TryComp(bracer, out YautjaBracerComponent? bracerComp) ||
            bracerComp.SelfDestructArmed ||
            HasComp<BadBloodScuttleComponent>(bracer))
        {
            return false;
        }

        var now = _timing.CurTime;
        var scuttle = AddComp<BadBloodScuttleComponent>(bracer);
        scuttle.DetonateAt = now + scuttle.Fuse;
        scuttle.NextWarning = now;

        var light = _pointLight.EnsureLight(bracer);
        _pointLight.SetColor(bracer, scuttle.LightColor, light);
        _pointLight.SetRadius(bracer, scuttle.LightRadius, light);
        _pointLight.SetEnergy(bracer, scuttle.LightEnergy, light);
        _pointLight.SetEnabled(bracer, false, light);

        var message = Loc.GetString("cmu-bad-blood-scuttle-armed-clan",
            ("seconds", (int) Math.Ceiling(scuttle.Fuse.TotalSeconds)));
        var clan = EntityQueryEnumerator<YautjaBadBloodComponent>();
        while (clan.MoveNext(out var uid, out _))
        {
            if (!TerminatingOrDeleted(uid))
                _popup.PopupEntity(message, uid, uid, PopupType.LargeCaution);
        }

        var by = cause is { } source ? ToPrettyString(source).ToString() : "no one";
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Bad Blood bracer scuttle armed on {ToPrettyString(bracer):bracer} by {by}, detonation in {scuttle.Fuse.TotalSeconds}s");
        return true;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BadBloodScuttleComponent>();
        while (query.MoveNext(out var uid, out var scuttle))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (now >= scuttle.DetonateAt)
            {
                Detonate((uid, scuttle));
                continue;
            }

            if (now >= scuttle.NextWarning)
                Warn((uid, scuttle), now);
        }
    }

    private void Warn(Entity<BadBloodScuttleComponent> ent, TimeSpan now)
    {
        var remaining = ent.Comp.DetonateAt - now;
        var final = remaining <= ent.Comp.FinalCountdown;
        var seconds = Math.Max(1, (int) Math.Ceiling(remaining.TotalSeconds));
        // a worn or stowed bracer has no place of its own, so everything is shown where its holder stands
        var coords = _transform.GetMoverCoordinates(ent.Owner);

        if (seconds != ent.Comp.LastWarnedSeconds)
        {
            ent.Comp.LastWarnedSeconds = seconds;
            _popup.PopupCoordinates(
                Loc.GetString(final ? "cmu-bad-blood-scuttle-warning-final" : "cmu-bad-blood-scuttle-warning",
                    ("seconds", seconds)),
                coords,
                final ? PopupType.LargeCaution : PopupType.MediumCaution);
        }

        if (TryComp(ent.Owner, out YautjaBracerComponent? bracer))
            _audio.PlayPvs(bracer.SelfDestructWarningSound, coords);

        ent.Comp.LightOn = !ent.Comp.LightOn;
        _pointLight.SetEnabled(ent.Owner, ent.Comp.LightOn);

        ent.Comp.NextWarning = now + (final ? ent.Comp.FinalWarningEvery : ent.Comp.WarningEvery);
    }

    private void Detonate(Entity<BadBloodScuttleComponent> ent)
    {
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Bad Blood bracer scuttle detonated from {ToPrettyString(ent.Owner):bracer}");

        // no gib loop and no gear sweep, unlike the honourable self-destruct
        _rmcExplosion.QueueExplosion(
            _transform.GetMapCoordinates(ent.Owner),
            ent.Comp.Explosion.Id,
            ent.Comp.TotalIntensity,
            ent.Comp.IntensitySlope,
            ent.Comp.MaxIntensity,
            ent.Owner,
            maxTileBreak: 0,
            canCreateVacuum: false);

        QueueDel(ent.Owner);
    }

    private void OnPickupAttempt(Entity<BadBloodScuttleComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnUnequipAttempt(Entity<BadBloodScuttleComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        args.Cancel();
        args.Reason = "cmu-bad-blood-scuttle-locked";
    }

    private void OnExamined(Entity<BadBloodScuttleComponent> ent, ref ExaminedEvent args)
    {
        var seconds = Math.Max(0, (int) Math.Ceiling((ent.Comp.DetonateAt - _timing.CurTime).TotalSeconds));
        args.PushMarkup(Loc.GetString("cmu-bad-blood-scuttle-examine", ("seconds", seconds)));
    }
}
