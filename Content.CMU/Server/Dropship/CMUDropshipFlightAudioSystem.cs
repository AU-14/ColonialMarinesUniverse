using Content.Server._RMC14.Dropship;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship;

/// <summary>
///     Plays a dropship's flight loop (cmss13-devs/cmss13-pve#1294) once its takeoff cue has finished, and
///     stops it when the dropship starts arriving, so the two engine sounds never play over each other.
/// </summary>
public sealed class CMUDropshipFlightAudioSystem : EntitySystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DropshipSystem _dropship = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, Flight> _flights = new();
    private readonly List<EntityUid> _landed = new();
    private TimeSpan _nextUpdate;

    private sealed class Flight
    {
        public TimeSpan LoopAt;
        public EntityUid? Loop;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        _landed.Clear();
        foreach (var (ship, flight) in _flights)
        {
            if (TryComp(ship, out FTLComponent? ftl) && ftl.State is FTLState.Starting or FTLState.Travelling)
                continue;

            _audio.Stop(flight.Loop);
            _landed.Add(ship);
        }

        foreach (var ship in _landed)
        {
            _flights.Remove(ship);
        }

        var query = EntityQueryEnumerator<DropshipComponent, FTLComponent>();
        while (query.MoveNext(out var ship, out var dropship, out var ftl))
        {
            if (dropship.FlightLoopSound is not { } loopSound)
                continue;

            if (!_flights.TryGetValue(ship, out var flight))
            {
                if (ftl.State != FTLState.Starting)
                    continue;

                // The loop waits for whichever takeoff cue is playing to end.
                var cue = _dropship.CMUIsDepartingWarship(ship) && dropship.AirlockedStartupSound != null
                    ? dropship.AirlockedStartupSound
                    : dropship.StartupSound;
                var length = cue == null ? TimeSpan.Zero : _audio.GetAudioLength(_audio.ResolveSound(cue));
                _flights[ship] = new Flight { LoopAt = now + length };
                continue;
            }

            if (flight.Loop != null || ftl.State != FTLState.Travelling || now < flight.LoopAt)
                continue;

            var audio = _audio.PlayPvs(loopSound, ship, loopSound.Params.WithLoop(true));
            _audio.SetGridAudio(audio);
            flight.Loop = audio?.Entity;
        }
    }
}
