using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Mutiny;
using Content.Shared._RMC14.Marines.Skills.Pamphlets;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Vendors;

namespace Content.Shared.CMU14.Mutiny;

public sealed class CMUMutinyIconSystem : EntitySystem
{
    public override void Initialize()
    {
        // last in the chain: the RMC mutineer and leader handlers had no order between each other and a used
        // pamphlet or vendor role override could paint over them, hiding who's in the mutiny
        SubscribeLocalEvent<CMUMutinyParticipantComponent, GetMarineIconEvent>(OnGetMarineIcon,
            after: [typeof(SharedMarineSystem), typeof(SquadSystem), typeof(SharedMutinySystem), typeof(SkillPamphletSystem), typeof(VendorRoleOverrideSystem)]);
    }

    private void OnGetMarineIcon(Entity<CMUMutinyParticipantComponent> ent, ref GetMarineIconEvent args)
    {
        if (ent.Comp.Icons.TryGetValue(ent.Comp.Side, out var icon))
            args.Icon = icon;
    }
}
