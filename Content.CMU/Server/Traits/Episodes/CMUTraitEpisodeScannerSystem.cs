using Content.Shared.CMU14.Medical.Diagnostics;
using Content.Shared.CMU14.Traits.Asthmatic;
using Content.Shared.CMU14.Traits.Epilepsy;
using Content.Shared.CMU14.Traits.PanicProne;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Traits.Episodes;

/// <summary>
///     A health scanner flags the worst trait episodes while they're happening: a seizure, an asthma attack or a
///     panic attack. Allergic reactions are flagged by <c>CMUAllergyScannerSystem</c>.
/// </summary>
public sealed partial class CMUTraitEpisodeScannerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EpilepsyComponent, CMUHealthScannerReadingEvent>(OnEpilepsyReading);
        SubscribeLocalEvent<RespiratoryStrainComponent, CMUHealthScannerReadingEvent>(OnAsthmaReading);
        SubscribeLocalEvent<PanicComponent, CMUHealthScannerReadingEvent>(OnPanicReading);
    }

    private void OnEpilepsyReading(Entity<EpilepsyComponent> ent, ref CMUHealthScannerReadingEvent args)
    {
        if (_timing.CurTime < ent.Comp.SeizingUntil)
            args.State.CMUSeizure = true;
    }

    private void OnAsthmaReading(Entity<RespiratoryStrainComponent> ent, ref CMUHealthScannerReadingEvent args)
    {
        if (ent.Comp.Peaked)
            args.State.CMUAsthmaAttack = true;
    }

    private void OnPanicReading(Entity<PanicComponent> ent, ref CMUHealthScannerReadingEvent args)
    {
        if (ent.Comp.Peaked)
            args.State.CMUPanicAttack = true;
    }
}
