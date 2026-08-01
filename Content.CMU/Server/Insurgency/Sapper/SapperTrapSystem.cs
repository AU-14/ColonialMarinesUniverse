using Content.Server.Explosion.EntitySystems;
using Content.Shared._AU14.Insurgency.Sapper;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Insurgency.Sapper;

/// <summary>
///     Server-side timing for sapper traps:
///     - flips a planted trap to armed once its arming delay elapses,
///     - and, for snare traps, ensnares whoever trips them (handled by <see cref="SapperSnareSystem"/>).
///     Proximity reveal is NO LONGER server-side: it used to flip a global Revealed flag that showed
///     the trap to everyone once any enemy walked near. It is now a per-viewer decision made entirely
///     client-side in SapperTrapVisualsSystem (only the approaching player sees it).
/// </summary>
public sealed class SapperTrapSystem : SharedSapperTrapSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Victim feedback when any sapper trap goes off: a sharp sound and an effect right on them,
        // on top of whatever the trap's own payload does.
        SubscribeLocalEvent<SapperTrapComponent, TriggerEvent>(OnTripped);
        SubscribeLocalEvent<SapperTrapComponent, MapInitEvent>(OnMapInit);
    }

    private void OnTripped(Entity<SapperTrapComponent> ent, ref TriggerEvent args)
    {
        if (!ent.Comp.Deployed || args.User is not { } victim)
            return;

        var coords = Transform(victim).Coordinates;
        if (ent.Comp.TripEffect is { } effect)
            Spawn(effect, coords);
        if (ent.Comp.TripSound is { } sound)
            _audio.PlayPvs(sound, coords);
    }

    private void OnMapInit(Entity<SapperTrapComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Deployed && !ent.Comp.Armed && ent.Comp.ArmsAt is { } armsAt)
            ScheduleArming(ent, armsAt);
    }

    protected override void ScheduleArming(Entity<SapperTrapComponent> ent, TimeSpan armsAt)
    {
        var delay = armsAt - Timing.CurTime;
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;

        var uid = ent.Owner;
        Timer.Spawn(delay, () => ArmIfCurrent(uid, armsAt));
    }

    private void ArmIfCurrent(EntityUid uid, TimeSpan expectedArmsAt)
    {
        if (!TryComp(uid, out SapperTrapComponent? comp) ||
            !comp.Deployed ||
            comp.Armed ||
            comp.ArmsAt != expectedArmsAt)
            return;

        comp.Armed = true;
        comp.ArmsAt = null;
        Dirty(uid, comp);
    }
}
