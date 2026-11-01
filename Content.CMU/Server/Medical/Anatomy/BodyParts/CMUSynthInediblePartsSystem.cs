using Content.Shared._RMC14.Synth;
using Content.Shared.Body;
using Content.Shared.Nutrition.Components;

namespace Content.Server.CMU14.Medical.Anatomy.BodyParts;

public sealed partial class CMUSynthInediblePartsSystem : EntitySystem
{
    public override void Initialize()
    {
        // synths use the normal organic human parts, which are Edible, so a synth arm off the floor ate like meat.
        // hooked on removal since every way off the body (surgery, sever, gib, ape) goes through the organ container
        SubscribeLocalEvent<SynthComponent, OrganRemovedFromEvent>(OnOrganRemoved);
    }

    private void OnOrganRemoved(Entity<SynthComponent> ent, ref OrganRemovedFromEvent args)
    {
        if (TerminatingOrDeleted(args.Organ))
            return;

        RemComp<EdibleComponent>(args.Organ);
    }
}
