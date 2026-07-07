using Content.Server.StatusEffectNew;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUAnesthesiaSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    private const float MinimumNitrousMoles = 0.01f;
    private static readonly EntProtoId ForcedSleeping = SleepingSystem.StatusEffectForcedSleeping;
    private static readonly TimeSpan ActiveRefreshInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextActiveRefresh;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextActiveRefresh)
            return;

        _nextActiveRefresh = _timing.CurTime + ActiveRefreshInterval;

        var anesthesiaQuery = EntityQueryEnumerator<CMUAnesthesiaSleepingComponent>();
        while (anesthesiaQuery.MoveNext(out var uid, out _))
        {
            if (!HasComp<CMUHumanMedicalComponent>(uid))
                ClearAnesthesia(uid, wake: false);
        }

        var query = EntityQueryEnumerator<CMUHumanMedicalComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            RefreshAnesthesia(uid);
        }
    }

    private void RefreshAnesthesia(EntityUid body)
    {
        if (HasActiveInhaledAnesthesia(body))
        {
            ApplyAnesthesia(body);
            return;
        }

        ClearAnesthesia(body, wake: true);
    }

    private bool HasActiveInhaledAnesthesia(EntityUid body)
    {
        if (!TryComp<InternalsComponent>(body, out var internals) ||
            !_internals.AreInternalsWorking(body, internals) ||
            internals.GasTankEntity is not { } tankUid ||
            !TryComp<GasTankComponent>(tankUid, out var gasTank))
        {
            return false;
        }

        return gasTank.Air.GetMoles(Gas.NitrousOxide) > MinimumNitrousMoles;
    }

    private void ApplyAnesthesia(EntityUid body)
    {
        if (!TryComp<CMUAnesthesiaSleepingComponent>(body, out var anesthesia))
        {
            anesthesia = AddComp<CMUAnesthesiaSleepingComponent>(body);
            anesthesia.HadForcedSleeping = _status.HasStatusEffect(body, ForcedSleeping);
            anesthesia.WasSleeping = HasComp<SleepingComponent>(body);
        }

        if (!_status.TrySetStatusEffectDuration(body, ForcedSleeping, duration: null))
        {
            RemComp<CMUAnesthesiaSleepingComponent>(body);
            return;
        }

        _sleeping.TrySleeping((body, null));
    }

    private void ClearAnesthesia(EntityUid body, bool wake)
    {
        if (!TryComp<CMUAnesthesiaSleepingComponent>(body, out var anesthesia))
            return;

        RemComp<CMUAnesthesiaSleepingComponent>(body);

        if (!anesthesia.HadForcedSleeping)
            _status.TryRemoveStatusEffect(body, ForcedSleeping);

        if (wake && !anesthesia.WasSleeping)
            _sleeping.TryWaking((body, null), force: true);
    }
}
