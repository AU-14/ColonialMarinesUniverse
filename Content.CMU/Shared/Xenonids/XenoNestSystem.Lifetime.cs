namespace Content.Shared._RMC14.Xenonids.Construction.Nest;

public sealed partial class XenoNestSystem
{
    private void CMUOnNestedTerminating(Entity<XenoNestedComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_net.IsServer || !TryComp<XenoNestComponent>(ent.Comp.Nest, out var nest) ||
            nest.Nested != ent.Owner)
            return;

        // DetachNested deliberately avoids reparenting a terminating victim. Release its
        // backlink here so the surviving nest cannot serialize the deleted victim.
        nest.Nested = null;
        Dirty(ent.Comp.Nest, nest);
        if (TryComp<XenoNestSurfaceComponent>(nest.Surface, out var surface))
        {
            foreach (var (direction, uid) in surface.Nests)
            {
                if (uid != ent.Comp.Nest)
                    continue;
                surface.Nests.Remove(direction);
                Dirty(nest.Surface!.Value, surface);
                break;
            }
        }
        QueueDel(ent.Comp.Nest);
    }
}
