using Content.Shared.CMU14.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Doors;

/// <summary>
///     Watches the air around each <see cref="CMUFirelockSealComponent"/> firelock. An open one shuts and seals as soon
///     as the pressure next to it drops or air starts rushing past it, an opened sealed one shuts again once its time
///     is up, and a sealed one opens back up when the air on every side is safe.
/// </summary>
public sealed partial class CMUFirelockSealServerSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedFirelockSystem _firelock = default!;
    [Dependency] private CMUAtmosHazardSystem _hazard = default!;
    [Dependency] private CMUFirelockSealSystem _seal = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<CMUFirelockSealComponent, DoorComponent, FirelockComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var seal, out var door, out var firelock, out var xform))
        {
            if (!seal.RequiresPower)
                _seal.KeepPowered(uid);

            if (seal.CloseAt is { } closeAt)
            {
                // Opened while sealed: it stays that way for a few seconds, then shuts no matter what.
                if (now < closeAt || door.State is DoorState.Opening)
                    continue;

                if (door.State != DoorState.Open || _firelock.EmergencyPressureStop(uid, firelock, door))
                {
                    seal.CloseAt = null;
                    Dirty(uid, seal);
                }

                continue;
            }

            switch (door.State)
            {
                case DoorState.Open when seal.Sealed || IsHazardous((uid, seal, xform), true):
                    // Keeps trying each check if something is standing in the way.
                    if (!seal.Sealed)
                    {
                        seal.Sealed = true;
                        Dirty(uid, seal);
                        _audio.PlayPvs(seal.AlarmSound, uid);
                        var sealedEv = new CMUFirelockSealedEvent(uid);
                        RaiseLocalEvent(ref sealedEv);
                    }

                    _firelock.EmergencyPressureStop(uid, firelock, door);
                    break;
                case DoorState.Closed:
                    // A firelock that was already shut when the air went bad seals too, so it can't just be opened.
                    var hazardous = IsHazardous((uid, seal, xform), false);
                    if (hazardous == seal.Sealed)
                        break;

                    seal.Sealed = hazardous;
                    Dirty(uid, seal);
                    if (hazardous)
                    {
                        var sealedEv = new CMUFirelockSealedEvent(uid);
                        RaiseLocalEvent(ref sealedEv);
                    }
                    else
                    {
                        _door.StartOpening(uid, door);
                    }
                    break;
            }
        }
    }

    private bool IsHazardous(Entity<CMUFirelockSealComponent, TransformComponent> ent, bool open)
    {
        // A closed firelock's own tile holds no air.
        return _hazard.IsHazardous(ent, ent.Comp2, ent.Comp1.LowPressure, ent.Comp1.PressureDifference, open);
    }
}
