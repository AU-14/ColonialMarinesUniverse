using Content.Server.StatusEffectNew;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._RMC14.Synth;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Core;

/// <summary>
///     Applies nitrous anesthesia when a CMU medical body connects working internals.
///     State changes drive the system, so it never scans all medical bodies.
/// </summary>
public sealed partial class CMUAnesthesiaSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    private const float MinimumNitrousMoles = 0.01f;
    private static readonly EntProtoId AnesthesiaSleeping = "StatusEffectCMUAnesthesia";
    private static readonly EntProtoId Drowsiness = "StatusEffectDrowsiness";
    private static readonly TimeSpan InductionDuration = TimeSpan.FromSeconds(6);

    private uint _nextInductionId;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUHumanMedicalComponent, CMUInternalsChangedEvent>(OnInternalsChanged);
    }

    private void OnInternalsChanged(
        Entity<CMUHumanMedicalComponent> ent,
        ref CMUInternalsChangedEvent args)
    {
        if (args.Working &&
            args.GasTank is { } tank &&
            !HasComp<SynthComponent>(ent) &&
            TryComp<GasTankComponent>(tank, out var gasTank) &&
            gasTank.Air.GetMoles(Gas.NitrousOxide) > MinimumNitrousMoles)
        {
            StartInduction(ent);
            return;
        }

        ClearAnesthesia(ent);
    }

    private void StartInduction(EntityUid body)
    {
        if (HasComp<CMUAnesthesiaStateComponent>(body))
            return;

        var anesthesia = AddComp<CMUAnesthesiaStateComponent>(body);
        anesthesia.InductionId = unchecked(++_nextInductionId);
        anesthesia.WasSleeping = HasComp<SleepingComponent>(body);
        anesthesia.DrowsinessApplied = !_status.HasStatusEffect(body, Drowsiness) &&
            _status.TrySetStatusEffectDuration(body, Drowsiness, InductionDuration);

        _popup.PopupEntity(Loc.GetString("effect-sleepy"), body, body, PopupType.Medium);

        var inductionId = anesthesia.InductionId;
        Timer.Spawn(InductionDuration, () => FinishInduction(body, inductionId));
    }

    private void FinishInduction(EntityUid body, uint inductionId)
    {
        if (!TryComp<CMUAnesthesiaStateComponent>(body, out var anesthesia) ||
            anesthesia.InductionId != inductionId)
        {
            return;
        }

        if (anesthesia.DrowsinessApplied)
        {
            _status.TryRemoveStatusEffect(body, Drowsiness);
            anesthesia.DrowsinessApplied = false;
        }

        if (!_status.TrySetStatusEffectDuration(body, AnesthesiaSleeping, duration: null))
        {
            RemComp<CMUAnesthesiaStateComponent>(body);
            return;
        }

        anesthesia.SleepApplied = !anesthesia.WasSleeping;
        _sleeping.TrySleeping((body, null));
    }

    private void ClearAnesthesia(EntityUid body)
    {
        if (!TryComp<CMUAnesthesiaStateComponent>(body, out var anesthesia))
            return;

        RemComp<CMUAnesthesiaStateComponent>(body);

        if (anesthesia.DrowsinessApplied)
            _status.TryRemoveStatusEffect(body, Drowsiness);

        _status.TryRemoveStatusEffect(body, AnesthesiaSleeping);

        if (anesthesia.SleepApplied)
            _sleeping.TryWaking((body, null));
    }
}
