using Content.Shared.CMU14.ZLevels.Core.Components;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed partial class CMUZLevelLadderSystem
{
    public bool StartControlledClimb(EntityUid ladder, EntityUid user, int offset)
    {
        if (!TryComp<CMUZLevelLadderComponent>(ladder, out var component) ||
            offset != GetMovementOffset(component, offset > 0) ||
            (offset > 0 ? !component.CanMoveUp : !component.CanMoveDown) ||
            !_interaction.InRangeUnobstructed(user, ladder, component.Range))
            return false;
        StartClimb((ladder, component), user, offset);
        return true;
    }
}
