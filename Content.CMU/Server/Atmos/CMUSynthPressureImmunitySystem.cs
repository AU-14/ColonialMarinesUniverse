using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Synth;
using Content.Shared.Atmos;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Synthetics don't take pressure damage, from vacuum or from high pressure.
/// </summary>
public sealed partial class CMUSynthPressureImmunitySystem : EntitySystem
{
    [Dependency] private BarotraumaSystem _barotrauma = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SynthComponent, RefreshPressureImmunityEvent>(OnRefreshPressureImmunity);
        SubscribeLocalEvent<SynthComponent, ComponentInit>(OnSynthInit);
    }

    private void OnRefreshPressureImmunity(Entity<SynthComponent> ent, ref RefreshPressureImmunityEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.IsImmune = true;
    }

    // Someone can become a synth after their pressure damage was set up.
    private void OnSynthInit(Entity<SynthComponent> ent, ref ComponentInit args)
    {
        _barotrauma.RefreshPressureImmunity(ent);
    }
}
