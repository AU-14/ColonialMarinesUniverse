using Content.Shared._RMC14.Interaction;

namespace Content.Shared.Interaction;

public sealed partial class RotateToFaceSystem
{
    [Dependency] private RMCInteractionSystem _rmcInteraction = default!;

    private void CapRMCWorldRotation(Entity<TransformComponent> entity, ref Angle rotation)
    {
        _rmcInteraction.TryCapWorldRotation((entity.Owner, null, entity.Comp), ref rotation);
    }
}
