namespace Content.Client.UserInterface.Systems.Actions;

public sealed partial class ActionUIController
{
    private readonly List<EntityUid?> _cmuDesiredVehicleActions = new();
    private readonly List<EntityUid?> _cmuRebuiltVehicleActions = new();
    private readonly HashSet<EntityUid> _cmuRemainingVehicleActions = new();
    private readonly List<(EntityUid Uid, int SortOrder, int Index)> _cmuHardpointActions = new();
}
