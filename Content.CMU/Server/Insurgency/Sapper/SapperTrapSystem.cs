using Content.Server.Explosion.EntitySystems;
using Content.Shared._AU14.Insurgency.Sapper;
using Robust.Shared.Audio.Systems;

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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = Timing.CurTime;

        // Arming is cheap and should be responsive, so check it every tick.
        var armQuery = EntityQueryEnumerator<SapperTrapComponent>();
        while (armQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.Deployed && !comp.Armed && comp.ArmsAt is { } armsAt && now >= armsAt)
            {
                comp.Armed = true;
                comp.ArmsAt = null;
                Dirty(uid, comp);
            }
        }

    }
}
