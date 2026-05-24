using Content.Client.Damage;
using Content.Shared._RMC14.Synth;
using Content.Shared.Damage.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Synth;

public sealed partial class SynthSystem : SharedSynthSystem
{ // TODO rework this code why is damage visuals client only
    [Dependency] private DamageVisualsSystem _damageVisuals = default!;

    private static readonly ProtoId<DamageGroupPrototype> GroupToChange = "Brute";

    protected override void MakeSynth(Entity<SynthComponent> ent)
    {
        base.MakeSynth(ent);

        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        if (!TryComp<DamageVisualsComponent>(ent.Owner, out var damageVisuals))
            return;

        _damageVisuals.ChangeDamageGroupColor((ent.Owner, sprite), damageVisuals, GroupToChange, ent.Comp.DamageVisualsColor);
    }
}
