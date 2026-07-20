namespace Content.Shared.UserInterface;

public sealed partial class ActivatableUISystem
{
    /// <summary>
    /// Legacy RMC entry point for toggling an activatable UI after normal interaction checks.
    /// </summary>
    public bool InteractUI(EntityUid user, EntityUid uiEntity)
    {
        return TryComp<ActivatableUIComponent>(uiEntity, out var activatable) &&
               InteractUI(user, uiEntity, activatable);
    }
}
