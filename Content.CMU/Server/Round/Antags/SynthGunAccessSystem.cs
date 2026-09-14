using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Round.Antags;

namespace Content.Server.CMU14.Round.Antags;

public sealed partial class SynthGunAccessSystem : EntitySystem
{
    [Dependency] private readonly SharedSynthSystem _synth = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SynthGunAccessComponent, ComponentStartup>(OnGunAccessStartup);
    }

    private void OnGunAccessStartup(Entity<SynthGunAccessComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SynthComponent>(ent, out var synth) || synth.CanUseGuns)
            return;

        _synth.SetGunRestriction(ent, true);
    }
}
