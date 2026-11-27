using Content.Shared.StepTrigger.Components;

namespace Content.Shared.StepTrigger.Systems;

public sealed partial class StepTriggerSystem
{
    // A rescued mob can still overlap the sensor, especially on a catwalk or with a large fixture.
    public void RearmAfterLedgeGrab(EntityUid chasm, EntityUid mob)
    {
        if (TryComp<StepTriggerComponent>(chasm, out var trigger) && trigger.CurrentlySteppedOn.Remove(mob))
            Dirty(chasm, trigger);
    }
}
