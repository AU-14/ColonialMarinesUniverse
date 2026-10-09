using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Vehicle.Components;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Plays a running engine's loops on this client: the idle and a sped-up driving loop together,
/// faded into each other with the vehicle's speed so the engine sounds higher the faster it goes.
/// The loops are local, so their volumes can follow the speed every frame.
/// </summary>
public sealed class CMUVehicleEngineLoopSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, Loops> _loops = new();
    private readonly HashSet<EntityUid> _running = new();
    private readonly List<EntityUid> _stopped = new();

    private sealed class Loops
    {
        public bool Rough;
        public (EntityUid Entity, float Volume)? Idle;
        public (EntityUid Entity, float Volume)? Drive;
    }

    public override void Shutdown()
    {
        _running.Clear();
        StopUnused();
    }

    public override void FrameUpdate(float frameTime)
    {
        _running.Clear();
        var query = EntityQueryEnumerator<CMUVehicleEngineSoundComponent, GridVehicleMoverComponent>();
        while (query.MoveNext(out var uid, out var engine, out var mover))
        {
            // A vehicle out of view is detached, not deleted; its loops stop until it comes back.
            if (!engine.EngineRunning || (MetaData(uid).Flags & MetaDataFlags.Detached) != 0)
                continue;

            _running.Add(uid);
            if (!_loops.TryGetValue(uid, out var loops) || loops.Rough != engine.Rough)
            {
                Stop(uid);
                loops = new Loops
                {
                    Rough = engine.Rough,
                    Idle = Play(uid, engine.Rough ? engine.RoughSound : engine.IdleSound),
                    Drive = Play(uid, engine.Rough ? engine.RoughDriveSound : engine.DriveSound),
                };
                _loops[uid] = loops;
            }

            // An equal-power fade from the idle at rest to the driving loop at top speed.
            var speed = mover.MaxSpeed > 0f ? Math.Clamp(MathF.Abs(mover.CurrentSpeed) / mover.MaxSpeed, 0f, 1f) : 0f;
            SetGain(loops.Idle, MathF.Cos(speed * MathF.PI / 2f));
            SetGain(loops.Drive, MathF.Sin(speed * MathF.PI / 2f));
        }

        StopUnused();
    }

    private (EntityUid Entity, float Volume)? Play(EntityUid uid, SoundSpecifier? sound)
    {
        if (sound == null)
            return null;

        var audioParams = sound.Params.WithLoop(true);
        if (_audio.PlayEntity(sound, Filter.Local(), uid, false, audioParams) is not { } stream)
            return null;

        return (stream.Entity, audioParams.Volume);
    }

    private void SetGain((EntityUid Entity, float Volume)? loop, float gain)
    {
        if (loop is not { } playing || !TryComp(playing.Entity, out AudioComponent? audio) || !Audible(playing.Entity, audio))
            return;

        _audio.SetVolume(playing.Entity, playing.Volume + SharedAudioSystem.GainToVolume(gain), audio);
    }

    /// <summary>
    /// The audio system mutes a source on another map or out of range without moving it, and setting the
    /// volume would unmute it there. Same checks as its own, so the loops are only set where they're heard.
    /// </summary>
    private bool Audible(EntityUid loop, AudioComponent audio)
    {
        var listener = _eye.CurrentEye.Position;
        var xform = Transform(loop);
        if (listener.MapId != xform.MapID)
            return false;

        var distance = (_transform.GetWorldPosition(xform) - listener.Position).Length();
        return _audio.GetAudioDistance(distance) <= audio.MaxDistance;
    }

    private void StopUnused()
    {
        _stopped.Clear();
        foreach (var uid in _loops.Keys)
        {
            if (!_running.Contains(uid))
                _stopped.Add(uid);
        }

        foreach (var uid in _stopped)
        {
            Stop(uid);
        }
    }

    private void Stop(EntityUid uid)
    {
        if (!_loops.Remove(uid, out var loops))
            return;

        _audio.Stop(loops.Idle?.Entity);
        _audio.Stop(loops.Drive?.Entity);
    }
}
