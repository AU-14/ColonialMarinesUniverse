using Content.Server._CMU14.Medical.Human.Components;
using Content.Server.StatusEffectNew;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Human.Systems;

public sealed partial class HumanAnesthesiaSystem : EntitySystem
{
    private const float MinimumNitrousMoles = 0.01f;

    private static readonly EntProtoId ForcedSleeping = SleepingSystem.StatusEffectForcedSleeping;
    private static readonly TimeSpan ActiveRefreshInterval = TimeSpan.FromSeconds(1);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    private TimeSpan _nextActiveRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalStartupEvent>(OnHumanMedicalStartup);
        SubscribeLocalEvent<HumanMedicalShutdownEvent>(OnHumanMedicalShutdown);
        SubscribeLocalEvent<HumanMedicalComponent, InternalsGasTankChangedEvent>(OnInternalsGasTankChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextActiveRefresh)
            return;

        _nextActiveRefresh = _timing.CurTime + ActiveRefreshInterval;

        var query = EntityQueryEnumerator<CMUAnesthesiaSleepingComponent, HumanMedicalComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            RefreshAnesthesia(uid);
        }
    }

    private void OnHumanMedicalStartup(ref HumanMedicalStartupEvent args)
    {
        RefreshAnesthesia(args.Body);
    }

    private void OnHumanMedicalShutdown(ref HumanMedicalShutdownEvent args)
    {
        ClearAnesthesia(args.Body, wake: false);
    }

    private void OnInternalsGasTankChanged(Entity<HumanMedicalComponent> ent, ref InternalsGasTankChangedEvent args)
    {
        RefreshAnesthesia(ent.Owner);
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
