using Content.Shared.CMU14.Medical.Monitor;
using Robust.Client.Graphics;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Medical.Monitor;

public sealed class CMUPatientMonitorSystem : SharedCMUPatientMonitorSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Beats older than this have scrolled off the screen.
    /// </summary>
    private static readonly TimeSpan BeatHistory = TimeSpan.FromSeconds(6);

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new CMUMonitorLeadsOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CMUMonitorLeadsOverlay>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        // Beats are generated here from the heart rate on the screen. Each one plays the QRS beep and is recorded
        // so the ECG and pleth draw a complex at exactly that moment, so what you hear matches what you see.
        var now = _timing.RealTime;
        var query = EntityQueryEnumerator<CMUPatientMonitorComponent>();
        while (query.MoveNext(out var uid, out var monitor))
        {
            var beats = monitor.BeatTimes;
            while (beats.Count > 0 && now - beats[0] > BeatHistory)
                beats.RemoveAt(0);

            if (monitor.AttachedTo == null ||
                monitor.DisplayHeartRate <= 0 ||
                monitor.DisplayRhythm == CMUMonitorRhythm.None ||
                CMUMonitorRhythms.IsLethal(monitor.DisplayRhythm))
            {
                continue;
            }

            // Recomputed every tick, so a change in rate takes effect on the very next beat.
            var interval = TimeSpan.FromSeconds(60f / monitor.DisplayHeartRate * monitor.BeatJitter);
            if (beats.Count > 0 && now < beats[^1] + interval)
                continue;

            beats.Add(now);
            monitor.BeatJitter = monitor.DisplayRhythm == CMUMonitorRhythm.Irregular
                ? _random.NextFloat(0.65f, 1.35f)
                : 1f;

            // Silencing the alarms mutes the beep too; the beats still show on the ECG.
            if (monitor.BeatSound != null && !monitor.AlarmsSilenced)
                _audio.PlayEntity(monitor.BeatSound, Filter.Local(), uid, false);
        }
    }
}
