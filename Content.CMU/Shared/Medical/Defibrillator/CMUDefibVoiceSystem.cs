using Content.Shared.Medical;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Defibrillator;

/// <summary>
/// Plays a defibrillator's voice prompts. An AED analyzes before every shock: "shock advised" and it charges at
/// the energy it picked, then "stand clear" just before the shock; "no shock advised" and it won't charge at all.
/// </summary>
public sealed partial class CMUDefibVoiceSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CMUDefibChargeSystem _charge = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUDefibVoiceComponent, CMUDefibZapAttemptEvent>(OnZapAttempt);
        SubscribeLocalEvent<CMUDefibVoiceComponent, CMUDefibZapStartedEvent>(OnZapStarted);
        SubscribeLocalEvent<CMUDefibVoiceComponent, DefibrillatorZapDoAfterEvent>(OnZapDoAfter);
    }

    private void OnZapAttempt(Entity<CMUDefibVoiceComponent> ent, ref CMUDefibZapAttemptEvent args)
    {
        if (!ent.Comp.Automated || args.Cancelled)
            return;

        var advice = _charge.Analyze(ent, args.Target);
        if (!advice.Shockable)
        {
            args.Cancelled = true;
            Prompt(ent, ent.Comp.NoShockAdvisedSound, args.User);
            _popup.PopupEntity(Loc.GetString("cmu-defib-no-shock-advised"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        _charge.SetAdvisedJoules(ent, advice.Joules);
        Prompt(ent, ent.Comp.ShockAdvisedSound, args.User);
        _popup.PopupEntity(Loc.GetString("cmu-defib-shock-advised", ("joules", advice.Joules)), ent, args.User);
    }

    private void OnZapStarted(Entity<CMUDefibVoiceComponent> ent, ref CMUDefibZapStartedEvent args)
    {
        if (_net.IsClient || ent.Comp.StandClearSound == null)
            return;

        var wait = args.Delay - ent.Comp.StandClearBeforeShock;
        if (wait < ent.Comp.StandClearMinDelay)
            wait = ent.Comp.StandClearMinDelay;

        ent.Comp.StandClearAt = _timing.CurTime + wait;
    }

    private void OnZapDoAfter(Entity<CMUDefibVoiceComponent> ent, ref DefibrillatorZapDoAfterEvent args)
    {
        // Charging finished or was interrupted; either way there's nothing left to announce.
        ent.Comp.StandClearAt = null;
    }

    /// <summary>
    /// Plays the analysis prompt for a finished analysis, e.g. one started from a monitor.
    /// </summary>
    public void Announce(EntityUid defib, CMUDefibAdvice advice)
    {
        if (!TryComp<CMUDefibVoiceComponent>(defib, out var voice))
            return;

        Prompt(defib, advice.Shockable ? voice.ShockAdvisedSound : voice.NoShockAdvisedSound, null);
    }

    /// <summary>
    /// Drops any prompt still waiting to play, e.g. when a charge is cancelled.
    /// </summary>
    public void CancelPrompts(EntityUid defib)
    {
        if (TryComp<CMUDefibVoiceComponent>(defib, out var voice))
            voice.StandClearAt = null;
    }

    public void AnnounceAnalyzing(EntityUid defib)
    {
        if (TryComp<CMUDefibVoiceComponent>(defib, out var voice))
            Prompt(defib, voice.AnalyzingSound, null);
    }

    /// <summary>
    /// Says a prompt, predicted for the user who caused it if there is one.
    /// </summary>
    private void Prompt(EntityUid defib, SoundSpecifier? sound, EntityUid? user)
    {
        if (sound == null)
            return;

        if (user != null)
            _audio.PlayPredicted(sound, defib, user);
        else
            _audio.PlayPvs(sound, defib);

        var ev = new CMUDefibPromptPlayedEvent(sound);
        RaiseLocalEvent(defib, ref ev);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUDefibVoiceComponent>();
        while (query.MoveNext(out var uid, out var voice))
        {
            if (voice.StandClearAt is not { } at || now < at)
                continue;

            voice.StandClearAt = null;
            Prompt(uid, voice.StandClearSound, null);
        }
    }
}
