namespace Content.Shared._RMC14.Power;

public abstract partial class SharedRMCPowerSystem
{
    private void RemoveCMUReceiverFromArea(Entity<RMCPowerReceiverComponent> ent)
    {
        var previousArea = ent.Comp.Area;
        var previousLoad = ent.Comp.LastLoad;
        ent.Comp.Area = null;
        ent.Comp.LastLoad = 0;

        // Termination and component removal can both run. Only the first removal releases load.
        // Use the registered area even if the receiver has already moved to another area/nullspace.
        if (previousArea is not { } areaUid
            || TerminatingOrDeleted(areaUid)
            || !_areaPowerQuery.TryComp(areaUid, out var area)
            || !GetAreaReceivers((areaUid, area), ent.Comp.Channel).Remove(ent))
            return;

        area.Load[(int) ent.Comp.Channel] -= previousLoad;
        Dirty(areaUid, area);
    }
}
