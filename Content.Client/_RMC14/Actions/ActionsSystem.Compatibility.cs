namespace Content.Client.Actions;

public sealed partial class ActionsSystem
{
    public void SetAssignments(List<SlotAssignment> assignments)
    {
        AssignSlot?.Invoke(assignments);
    }
}
