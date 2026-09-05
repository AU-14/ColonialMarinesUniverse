using Content.Shared._RMC14.Medical.Stasis;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Core;

/// <summary>
/// The exact ownership boundary of the stasis marker. During removal the marker
/// is still queryable, so consumers must use Active instead of querying it again.
/// Time is the global simulation time; consumers apply their own entity pause clock.
/// </summary>
[ByRefEvent]
public readonly record struct CMUMedicalStasisChangedEvent(bool Active, TimeSpan Time);

public sealed partial class CMUMedicalStasisSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMInStasisComponent, ComponentInit>(OnEntered);
        SubscribeLocalEvent<CMInStasisComponent, ComponentShutdown>(OnLeft);
    }

    private void OnEntered(Entity<CMInStasisComponent> ent, ref ComponentInit args)
    {
        var changed = new CMUMedicalStasisChangedEvent(true, _timing.CurTime);
        RaiseLocalEvent(ent.Owner, ref changed);
    }

    private void OnLeft(Entity<CMInStasisComponent> ent, ref ComponentShutdown args)
    {
        var changed = new CMUMedicalStasisChangedEvent(false, _timing.CurTime);
        RaiseLocalEvent(ent.Owner, ref changed);
    }
}
