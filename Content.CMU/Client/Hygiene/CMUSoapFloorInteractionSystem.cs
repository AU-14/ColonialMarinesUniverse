using Content.Shared.CMU14.Hygiene;
using Content.Shared.Fluids.Components;
using Content.Shared.Forensics.Systems;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Client.CMU14.Hygiene;

public sealed partial class CMUSoapFloorInteractionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFourUseSoapComponent, AfterInteractEvent>(OnAfterInteract,
            before: new[] { typeof(ForensicsSystem), typeof(IngestionSystem) });
    }

    private void OnAfterInteract(Entity<CMUFourUseSoapComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Handled && args.CanReach && (args.Target == null || HasComp<PuddleComponent>(args.Target)))
            args.Handled = true;
    }
}
