using Content.Shared._RMC14.StepTrigger;

namespace Content.Shared.StepTrigger.Systems;

public sealed partial class StepTriggerImmuneSystem
{
    private bool IsRmcStepTriggerImmune(EntityUid tripper)
    {
        return HasComp<ImmuneToClothingRequiredStepTriggerComponent>(tripper);
    }
}
