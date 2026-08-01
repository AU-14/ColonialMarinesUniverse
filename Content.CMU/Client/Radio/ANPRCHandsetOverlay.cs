using Content.Shared._AU14.Radio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._AU14.Radio;

// draws the cord between a manpack and its handset while the handset is off the pack,
// same idea as the RMC rotary phone cable overlay
public sealed partial class ANPRCHandsetOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public ANPRCHandsetOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var containerSystem = _entity.System<ContainerSystem>();
        var transformSystem = _entity.System<TransformSystem>();
        var handle = args.WorldHandle;

        var packs = _entity.EntityQueryEnumerator<ANPRCRadioComponent>();
        while (packs.MoveNext(out var packId, out var pack))
        {
            if (pack.Handset is not { Valid: true } handset ||
                !containerSystem.TryGetContainer(packId, ANPRCRadioComponent.HandsetContainerId, out var container) ||
                container.ContainedEntities.Count > 0 ||
                !_entity.TryGetComponent(packId, out TransformComponent? packTransform) ||
                packTransform.MapID == MapId.Nullspace ||
                !_entity.TryGetComponent(handset, out TransformComponent? handsetTransform) ||
                handsetTransform.MapID == MapId.Nullspace)
            {
                continue;
            }

            var packPosition = transformSystem.GetMapCoordinates(packId);
            var handsetPosition = transformSystem.GetMapCoordinates(handset);
            handle.DrawLine(packPosition.Position, handsetPosition.Position, Color.Black);
        }
    }
}
