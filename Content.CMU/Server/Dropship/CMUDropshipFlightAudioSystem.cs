using Content.Server._RMC14.Dropship;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.Audio;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship;

/// <summary>
///     Plays a dropship's flight loop (cmss13-devs/cmss13-pve#1294) once its takeoff cue has finished, and keeps
///     it going until the moment the dropship touches down. Out of a warship's bay, the ground takeoff cue plays
///     straight after the airlocked one, and the loop follows that. Each play of the loop starts a second before the last
///     one ends, so the engine never drops out between them. The old landing sound plays aboard on top of the new
///     one through the approach.
/// </summary>
public sealed class CMUDropshipFlightAudioSystem : EntitySystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.1);
    private static readonly TimeSpan LoopOverlap = TimeSpan.FromSeconds(1);

    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DropshipSystem _dropship = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, Flight> _flights = new();
    private readonly List<EntityUid> _landed = new();
    private TimeSpan _nextUpdate;

    private sealed class Flight
    {
        public TimeSpan NextPlay;
        public TimeSpan? TakeoffAt;
        public bool Arriving;
        public readonly List<EntityUid> Playing = new();
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
            if (TryComp(ship, out FTLComponent? ftl) && ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving)
                continue;

            foreach (var audio in flight.Playing)
            {
                _audio.Stop(audio);
            }

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

                // The loop waits for the takeoff cue to end. Out of a warship's bay, the ground takeoff cue
                // plays right after the airlocked one.
                var takeoffLength = dropship.StartupSound == null
                    ? TimeSpan.Zero
                    : _audio.GetAudioLength(_audio.ResolveSound(dropship.StartupSound));
                var newFlight = new Flight { NextPlay = now + takeoffLength - LoopOverlap };
                if (_dropship.CMUIsDepartingWarship(ship) && dropship.AirlockedStartupSound is { } airlocked)
                {
                    var airlockedLength = _audio.GetAudioLength(_audio.ResolveSound(airlocked));
                    newFlight.TakeoffAt = now + airlockedLength;
                    newFlight.NextPlay += airlockedLength;
                }

                _flights[ship] = newFlight;
                continue;
            }

            flight.Playing.RemoveAll(audio => !Exists(audio));

            if (flight.TakeoffAt is { } takeoffAt && now >= takeoffAt)
            {
                flight.TakeoffAt = null;
                if (dropship.StartupSound is { } takeoff)
                {
                    var takeoffAudio = _audio.PlayPvs(takeoff, ship);
                    _audio.SetGridAudio(takeoffAudio);
                    if (takeoffAudio != null)
                        flight.Playing.Add(takeoffAudio.Value.Entity);
                }
            }

            if (ftl.State == FTLState.Arriving && !flight.Arriving)
            {
                flight.Arriving = true;
                if (dropship.InteriorArrivalSound is { } landing)
                {
                    var landingAudio = _audio.PlayPvs(landing, ship);
                    _audio.SetGridAudio(landingAudio);
                }
            }

            if (ftl.State is not (FTLState.Travelling or FTLState.Arriving) || now < flight.NextPlay)
                continue;

            var resolved = _audio.ResolveSound(loopSound);
            var length = _audio.GetAudioLength(resolved);
            var played = _audio.PlayPvs(resolved, ship, loopSound.Params);
            _audio.SetGridAudio(played);
            if (played != null)
                flight.Playing.Add(played.Value.Entity);

            flight.NextPlay = now + length - LoopOverlap;
        }
    }
}
