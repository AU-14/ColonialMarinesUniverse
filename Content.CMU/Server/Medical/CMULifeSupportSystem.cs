using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical;

public sealed partial class CMULifeSupportSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BarotraumaSystem _barotrauma = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMULifeSupportComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMULifeSupportComponent, ComponentRemove>(OnRemove);
    }

    private void OnStartup(Entity<CMULifeSupportComponent> ent, ref ComponentStartup args)
    {
        var proto = _protoManager.Index(ent.Comp.Grants);
        EntityManager.AddComponents(ent, proto, removeExisting: false);

        if (TryComp<BarotraumaComponent>(ent, out var barotrauma))
            _barotrauma.UpdateCachedResistances(ent, barotrauma);

        if (TryComp<RespiratorComponent>(ent, out var respirator)
            && respirator.NextUpdate == TimeSpan.Zero)
            respirator.NextUpdate = _timing.CurTime + respirator.AdjustedUpdateInterval;

        _barotrauma.RefreshPressureImmunity(ent);
    }

    private void OnRemove(Entity<CMULifeSupportComponent> ent, ref ComponentRemove args)
    {
        EntityManager.RemoveComponents(ent, _protoManager.Index(ent.Comp.Grants));
    }
}
