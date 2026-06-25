using Content.Shared._RMC14.Synth;

namespace Content.Shared._CMU14.Threats.Mobs.SubvertedSynth;

public sealed class SubvertedSynthSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SubvertedSynthComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SubvertedSynthComponent, ComponentRemove>(OnRemove);
    }

    // hacky hack just to dirty the entity
    private void OnInit(EntityUid uid, SubvertedSynthComponent comp, ComponentInit args)
    {
        if (TryComp(uid, out SynthComponent? sc)) Dirty(uid, sc);
        DirtyEntity(uid);
    }

    private void OnRemove(EntityUid uid, SubvertedSynthComponent comp, ComponentRemove args)
    {
        if (TryComp(uid, out SynthComponent? sc))
            Dirty(uid, sc);
        DirtyEntity(uid);
    }
}
