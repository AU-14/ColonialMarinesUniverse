using Content.Client.Damage;
using Content.Shared._RMC14.Synth;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Synth;

public sealed partial class SynthSystem : SharedSynthSystem
{ // TODO rework this code why is damage visuals client only
    [Dependency] private DamageVisualsSystem _damageVisuals = default!;

    protected override void MakeSynth(Entity<SynthComponent> ent)
    {
        base.MakeSynth(ent);

        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        if (!TryComp<DamageVisualsComponent>(ent.Owner, out var damageVisuals))
            return;

        if (damageVisuals.DamageOverlayGroups == null)
            return;

        foreach (var group in damageVisuals.DamageOverlayGroups.Keys)
        {
            _damageVisuals.ChangeDamageGroupColor((ent.Owner, sprite), damageVisuals, group, ent.Comp.DamageVisualsColor);
        }
    }
}
