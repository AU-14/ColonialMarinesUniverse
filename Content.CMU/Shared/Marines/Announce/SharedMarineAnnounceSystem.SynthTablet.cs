using Content.Shared._RMC14.Synth;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Marines.Announce;

public abstract partial class SharedMarineAnnounceSystem
{
    [Dependency] private TagSystem _cmuTag = default!;

    private static readonly ProtoId<TagPrototype> CMUCommandTabletTag = "CommandTablet";

    // synths announce off a command tablet without leadership, giving them
    // leadership would also hand them marine order actions
    private bool CMUCanAnnounce(Entity<MarineCommunicationsComputerComponent> ent, EntityUid actor)
    {
        if (_skills.HasSkill(actor, ent.Comp.AnnounceSkill, ent.Comp.AnnounceSkillLevel))
            return true;

        return HasComp<SynthComponent>(actor) && _cmuTag.HasTag(ent.Owner, CMUCommandTabletTag);
    }
}
