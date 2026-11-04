using Content.Shared.Body;
using Content.Shared.Gibbing;

namespace Content.Shared.CMU14.Xenos.Gibbing;

public sealed partial class CMUDeleteOnBodyGibSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUDeleteOnBodyGibComponent, BodyRelayedEvent<BeingGibbedEvent>>(OnBodyGibbed);
    }

    private void OnBodyGibbed(Entity<CMUDeleteOnBodyGibComponent> ent, ref BodyRelayedEvent<BeingGibbedEvent> args)
    {
        // queued: GibBody still walks the part list after this and calls DropNextTo on it
        QueueDel(ent);
    }
}
